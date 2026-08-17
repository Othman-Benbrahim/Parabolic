#!/usr/bin/env python3
from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

manifest = json.loads((ROOT / "extension/firefox/manifest.json").read_text(encoding="utf-8"))
assert manifest["manifest_version"] == 3
assert manifest["version"] == "0.5.0"

ET.parse(ROOT / "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj")
ET.parse(ROOT / "Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj")
ET.parse(ROOT / "Nickvision.Parabolic.Shared/Nickvision.Parabolic.Shared.csproj")

server = (ROOT / "Nickvision.Parabolic.NativeHost/NativeMessagingServer.cs").read_text(encoding="utf-8-sig")
coordinator = (ROOT / "Nickvision.Parabolic.NativeHost/PersistentDownloadCoordinator.cs").read_text(encoding="utf-8-sig")
service = (ROOT / "Nickvision.Parabolic.DownloadService/Program.cs").read_text(encoding="utf-8-sig")
workflow = (ROOT / ".github/workflows/windows.yml").read_text(encoding="utf-8")
installer = (ROOT / "inno/setup.iss").read_text(encoding="utf-8-sig")

assert "ProtocolVersion = 2" in server
for capability in ("persistent-queue", "priority", "pause-resume", "list-downloads"):
    assert capability in server
assert "OrderByDescending(download => download.Options.Priority)" in (
    ROOT / "Nickvision.Parabolic.Shared/Services/DownloadService.cs"
).read_text(encoding="utf-8-sig")
assert "browser_recovery_queue" in (
    ROOT / "Nickvision.Parabolic.Shared/Services/BackgroundRecoveryService.cs"
).read_text(encoding="utf-8-sig")
assert "KeepPartialFiles = true" in server
assert "PersistentDownloadCoordinator" in coordinator
assert "PipeOptions.CurrentUserOnly" in service
assert "DOWNLOAD_SERVICE_PROJECT" in workflow
assert "DOWNLOAD_SERVICE_FILES_PATH" in workflow
assert "Nickvision.Parabolic.DownloadService.exe" in installer

print("Persistent download service source smoke test passed")
