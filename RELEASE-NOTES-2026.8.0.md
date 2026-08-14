# Parabolic 2026.8.0 — Firefox Native Bridge Edition

This release adds a direct Firefox-to-Parabolic download workflow while keeping the full desktop application available on Windows, Linux and macOS.

## Highlights

- Added Firefox add-on 0.4.0 with an automatic in-player **Download video** button.
- Added a Windows Native Messaging host for background downloads without opening or focusing the Parabolic window.
- Added Best, 1080p, 720p, 480p and Audio-only presets.
- Added exact yt-dlp format discovery, progress updates, cancellation and open-folder actions.
- Added explicit detection and installation of stable yt-dlp updates from the Firefox interface.
- Improved Native Messaging startup reliability and download launch time.
- Fixed YouTube format selection and download-state handling.
- Added and validated Windows x64/ARM64 installers and portable packages.
- Fixed Linux Flatpak builds for x86_64 and aarch64, including refreshed FFmpeg sources.
- Added macOS x64 and ARM64 application bundles.

## Installation notes

### Windows and Firefox

Install the Windows setup package that matches your architecture before installing the Firefox add-on. The setup package registers `com.nickvision.parabolic`, which Firefox needs to communicate with Parabolic.

The portable Windows package does not register Native Messaging automatically and is therefore not the recommended package for Firefox integration.

Firefox requires Mozilla signing for permanent add-on installation. The unsigned XPI is intended for temporary testing or Mozilla Add-ons submission.

### Linux

Download the Flatpak bundle matching your architecture and install it with:

```bash
flatpak install ./Parabolic-2026.8.0-Linux-x86_64.flatpak
```

The desktop application is supported on Linux; the Firefox Native Messaging bridge included in this release is currently packaged for Windows only.

### macOS

Download the archive matching your Mac, extract `Parabolic.app`, and move it to Applications.

## Important limitations

- DRM-protected media cannot be downloaded.
- Website changes may temporarily break individual formats; use **Check and update yt-dlp** from the Firefox quality menu when this happens.
- Closing Firefox terminates downloads owned by its Native Messaging host.

## Credits

This community edition is based on [NickvisionApps/Parabolic](https://github.com/NickvisionApps/Parabolic). Thanks to the upstream Parabolic, yt-dlp, FFmpeg, aria2 and Deno contributors.
