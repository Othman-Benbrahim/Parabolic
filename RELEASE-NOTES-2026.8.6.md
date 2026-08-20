# Parabolic 2026.8.6 — native Linux and macOS support

Parabolic 2026.8.6 coordinates with Firefox add-on 0.8.2. The persistent Firefox download-manager integration is now packaged for Windows, native Linux and macOS. No Chrome, Edge or Flatpak package is produced by this release.

## Highlights

- Adds native Linux x64 and ARM64 tarballs with a per-user installer.
- Adds macOS Intel and Apple Silicon ZIPs containing `Parabolic.app` and a Firefox integration command.
- Ports the persistent download service from a Windows-only named pipe to secured per-user named pipes or Unix domain sockets.
- Registers Firefox Native Messaging in the platform-specific user location.
- Uses `systemd --user` on Linux and a per-user `LaunchAgent` on macOS, with Firefox on-demand startup as a fallback.
- Bundles yt-dlp, Deno and N_m3u8DL-RE in Linux packages; the macOS application bundle contains its media tools.
- Preserves the permalink-first, HLS/DASH, direct-stream and optional Cobalt resolver order.
- Keeps DRM decryption deliberately unsupported.

## Installation notes

- Linux: install GTK4, libadwaita, FFmpeg and aria2, extract the matching tarball and run `./install.sh`.
- macOS: move `Parabolic.app` to `/Applications`, then open `Install Firefox integration.command`.
- Restart Firefox after installing or updating Parabolic's native integration.
- The macOS GitHub Actions artifacts are ad-hoc signed development bundles. Apple Developer ID signing and notarization remain necessary for polished public distribution.

## Known limitation

Some LinkedIn video players still expose no durable permalink or usable manifest/direct stream. They may return `Parabolic could not find downloadable media`; improved LinkedIn-specific extraction is deferred to a later release.

## Versions

- Firefox add-on: 0.8.2, Manifest V3, Native Messaging protocol v3.
- Desktop/service: 2026.8.6.
- Native targets: Windows x64/ARM64, Linux x64/ARM64, macOS Intel/Apple Silicon.
- N_m3u8DL-RE: 0.6.0-beta, architecture matched.
