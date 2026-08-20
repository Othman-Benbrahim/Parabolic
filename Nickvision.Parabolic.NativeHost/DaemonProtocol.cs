using System;

namespace Nickvision.Parabolic.NativeHost;

public static class DaemonProtocol
{
    public const string PipeName = "Parabolic.DownloadManager.v1";
    public static string ServiceExecutableName => OperatingSystem.IsWindows()
        ? "Nickvision.Parabolic.DownloadService.exe"
        : "Nickvision.Parabolic.DownloadService";
}
