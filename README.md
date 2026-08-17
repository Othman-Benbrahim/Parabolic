<div align="center">

![Parabolic](resources/banner.png)

# 🎬 Parabolic Download Manager Edition

### Download web video and audio from Parabolic or directly inside Firefox

[![Windows](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/windows.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/windows.yml)
[![Flatpak](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/flatpak.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/flatpak.yml)
[![macOS](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/macos.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/macos.yml)
[![Firefox Add-on](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/firefox.yml/badge.svg)](https://github.com/Othman-Benbrahim/Parabolic/actions/workflows/firefox.yml)

[Features](#-features) •
[Firefox integration](#-firefox-integration) •
[Installation](#-installation) •
[Building](#-building) •
[Credits](#-credits)

</div>

> [!NOTE]
> This repository is a community adaptation of [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic). Version **2026.8.3** implements the Firefox-only final step of the download-manager roadmap with coordinated Firefox add-on **0.7.0**. Validate the Windows setup and unsigned add-on before publishing the final GitHub Release.

## ✨ Features

- Download video and audio from the hundreds of websites supported by [yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md).
- Export video as MP4 or WebM and audio as MP3, Opus, FLAC or WAV.
- Run concurrent downloads and retrieve subtitles, thumbnails and metadata.
- Choose quick quality presets or inspect the exact formats returned by yt-dlp.
- Update the stable yt-dlp downloader on demand when websites change their formats.
- Keep browser downloads running after Firefox closes with a per-user background service.
- Recover interrupted browser downloads from a dedicated SQLite queue.
- Schedule downloads for a future time and order queued work with High, Normal and Low priorities.
- Route detected MP4, HLS, and DASH media directly, with yt-dlp and optional self-hosted Cobalt resolvers.
- Apply an optional bandwidth limit to each new browser download.
- Renew scheduled Cobalt URLs when the task starts and fall back to the stable page when a direct CDN URL expires.
- Choose conservative, balanced, or aggressive fragment/retry behavior for unstable networks and CDNs.
- Explicitly inherit Parabolic authentication/proxy settings, use the local Firefox session, or disable cookies/proxy use for browser-started tasks.
- Use native WinUI on Windows and the GNOME interface on Linux and macOS.
- Build and package Windows x64/ARM64, Linux Flatpak x86_64/aarch64 and macOS x64/ARM64.

## 🦊 Firefox integration

The Firefox add-on detects a suitable video and places a **Download video** control directly over the player. The main button downloads with the saved preset; its arrow opens quality, exact-format and yt-dlp update actions.

On Windows, Firefox communicates with `com.nickvision.parabolic`, a lightweight Native Messaging relay installed with Parabolic. The relay forwards requests over a current-user named pipe to the persistent download service. It can:

- start a download without bringing the Parabolic window to the foreground;
- use Best, 1080p, 720p, 480p and Audio-only presets;
- list exact formats and file sizes;
- report progress, merge status and completion;
- cancel a download or reveal its destination folder;
- pause, resume and reprioritize persistent downloads;
- synchronize active downloads after Firefox reconnects;
- schedule downloads and persist their start time across service restarts;
- use direct MP4/HLS/DASH routing, yt-dlp, or an explicitly configured Cobalt fallback;
- apply a per-download bandwidth limit, including aria2 transfers;
- renew temporary URLs at the actual scheduled start and retry transient CDN/network failures;
- use Firefox cookies only when explicitly selected, without the add-on extracting or storing them;
- inherit Parabolic's configured proxy or force a direct connection per browser task;
- detect and install the latest stable yt-dlp version on explicit request.

The Windows installer is required for this integration because it installs the host manifest and Firefox registry entries. The portable Windows archive contains the host executable but does **not** register Native Messaging automatically.

The desktop application works on Linux and macOS, but the Firefox Native Messaging bridge in this release is currently packaged for Windows only.

## 📥 Installation

Download versioned packages from this repository's [Releases](https://github.com/Othman-Benbrahim/Parabolic/releases).

### Windows

1. Download the x64 or ARM64 setup executable for your computer.
2. Close Firefox and Parabolic.
3. Run the installer. It can be installed over an earlier adapted build.
4. Install Firefox add-on `0.7.0`, then reload the video page.

Use the installer rather than the portable archive when you want the Firefox bridge.

### Firefox add-on

A permanently installed add-on must be signed by Mozilla. An unsigned XPI from GitHub Actions is intended for development or submission:

1. Open `about:debugging#/runtime/this-firefox`.
2. Select **Load Temporary Add-on**.
3. Select the XPI or `extension/firefox/manifest.json`.

Temporary add-ons are removed when Firefox closes.

### Linux Flatpak

Download the bundle matching your CPU, then run:

```bash
flatpak install ./Parabolic-2026.8.3-Linux-x86_64.flatpak
flatpak run org.nickvision.tubeconverter
```

Use the `aarch64` bundle instead on ARM64 Linux.

### macOS

Download and extract the x64 or ARM64 archive produced for your Mac, then move `Parabolic.app` to Applications.

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

Additional GNOME dependencies are GTK4, libadwaita and blueprint-compiler. Windows builds require the Windows App SDK and gettext tooling.

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

The GitHub Actions workflows are the recommended release path because they also package dependencies, validate the Native Messaging protocol and create installers or bundles for each architecture.

## 🤝 Contributing

Bug reports and pull requests are welcome. Please include the operating system, architecture, Parabolic version, add-on version and the first relevant error from the workflow or application log.

Translations continue to be managed by the upstream project on [Weblate](https://hosted.weblate.org/projects/nickvision-tube-converter/).

## 🙏 Credits

- [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic) and its contributors for the original application.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp), FFmpeg, aria2 and Deno.
- Firefox Native Messaging adaptation and release integration maintained in this fork by Othman Benbrahim.

Parabolic is distributed under the licenses included in this repository and follows the [GNOME Code of Conduct](https://conduct.gnome.org/).
