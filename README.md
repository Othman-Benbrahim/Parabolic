<div align="center">

![Parabolic](resources/banner.png)

# 🎬 Parabolic Download Manager Edition
### Download web video and audio from Parabolic or directly inside Firefox

[![Windows](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/windows.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/windows.yml)
[![Linux Flatpak](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/flatpak.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/flatpak.yml)
[![macOS](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/macos.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/macos.yml)
[![Firefox Add-on](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/firefox.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/firefox.yml)

[Features](#-features) •
[Firefox integration](#-firefox-integration) •
[Firefox controls](#-firefox-controls-and-settings) •
[Known limitations](#-known-limitations) •
[Installation](#-installation) •
[Building](#-building) •
[Credits](#-credits)

</div>

> [!NOTE]
> This repository is a community adaptation of [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic). Release **Parabolic 2026.9.0** pairs with the Firefox add-on **0.8.3** on Windows, Linux through Flatpak, and macOS. The Firefox extension ID is `parabolic-media-detector@othmanbenbrahim.dev`. Facebook video recovery is confirmed. Some LinkedIn players are not yet resolved and are listed as a known limitation. Chrome and Edge packages are not built or supported.

### Persistent automation in 2026.9.0

- Downloads use explicit task states, typed failure categories and bounded automatic retries.
- RSS and Atom feeds can be followed from Firefox; new items are queued by the persistent service even after Firefox closes.
- Direct HTTP/HTTPS links can be pasted into the Firefox popup or sent from its context menu.
- The resolver layer uses registries for individual media and collections instead of a fixed list of hard-coded branches.
- Completed files are verified before success is reported; optional SHA-256 calculation is available in Firefox settings.
- The same daemon and protocol implementation is compiled for Windows x64/ARM64, Linux x86_64/aarch64 and macOS Intel/Apple Silicon.

## ✨ Features

### Downloads and media

- Download video and audio from the hundreds of websites supported by [yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md).
- Export video as MP4 or WebM and audio as MP3, Opus, FLAC or WAV.
- Run concurrent downloads.
- Retrieve subtitles, thumbnails and metadata.
- Choose quick quality presets or inspect the exact formats returned by yt-dlp.
- Update the stable yt-dlp downloader on demand when websites change their formats.
- Download direct HTTP/HTTPS media and file links from Firefox.
- Download the current page, a detected media stream, a specific right-clicked link, or a copied URL.

### Persistent downloads and queue management

- Keep browser-started downloads running after Firefox closes with a per-user background service.
- Recover interrupted browser downloads from a dedicated SQLite queue.
- Pause and resume persistent downloads.
- Cancel downloads.
- Reprioritize queued downloads.
- Use **High**, **Normal** and **Low** queue priorities.
- Schedule downloads for a future date and time.
- Persist scheduled start times across service restarts.
- Synchronize active tasks again when Firefox reconnects.
- Report analysis, queueing, downloading, processing/merging, retry, completion, cancellation and failure states.
- Verify that output files are valid and non-empty before reporting completion.
- Optionally compute a SHA-256 checksum after download.
- Automatically retry transient network, CDN and rate-limit failures with bounded exponential backoff.
- Configure the maximum number of task attempts.

### Resolver and recovery pipeline

- Prefer the real page, post or Reel permalink and try yt-dlp first.
- Retry recent non-DRM HLS/DASH manifests detected by Firefox with bundled **N_m3u8DL-RE**.
- Retry a detected direct MP4/video response when page extraction and manifest recovery fail.
- Use an optional self-hosted **Cobalt** endpoint when no suitable local path is available.
- Choose between **Automatic**, **yt-dlp only**, and **Cobalt** resolver strategies.
- Configure Cobalt authentication as none, API key or bearer token.
- Renew temporary scheduled Cobalt URLs at actual start time.
- Fall back to the stable page URL when an expiring CDN URL is no longer usable.

### Network and access controls

- Apply an optional bandwidth limit to each new browser download, including aria2 transfers.
- Choose **Conservative**, **Balanced**, or **Aggressive** network/CDN behavior.
- Use Parabolic's existing website authentication settings.
- Use the local Firefox session when a site requires authentication.
- Disable cookies entirely for a browser-started task.
- Use Parabolic's configured proxy or force a direct connection.
- Optionally send the page URL as the HTTP referrer when a protected CDN requires it.
- Firefox cookies are only requested when explicitly selected; the add-on does not extract or store them itself.

### Platforms

- **Windows:** WinUI, x64 and ARM64.
- **Linux:** sandboxed GNOME 50 Flatpak, x86_64 and aarch64.
- **macOS:** native GNOME interface, Intel and Apple Silicon.
- The Native Messaging protocol and persistent service use the same architecture across supported platforms.

## 🦊 Firefox integration

The Firefox add-on detects a suitable video and places a **Download video** control directly over the player. The main button downloads using the saved one-click preset, while the arrow opens the extended download menu.

Firefox communicates with `com.nickvision.parabolic`, a lightweight Native Messaging relay installed with Parabolic. The relay forwards requests over a current-user named pipe on Windows or a Unix domain socket on Linux/macOS to the persistent download service.

The integration can:

- start a download without bringing the Parabolic window to the foreground;
- use **Best**, **1080p**, **720p**, **480p** and **Audio only** presets;
- list exact media formats and estimated file sizes;
- report progress, processing/merge status and completion directly in Firefox;
- cancel a download or reveal its destination folder;
- pause, resume and reprioritize persistent downloads;
- synchronize active downloads after Firefox reconnects;
- schedule downloads and persist their start time across service restarts;
- prefer a durable page permalink with yt-dlp;
- use N_m3u8DL-RE for a recent non-DRM HLS/DASH manifest detected by Firefox;
- retry a recent direct MP4/video response observed by Firefox when other extraction paths fail;
- use an explicitly configured Cobalt fallback;
- apply a per-download bandwidth limit;
- renew temporary URLs at the actual scheduled start;
- retry transient CDN/network failures;
- use Firefox cookies only when explicitly selected;
- inherit Parabolic's configured proxy or force a direct connection;
- detect and install the latest stable yt-dlp version on explicit request;
- detect HTML5 video/audio, HLS playlists, DASH manifests and supported embedded players;
- recognize YouTube adaptive `googlevideo.com` traffic and associate it with the stable watch-page URL;
- expose detected-source diagnostics through the toolbar popup and optional badge.

The Windows installer registers the bridge automatically. Linux publishes a separate Firefox integration helper that launches the host contained in the Flatpak. The macOS ZIP provides **Install Firefox integration.command** after the application is moved into `/Applications`.

The Windows portable archive and the Flatpak bundle alone do **not** register Native Messaging.

## 🎛 Firefox controls and settings

### In-player widget

When media is detected, the Firefox add-on can display a compact Parabolic widget directly over the active video.

Available controls include:

- **Download video** — starts a one-click download using the configured default preset.
- **Quality menu** — select Best, 1080p, 720p, 480p or Audio only.
- **Exact formats** — inspect formats returned by yt-dlp and select a specific one.
- **Schedule download** — choose a future start date and time.
- **Check and update yt-dlp** — explicitly check for and install the current stable downloader.
- **Compatibility mode** — open the installed Parabolic application through the legacy protocol when needed.
- **Direct download** — quick access to downloading a direct HTTP/HTTPS URL.
- **RSS** — quick access to RSS/Atom subscription management.
- **Settings** — open the extension settings directly from the video widget.
- **Close (×)** — hide the complete widget for the current page. The widget becomes available again after the page is reloaded.

The widget can be globally enabled or disabled from the extension settings, independently of the temporary per-page close button.

### Popup

The Firefox toolbar popup provides a second interface for downloads and diagnostics:

- show Native Messaging bridge status;
- quick-download the current page;
- paste a direct HTTP/HTTPS URL;
- optionally fill the direct-download field from the clipboard while the popup is open;
- list detected media sources;
- clear detected-source diagnostics;
- manage RSS/Atom subscriptions;
- manually check subscribed feeds;
- filter feed items with optional keywords;
- download only the latest feed item when **Latest item only** is enabled.

Clipboard monitoring is opt-in, only operates while the popup is open, and does not automatically start a download.

### RSS and Atom subscriptions

Firefox can register RSS and Atom feeds with the persistent Parabolic service.

Available options:

- follow an RSS or Atom feed;
- apply optional keyword filters;
- use **Latest item only** mode;
- manually request a feed check;
- prevent duplicate downloads;
- keep feed processing available through the persistent service after Firefox closes.

### Video widget preferences

- Enable or disable media detection.
- Enable or disable the in-player overlay.
- Choose the one-click quality preset:
  - Best quality
  - Up to 1080p
  - Up to 720p
  - Up to 480p
  - Audio only
- Choose widget position:
  - Top right
  - Top left
  - Bottom right
  - Bottom left
- Enable or disable the detected-source counter on the Firefox toolbar icon.

### Queue and verification preferences

- Default priority:
  - High
  - Normal
  - Low
- Per-download bandwidth limit in KiB/s (`0` = unlimited).
- Maximum task attempts for transient failures.
- Optional SHA-256 checksum after successful output verification.

### Resolver preferences

- Resolver strategy:
  - Automatic
  - yt-dlp only
  - Cobalt
- Optional self-hosted Cobalt API endpoint.
- Cobalt authentication:
  - None
  - API key
  - Bearer token
- Cobalt authentication token stored in Firefox local storage and not in Parabolic's persistent recovery queue.

### Network and authentication preferences

- Network/CDN strategy:
  - Conservative
  - Balanced
  - Aggressive
- Website authentication:
  - Use Parabolic settings
  - Use Firefox session
  - No cookies
- Proxy route:
  - Use Parabolic settings
  - Direct connection
- Optional HTTP referrer forwarding for sites/CDNs that require the originating page.

### Compatibility preferences and shortcuts

- Enable or disable **Download link with Parabolic** in the Firefox right-click menu.
- Right-click a specific link to send that exact URL to Parabolic.
- Use the current tab URL when no specific link is selected.
- Press **Alt + P** to send the active tab URL to Parabolic.
- Optionally open the old `parabolic://` integration when the Native Messaging bridge is unavailable.
- Optionally remove YouTube playlist parameters and download only the current video.
- Optionally watch the clipboard while the popup is open.

## ⚠️ Known limitations

- Facebook videos have been validated with the permalink, HLS/DASH and direct-stream recovery paths.
- A direct CDN fallback can occasionally contain video without a separate audio track.
- DRM-protected streams are deliberately unsupported. The project does not request, store or use decryption keys.
- Firefox must be restarted after installing or replacing a Native Messaging manifest.
- A Firefox Flatpak or Snap requires the WebExtensions XDG desktop portal supplied by the distribution. Firefox installed directly from Mozilla or a distribution package is the supported Linux configuration.
- The macOS CI bundle is ad-hoc signed. Public distribution outside GitHub testing should additionally use an Apple Developer ID signature and notarization.

## 📥 Installation

Download versioned packages from this repository's [Releases](https://github.com/Othman-Benbrahim/Parabolic/releases).

For release **Parabolic 2026.9.0**, use the package matching the operating system and architecture together with Firefox add-on **0.8.3**.

### Windows

1. Download `NickvisionParabolicSetup-x64` for most Windows computers, or the ARM64 setup for Windows on ARM.
2. Close Firefox and Parabolic.
3. Run the installer. It can be installed over an earlier adapted build.
4. Install the signed Firefox add-on **0.8.3**, then reload the video page.

Use the installer rather than the portable archive when you want the Firefox bridge.

### Linux Flatpak

1. Install the matching `Parabolic-2026.9.0-x86_64.flatpak` or `aarch64` bundle with `flatpak install --user ./FILE.flatpak`.
2. Extract `Parabolic-2026.9.0-firefox-flatpak-integration.tar.gz`.
3. Run `chmod +x install-flatpak-firefox-integration.sh`, then `./install-flatpak-firefox-integration.sh`.
4. Restart Firefox and install or update add-on **0.8.3**.

The Flatpak bundles the GNOME runtime integration, yt-dlp, FFmpeg, aria2, Deno, N_m3u8DL-RE, the Native Messaging relay and the persistent download service.

The helper writes only a launcher below `~/.local/lib/parabolic-flatpak` and Firefox's manifest below `~/.mozilla/native-messaging-hosts`. Run `uninstall-flatpak-firefox-integration.sh` before removing the Flatpak when you no longer need the extension bridge.

### macOS

1. Extract the Intel x64 or Apple Silicon ARM64 ZIP.
2. Move `Parabolic.app` into `/Applications`.
3. Open **Install Firefox integration.command**, then restart Firefox.
4. Install Firefox add-on **0.8.3**.

The command installs only per-user Firefox and LaunchAgent files. The GitHub Actions bundle is ad-hoc signed; Gatekeeper may require explicit approval for development builds.

### Firefox add-on

Current AMO release information:

- **Version:** `0.8.3`
- **Extension ID:** `parabolic-media-detector@othmanbenbrahim.dev`
- **Application pairing:** Parabolic `2026.9.0`

A permanently installed add-on must be signed by Mozilla. Upload the add-on ZIP to Mozilla Add-ons for validation and signing.

An unsigned development XPI can be loaded temporarily:

1. Open `about:debugging#/runtime/this-firefox`.
2. Select **Load Temporary Add-on**.
3. Select the XPI or `extension/firefox/manifest.json`.

Temporary add-ons are removed when Firefox closes.

## ⚖️ Copyright notice

> [!CAUTION]
> Videos on YouTube and other websites may be protected by copyright or access restrictions. The project does not endorse downloading material without authorization. Users are responsible for complying with applicable law and each website's terms.

DRM decryption is not supported.

## 📸 Screenshots

<details>
<summary><b>GNOME interface</b></summary>

| Home | Active downloads |
|:---:|:---:|
| ![GNOME home](Nickvision.Parabolic.GNOME/Screenshots/Home.png) | ![GNOME downloads](Nickvision.Parabolic.GNOME/Screenshots/Downloading.png) |

| Dark mode | Add download |
|:---:|:---:|
| ![GNOME dark mode](Nickvision.Parabolic.GNOME/Screenshots/DarkMode.png) | ![GNOME add download](Nickvision.Parabolic.GNOME/Screenshots/AddDownloadDialog.png) |

</details>

<details>
<summary><b>Windows interface</b></summary>

| Home | Active downloads |
|:---:|:---:|
| ![Windows home](Nickvision.Parabolic.WinUI/Screenshots/Home.png) | ![Windows downloads](Nickvision.Parabolic.WinUI/Screenshots/Downloading.png) |

| Dark mode | Add download |
|:---:|:---:|
| ![Windows dark mode](Nickvision.Parabolic.WinUI/Screenshots/DarkMode.png) | ![Windows add download](Nickvision.Parabolic.WinUI/Screenshots/AddDownloadDialog.png) |

</details>

## 🔨 Building

Parabolic targets **.NET 10**.

Additional GNOME dependencies are supplied by the GNOME 50 Flatpak runtime on Linux. Windows builds require the Windows App SDK and gettext tooling.

```bash
# GNOME desktop application
dotnet run --project Nickvision.Parabolic.GNOME
```

```powershell
# Windows desktop application
dotnet run --project .\Nickvision.Parabolic.WinUI

# Firefox Native Messaging host
dotnet publish .\Nickvision.Parabolic.NativeHost -c Release -r win-x64

# Persistent background download service
dotnet publish .\Nickvision.Parabolic.DownloadService -c Release -r win-x64
```

```bash
# Prepare the architecture-matched resolver, then build the Linux Flatpak
bash resources/linux/prepare-flatpak-tools.sh linux-x64
flatpak-builder --force-clean build-dir flatpak/org.nickvision.tubeconverter.json

# macOS bundle and Firefox integration package, executed on macOS
resources/macos/publish-and-package.sh osx-arm64
```

The GitHub Actions workflows are the recommended release path because they also package dependencies, validate the Native Messaging protocol and create installers or bundles for each architecture.

## 🤝 Contributing

Bug reports and pull requests are welcome. Please include the operating system, architecture, Parabolic version, add-on version and the first relevant error from the workflow or application log.

Translations continue to be managed by the upstream project on [Weblate](https://hosted.weblate.org/projects/nickvision-tube-converter/).

## 🙏 Credits

- [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic) and its contributors for the original application.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp), FFmpeg, aria2 and Deno.
- Firefox Native Messaging adaptation and release integration maintained in this fork by Othman Benbrahim.

Parabolic is distributed under the licenses included in this repository and follows the [GNOME Code of Conduct](https://conduct.gnome.org/).

