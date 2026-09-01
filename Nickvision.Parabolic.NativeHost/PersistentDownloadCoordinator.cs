using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal sealed class PersistentDownloadCoordinator : IDisposable
{
    private readonly IDownloadService _downloadService;
    private readonly IPostDownloadPipeline _postDownloadPipeline;
    private readonly object _sync;
    private readonly Dictionary<int, PersistentDownloadSession> _byInternalId;
    private readonly Dictionary<string, PersistentDownloadSession> _byExternalId;
    private readonly SemaphoreSlim _mutationLock;
    private readonly CancellationTokenSource _lifetime;

    public event Action<DownloadEventPayload>? EventProduced;

    public PersistentDownloadCoordinator(
        IDownloadService downloadService,
        IPostDownloadPipeline postDownloadPipeline)
    {
        _downloadService = downloadService;
        _postDownloadPipeline = postDownloadPipeline;
        _sync = new object();
        _byInternalId = new Dictionary<int, PersistentDownloadSession>();
        _byExternalId = new Dictionary<string, PersistentDownloadSession>(StringComparer.Ordinal);
        _mutationLock = new SemaphoreSlim(1, 1);
        _lifetime = new CancellationTokenSource();
        _downloadService.DownloadAdded += DownloadService_DownloadAdded;
        _downloadService.DownloadCompleted += DownloadService_DownloadCompleted;
        _downloadService.DownloadProgressChanged += DownloadService_DownloadProgressChanged;
        _downloadService.DownloadStartedFromQueue += DownloadService_DownloadStartedFromQueue;
        _downloadService.DownloadStopped += DownloadService_DownloadStopped;
    }

    public async Task RecoverAllAsync()
    {
        await _mutationLock.WaitAsync();
        try
        {
            await _downloadService.RecoverAllAsync();
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<DownloadSnapshot> EnqueueAsync(
        DownloadOptions options,
        string externalId,
        int tabId,
        CancellationToken cancellationToken)
    {
        options.ClientRequestId = externalId;
        options.ClientTabId = tabId;
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var internalId = await _downloadService.AddAsync(options, false);
            lock (_sync)
            {
                if (_byInternalId.TryGetValue(internalId, out var session))
                {
                    return session.ToSnapshot();
                }
                session = new PersistentDownloadSession(
                    internalId,
                    externalId,
                    tabId,
                    options.Url,
                    Path.Combine(options.SaveFolder, $"{options.SaveFilename}{options.FileType.DotExtension}"),
                    options.Priority,
                    options.ScheduledAt.HasValue && options.ScheduledAt.Value > DateTimeOffset.UtcNow
                        ? DownloadTaskState.Scheduled
                        : DownloadTaskState.Queued,
                    options.ResolverName,
                    options.ScheduledAt,
                    options.SpeedLimitKbps,
                    options.TaskAttempt,
                    options.MaxTaskAttempts,
                    options.GroupKey,
                    options.CollectionId,
                    options.PostProcessingSteps);
                _byInternalId[internalId] = session;
                _byExternalId[externalId] = session;
                return session.ToSnapshot();
            }
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public IReadOnlyList<DownloadSnapshot> ListActive()
    {
        lock (_sync)
        {
            return _byExternalId.Values
                .Where(session => !DownloadTaskStateMachine.IsTerminal(session.State))
                .OrderByDescending(session => session.Priority)
                .ThenBy(session => session.InternalId)
                .Select(session => session.ToSnapshot())
                .ToList();
        }
    }

    public async Task CancelAsync(string externalId, CancellationToken cancellationToken)
    {
        var session = GetSession(externalId);
        var cancelledRetry = false;
        lock (_sync)
        {
            if (session.State == DownloadTaskState.RetryScheduled)
            {
                session.RetryCancellation?.Cancel();
                session.State = DownloadTaskState.Cancelled;
                session.NextRetryAt = null;
                cancelledRetry = true;
            }
        }
        if (cancelledRetry)
        {
            Publish(session);
            return;
        }
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await _downloadService.StopAsync(session.InternalId))
            {
                throw new NativeRequestException("DOWNLOAD_NOT_RUNNING", "The requested download is no longer running.");
            }
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public void Pause(string externalId)
    {
        var session = GetSession(externalId);
        if (!_downloadService.Pause(session.InternalId))
        {
            throw new NativeRequestException("DOWNLOAD_NOT_RUNNING", "The requested download cannot be paused.");
        }
        TransitionAndPublish(session, DownloadTaskState.Paused);
    }

    public void Resume(string externalId)
    {
        var session = GetSession(externalId);
        if (!_downloadService.Resume(session.InternalId))
        {
            throw new NativeRequestException("DOWNLOAD_NOT_PAUSED", "The requested download is not paused.");
        }
        TransitionAndPublish(session, DownloadTaskState.Running);
    }

    public async Task SetPriorityAsync(
        string externalId,
        DownloadPriority priority,
        CancellationToken cancellationToken)
    {
        var session = GetSession(externalId);
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await _downloadService.SetPriorityAsync(session.InternalId, priority))
            {
                throw new NativeRequestException("DOWNLOAD_NOT_FOUND", "The requested download was not found in the active queue.");
            }
            lock (_sync)
            {
                session.Priority = priority;
            }
        }
        finally
        {
            _mutationLock.Release();
        }
        Publish(session);
    }

    public void OpenFolder(string externalId)
    {
        var session = GetSession(externalId);
        if (string.IsNullOrWhiteSpace(session.Path) || !File.Exists(session.Path))
        {
            throw new NativeRequestException("FILE_NOT_FOUND", "The completed download file was not found.");
        }
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add($"/select,{session.Path}");
        }
        else if (OperatingSystem.IsMacOS())
        {
            startInfo = new ProcessStartInfo("open")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(session.Path);
        }
        else if (OperatingSystem.IsLinux())
        {
            var directory = Path.GetDirectoryName(session.Path)
                ?? throw new NativeRequestException("FILE_NOT_FOUND", "The completed download folder was not found.");
            startInfo = new ProcessStartInfo("xdg-open")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(directory);
        }
        else
        {
            throw new NativeRequestException("UNSUPPORTED_PLATFORM", "Opening the containing folder is not supported on this operating system.");
        }
        Process.Start(startInfo);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _downloadService.DownloadAdded -= DownloadService_DownloadAdded;
        _downloadService.DownloadCompleted -= DownloadService_DownloadCompleted;
        _downloadService.DownloadProgressChanged -= DownloadService_DownloadProgressChanged;
        _downloadService.DownloadStartedFromQueue -= DownloadService_DownloadStartedFromQueue;
        _downloadService.DownloadStopped -= DownloadService_DownloadStopped;
        _mutationLock.Dispose();
        _lifetime.Dispose();
    }

    private PersistentDownloadSession GetSession(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new NativeRequestException("INVALID_DOWNLOAD_ID", "The download identifier is missing.");
        }
        lock (_sync)
        {
            if (_byExternalId.TryGetValue(externalId, out var session))
            {
                return session;
            }
        }
        throw new NativeRequestException("DOWNLOAD_NOT_FOUND", "The requested Parabolic download was not found.");
    }

    private void DownloadService_DownloadAdded(object? sender, DownloadAddedEventArgs eventArgs)
    {
        var options = eventArgs.Options;
        if (options is null || string.IsNullOrWhiteSpace(options.ClientRequestId))
        {
            return;
        }
        var session = new PersistentDownloadSession(
            eventArgs.Id,
            options.ClientRequestId,
            options.ClientTabId,
            eventArgs.Url,
            eventArgs.Path,
            options.Priority,
            options.ScheduledAt.HasValue && options.ScheduledAt.Value > DateTimeOffset.UtcNow
                ? DownloadTaskState.Scheduled
                : eventArgs.Status == DownloadStatus.Queued ? DownloadTaskState.Queued : DownloadTaskState.Running,
            options.ResolverName,
            options.ScheduledAt,
            options.SpeedLimitKbps,
            options.TaskAttempt,
            options.MaxTaskAttempts,
            options.GroupKey,
            options.CollectionId,
            options.PostProcessingSteps);
        lock (_sync)
        {
            if (_byExternalId.TryGetValue(session.ExternalId, out var previous))
            {
                previous.RetryCancellation?.Dispose();
                _byInternalId.Remove(previous.InternalId);
            }
            _byInternalId[eventArgs.Id] = session;
            _byExternalId[session.ExternalId] = session;
        }
        Publish(session);
    }

    private void DownloadService_DownloadStartedFromQueue(object? sender, DownloadEventArgs eventArgs)
    {
        if (TryGetSession(eventArgs.Id, out var session))
        {
            TransitionAndPublish(session, DownloadTaskState.Running);
        }
    }

    private void DownloadService_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs eventArgs)
    {
        if (!TryGetSession(eventArgs.Id, out var session))
        {
            return;
        }
        var log = eventArgs.LogChunk.ToString();
        var state = log.Contains("Merger", StringComparison.OrdinalIgnoreCase)
            || log.Contains("Merging", StringComparison.OrdinalIgnoreCase)
                ? DownloadTaskState.Processing
                : session.State == DownloadTaskState.Paused
                    ? DownloadTaskState.Paused
                    : DownloadTaskState.Running;
        lock (_sync)
        {
            if (log.Contains("N_m3u8DL-RE", StringComparison.OrdinalIgnoreCase))
            {
                session.Resolver = "n-m3u8dl-re";
            }
            if (DownloadTaskStateMachine.CanTransition(session.State, state))
            {
                session.State = state;
            }
            session.Progress = double.IsFinite(eventArgs.Progress) && eventArgs.Progress >= 0
                ? Math.Clamp(eventArgs.Progress * 100.0, 0.0, 100.0)
                : session.Progress;
            session.Speed = eventArgs.Speed > 0 ? eventArgs.SpeedString : null;
            session.Eta = eventArgs.Eta >= 0 ? eventArgs.Eta : null;
            session.Message = GetProgressMessage(eventArgs.LogChunk);
        }
        Publish(session);
    }

    private async void DownloadService_DownloadCompleted(object? sender, DownloadCompletedEventArgs eventArgs)
    {
        if (!TryGetSession(eventArgs.Id, out var session))
        {
            return;
        }
        try
        {
            lock (_sync)
            {
                session.Path = eventArgs.Path;
            }
            if (eventArgs.Status == DownloadStatus.Success)
            {
                TransitionAndPublish(session, DownloadTaskState.Processing);
                var result = await _postDownloadPipeline.ExecuteAsync(
                    session.Path,
                    session.PostProcessingSteps,
                    step =>
                    {
                        lock (_sync)
                        {
                            session.ProcessingStep = step;
                        }
                        Publish(session);
                    },
                    _lifetime.Token);
                lock (_sync)
                {
                    session.Progress = 100.0;
                    session.Message = null;
                    session.ProcessingStep = null;
                    session.Sha256 = result.Sha256;
                }
                TransitionAndPublish(session, DownloadTaskState.Completed);
                return;
            }

            var message = GetDownloadFailureMessage(eventArgs.Log);
            var failure = DownloadErrorClassifier.Classify(message);
            lock (_sync)
            {
                session.Message = message;
                session.Failure = failure;
            }
            if (failure.Retryable && session.Attempt < session.MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, session.Attempt) * 2));
                lock (_sync)
                {
                    session.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
                    session.RetryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                }
                TransitionAndPublish(session, DownloadTaskState.RetryScheduled);
                await Task.Delay(delay, session.RetryCancellation.Token);
                if (!await _downloadService.RetryAsync(session.InternalId))
                {
                    lock (_sync)
                    {
                        session.NextRetryAt = null;
                    }
                    TransitionAndPublish(session, DownloadTaskState.Failed);
                }
                return;
            }
            TransitionAndPublish(session, DownloadTaskState.Failed);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || session.State == DownloadTaskState.Cancelled)
        {
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                session.Message = exception.Message;
                session.ProcessingStep = null;
                session.Failure = new DownloadFailureInfo(
                    DownloadErrorCategory.PostProcessing,
                    false,
                    "Review the completed file and the post-processing settings.");
            }
            TransitionAndPublish(session, DownloadTaskState.Failed);
        }
    }

    private void DownloadService_DownloadStopped(object? sender, DownloadEventArgs eventArgs)
    {
        if (TryGetSession(eventArgs.Id, out var session))
        {
            TransitionAndPublish(session, DownloadTaskState.Cancelled);
        }
    }

    private bool TryGetSession(int internalId, out PersistentDownloadSession session)
    {
        lock (_sync)
        {
            return _byInternalId.TryGetValue(internalId, out session!);
        }
    }

    private void TransitionAndPublish(PersistentDownloadSession session, DownloadTaskState state)
    {
        lock (_sync)
        {
            if (!DownloadTaskStateMachine.CanTransition(session.State, state))
            {
                return;
            }
            session.State = state;
        }
        Publish(session);
    }

    private void Publish(PersistentDownloadSession session)
    {
        DownloadEventPayload payload;
        lock (_sync)
        {
            payload = new DownloadEventPayload
            {
                DownloadId = session.ExternalId,
                TabId = session.TabId,
                Status = DownloadTaskStateMachine.ToProtocolValue(session.State),
                Progress = session.Progress,
                Speed = session.Speed,
                Eta = session.Eta,
                Filename = string.IsNullOrWhiteSpace(session.Path) ? null : Path.GetFileName(session.Path),
                Message = session.Message,
                Priority = session.Priority.ToString().ToLowerInvariant(),
                Resolver = session.Resolver,
                ScheduledAt = session.ScheduledAt?.ToString("O"),
                SpeedLimitKbps = session.SpeedLimitKbps,
                ErrorCategory = session.Failure?.Category.ToString().ToLowerInvariant(),
                Retryable = session.Failure?.Retryable,
                ActionHint = session.Failure?.ActionHint,
                Attempt = session.Attempt,
                MaxAttempts = session.MaxAttempts,
                NextRetryAt = session.NextRetryAt?.ToString("O"),
                GroupKey = string.IsNullOrWhiteSpace(session.GroupKey) ? null : session.GroupKey,
                CollectionId = string.IsNullOrWhiteSpace(session.CollectionId) ? null : session.CollectionId,
                ProcessingStep = session.ProcessingStep,
                Sha256 = session.Sha256
            };
        }
        EventProduced?.Invoke(payload);
    }

    private static string GetDownloadFailureMessage(ReadOnlyMemory<char> log)
    {
        var lines = log.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var error = lines.LastOrDefault(line => line.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
            ?? lines.LastOrDefault();
        if (string.IsNullOrWhiteSpace(error))
        {
            return "The download failed before the selected resolver returned an error message.";
        }
        error = error.Trim();
        return error.Length <= 400 ? error : $"{error[..397]}...";
    }

    private static string? GetProgressMessage(ReadOnlyMemory<char> logChunk)
    {
        var message = logChunk.ToString().Trim();
        if (string.IsNullOrWhiteSpace(message)
            || message.StartsWith("[Parabolic] Progress", StringComparison.Ordinal)
            || message.StartsWith("[debug]", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return message.Length <= 300 ? message : $"{message[..297]}...";
    }

    private sealed class PersistentDownloadSession
    {
        public int InternalId { get; }
        public string ExternalId { get; }
        public int TabId { get; }
        public Uri Url { get; }
        public string Path { get; set; }
        public DownloadPriority Priority { get; set; }
        public DownloadTaskState State { get; set; }
        public double? Progress { get; set; }
        public string? Speed { get; set; }
        public int? Eta { get; set; }
        public string? Message { get; set; }
        public string Resolver { get; set; }
        public DateTimeOffset? ScheduledAt { get; }
        public int? SpeedLimitKbps { get; }
        public int Attempt { get; }
        public int MaxAttempts { get; }
        public string GroupKey { get; }
        public string CollectionId { get; }
        public IReadOnlyList<string> PostProcessingSteps { get; }
        public DownloadFailureInfo? Failure { get; set; }
        public DateTimeOffset? NextRetryAt { get; set; }
        public string? ProcessingStep { get; set; }
        public string? Sha256 { get; set; }
        public CancellationTokenSource? RetryCancellation { get; set; }

        public PersistentDownloadSession(
            int internalId,
            string externalId,
            int tabId,
            Uri url,
            string path,
            DownloadPriority priority,
            DownloadTaskState state,
            string resolver,
            DateTimeOffset? scheduledAt,
            int? speedLimitKbps,
            int attempt,
            int maxAttempts,
            string groupKey,
            string collectionId,
            IReadOnlyList<string> postProcessingSteps)
        {
            InternalId = internalId;
            ExternalId = externalId;
            TabId = tabId;
            Url = url;
            Path = path;
            Priority = priority;
            State = state;
            Resolver = string.IsNullOrWhiteSpace(resolver) ? "yt-dlp" : resolver;
            ScheduledAt = scheduledAt;
            SpeedLimitKbps = speedLimitKbps;
            Attempt = Math.Max(1, attempt);
            MaxAttempts = Math.Clamp(maxAttempts, 1, 10);
            GroupKey = groupKey ?? string.Empty;
            CollectionId = collectionId ?? string.Empty;
            PostProcessingSteps = postProcessingSteps ?? ["verify-output"];
        }

        public DownloadSnapshot ToSnapshot() => new()
        {
            DownloadId = ExternalId,
            TabId = TabId,
            Url = Url.ToString(),
            Filename = string.IsNullOrWhiteSpace(Path) ? string.Empty : System.IO.Path.GetFileName(Path),
            Status = DownloadTaskStateMachine.ToProtocolValue(State),
            Priority = Priority.ToString().ToLowerInvariant(),
            Progress = Progress,
            Speed = Speed,
            Eta = Eta,
            Message = Message,
            Resolver = Resolver,
            ScheduledAt = ScheduledAt?.ToString("O"),
            SpeedLimitKbps = SpeedLimitKbps,
            ErrorCategory = Failure?.Category.ToString().ToLowerInvariant(),
            Retryable = Failure?.Retryable,
            ActionHint = Failure?.ActionHint,
            Attempt = Attempt,
            MaxAttempts = MaxAttempts,
            NextRetryAt = NextRetryAt?.ToString("O"),
            GroupKey = string.IsNullOrWhiteSpace(GroupKey) ? null : GroupKey,
            CollectionId = string.IsNullOrWhiteSpace(CollectionId) ? null : CollectionId,
            ProcessingStep = ProcessingStep,
            Sha256 = Sha256
        };
    }
}
