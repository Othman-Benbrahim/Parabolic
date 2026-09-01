#!/usr/bin/env python3
from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

manifest = json.loads((ROOT / "extension/firefox/manifest.json").read_text(encoding="utf-8"))
assert manifest["manifest_version"] == 3
assert manifest["version"] == "0.9.0"

ET.parse(ROOT / "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj")
ET.parse(ROOT / "Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj")
ET.parse(ROOT / "Nickvision.Parabolic.Shared/Nickvision.Parabolic.Shared.csproj")

server = (ROOT / "Nickvision.Parabolic.NativeHost/NativeMessagingServer.cs").read_text(encoding="utf-8-sig")
protocol_models = (ROOT / "Nickvision.Parabolic.NativeHost/NativeProtocolModels.cs").read_text(encoding="utf-8-sig")
coordinator = (ROOT / "Nickvision.Parabolic.NativeHost/PersistentDownloadCoordinator.cs").read_text(encoding="utf-8-sig")
service = (ROOT / "Nickvision.Parabolic.DownloadService/Program.cs").read_text(encoding="utf-8-sig")
workflow = (ROOT / ".github/workflows/windows.yml").read_text(encoding="utf-8")
installer = (ROOT / "inno/setup.iss").read_text(encoding="utf-8-sig")

assert "ProtocolVersion = 3" in server
for capability in ("persistent-queue", "priority", "pause-resume", "list-downloads", "resolver-pipeline", "cobalt", "direct-media", "direct-stream-fallback", "hls-dash", "n-m3u8dl-re", "permalink-first", "bandwidth-limit", "scheduling", "url-renewal", "cdn-retry", "firefox-auth", "proxy-control"):
    assert capability in server
assert "OrderByDescending(download => download.Options.Priority)" in (
    ROOT / "Nickvision.Parabolic.Shared/Services/DownloadService.cs"
).read_text(encoding="utf-8-sig")
assert "browser_recovery_queue" in (
    ROOT / "Nickvision.Parabolic.Shared/Services/BackgroundRecoveryService.cs"
).read_text(encoding="utf-8-sig")
assert "KeepPartialFiles = true" in server
assert "ResolveQuickDownloadOptionsAsync" in server
assert "ParseScheduledAt" in server
assert "SpeedLimitKbps" in server
assert "BuildDeferredCobaltOptions" in server
assert "COBALT_SCHEDULE_AUTH_UNSUPPORTED" in server
assert "LooksTemporaryUrl" in server
assert "ApplyNetworkControls" in server
download_response = protocol_models.split("internal sealed class DownloadResponse", 1)[1].split("public sealed class DownloadSnapshot", 1)[0]
for field in ("Resolver", "ScheduledAt", "SpeedLimitKbps"):
    assert field in download_response
resolvers = (ROOT / "Nickvision.Parabolic.NativeHost/MediaResolvers.cs").read_text(encoding="utf-8-sig")
assert "DirectMediaResolver" in resolvers
assert "CobaltMediaResolver" in resolvers
assert "LocalProcessing = \"disabled\"" in resolvers
assert "PersistentDownloadCoordinator" in coordinator
assert "NamedPipeServerStreamAcl.Create" in service
assert "WindowsIdentity.GetCurrent" in service
assert "PipeAccessRights.FullControl" in service
windows_pipe_section = service.split("#if WINDOWS", 1)[1].split("#else", 1)[0]
unix_pipe_section = service.split("#else", 1)[1].split("#endif", 1)[0]
assert "NamedPipeServerStreamAcl.Create" in windows_pipe_section
assert "PipeOptions.CurrentUserOnly" in unix_pipe_section
assert "DOWNLOAD_SERVICE_PROJECT" in workflow
assert "DOWNLOAD_SERVICE_FILES_PATH" in workflow
assert "Nickvision.Parabolic.DownloadService.exe" in installer

download_options = (ROOT / "Nickvision.Parabolic.Shared/Models/DownloadOptions.cs").read_text(encoding="utf-8-sig")
for field in ("AuthenticationMode", "ProxyMode", "HttpReferer", "HttpUserAgent", "ConcurrentFragments", "NetworkRetries", "SocketTimeoutSeconds", "FallbackUrl", "ManifestFallbackUrl", "DirectFallbackUrl", "DownloadEngine", "RenewalMode", "RenewalEndpoint", "RenewalSourceUrl"):
    assert field in download_options
renewal = (ROOT / "Nickvision.Parabolic.Shared/Services/UrlRenewalService.cs").read_text(encoding="utf-8-sig")
assert "RenewAsync" in renewal
assert "options.Url = renewed" in renewal
ytdlp = (ROOT / "Nickvision.Parabolic.Shared/Services/YtdlpExecutableService.cs").read_text(encoding="utf-8-sig")
for argument in ("--retries", "--fragment-retries", "--retry-sleep", "--socket-timeout", "--concurrent-fragments", "--referer", "--user-agent", "--cookies-from-browser"):
    assert argument in ytdlp
assert 'arguments.Add("firefox")' in ytdlp
assert 'ProxyMode, "direct"' in ytdlp

nm3u8dl = (ROOT / "Nickvision.Parabolic.Shared/Services/Nm3u8dlExecutableService.cs").read_text(encoding="utf-8-sig")
for argument in ("--auto-select", "--download-retry-count", "--http-request-timeout", "--ffmpeg-binary-path", "--mux-after-done", "--header", "--disable-update-check"):
    assert argument in nm3u8dl
download_model = (ROOT / "Nickvision.Parabolic.Shared/Models/Download.cs").read_text(encoding="utf-8-sig")
assert "TryRestartWithManifestFallbackAsync" in download_model
assert "TryRestartWithDirectFallbackAsync" in download_model
assert "Parabolic does not request, store, or use decryption keys" in download_model
assert "N_m3u8DL-RE.exe" in workflow
assert "N_m3u8DL-RE.exe" in installer
assert "N_m3u8DL-RE-LICENSE.txt" in installer

chrome_files = list((ROOT / "extension/chrome").glob("**/*"))
assert chrome_files, "Upstream Chrome sources should remain present but untouched by the Firefox package"

print("Persistent download service source smoke test passed")
