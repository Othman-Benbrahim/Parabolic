#!/usr/bin/env python3
"""Fail early when the Flatpak workflow and offline sources drift apart."""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "flatpak" / "org.nickvision.tubeconverter.json"
NUGET_SOURCES = ROOT / "flatpak" / "nuget-sources.json"
PYTHON_SOURCES = ROOT / "flatpak" / "python3-modules.json"
WORKFLOW = ROOT / ".github" / "workflows" / "flatpak.yml"
PUBLISH_SCRIPT = ROOT / "resources" / "linux" / "publish-and-install.sh"
PREPARE_TOOLS = ROOT / "resources" / "linux" / "prepare-flatpak-tools.sh"
FIREFOX_INSTALLER = ROOT / "resources" / "linux" / "install-flatpak-firefox-integration.sh"


def load_json(path: Path):
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Flatpak validation failed: {message}")


manifest = load_json(MANIFEST)
nuget_sources = load_json(NUGET_SOURCES)
python_modules = load_json(PYTHON_SOURCES)
workflow = WORKFLOW.read_text(encoding="utf-8")
publish_script = PUBLISH_SCRIPT.read_text(encoding="utf-8")
prepare_tools = PREPARE_TOOLS.read_text(encoding="utf-8")
firefox_installer = FIREFOX_INSTALLER.read_text(encoding="utf-8")

runtime_version = str(manifest["runtime-version"])
require(
    "flatpak-github-actions:gnome-50" in workflow,
    "workflow must use the GNOME 50 Flatpak builder image",
)
require(runtime_version == "50", "application runtime must remain GNOME 50")
require(
    "org.freedesktop.Sdk.Extension.dotnet10" in manifest.get("sdk-extensions", []),
    "the .NET 10 SDK extension is required",
)
finish_args = set(manifest.get("finish-args", []))
require("--filesystem=xdg-run/parabolic-pipes:create" in finish_args, "shared per-user pipe directory is required")
require("--filesystem=xdg-download" in finish_args, "download folder access is required")
require(not any("google-chrome" in arg or "microsoft-edge" in arg for arg in finish_args), "Firefox-only build must not request Chrome or Edge profile access")

destinations = [
    source.get("dest-filename")
    for source in nuget_sources
    if source.get("dest-filename")
]
duplicates = sorted(name for name, count in Counter(destinations).items() if count > 1)
require(not duplicates, f"duplicate NuGet destination files: {', '.join(duplicates)}")

python_urls = [
    source.get("url")
    for source in python_modules.get("sources", [])
    if source.get("url")
]
require(len(python_urls) == len(set(python_urls)), "duplicate Python source URLs")

app_module = next(
    (
        module
        for module in manifest.get("modules", [])
        if isinstance(module, dict) and module.get("name") == manifest.get("app-id")
    ),
    {},
)
architectures = app_module.get("build-options", {}).get("arch", {})
require({"x86_64", "aarch64"}.issubset(architectures), "x86_64/aarch64 build options are required")

nm3u8dl_module = next(
    (
        module
        for module in manifest.get("modules", [])
        if isinstance(module, dict) and module.get("name") == "n-m3u8dl-re"
    ),
    {},
)
require(nm3u8dl_module, "N_m3u8DL-RE Flatpak module is required")
require("generated/N_m3u8DL-RE" in json.dumps(nm3u8dl_module), "N_m3u8DL-RE must use the architecture-matched generated source")
require("v0.6.0-beta" in prepare_tools, "N_m3u8DL-RE preparation must pin v0.6.0-beta")
require("linux-x64" in workflow and "linux-arm64" in workflow, "workflow must prepare both N_m3u8DL-RE architectures")

for project in (
    "Nickvision.Parabolic.GNOME",
    "Nickvision.Parabolic.NativeHost",
    "Nickvision.Parabolic.DownloadService",
):
    require(project in publish_script, f"Flatpak publish script must include {project}")
require("parabolic-pipes" in publish_script and "TMPDIR" in publish_script, "Native Messaging bridge must use the shared Flatpak pipe directory")
require("flatpak run --command=org.nickvision.tubeconverter.NativeHost" in firefox_installer, "Firefox host launcher must enter the Parabolic sandbox")
require("parabolic-media-detector@othmanbenbrahim.dev" in firefox_installer, "Firefox extension ID must be allowlisted")
require("Parabolic-2026.8.6-flatpak-" in workflow, "architecture-specific Flatpak artifacts are required")
require("firefox-flatpak-integration" in workflow, "Firefox integration helper artifact is required")

ffmpeg_module = next(
    (
        module
        for module in manifest.get("modules", [])
        if isinstance(module, dict) and module.get("name") == "ffmpeg"
    ),
    {},
)
ffmpeg_sources = ffmpeg_module.get("sources", [])
ffmpeg_arches = {
    arch
    for source in ffmpeg_sources
    for arch in source.get("only-arches", [])
}
require(ffmpeg_arches == {"x86_64", "aarch64"}, "FFmpeg archives are required for both architectures")
require(
    all(len(source.get("sha256", "")) == 64 for source in ffmpeg_sources),
    "every FFmpeg archive must have a SHA-256 digest",
)

print(
    f"Flatpak inputs OK: GNOME {runtime_version} runtime with GNOME 50 builder, "
    f"{len(destinations)} NuGet files, {len(python_urls)} Python sources"
)
