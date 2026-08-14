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

runtime_version = str(manifest["runtime-version"])
require(
    "flatpak-github-actions:gnome-49" in workflow,
    "workflow must use the supported GNOME 49 Flatpak builder image",
)
require(runtime_version == "50", "application runtime must remain GNOME 50")
require(
    "org.freedesktop.Sdk.Extension.dotnet10" in manifest.get("sdk-extensions", []),
    "the .NET 10 SDK extension is required",
)

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

print(
    f"Flatpak inputs OK: GNOME {runtime_version} runtime with GNOME 49 builder, "
    f"{len(destinations)} NuGet files, {len(python_urls)} Python sources"
)
