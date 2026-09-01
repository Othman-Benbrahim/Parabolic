using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal sealed record ResolvedMedia(Uri Url, string Filename, string ResolverName, string SourceKind);

internal sealed record ResolverCapabilities(
    bool HandlesDirectFiles,
    bool HandlesPages,
    bool HandlesCollections,
    bool RequiresNetwork);

internal interface IMediaResolver
{
    string Name { get; }
    ResolverCapabilities Capabilities { get; }
    Task<ResolvedMedia?> ResolveAsync(MediaRequest request, CancellationToken cancellationToken);
}

internal sealed class MediaResolverRegistry
{
    private readonly IReadOnlyDictionary<string, IMediaResolver> _resolvers;

    public MediaResolverRegistry(IEnumerable<IMediaResolver> resolvers)
    {
        _resolvers = resolvers.ToDictionary(resolver => resolver.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Names => _resolvers.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();

    public Task<ResolvedMedia?> ResolveAsync(
        string name,
        MediaRequest request,
        CancellationToken cancellationToken)
    {
        if (!_resolvers.TryGetValue(name, out var resolver))
        {
            throw new NativeRequestException("RESOLVER_NOT_FOUND", $"The requested media resolver is not registered: {name}.");
        }
        return resolver.ResolveAsync(request, cancellationToken);
    }
}

internal sealed class DirectMediaResolver : IMediaResolver
{
    private static readonly HashSet<string> DirectSourceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio", "context-menu", "dash", "direct", "hls", "html5", "network", "video"
    };

    public string Name => "direct";
    public ResolverCapabilities Capabilities { get; } = new(true, false, false, false);

    public Task<ResolvedMedia?> ResolveAsync(MediaRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DirectSourceKinds.Contains(request.SourceKind)
            || !TryCreateHttpUri(request.MediaUrl, out var mediaUri))
        {
            return Task.FromResult<ResolvedMedia?>(null);
        }
        return Task.FromResult<ResolvedMedia?>(new ResolvedMedia(
            mediaUri,
            request.Title,
            Name,
            string.IsNullOrWhiteSpace(request.SourceKind) ? "direct" : request.SourceKind.ToLowerInvariant()));
    }

    internal static bool TryCreateHttpUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }
}

internal sealed class CobaltMediaResolver : IMediaResolver
{
    private readonly HttpClient _httpClient;

    public string Name => "cobalt";
    public ResolverCapabilities Capabilities { get; } = new(false, true, false, true);

    public CobaltMediaResolver(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResolvedMedia?> ResolveAsync(MediaRequest request, CancellationToken cancellationToken)
    {
        if (!DirectMediaResolver.TryCreateHttpUri(request.CobaltEndpoint, out var endpoint))
        {
            return null;
        }
        if (!DirectMediaResolver.TryCreateHttpUri(request.PageUrl, out var source)
            && !DirectMediaResolver.TryCreateHttpUri(request.FrameUrl, out source)
            && !DirectMediaResolver.TryCreateHttpUri(request.MediaUrl, out source))
        {
            throw new NativeRequestException("INVALID_URL", "Cobalt requires an HTTP or HTTPS source URL.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ApplyAuthorization(message, request.CobaltAuthScheme, request.CobaltAuthToken);
        var cobaltRequest = new CobaltRequest
        {
            Url = source.AbsoluteUri,
            VideoQuality = request.Preset is "1080" or "720" or "480" ? request.Preset : "max",
            DownloadMode = request.Preset == "audio" ? "audio" : "auto",
            FilenameStyle = "basic",
            LocalProcessing = "disabled"
        };
        message.Content = new StringContent(
            JsonSerializer.Serialize(cobaltRequest, NativeJsonContext.Default.CobaltRequest),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new NativeRequestException(
                "COBALT_HTTP_ERROR",
                $"The configured Cobalt instance returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var status = GetString(root, "status");
        if (status == "error")
        {
            var code = root.TryGetProperty("error", out var error)
                ? GetString(error, "code")
                : string.Empty;
            throw new NativeRequestException(
                "COBALT_RESOLUTION_FAILED",
                string.IsNullOrWhiteSpace(code)
                    ? "Cobalt could not resolve this media."
                    : $"Cobalt could not resolve this media ({code}).");
        }

        var resolvedUrl = status switch
        {
            "tunnel" or "redirect" => GetString(root, "url"),
            "picker" => GetPickerUrl(root),
            "local-processing" => GetSingleTunnelUrl(root),
            _ => string.Empty
        };
        if (!DirectMediaResolver.TryCreateHttpUri(resolvedUrl, out var resolvedUri))
        {
            throw new NativeRequestException(
                "COBALT_UNSUPPORTED_RESPONSE",
                "Cobalt did not return a downloadable HTTP media URL.");
        }
        return new ResolvedMedia(
            resolvedUri,
            GetString(root, "filename"),
            Name,
            "cobalt");
    }

    private static void ApplyAuthorization(HttpRequestMessage message, string scheme, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.Equals(scheme, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        message.Headers.Authorization = scheme.ToLowerInvariant() switch
        {
            "api-key" => new AuthenticationHeaderValue("Api-Key", token.Trim()),
            "bearer" => new AuthenticationHeaderValue("Bearer", token.Trim()),
            _ => throw new NativeRequestException(
                "INVALID_COBALT_AUTH",
                "Cobalt authentication must be none, API key, or bearer token.")
        };
    }

    private static string GetPickerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("picker", out var picker) || picker.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }
        var items = picker.EnumerateArray().ToList();
        var preferred = items.FirstOrDefault(item =>
            GetString(item, "type") is "video" or "gif");
        if (preferred.ValueKind == JsonValueKind.Undefined)
        {
            preferred = items.FirstOrDefault();
        }
        return preferred.ValueKind == JsonValueKind.Undefined ? string.Empty : GetString(preferred, "url");
    }

    private static string GetSingleTunnelUrl(JsonElement root)
    {
        if (!root.TryGetProperty("tunnel", out var tunnel) || tunnel.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }
        var urls = tunnel.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : GetString(item, "url"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        return urls.Count == 1 ? urls[0] : string.Empty;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
}
