using Nickvision.Desktop.Application;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Nickvision.Parabolic.Shared.Services;

public sealed class Nm3u8dlExecutableService : INm3u8dlExecutableService
{
    private readonly IConfigurationService _configurationService;

    public Nm3u8dlExecutableService(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public Process GetDownloadProcess(DownloadOptions downloadOptions)
    {
        if (!IsHttpUri(downloadOptions.Url))
        {
            throw new InvalidOperationException("N_m3u8DL-RE requires an HTTP HLS (.m3u8) or DASH (.mpd) manifest.");
        }
        if (downloadOptions.FileType.IsAudio)
        {
            throw new InvalidOperationException("The N_m3u8DL-RE fallback is currently limited to video downloads.");
        }

        var executablePath = Nickvision.Desktop.System.Environment.FindDependency("N_m3u8DL-RE")
            ?? (OperatingSystem.IsWindows() ? "N_m3u8DL-RE.exe" : "N_m3u8DL-RE");
        var ffmpegPath = Nickvision.Desktop.System.Environment.FindDependency("ffmpeg") ?? "ffmpeg";
        var retries = Math.Clamp(downloadOptions.NetworkRetries, 1, 100);
        var timeout = Math.Clamp(downloadOptions.SocketTimeoutSeconds, 5, 300);
        var threads = Math.Clamp(downloadOptions.ConcurrentFragments, 1, 32);
        var muxFormat = downloadOptions.FileType == MediaFileType.MKV ? "mkv" : "mp4";
        var temporaryDirectory = Path.Combine(downloadOptions.SaveFolder, ".parabolic-nm3u8dl");
        Directory.CreateDirectory(downloadOptions.SaveFolder);

        var arguments = new List<string>(48)
        {
            downloadOptions.Url.AbsoluteUri,
            "--save-dir", downloadOptions.SaveFolder,
            "--tmp-dir", temporaryDirectory,
            "--save-name", downloadOptions.SaveFilename,
            "--auto-select",
            "--concurrent-download",
            "--thread-count", threads.ToString(CultureInfo.InvariantCulture),
            "--download-retry-count", retries.ToString(CultureInfo.InvariantCulture),
            "--http-request-timeout", timeout.ToString(CultureInfo.InvariantCulture),
            "--ffmpeg-binary-path", ffmpegPath,
            "--mux-after-done", $"format={muxFormat}:muxer=ffmpeg:skip_sub=true",
            "--no-ansi-color",
            "--no-log",
            "--no-date-info",
            "--write-meta-json", "false",
            "--disable-update-check"
        };

        if (!string.IsNullOrWhiteSpace(downloadOptions.HttpReferer))
        {
            arguments.Add("--header");
            arguments.Add($"Referer: {downloadOptions.HttpReferer}");
        }
        if (!string.IsNullOrWhiteSpace(downloadOptions.HttpUserAgent))
        {
            arguments.Add("--header");
            arguments.Add($"User-Agent: {downloadOptions.HttpUserAgent}");
        }

        var speedLimit = downloadOptions.SpeedLimitKbps ?? _configurationService.SpeedLimit;
        if (speedLimit.HasValue && speedLimit.Value > 0)
        {
            arguments.Add("--max-speed");
            arguments.Add($"{speedLimit.Value}K");
        }

        if (!string.Equals(downloadOptions.ProxyMode, "direct", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_configurationService.ProxyUrl))
        {
            arguments.Add("--custom-proxy");
            arguments.Add(_configurationService.ProxyUrl);
        }
        else if (string.Equals(downloadOptions.ProxyMode, "direct", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("--use-system-proxy");
            arguments.Add("false");
        }

        return new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo(executablePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
    }

    private static bool IsHttpUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
