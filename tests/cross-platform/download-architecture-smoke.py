#!/usr/bin/env python3
"""Dependency-free structural checks for the 2026.9 download architecture."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

download_service = (ROOT / "Nickvision.Parabolic.Shared/Services/DownloadService.cs").read_text(encoding="utf-8-sig")
clear_completed = download_service.split("public IReadOnlyList<int> ClearCompleted()", 1)[1].split("public IReadOnlyList<int> ClearQueued()", 1)[0]
dispose = download_service.split("private void Dispose(bool disposing)", 1)[1]
assert "_scheduleTimer.Dispose()" not in clear_completed
assert "_scheduleTimer.Dispose()" in dispose

state_machine = (ROOT / "Nickvision.Parabolic.NativeHost/DownloadTaskStateMachine.cs").read_text(encoding="utf-8")
for state in ("Scheduled", "Queued", "Running", "Paused", "Processing", "RetryScheduled", "Completed", "Failed", "Cancelled"):
    assert f"DownloadTaskState.{state}" in state_machine
for category in ("Authentication", "RateLimited", "GeoRestricted", "Network", "DiskFull", "Permission", "Dependency", "MediaUnavailable", "DrmProtected"):
    assert category in state_machine

coordinator = (ROOT / "Nickvision.Parabolic.NativeHost/PersistentDownloadCoordinator.cs").read_text(encoding="utf-8-sig")
for marker in ("DownloadTaskStateMachine.CanTransition", "DownloadErrorClassifier.Classify", "RetryScheduled", "Task.Delay(delay", "IPostDownloadPipeline"):
    assert marker in coordinator

pipeline = (ROOT / "Nickvision.Parabolic.NativeHost/PostDownloadPipeline.cs").read_text(encoding="utf-8")
for marker in ("verify-output", "sha256", "FileInfo", "SHA256.HashDataAsync"):
    assert marker in pipeline

rss = (ROOT / "Nickvision.Parabolic.NativeHost/RssSubscriptionService.cs").read_text(encoding="utf-8")
for marker in ("DtdProcessing.Prohibit", "MaxCharactersInDocument", "DownloadLatestOnly", "KeywordFilter", "PollMinutes", "SeenItemIds"):
    assert marker in rss

resolvers = (ROOT / "Nickvision.Parabolic.NativeHost/MediaResolvers.cs").read_text(encoding="utf-8")
collections = (ROOT / "Nickvision.Parabolic.NativeHost/CollectionResolvers.cs").read_text(encoding="utf-8")
assert "MediaResolverRegistry" in resolvers and "ResolverCapabilities" in resolvers
assert "ICollectionResolver" in collections and "RssCollectionResolver" in collections

server = (ROOT / "Nickvision.Parabolic.NativeHost/NativeMessagingServer.cs").read_text(encoding="utf-8-sig")
for capability in ("task-state-machine", "typed-errors", "rss-subscriptions", "collections", "direct-http", "post-processing-pipeline"):
    assert capability in server
for request in ("list-subscriptions", "add-subscription", "remove-subscription", "check-subscriptions", "resolve-collection"):
    assert f'case "{request}"' in server

project = (ROOT / "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj").read_text(encoding="utf-8-sig")
for source in ("DownloadTaskStateMachine.cs", "PostDownloadPipeline.cs", "RssSubscriptionService.cs", "CollectionResolvers.cs"):
    assert source in project

manifest = json.loads((ROOT / "extension/firefox/manifest.json").read_text(encoding="utf-8"))
assert "clipboardRead" in manifest["optional_permissions"]
background = (ROOT / "extension/firefox/background.js").read_text(encoding="utf-8")
popup = (ROOT / "extension/firefox/popup.js").read_text(encoding="utf-8")
for marker in ("native-add-subscription", "native-list-subscriptions", "subscribeParabolicRss", "postProcessingSteps"):
    assert marker in background
for marker in ("pasteDirectUrl", "downloadDirectUrl", "loadSubscriptions", "startClipboardWatcher"):
    assert marker in popup

for project_name in (
    "Nickvision.Parabolic.NativeHost/Nickvision.Parabolic.NativeHost.csproj",
    "Nickvision.Parabolic.DownloadService/Nickvision.Parabolic.DownloadService.csproj",
):
    project_text = (ROOT / project_name).read_text(encoding="utf-8-sig")
    for rid in ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"):
        assert rid in project_text

print("Cross-platform download architecture smoke test passed")
