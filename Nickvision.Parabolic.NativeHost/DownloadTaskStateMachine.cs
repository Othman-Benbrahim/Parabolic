using System;
using System.Collections.Generic;

namespace Nickvision.Parabolic.NativeHost;

internal enum DownloadTaskState
{
    Scheduled,
    Queued,
    Resolving,
    Running,
    Paused,
    Processing,
    RetryScheduled,
    Completed,
    Failed,
    Cancelled
}

internal enum DownloadErrorCategory
{
    None,
    Authentication,
    RateLimited,
    GeoRestricted,
    Network,
    DiskFull,
    Permission,
    Dependency,
    MediaUnavailable,
    DrmProtected,
    PostProcessing,
    Unknown
}

internal sealed record DownloadFailureInfo(
    DownloadErrorCategory Category,
    bool Retryable,
    string ActionHint);

internal static class DownloadTaskStateMachine
{
    private static readonly IReadOnlyDictionary<DownloadTaskState, HashSet<DownloadTaskState>> Transitions =
        new Dictionary<DownloadTaskState, HashSet<DownloadTaskState>>
        {
            [DownloadTaskState.Scheduled] = [DownloadTaskState.Queued, DownloadTaskState.Running, DownloadTaskState.Cancelled],
            [DownloadTaskState.Queued] = [DownloadTaskState.Resolving, DownloadTaskState.Running, DownloadTaskState.Paused, DownloadTaskState.Processing, DownloadTaskState.RetryScheduled, DownloadTaskState.Completed, DownloadTaskState.Failed, DownloadTaskState.Cancelled],
            [DownloadTaskState.Resolving] = [DownloadTaskState.Running, DownloadTaskState.RetryScheduled, DownloadTaskState.Failed, DownloadTaskState.Cancelled],
            [DownloadTaskState.Running] = [DownloadTaskState.Paused, DownloadTaskState.Processing, DownloadTaskState.RetryScheduled, DownloadTaskState.Completed, DownloadTaskState.Failed, DownloadTaskState.Cancelled],
            [DownloadTaskState.Paused] = [DownloadTaskState.Running, DownloadTaskState.Cancelled],
            [DownloadTaskState.Processing] = [DownloadTaskState.Completed, DownloadTaskState.RetryScheduled, DownloadTaskState.Failed, DownloadTaskState.Cancelled],
            [DownloadTaskState.RetryScheduled] = [DownloadTaskState.Queued, DownloadTaskState.Running, DownloadTaskState.Failed, DownloadTaskState.Cancelled],
            [DownloadTaskState.Completed] = [],
            [DownloadTaskState.Failed] = [],
            [DownloadTaskState.Cancelled] = []
        };

    public static bool IsTerminal(DownloadTaskState state) =>
        state is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled;

    public static bool CanTransition(DownloadTaskState current, DownloadTaskState next) =>
        current == next || Transitions[current].Contains(next);

    public static string ToProtocolValue(DownloadTaskState state) => state switch
    {
        DownloadTaskState.Running => "downloading",
        DownloadTaskState.RetryScheduled => "retry-scheduled",
        _ => state.ToString().ToLowerInvariant()
    };
}

internal static class DownloadErrorClassifier
{
    public static DownloadFailureInfo Classify(string? message)
    {
        var value = message ?? string.Empty;
        if (ContainsAny(value, "HTTP Error 401", "HTTP Error 403", "Sign in", "login required", "cookies"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.Authentication, false, "Sign in to the website or enable Firefox-session authentication.");
        }
        if (ContainsAny(value, "DRM", "Widevine", "encrypted media", "This video is DRM protected"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.DrmProtected, false, "DRM-protected media is not supported and will not be retried.");
        }
        if (ContainsAny(value, "HTTP Error 429", "Too Many Requests", "rate limit"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.RateLimited, true, "Wait before retrying or select the conservative network strategy.");
        }
        if (ContainsAny(value, "not available in your country", "geo", "region"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.GeoRestricted, false, "This media is restricted for the current network location.");
        }
        if (ContainsAny(value, "No space left", "disk full", "not enough space"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.DiskFull, false, "Free disk space or select another download folder.");
        }
        if (ContainsAny(value, "Permission denied", "Access is denied", "UnauthorizedAccessException"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.Permission, false, "Choose a writable download folder and verify application permissions.");
        }
        if (ContainsAny(value, "ffmpeg", "aria2c", "N_m3u8DL-RE", "executable was not found"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.Dependency, false, "Repair or update the required download dependency.");
        }
        if (ContainsAny(value, "timed out", "timeout", "connection reset", "temporarily unavailable", "network is unreachable", "HTTP Error 5"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.Network, true, "Parabolic will retry with exponential backoff.");
        }
        if (ContainsAny(value, "Unsupported URL", "Video unavailable", "media is unavailable", "No video formats found"))
        {
            return new DownloadFailureInfo(DownloadErrorCategory.MediaUnavailable, false, "Open the page in Firefox and retry with media detection enabled.");
        }
        return new DownloadFailureInfo(DownloadErrorCategory.Unknown, true, "Retry the task; if it fails again, inspect the download log.");
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
