#!/usr/bin/env python3

from pathlib import Path
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

manifest = json.loads((ROOT / "extension/firefox/manifest.json").read_text(encoding="utf-8"))
assert manifest["version"] == "0.8.2"
assert manifest["browser_specific_settings"]["gecko"]["id"] == "parabolic-media-detector@othmanbenbrahim.dev"

host_project = (ROOT / "Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj").read_text(encoding="utf-8-sig")
service_project = (ROOT / "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj").read_text(encoding="utf-8-sig")
for project_path, project in (
    (ROOT / "Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj", host_project),
    (ROOT / "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj", service_project),
):
    ET.parse(project_path)
    for expected in ("net10.0", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"):
        assert expected in project
    assert "2026.8.6" in project

daemon = (ROOT / "Nickvision.Parabolic.NativeHost/DaemonProtocol.cs").read_text(encoding="utf-8-sig")
relay = (ROOT / "Nickvision.Parabolic.NativeHost/NativeHostRelay.cs").read_text(encoding="utf-8-sig")
service = (ROOT / "Nickvision.Parabolic.DownloadService/Program.cs").read_text(encoding="utf-8-sig")
coordinator = (ROOT / "Nickvision.Parabolic.NativeHost/PersistentDownloadCoordinator.cs").read_text(encoding="utf-8-sig")
assert 'OperatingSystem.IsWindows()' in daemon
assert '"Nickvision.Parabolic.DownloadService"' in daemon
assert 'OperatingSystem.IsMacOS()' in relay
assert 'Path.PathSeparator' in relay
assert '#if WINDOWS' in service
assert 'PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly' in service
assert 'OperatingSystem.IsMacOS()' in coordinator and 'ProcessStartInfo("open")' in coordinator
assert 'OperatingSystem.IsLinux()' in coordinator and 'ProcessStartInfo("xdg-open")' in coordinator

add_download_blueprint = (ROOT / "Nickvision.Parabolic.GNOME/Blueprints/AddDownloadDialog.blp").read_text(encoding="utf-8")
add_download_view = (ROOT / "Nickvision.Parabolic.GNOME/Views/AddDownloadDialog.cs").read_text(encoding="utf-8-sig")
assert "Adw.ButtonRow" not in add_download_blueprint
assert "Adw.SpinnerPaintable" not in add_download_blueprint
assert "Adw.ActionRow" in add_download_blueprint
assert "Gtk.Spinner" in add_download_blueprint
assert "selectBatchFileButton" in add_download_blueprint and "selectBatchFileButton" in add_download_view

linux_install = (ROOT / "resources/linux/install-native.sh").read_text(encoding="utf-8")
mac_install = (ROOT / "resources/macos/install-firefox-integration.command").read_text(encoding="utf-8")
for text in (linux_install, mac_install):
    assert "com.nickvision.parabolic" in text
    assert "parabolic-media-detector@othmanbenbrahim.dev" in text
assert ".mozilla/native-messaging-hosts" in linux_install
assert "systemd/user" in linux_install
assert "dbus-1/services" in linux_install
assert "Library/Application Support/Mozilla/NativeMessagingHosts" in mac_install
assert "Library/LaunchAgents" in mac_install

for workflow in (".github/workflows/linux-native.yml", ".github/workflows/macos.yml"):
    content = (ROOT / workflow).read_text(encoding="utf-8")
    assert "linux" in content.lower() or "macos" in content.lower()
    assert "N_m3u8DL-RE" in content
    assert "native-bridge-source-smoke.py" in content

print("Cross-platform native bridge source smoke test passed")
