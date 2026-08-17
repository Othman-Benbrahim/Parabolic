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

public sealed class PersistentDownloadCoordinator : IDisposable
{
    private readonly IDownloadService _downloadService;
    private readonly object _sync;
    private readonly Dictionary<int, PersistentDownloadSession> _byInternalId;
    private readonly Dictionary<string, PersistentDownloadSession> _byExternalId;
    private readonly SemaphoreSlim _mutationLock;

    public event Action<DownloadEventPayload>? EventProduced;

    public PersistentDownloadCoordinator(IDownloadService downloadService)
    {
        _downloadService = downloadService;
        _sync = new object();
        _byInternalId = new Dictionary<int, PersistentDownloadSession>();
        _byExternalId = new Dictionary<string, PersistentDownloadSession>(StringComparer.Ordinal);
        _mutationLock = new SemaphoreSlim(1, 1);
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
                        ? "scheduled"
                        : "queued",
                    options.ResolverName,
                    options.ScheduledAt,
                    options.SpeedLimitKbps);
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
                .Where(session => !IsTerminal(session.Status))
                .OrderByDescending(session => session.Priority)
                .ThenBy(session => session.InternalId)
                .Select(session => session.ToSnapshot())
                .ToList();
        }
    }

    public async Task CancelAsync(string externalId, CancellationToken cancellationToken)
    {
        var session = GetSession(externalId);
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
        UpdateAndPublish(session, "paused");
    }

    public void Resume(string externalId)
    {
        var session = GetSession(externalId);
        if (!_downloadService.Resume(session.InternalId))
        {
            throw new NativeRequestException("DOWNLOAD_NOT_PAUSED", "The requested download is not paused.");
        }
        UpdateAndPublish(session, "downloading");
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
        Publish(session, session.Status);
    }

    public void OpenFolder(string externalId)
    {
        var session = GetSession(externalId);
        if (string.IsNullOrWhiteSpace(session.Path) || !File.Exists(session.Path))
        {
            throw new NativeRequestException("FILE_NOT_FOUND", "The completed download file was not found.");
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new NativeRequestException("UNSUPPORTED_PLATFORM", "Opening the containing folder is available on Windows only.");
        }
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            Arguments = $"/select,\"{session.Path}\"",
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        _downloadService.DownloadAdded -= DownloadService_DownloadAdded;
        _downloadService.DownloadCompleted -= DownloadService_DownloadCompleted;
        _downloadService.DownloadProgressChanged -= DownloadService_DownloadProgressChanged;
        _downloadService.DownloadStartedFromQueue -= DownloadService_DownloadStartedFromQueue;
        _downloadService.DownloadStopped -= DownloadService_DownloadStopped;
        _mutationLock.Dispose();
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
                ? "scheduled"
                : eventArgs.Status == DownloadStatus.Queued ? "queued" : "downloading",
            options.ResolverName,
            options.ScheduledAt,
            options.SpeedLimitKbps);
        lock (_sync)
        {
            _byInternalId[eventArgs.Id] = session;
            _byExternalId[session.ExternalId] = session;
        }
        Publish(session, session.Status);
    }

    private void DownloadService_DownloadStartedFromQueue(object? sender, DownloadEventArgs eventArgs)
    {
        if (TryGetSession(eventArgs.Id, out var session))
        {
            UpdateAndPublish(session, "downloading");
        }
    }

    private void DownloadService_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs eventArgs)
    {
        if (!TryGetSession(eventArgs.Id, out var session))
        {
            return;
        }
        var log = eventArgs.LogChunk.ToString();
        var status = log.Contains("ERROR:", StringComparison.OrdinalIgnoreCase)
            ? "failed"
            : log.Contains("Merger", StringComparison.OrdinalIgnoreCase)
                || log.Contains("Merging", StringComparison.OrdinalIgnoreCase)
                ? "merging"
                : session.Status == "paused" ? "paused" : "downloading";
        lock (_sync)
        {
            session.Status = status;
            session.Progress = double.IsFinite(eventArgs.Progress) && eventArgs.Progress >= 0
                ? Math.Clamp(eventArgs.Progress * 100.0, 0.0, 100.0)
                : session.Progress;
            session.Speed = eventArgs.Speed > 0 ? eventArgs.SpeedString : null;
            session.Eta = eventArgs.Eta >= 0 ? eventArgs.Eta : null;
            session.Message = GetProgressMessage(eventArgs.LogChunk);
        }
        Publish(session, status);
    }

    private void DownloadService_DownloadCompleted(object? sender, DownloadCompletedEventArgs eventArgs)
    {
        if (!TryGetSession(eventArgs.Id, out var session))
        {
            return;
        }
        lock (_sync)
        {
            session.Path = eventArgs.Path;
            session.Status = eventArgs.Status == DownloadStatus.Success ? "completed" : "failed";
            session.Progress = eventArgs.Status == DownloadStatus.Success ? 100.0 : session.Progress;
            session.Message = eventArgs.Status == DownloadStatus.Success
                ? null
                : GetDownloadFailureMessage(eventArgs.Log);
        }
        Publish(session, session.Status);
    }

    private void DownloadService_DownloadStopped(object? sender, DownloadEventArgs eventArgs)
    {
        if (TryGetSession(eventArgs.Id, out var session))
        {
            UpdateAndPublish(session, "cancelled");
        }
    }

    private bool TryGetSession(int internalId, out PersistentDownloadSession session)
    {
        lock (_sync)
        {
            return _byInternalId.TryGetValue(internalId, out session!);
        }
    }

    private void UpdateAndPublish(PersistentDownloadSession session, string status)
    {
        lock (_sync)
        {
            session.Status = status;
        }
        Publish(session, status);
    }

    private void Publish(PersistentDownloadSession session, string status)
    {
        DownloadEventPayload payload;
        lock (_sync)
        {
            payload = new DownloadEventPayload
            {
                DownloadId = session.ExternalId,
                TabId = session.TabId,
                Status = status,
                Progress = session.Progress,
                Speed = session.Speed,
                Eta = session.Eta,
                Filename = string.IsNullOrWhiteSpace(session.Path) ? null : Path.GetFileName(session.Path),
                Message = session.Message,
                Priority = session.Priority.ToString().ToLowerInvariant(),
                Resolver = session.Resolver,
                ScheduledAt = session.ScheduledAt?.ToString("O"),
                SpeedLimitKbps = session.SpeedLimitKbps
            };
        }
        EventProduced?.Invoke(payload);
    }

    private static bool IsTerminal(string status) =>
        status is "completed" or "failed" or "cancelled";

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
        public string Status { get; set; }
        public double? Progress { get; set; }
        public string? Speed { get; set; }
        public int? Eta { get; set; }
        public string? Message { get; set; }
        public string Resolver { get; }
        public DateTimeOffset? ScheduledAt { get; }
        public int? SpeedLimitKbps { get; }

        public PersistentDownloadSession(
            int internalId,
            string externalId,
            int tabId,
            Uri url,
            string path,
            DownloadPriority priority,
            string status,
            string resolver,
            DateTimeOffset? scheduledAt,
            int? speedLimitKbps)
        {
            InternalId = internalId;
            ExternalId = externalId;
            TabId = tabId;
            Url = url;
            Path = path;
            Priority = priority;
            Status = status;
            Resolver = string.IsNullOrWhiteSpace(resolver) ? "yt-dlp" : resolver;
            ScheduledAt = scheduledAt;
            SpeedLimitKbps = speedLimitKbps;
        }

        public DownloadSnapshot ToSnapshot() => new()
        {
            DownloadId = ExternalId,
            TabId = TabId,
            Url = Url.ToString(),
            Filename = string.IsNullOrWhiteSpace(Path) ? string.Empty : System.IO.Path.GetFileName(Path),
            Status = Status,
            Priority = Priority.ToString().ToLowerInvariant(),
            Progress = Progress,
            Speed = Speed,
            Eta = Eta,
            Message = Message,
            Resolver = Resolver,
            ScheduledAt = ScheduledAt?.ToString("O"),
            SpeedLimitKbps = SpeedLimitKbps
        };
    }
}
