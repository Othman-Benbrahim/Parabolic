using ATL;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Helpers;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Models;

public partial class Download : IDisposable
{
    private static int _nextId;

    private readonly IConfigurationService _configurationService;
    private readonly ITranslationService _translationService;
    private readonly IYtdlpExecutableService _ytdlpExecutableService;
    private readonly INm3u8dlExecutableService _nm3u8dlExecutableService;
    private readonly IUrlRenewalService _urlRenewalService;
    private readonly StringBuilder _logBuilder;
    private bool _removeSourceData;
    private Process? _process;
    private int _progressSkipCounter;
    private int _processRestartCount;
    private int _currentProcessLogStart;
    private bool _urlRenewed;
    private bool _manifestFallbackUsed;
    private bool _directFallbackUsed;

    public int Id { get; }
    public DownloadOptions Options { get; }
    public string FilePath { get; private set; }
    public DownloadStatus Status { get; private set; }
    public string Log => _logBuilder.ToString();

    public event EventHandler<DownloadCompletedEventArgs>? Completed;
    public event EventHandler<DownloadProgressChangedEventArgs>? ProgressChanged;

    static Download()
    {
        _nextId = 0;
    }

    public Download(IConfigurationService configurationService, ITranslationService translationService, IYtdlpExecutableService ytdlpExecutableService, INm3u8dlExecutableService nm3u8dlExecutableService, IUrlRenewalService urlRenewalService, DownloadOptions options)
    {
        _configurationService = configurationService;
        _translationService = translationService;
        _ytdlpExecutableService = ytdlpExecutableService;
        _nm3u8dlExecutableService = nm3u8dlExecutableService;
        _urlRenewalService = urlRenewalService;
        _logBuilder = new StringBuilder();
        _removeSourceData = false;
        _process = null;
        _progressSkipCounter = 0;
        _processRestartCount = 0;
        _currentProcessLogStart = 0;
        _urlRenewed = false;
        _manifestFallbackUsed = string.Equals(options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase);
        _directFallbackUsed = string.Equals(options.ResolverName, "direct-fallback", StringComparison.OrdinalIgnoreCase);
        Id = _nextId++;
        Options = options;
        FilePath = Path.Combine(Options.SaveFolder, $"{Options.SaveFilename}{Options.FileType.DotExtension}");
        Status = DownloadStatus.Queued;
    }

    ~Download()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Pause()
    {
        if (Status != DownloadStatus.Running)
        {
            return;
        }
        Status = DownloadStatus.Paused;
        _process?.Suspend();
    }

    public void Resume()
    {
        if (Status != DownloadStatus.Paused)
        {
            return;
        }
        Status = DownloadStatus.Running;
        _process?.Resume();
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NaN, 0.0, 0));
    }

    public async void Start()
    {
        if (Status == DownloadStatus.Running || Status == DownloadStatus.Paused)
        {
            return;
        }
        if (File.Exists(FilePath) && !_configurationService.OverwriteExistingFiles)
        {
            var log = _translationService?._("The file already exists and overwriting is disabled.") ?? "The file already exists and overwriting is disabled.";
            _logBuilder.AppendLine(log);
            Status = DownloadStatus.Error;
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, log.AsMemory(), double.NaN, 0.0, 0));
            Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, log.AsMemory(), false));
            return;
        }
        try
        {
            if (!_urlRenewed && !string.Equals(Options.RenewalMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, "Renewing temporary media URL...".AsMemory(), double.NaN, 0.0, 0));
                await _urlRenewalService.RenewAsync(Options, CancellationToken.None);
                _urlRenewed = true;
            }
            _removeSourceData = _configurationService.RemoveSourceData;
            _currentProcessLogStart = _logBuilder.Length;
            _process = string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase)
                ? _nm3u8dlExecutableService.GetDownloadProcess(Options)
                : _ytdlpExecutableService.GetDownloadProcess(Options);
            Status = DownloadStatus.Running;
            _process.Exited += Process_Exited;
            _process.OutputDataReceived += Process_OutputDataReceived;
            _process.ErrorDataReceived += Process_OutputDataReceived;
            _process.Start();
            _process.SetAsParentProcess();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, (_translationService?._("Starting download...") ?? "Starting download...").AsMemory(), double.NaN, 0.0, 0));
        }
        catch (Exception exception)
        {
            var log = $"Unable to start download: {exception.Message}";
            _logBuilder.AppendLine(log);
            Status = DownloadStatus.Error;
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, log.AsMemory(), double.NaN, 0.0, 0));
            Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, log.AsMemory(), false));
        }
    }

    public void Stop()
    {
        if (Status != DownloadStatus.Running)
        {
            return;
        }
        Status = DownloadStatus.Stopped;
        _process?.Kill(true);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }
        if (_process is not null && !_process.HasExited)
        {
            _process?.Kill(true);
            _process?.WaitForExit();
        }
    }

    private async void Process_Exited(object? sender, EventArgs e)
    {
        if (Status != DownloadStatus.Stopped)
        {
            Status = _process?.ExitCode == 0 ? DownloadStatus.Success : DownloadStatus.Error;
        }
        if (Status == DownloadStatus.Success)
        {
            try
            {
                var finalPath = string.Empty;
                var log = _logBuilder.ToString();
                var endIndex = log.Length;
                for (var i = 0; i < 2 && endIndex > 0; i++)
                {
                    var startIndex = log.LastIndexOf('\n', endIndex - 1);
                    var line = (startIndex == -1 ? log[..endIndex] : log[(startIndex + 1)..endIndex]).Trim('\r');
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        finalPath = line;
                        break;
                    }
                    endIndex = startIndex == -1 ? 0 : startIndex;
                }
                if ((!File.Exists(finalPath))
                    && string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(Options.SaveFolder))
                {
                    finalPath = Directory.EnumerateFiles(Options.SaveFolder, $"{Options.SaveFilename}*.*")
                        .Where(path => path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault() ?? string.Empty;
                }
                if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                {
                    FilePath = finalPath;
                    if (_removeSourceData)
                    {
                        var track = new Track(FilePath);
                        track.Comment = string.Empty;
                        track.Description = string.Empty;
                        track.EncodedBy = string.Empty;
                        track.Encoder = string.Empty;
                        if (track.AdditionalFields.ContainsKey("comment"))
                        {
                            track.AdditionalFields.Remove("comment");
                        }
                        if (track.AdditionalFields.ContainsKey("COMMENT"))
                        {
                            track.AdditionalFields.Remove("COMMENT");
                        }
                        if (track.AdditionalFields.ContainsKey("description"))
                        {
                            track.AdditionalFields.Remove("description");
                        }
                        if (track.AdditionalFields.ContainsKey("DESCRIPTION"))
                        {
                            track.AdditionalFields.Remove("DESCRIPTION");
                        }
                        if (track.AdditionalFields.ContainsKey("purl"))
                        {
                            track.AdditionalFields.Remove("purl");
                        }
                        if (track.AdditionalFields.ContainsKey("PURL"))
                        {
                            track.AdditionalFields.Remove("PURL");
                        }
                        if (track.AdditionalFields.ContainsKey("synopsis"))
                        {
                            track.AdditionalFields.Remove("synopsis");
                        }
                        if (track.AdditionalFields.ContainsKey("SYNOPSIS"))
                        {
                            track.AdditionalFields.Remove("SYNOPSIS");
                        }
                        if (track.AdditionalFields.ContainsKey("url"))
                        {
                            track.AdditionalFields.Remove("url");
                        }
                        if (track.AdditionalFields.ContainsKey("URL"))
                        {
                            track.AdditionalFields.Remove("URL");
                        }
                        await track.SaveAsync();
                    }
                }
            }
            catch { }
        }
        else if (Status == DownloadStatus.Error && await TryRestartWithManifestFallbackAsync())
        {
            return;
        }
        else if (Status == DownloadStatus.Error && await TryRestartWithDirectFallbackAsync())
        {
            return;
        }
        else if (Status == DownloadStatus.Error && await TryRestartAfterNetworkFailureAsync())
        {
            return;
        }
        if (Status == DownloadStatus.Error
            && string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase)
            && ContainsDrmIndicator(CurrentProcessLog))
        {
            const string drmMessage = "This HLS/DASH fallback appears to be DRM-protected. Parabolic does not request, store, or use decryption keys.";
            _logBuilder.AppendLine(drmMessage);
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, drmMessage.AsMemory(), double.NaN, 0.0, 0));
        }
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty));
        Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, Log.AsMemory(), true));
        if (_process is not null)
        {
            _process.Exited -= Process_Exited;
            _process.ErrorDataReceived -= Process_OutputDataReceived;
            _process.OutputDataReceived -= Process_OutputDataReceived;
            _process.Dispose();
            _process = null;
        }
    }

    private static bool ContainsDrmIndicator(string log) =>
        log.Contains("DRM", StringComparison.OrdinalIgnoreCase)
        || log.Contains("ContentProtection", StringComparison.OrdinalIgnoreCase)
        || log.Contains("PSSH", StringComparison.OrdinalIgnoreCase)
        || log.Contains("decryption key", StringComparison.OrdinalIgnoreCase)
        || log.Contains("SAMPLE-AES", StringComparison.OrdinalIgnoreCase)
        || log.Contains("CENC", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryRestartWithManifestFallbackAsync()
    {
        if (_manifestFallbackUsed
            || Options.ManifestFallbackUrl is null
            || Options.FileType.IsAudio
            || string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var log = CurrentProcessLog;
        var extractionFailure = log.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase)
            || log.Contains("Unable to extract", StringComparison.OrdinalIgnoreCase)
            || log.Contains("No video formats", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 401", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase);
        if (!extractionFailure)
        {
            return false;
        }

        Options.Url = Options.ManifestFallbackUrl;
        Options.DownloadEngine = "n-m3u8dl-re";
        Options.ResolverName = "n-m3u8dl-re";
        Options.RenewalMode = "none";
        _manifestFallbackUsed = true;
        const string message = "yt-dlp could not extract this page; retrying the detected HLS/DASH stream with N_m3u8DL-RE...";
        _logBuilder.AppendLine(message);
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, message.AsMemory(), double.NaN, 0.0, 0));
        DisposeProcess();
        Status = DownloadStatus.Queued;
        await Task.Delay(250);
        Start();
        return true;
    }

    private async Task<bool> TryRestartWithDirectFallbackAsync()
    {
        if (_directFallbackUsed || Options.DirectFallbackUrl is null || Options.FileType.IsAudio)
        {
            return false;
        }

        var log = CurrentProcessLog;
        var extractionFailure = string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase)
            || log.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase)
            || log.Contains("Unable to extract", StringComparison.OrdinalIgnoreCase)
            || log.Contains("No video formats", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 401", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase);
        if (!extractionFailure)
        {
            return false;
        }

        Options.Url = Options.DirectFallbackUrl;
        Options.DirectFallbackUrl = null;
        Options.ManifestFallbackUrl = null;
        Options.FallbackUrl = null;
        Options.DownloadEngine = "yt-dlp";
        Options.ResolverName = "direct-fallback";
        Options.SourceKind = "video";
        Options.RenewalMode = "none";
        _directFallbackUsed = true;
        const string message = "Page extraction failed; retrying the direct MP4/video stream detected by Firefox...";
        _logBuilder.AppendLine(message);
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, message.AsMemory(), double.NaN, 0.0, 0));
        DisposeProcess();
        Status = DownloadStatus.Queued;
        await Task.Delay(250);
        Start();
        return true;
    }

    private async Task<bool> TryRestartAfterNetworkFailureAsync()
    {
        const int maxProcessRestarts = 2;
        if (_processRestartCount >= maxProcessRestarts)
        {
            return false;
        }

        var log = CurrentProcessLog;
        var temporaryUrlFailure = log.Contains("HTTP Error 401", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase)
            || log.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase)
            || log.Contains("URL expired", StringComparison.OrdinalIgnoreCase)
            || log.Contains("signature", StringComparison.OrdinalIgnoreCase);
        var retryableNetworkFailure = temporaryUrlFailure
            || log.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || log.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
            || log.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
            || log.Contains("temporary failure", StringComparison.OrdinalIgnoreCase)
            || log.Contains("network is unreachable", StringComparison.OrdinalIgnoreCase);
        if (!retryableNetworkFailure)
        {
            return false;
        }

        if (temporaryUrlFailure
            && Options.FallbackUrl is not null
            && !string.Equals(Options.ResolverName, "direct-fallback", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase))
        {
            Options.Url = Options.FallbackUrl;
            Options.ResolverName = "yt-dlp-refresh";
            Options.RenewalMode = "none";
        }
        else if (string.Equals(Options.RenewalMode, "cobalt", StringComparison.OrdinalIgnoreCase))
        {
            _urlRenewed = false;
        }

        _processRestartCount++;
        var delay = TimeSpan.FromSeconds(Math.Min(15, 1 << _processRestartCount));
        var message = $"Network/CDN retry {_processRestartCount}/{maxProcessRestarts} in {delay.TotalSeconds:0} seconds...";
        _logBuilder.AppendLine(message);
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, message.AsMemory(), double.NaN, 0.0, 0));
        DisposeProcess();
        Status = DownloadStatus.Queued;
        await Task.Delay(delay);
        Start();
        return true;
    }

    private void DisposeProcess()
    {
        if (_process is null)
        {
            return;
        }
        _process.Exited -= Process_Exited;
        _process.ErrorDataReceived -= Process_OutputDataReceived;
        _process.OutputDataReceived -= Process_OutputDataReceived;
        _process.Dispose();
        _process = null;
    }

    private string CurrentProcessLog => _currentProcessLogStart >= 0 && _currentProcessLogStart <= _logBuilder.Length
        ? _logBuilder.ToString(_currentProcessLogStart, _logBuilder.Length - _currentProcessLogStart)
        : _logBuilder.ToString();

    private async void Process_OutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (_progressSkipCounter > 0 || e.Data is null || string.IsNullOrEmpty(e.Data) || string.IsNullOrWhiteSpace(e.Data) || e.Data.StartsWith(" ***", StringComparison.Ordinal))
        {
            if (_progressSkipCounter > 0)
            {
                _progressSkipCounter--;
            }
            else if (e.Data?.StartsWith(" ***", StringComparison.Ordinal) ?? false)
            {
                _progressSkipCounter = 4;
            }
            return;
        }
        _logBuilder.AppendLine(e.Data);
        try
        {
            if (e.Data.StartsWith("[Parabolic] Progress", StringComparison.Ordinal))
            {
                var fields = e.Data.Split(';');
                if (fields.Length != 7 || fields[1] == "NA")
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NaN, 0.0, 0));
                }
                else if (fields[1] == "finished" || fields[1] == "processing")
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NaN, 0.0, 0));
                }
                else
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id,
                        e.Data.AsMemory(),
                        (fields[2] != "NA" ? double.Parse(fields[2], NumberStyles.Any, CultureInfo.InvariantCulture) : 0.0) / (fields[3] != "NA" ? double.Parse(fields[3], NumberStyles.Any, CultureInfo.InvariantCulture) : (fields[4] != "NA" ? double.Parse(fields[4], NumberStyles.Any, CultureInfo.InvariantCulture) : 1.0)),
                        fields[5] != "NA" ? double.Parse(fields[5], NumberStyles.Any, CultureInfo.InvariantCulture) : 0.0,
                        fields[6] == "NA" || fields[6] == "Unknown" ? -1 : Convert.ToInt32(double.Parse(fields[6], NumberStyles.Any, CultureInfo.InvariantCulture))));
                }
            }
            else if (e.Data.StartsWith("[#", StringComparison.Ordinal))
            {
                var line = e.Data;
                if (OperatingSystem.IsWindows())
                {
                    var index = line.LastIndexOf('\r');
                    while (index >= 0)
                    {
                        var candidate = line[(index + 1)..];
                        if (candidate.TryParseAriaProgressLine(out var progress, out var speed, out var eta))
                        {
                            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), progress, speed, eta));
                            return;
                        }
                        line = line[..index];
                        index = line.LastIndexOf('\r');
                    }
                }
                if (!line.TryParseAriaProgressLine(out var finalProgress, out var finalSpeed, out var finalEta))
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NaN, 0.0, 0));
                    return;
                }
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), finalProgress, finalSpeed, finalEta));
            }
            else if (e.Data.StartsWith("[download] Sleeping", StringComparison.Ordinal))
            {
                var seconds = 0.0;
                var sleepingPrefixLength = "[download] Sleeping ".Length;
                var secondsEnd = e.Data.IndexOf(" second", sleepingPrefixLength, StringComparison.Ordinal);
                if (secondsEnd == -1 || !double.TryParse(e.Data.Substring(sleepingPrefixLength, secondsEnd - sleepingPrefixLength), NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NegativeInfinity, 0.0, 0));
                }
                else
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NegativeInfinity, seconds, 0));
                    while (seconds >= 1)
                    {
                        if (Status == DownloadStatus.Paused)
                        {
                            return;
                        }
                        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NegativeInfinity, Math.Floor(seconds--), 0));
                        await Task.Delay(1000);
                    }
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NaN, 0.0, 0));
                }
            }
            else if (string.Equals(Options.DownloadEngine, "n-m3u8dl-re", StringComparison.OrdinalIgnoreCase)
                && Regex.Match(e.Data, @"(?<!\d)(?<percent>\d{1,3}(?:\.\d+)?)%") is { Success: true } match
                && double.TryParse(match.Groups["percent"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var percent))
            {
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(
                    Id,
                    e.Data.AsMemory(),
                    Math.Clamp(percent / 100.0, 0.0, 1.0),
                    0.0,
                    -1));
            }
            else
            {
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NaN, 0.0, 0));
            }
        }
        catch
        {
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NaN, 0.0, 0));
        }
    }
}
