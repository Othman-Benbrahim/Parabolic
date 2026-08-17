using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public sealed class UrlRenewalService : IUrlRenewalService
{
    private readonly HttpClient _httpClient;

    public UrlRenewalService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task RenewAsync(DownloadOptions options, CancellationToken cancellationToken)
    {
        if (string.Equals(options.RenewalMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!string.Equals(options.RenewalMode, "cobalt", StringComparison.OrdinalIgnoreCase)
            || options.RenewalEndpoint is null
            || options.RenewalSourceUrl is null)
        {
            throw new InvalidOperationException("The saved media URL renewal request is invalid.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.RenewalEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(new UrlRenewalRequest
        {
            Url = options.RenewalSourceUrl.AbsoluteUri,
            VideoQuality = options.RenewalPreset is "1080" or "720" or "480" ? options.RenewalPreset : "max",
            DownloadMode = options.RenewalPreset == "audio" ? "audio" : "auto",
            FilenameStyle = "basic",
            LocalProcessing = "disabled"
        }, ApplicationJsonContext.Default.UrlRenewalRequest), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The configured Cobalt instance returned HTTP {(int)response.StatusCode} while renewing the URL.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var status = GetString(root, "status");
        if (status == "error")
        {
            var code = root.TryGetProperty("error", out var error) ? GetString(error, "code") : string.Empty;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(code)
                ? "Cobalt could not renew the media URL."
                : $"Cobalt could not renew the media URL ({code}).");
        }
        var resolvedUrl = status switch
        {
            "tunnel" or "redirect" => GetString(root, "url"),
            "picker" => GetPickerUrl(root),
            "local-processing" => GetSingleTunnelUrl(root),
            _ => string.Empty
        };
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var renewed)
            || (renewed.Scheme != Uri.UriSchemeHttp && renewed.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Cobalt did not return a downloadable HTTP media URL while renewing the task.");
        }
        options.Url = renewed;
        options.ResolverName = "cobalt";
    }

    private static string GetPickerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("picker", out var picker) || picker.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }
        var items = picker.EnumerateArray().ToList();
        var preferred = items.FirstOrDefault(item => GetString(item, "type") is "video" or "gif");
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

public sealed class UrlRenewalRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("videoQuality")]
    public string VideoQuality { get; set; } = "max";
    [JsonPropertyName("downloadMode")]
    public string DownloadMode { get; set; } = "auto";
    [JsonPropertyName("filenameStyle")]
    public string FilenameStyle { get; set; } = "basic";
    [JsonPropertyName("localProcessing")]
    public string LocalProcessing { get; set; } = "disabled";
}
