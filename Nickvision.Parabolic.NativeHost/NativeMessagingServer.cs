using Nickvision.Desktop.Application;
using Nickvision.Desktop.Helpers;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal sealed class NativeMessagingServer : IDisposable
{
    public const int ProtocolVersion = 1;

    private static readonly string[] SupportedPresets = ["best", "1080", "720", "480", "audio"];

    private readonly NativeMessagingTransport _transport;
    private readonly IConfigurationService _configurationService;
    private readonly IDiscoveryService _discoveryService;
    private readonly IDownloadService _downloadService;
    private readonly ConcurrentDictionary<string, CachedDiscovery> _discoveryCache;
    private readonly ConcurrentDictionary<int, DownloadSession> _sessionsByInternalId;
    private readonly ConcurrentDictionary<string, DownloadSession> _sessionsByExternalId;
    private readonly SemaphoreSlim _downloadMutationLock;
    private CancellationToken _shutdownToken;

    public NativeMessagingServer(
        NativeMessagingTransport transport,
        IConfigurationService configurationService,
        IDiscoveryService discoveryService,
        IDownloadService downloadService)
    {
        _transport = transport;
        _configurationService = configurationService;
        _discoveryService = discoveryService;
        _downloadService = downloadService;
        _discoveryCache = new ConcurrentDictionary<string, CachedDiscovery>(StringComparer.Ordinal);
        _sessionsByInternalId = new ConcurrentDictionary<int, DownloadSession>();
        _sessionsByExternalId = new ConcurrentDictionary<string, DownloadSession>(StringComparer.Ordinal);
        _downloadMutationLock = new SemaphoreSlim(1, 1);
        _shutdownToken = CancellationToken.None;
        _downloadService.DownloadProgressChanged += DownloadService_DownloadProgressChanged;
        _downloadService.DownloadCompleted += DownloadService_DownloadCompleted;
        _downloadService.DownloadStopped += DownloadService_DownloadStopped;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _shutdownToken = cancellationToken;
        while (!cancellationToken.IsCancellationRequested)
        {
            NativeRequest? request;
            try
            {
                request = await _transport.ReadRequestAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Unable to read Native Messaging request: {exception.Message}");
                break;
            }
            if (request is null)
            {
                break;
            }
            await HandleRequestAsync(request, cancellationToken);
        }
    }

    public void Dispose()
    {
        _downloadService.DownloadProgressChanged -= DownloadService_DownloadProgressChanged;
        _downloadService.DownloadCompleted -= DownloadService_DownloadCompleted;
        _downloadService.DownloadStopped -= DownloadService_DownloadStopped;
        _downloadMutationLock.Dispose();
    }

    private async Task HandleRequestAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ValidateEnvelope(request);
            switch (request.Type)
            {
                case "hello":
                    await SendSuccessAsync(request.RequestId, new HelloResponse
                    {
                        AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2026.8.0",
                        Capabilities = ["formats", "download", "progress", "cancel", "open-folder"]
                    }, NativeJsonContext.Default.HelloResponse, cancellationToken);
                    break;
                case "get-formats":
                    await HandleGetFormatsAsync(request, cancellationToken);
                    break;
                case "download":
                    await HandleDownloadAsync(request, cancellationToken);
                    break;
                case "cancel":
                    await HandleCancelAsync(request, cancellationToken);
                    break;
                case "open-folder":
                    await HandleOpenFolderAsync(request, cancellationToken);
                    break;
                default:
                    throw new NativeRequestException("UNKNOWN_REQUEST", $"Unsupported Native Messaging request: {request.Type}.");
            }
        }
        catch (NativeRequestException exception)
        {
            await SendErrorAsync(request.RequestId, exception.Code, exception.Message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Native Messaging request failed: {exception}");
            await SendErrorAsync(
                request.RequestId,
                "INTERNAL_ERROR",
                "Parabolic could not complete the request. Check the application log for details.",
                cancellationToken);
        }
    }

    private async Task HandleGetFormatsAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        var mediaRequest = DeserializePayload(request, NativeJsonContext.Default.MediaRequest);
        var discovery = await ResolveDiscoveryAsync(mediaRequest, cancellationToken);
        EnsureFormatChoices(discovery);
        await SendSuccessAsync(request.RequestId, new FormatsResponse
        {
            Formats = discovery.Formats.Values.Select(cached => cached.Choice).Take(20).ToList()
        }, NativeJsonContext.Default.FormatsResponse, cancellationToken);
    }

    private async Task HandleDownloadAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        var mediaRequest = DeserializePayload(request, NativeJsonContext.Default.MediaRequest);
        ValidatePreset(mediaRequest.Preset);
        var externalId = ($"download-{Guid.NewGuid():N}")[..21];
        DownloadOptions options;
        if (!string.IsNullOrWhiteSpace(mediaRequest.FormatId))
        {
            await SendEventAsync(new DownloadEventPayload
            {
                DownloadId = externalId,
                TabId = mediaRequest.TabId,
                Status = "analyzing"
            });

            var discovery = await ResolveDiscoveryAsync(mediaRequest, cancellationToken);
            EnsureFormatChoices(discovery);
            if (!discovery.Formats.TryGetValue(mediaRequest.FormatId, out var selectedFormat))
            {
                throw new NativeRequestException(
                    "INVALID_FORMAT",
                    "The requested format was not returned by Parabolic for this media.");
            }
            options = BuildDiscoveredDownloadOptions(mediaRequest, discovery, selectedFormat);
        }
        else
        {
            // Quick presets do not need a preliminary yt-dlp discovery. The download
            // process performs its own extraction, so enqueue it immediately.
            options = BuildDirectDownloadOptions(mediaRequest);
        }

        var session = new DownloadSession(externalId, mediaRequest.TabId, options.Url);
        await _downloadMutationLock.WaitAsync(cancellationToken);
        try
        {
            DownloadAddedEventArgs? added = null;
            EventHandler<DownloadAddedEventArgs>? handler = null;
            handler = (_, eventArgs) =>
            {
                if (added is not null || eventArgs.Url != options.Url)
                {
                    return;
                }
                added = eventArgs;
                session.InternalId = eventArgs.Id;
                session.Path = eventArgs.Path;
                _sessionsByInternalId[eventArgs.Id] = session;
                _sessionsByExternalId[externalId] = session;
            };
            _downloadService.DownloadAdded += handler;
            try
            {
                await _downloadService.AddAsync(options, false);
            }
            finally
            {
                _downloadService.DownloadAdded -= handler;
            }
            if (added is null)
            {
                throw new NativeRequestException("DOWNLOAD_NOT_STARTED", "Parabolic could not add the download to its queue.");
            }
            await SendSuccessAsync(request.RequestId, new DownloadResponse
            {
                DownloadId = externalId,
                Status = added.Status == DownloadStatus.Queued ? "queued" : "downloading"
            }, NativeJsonContext.Default.DownloadResponse, cancellationToken);
        }
        finally
        {
            _downloadMutationLock.Release();
        }
    }

    private async Task HandleCancelAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        var cancelRequest = DeserializePayload(request, NativeJsonContext.Default.CancelRequest);
        if (!_sessionsByExternalId.TryGetValue(cancelRequest.DownloadId, out var session) || session.InternalId < 0)
        {
            throw new NativeRequestException("DOWNLOAD_NOT_FOUND", "The requested Parabolic download was not found.");
        }
        await _downloadMutationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await _downloadService.StopAsync(session.InternalId))
            {
                throw new NativeRequestException("DOWNLOAD_NOT_RUNNING", "The requested download is no longer running.");
            }
        }
        finally
        {
            _downloadMutationLock.Release();
        }
        await SendSuccessAsync(request.RequestId, new EmptyPayload(), NativeJsonContext.Default.EmptyPayload, cancellationToken);
    }

    private async Task HandleOpenFolderAsync(NativeRequest request, CancellationToken cancellationToken)
    {
        var openRequest = DeserializePayload(request, NativeJsonContext.Default.OpenFolderRequest);
        if (!_sessionsByExternalId.TryGetValue(openRequest.DownloadId, out var session)
            || string.IsNullOrWhiteSpace(session.Path)
            || !File.Exists(session.Path))
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
        await SendSuccessAsync(request.RequestId, new EmptyPayload(), NativeJsonContext.Default.EmptyPayload, cancellationToken);
    }

    private async Task<CachedDiscovery> ResolveDiscoveryAsync(MediaRequest request, CancellationToken cancellationToken)
    {
        var candidates = new List<Uri>(3);
        AddCandidate(candidates, request.PageUrl);
        AddCandidate(candidates, request.FrameUrl);
        AddCandidate(candidates, request.MediaUrl);
        if (candidates.Count == 0)
        {
            throw new NativeRequestException("INVALID_URL", "Parabolic accepts only HTTP and HTTPS media URLs.");
        }

        Exception? lastException = null;
        foreach (var candidate in candidates)
        {
            if (_discoveryCache.TryGetValue(candidate.AbsoluteUri, out var cached)
                && DateTimeOffset.UtcNow - cached.CreatedAt < TimeSpan.FromMinutes(10))
            {
                return cached;
            }
            try
            {
                var result = await _discoveryService.GetForUrlAsync(candidate, null, cancellationToken);
                if (result.Media.Count == 0)
                {
                    continue;
                }
                var media = result.Media[0];
                var discovery = new CachedDiscovery(candidate, result, media);
                _discoveryCache[candidate.AbsoluteUri] = discovery;
                return discovery;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastException = exception;
            }
        }
        throw new NativeRequestException(
            "NO_MEDIA",
            "Parabolic could not find downloadable media at this address.",
            lastException ?? new InvalidOperationException("No media candidates succeeded."));
    }

    private DownloadOptions BuildDirectDownloadOptions(MediaRequest request)
    {
        var candidates = new List<Uri>(3);
        AddCandidate(candidates, request.PageUrl);
        AddCandidate(candidates, request.FrameUrl);
        AddCandidate(candidates, request.MediaUrl);
        if (candidates.Count == 0)
        {
            throw new NativeRequestException("INVALID_URL", "Parabolic accepts only HTTP and HTTPS media URLs.");
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? "Media"
            : request.Title.SanitizeForFilename(_configurationService.LimitCharacters).Trim();
        var isAudio = request.Preset == "audio";
        var options = new DownloadOptions(candidates[0])
        {
            SaveFilename = string.IsNullOrWhiteSpace(title) ? "Media" : title,
            SaveFolder = _configurationService.PreviousSaveFolder,
            FileType = isAudio
                ? _configurationService.PreviousAudioOnlyFileType
                : _configurationService.PreviousFullFileType,
            UseSleepPreset = false
        };
        ApplyPreset(options, request.Preset, isAudio);
        return options;
    }

    private DownloadOptions BuildDiscoveredDownloadOptions(
        MediaRequest request,
        CachedDiscovery discovery,
        CachedFormat selectedFormat)
    {
        var media = discovery.Media;
        var url = media.Url.IsEmpty ? discovery.SourceUrl : media.Url;
        var isAudio = selectedFormat.IsAudio || request.Preset == "audio" || media.Type == MediaType.Audio;
        var options = new DownloadOptions(url)
        {
            SaveFilename = string.IsNullOrWhiteSpace(media.Title) ? "Media" : media.Title,
            SaveFolder = !string.IsNullOrWhiteSpace(media.SuggestedSaveFolder) && Directory.Exists(media.SuggestedSaveFolder)
                ? media.SuggestedSaveFolder
                : _configurationService.PreviousSaveFolder,
            FileType = isAudio
                ? _configurationService.PreviousAudioOnlyFileType
                : _configurationService.PreviousFullFileType,
            PlaylistPosition = media.PlaylistPosition,
            RequiresPlaylistItems = media.RequiresPlaylistItems,
            UseSleepPreset = false
        };

        options.FormatSelector = selectedFormat.Selector;
        return options;
    }

    private static void ApplyPreset(DownloadOptions options, string preset, bool isAudio)
    {
        if (isAudio)
        {
            options.VideoFormat = Format.NoneVideo;
            options.AudioFormat = Format.BestAudio;
            return;
        }
        options.AudioFormat = Format.BestAudio;
        options.VideoResolution = preset switch
        {
            "1080" => new VideoResolution(1920, 1080),
            "720" => new VideoResolution(1280, 720),
            "480" => new VideoResolution(854, 480),
            _ => VideoResolution.Best
        };
    }

    private void EnsureFormatChoices(CachedDiscovery discovery)
    {
        if (!discovery.Formats.IsEmpty)
        {
            return;
        }
        var actualFormats = discovery.Media.Formats.Where(format =>
            format != Format.BestVideo
            && format != Format.WorstVideo
            && format != Format.NoneVideo
            && format != Format.BestAudio
            && format != Format.WorstAudio
            && format != Format.NoneAudio);

        foreach (var format in actualFormats
            .Where(format => format.Type == MediaType.Video)
            .OrderByDescending(format => format.VideoResolution?.Height ?? 0)
            .ThenByDescending(format => format.FrameRate)
            .ThenByDescending(format => format.Bitrate)
            .Take(16))
        {
            var containsAudio = format.AudioCodec.HasValue;
            var selector = containsAudio ? format.Id : $"{format.Id}+bestaudio/best";
            discovery.Formats.TryAdd(selector, new CachedFormat(
                selector,
                false,
                new FormatChoice
                {
                    Id = selector,
                    Label = format.VideoResolution is not null
                        ? $"{format.VideoResolution.Height}p"
                        : "Video",
                    Resolution = format.VideoResolution?.ToString() ?? string.Empty,
                    Ext = format.Extension,
                    FilesizeLabel = FormatBytes(format.Bytes),
                    Note = BuildVideoNote(format, containsAudio)
                }));
        }

        var remaining = Math.Max(0, 20 - discovery.Formats.Count);
        foreach (var format in actualFormats
            .Where(format => format.Type == MediaType.Audio)
            .OrderByDescending(format => format.Bitrate)
            .Take(remaining))
        {
            discovery.Formats.TryAdd(format.Id, new CachedFormat(
                format.Id,
                true,
                new FormatChoice
                {
                    Id = format.Id,
                    Label = format.Bitrate.HasValue ? $"{format.Bitrate.Value:0} kbps audio" : "Audio only",
                    Ext = format.Extension,
                    FilesizeLabel = FormatBytes(format.Bytes),
                    Note = format.AudioCodec?.ToString() ?? "audio"
                }));
        }
    }

    private void DownloadService_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs eventArgs)
    {
        if (!_sessionsByInternalId.TryGetValue(eventArgs.Id, out var session))
        {
            return;
        }
        var log = eventArgs.LogChunk.ToString();
        var status = log.Contains("Merger", StringComparison.OrdinalIgnoreCase)
            || log.Contains("Merging", StringComparison.OrdinalIgnoreCase)
            ? "merging"
            : "downloading";
        _ = SendEventAsync(new DownloadEventPayload
        {
            DownloadId = session.ExternalId,
            TabId = session.TabId,
            Status = status,
            Progress = double.IsFinite(eventArgs.Progress) && eventArgs.Progress >= 0
                ? Math.Clamp(eventArgs.Progress * 100.0, 0.0, 100.0)
                : null,
            Speed = eventArgs.Speed > 0 ? eventArgs.SpeedString : null,
            Eta = eventArgs.Eta >= 0 ? eventArgs.Eta : null,
            Filename = Path.GetFileName(session.Path)
        });
    }

    private void DownloadService_DownloadCompleted(object? sender, DownloadCompletedEventArgs eventArgs)
    {
        if (!_sessionsByInternalId.TryGetValue(eventArgs.Id, out var session))
        {
            return;
        }
        session.Path = eventArgs.Path;
        _ = SendEventAsync(new DownloadEventPayload
        {
            DownloadId = session.ExternalId,
            TabId = session.TabId,
            Status = eventArgs.Status == DownloadStatus.Success ? "completed" : "failed",
            Progress = eventArgs.Status == DownloadStatus.Success ? 100.0 : null,
            Filename = Path.GetFileName(eventArgs.Path),
            Message = eventArgs.Status == DownloadStatus.Success
                ? null
                : "The download failed. Check the Parabolic application log for details."
        });
    }

    private void DownloadService_DownloadStopped(object? sender, DownloadEventArgs eventArgs)
    {
        if (!_sessionsByInternalId.TryGetValue(eventArgs.Id, out var session))
        {
            return;
        }
        _ = SendEventAsync(new DownloadEventPayload
        {
            DownloadId = session.ExternalId,
            TabId = session.TabId,
            Status = "cancelled",
            Filename = Path.GetFileName(session.Path)
        });
    }

    private async Task SendEventAsync(DownloadEventPayload payload)
    {
        try
        {
            await _transport.WriteEventAsync(new NativeEventEnvelope { Payload = payload }, _shutdownToken);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Console.Error.WriteLine($"Unable to send Native Messaging event: {exception.Message}");
        }
    }

    private Task SendErrorAsync(string requestId, string code, string message, CancellationToken cancellationToken) =>
        _transport.WriteResponseAsync(new NativeResponse
        {
            RequestId = requestId,
            Ok = false,
            Error = new NativeError
            {
                Code = code,
                Message = message
            }
        }, cancellationToken);

    private Task SendSuccessAsync<T>(
        string requestId,
        T payload,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken) =>
        _transport.WriteResponseAsync(new NativeResponse
        {
            RequestId = requestId,
            Ok = true,
            Payload = JsonSerializer.SerializeToElement(payload, jsonTypeInfo)
        }, cancellationToken);

    private static T DeserializePayload<T>(NativeRequest request, JsonTypeInfo<T> jsonTypeInfo) where T : class =>
        request.Payload.Deserialize(jsonTypeInfo)
        ?? throw new NativeRequestException("INVALID_PAYLOAD", "The Native Messaging request payload is missing or invalid.");

    private static void ValidateEnvelope(NativeRequest request)
    {
        if (request.ProtocolVersion != ProtocolVersion)
        {
            throw new NativeRequestException(
                "UNSUPPORTED_PROTOCOL",
                $"Parabolic Native Messaging protocol {request.ProtocolVersion} is not supported.");
        }
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 200)
        {
            throw new NativeRequestException("INVALID_REQUEST_ID", "The Native Messaging request identifier is invalid.");
        }
        if (string.IsNullOrWhiteSpace(request.Type) || request.Type.Length > 50)
        {
            throw new NativeRequestException("INVALID_REQUEST_TYPE", "The Native Messaging request type is invalid.");
        }
    }

    private static void ValidatePreset(string preset)
    {
        if (!SupportedPresets.Contains(preset, StringComparer.Ordinal))
        {
            throw new NativeRequestException("INVALID_PRESET", "The requested Parabolic quality preset is not supported.");
        }
    }

    private static void AddCandidate(List<Uri> candidates, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || candidates.Contains(uri))
        {
            return;
        }
        candidates.Add(uri);
    }

    private static string BuildVideoNote(Format format, bool containsAudio)
    {
        var parts = new List<string>(4);
        if (format.VideoCodec.HasValue)
        {
            parts.Add(format.VideoCodec.Value.ToString());
        }
        if (format.FrameRate.HasValue)
        {
            parts.Add(format.FrameRate.Value switch
            {
                FrameRate.Fps24 => "24 FPS",
                FrameRate.Fps30 => "30 FPS",
                FrameRate.Fps60 => "60 FPS",
                _ => string.Empty
            });
        }
        parts.Add(containsAudio ? "video + audio" : "video + best audio");
        return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes == 0)
        {
            return string.Empty;
        }
        const double kib = 1024.0;
        const double mib = kib * 1024.0;
        const double gib = mib * 1024.0;
        return bytes switch
        {
            var value when value >= gib => $"{value / gib:0.0} GiB",
            var value when value >= mib => $"{value / mib:0.0} MiB",
            var value when value >= kib => $"{value / kib:0.0} KiB",
            _ => $"{bytes} B"
        };
    }

    private sealed class CachedDiscovery
    {
        public Uri SourceUrl { get; }
        public DiscoveryResult Result { get; }
        public Media Media { get; }
        public DateTimeOffset CreatedAt { get; }
        public ConcurrentDictionary<string, CachedFormat> Formats { get; }

        public CachedDiscovery(Uri sourceUrl, DiscoveryResult result, Media media)
        {
            SourceUrl = sourceUrl;
            Result = result;
            Media = media;
            CreatedAt = DateTimeOffset.UtcNow;
            Formats = new ConcurrentDictionary<string, CachedFormat>(StringComparer.Ordinal);
        }
    }

    private sealed class CachedFormat
    {
        public string Selector { get; }
        public bool IsAudio { get; }
        public FormatChoice Choice { get; }

        public CachedFormat(string selector, bool isAudio, FormatChoice choice)
        {
            Selector = selector;
            IsAudio = isAudio;
            Choice = choice;
        }
    }

    private sealed class DownloadSession
    {
        public string ExternalId { get; }
        public int TabId { get; }
        public Uri Url { get; }
        public int InternalId { get; set; }
        public string Path { get; set; }

        public DownloadSession(string externalId, int tabId, Uri url)
        {
            ExternalId = externalId;
            TabId = tabId;
            Url = url;
            InternalId = -1;
            Path = string.Empty;
        }
    }
}
