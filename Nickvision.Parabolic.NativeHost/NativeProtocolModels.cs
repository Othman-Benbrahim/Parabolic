using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nickvision.Parabolic.NativeHost;

internal sealed class NativeRequest
{
    public int ProtocolVersion { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}

internal sealed class NativeResponse
{
    public int ProtocolVersion { get; set; } = NativeMessagingServer.ProtocolVersion;
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = "response";
    public bool Ok { get; set; }
    public JsonElement? Payload { get; set; }
    public NativeError? Error { get; set; }
}

internal sealed class NativeEventEnvelope
{
    public int ProtocolVersion { get; set; } = NativeMessagingServer.ProtocolVersion;
    public string Type { get; set; } = "event";
    public DownloadEventPayload Payload { get; set; } = new();
}

internal sealed class NativeError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

internal sealed class HelloResponse
{
    public string AppVersion { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = NativeMessagingServer.ProtocolVersion;
    public IReadOnlyList<string> Capabilities { get; set; } = [];
}

internal sealed class MediaRequest
{
    public int TabId { get; set; } = -1;
    public string PageUrl { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string ManifestUrl { get; set; } = string.Empty;
    public string ManifestKind { get; set; } = string.Empty;
    public string DirectFallbackUrl { get; set; } = string.Empty;
    public string DirectFallbackKind { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Preset { get; set; } = "best";
    public string FormatId { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string FrameUrl { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string ResolverPreference { get; set; } = "auto";
    public string CobaltEndpoint { get; set; } = string.Empty;
    public string CobaltAuthScheme { get; set; } = "none";
    public string CobaltAuthToken { get; set; } = string.Empty;
    public int SpeedLimitKbps { get; set; }
    public string ScheduledAt { get; set; } = string.Empty;
    public string NetworkStrategy { get; set; } = "balanced";
    public string AuthenticationMode { get; set; } = "parabolic";
    public string ProxyMode { get; set; } = "parabolic";
    public bool SendPageReferer { get; set; }
}

internal sealed class CobaltRequest
{
    public string Url { get; set; } = string.Empty;
    public string VideoQuality { get; set; } = "max";
    public string DownloadMode { get; set; } = "auto";
    public string FilenameStyle { get; set; } = "basic";
    public string LocalProcessing { get; set; } = "disabled";
}

internal sealed class CancelRequest
{
    public string DownloadId { get; set; } = string.Empty;
}

internal sealed class OpenFolderRequest
{
    public string DownloadId { get; set; } = string.Empty;
}

internal sealed class DownloadControlRequest
{
    public string DownloadId { get; set; } = string.Empty;
}

internal sealed class SetPriorityRequest
{
    public string DownloadId { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
}

internal sealed class FormatsResponse
{
    public IReadOnlyList<FormatChoice> Formats { get; set; } = [];
}

internal sealed class FormatChoice
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Ext { get; set; } = string.Empty;
    public string FilesizeLabel { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

internal sealed class DownloadResponse
{
    public string DownloadId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string Priority { get; set; } = "normal";
    public string Resolver { get; set; } = "yt-dlp";
    public string? ScheduledAt { get; set; }
    public int? SpeedLimitKbps { get; set; }
}

public sealed class DownloadSnapshot
{
    public string DownloadId { get; set; } = string.Empty;
    public int TabId { get; set; } = -1;
    public string Url { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public double? Progress { get; set; }
    public string? Speed { get; set; }
    public int? Eta { get; set; }
    public string? Message { get; set; }
    public string Resolver { get; set; } = "yt-dlp";
    public string? ScheduledAt { get; set; }
    public int? SpeedLimitKbps { get; set; }
}

internal sealed class DownloadsResponse
{
    public IReadOnlyList<DownloadSnapshot> Downloads { get; set; } = [];
}

internal sealed class YtdlpUpdateResponse
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool UpdateAvailable { get; set; }
    public bool Updated { get; set; }
    public string Message { get; set; } = string.Empty;
}

internal sealed class EmptyPayload
{
}

public sealed class DownloadEventPayload
{
    public string DownloadId { get; set; } = string.Empty;
    public int TabId { get; set; } = -1;
    public string Status { get; set; } = string.Empty;
    public double? Progress { get; set; }
    public string? Speed { get; set; }
    public int? Eta { get; set; }
    public string? Filename { get; set; }
    public string? Message { get; set; }
    public string Priority { get; set; } = "normal";
    public string Resolver { get; set; } = "yt-dlp";
    public string? ScheduledAt { get; set; }
    public int? SpeedLimitKbps { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NativeRequest))]
[JsonSerializable(typeof(NativeResponse))]
[JsonSerializable(typeof(NativeEventEnvelope))]
[JsonSerializable(typeof(HelloResponse))]
[JsonSerializable(typeof(MediaRequest))]
[JsonSerializable(typeof(CobaltRequest))]
[JsonSerializable(typeof(CancelRequest))]
[JsonSerializable(typeof(OpenFolderRequest))]
[JsonSerializable(typeof(DownloadControlRequest))]
[JsonSerializable(typeof(SetPriorityRequest))]
[JsonSerializable(typeof(FormatsResponse))]
[JsonSerializable(typeof(FormatChoice))]
[JsonSerializable(typeof(DownloadResponse))]
[JsonSerializable(typeof(DownloadSnapshot))]
[JsonSerializable(typeof(DownloadsResponse))]
[JsonSerializable(typeof(YtdlpUpdateResponse))]
[JsonSerializable(typeof(EmptyPayload))]
internal partial class NativeJsonContext : JsonSerializerContext
{
}
