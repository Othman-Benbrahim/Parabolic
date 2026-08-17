#!/usr/bin/env python3
from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

manifest = json.loads((ROOT / "extension/firefox/manifest.json").read_text(encoding="utf-8"))
assert manifest["manifest_version"] == 3
assert manifest["version"] == "0.6.0"

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
for capability in ("persistent-queue", "priority", "pause-resume", "list-downloads", "resolver-pipeline", "cobalt", "direct-media", "hls-dash", "bandwidth-limit", "scheduling"):
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
assert "PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly" not in service
assert "DOWNLOAD_SERVICE_PROJECT" in workflow
assert "DOWNLOAD_SERVICE_FILES_PATH" in workflow
assert "Nickvision.Parabolic.DownloadService.exe" in installer

print("Persistent download service source smoke test passed")
