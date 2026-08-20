# Parabolic 2026.8.6 — Linux Flatpak and native macOS support

Parabolic 2026.8.6 coordinates with Firefox add-on 0.8.2. The persistent Firefox download-manager integration is packaged for Windows, Linux through Flatpak, and macOS. Chrome and Edge packages are not produced.

## Highlights

- Adds Linux x86_64 and aarch64 Flatpak bundles based on the GNOME 50 runtime.
- Adds a host-side helper that registers the Native Messaging relay contained in the Flatpak with Firefox.
- Adds macOS Intel and Apple Silicon ZIPs containing `Parabolic.app` and a Firefox integration command.
- Ports the persistent download service from a Windows-only named pipe to secured per-user named pipes or Unix domain sockets.
- Registers Firefox Native Messaging in the platform-specific user location.
- Starts the Flatpak download service on demand on Linux and uses a per-user `LaunchAgent` on macOS.
- Bundles yt-dlp, FFmpeg, aria2, Deno and N_m3u8DL-RE in the Linux Flatpak; the macOS application bundle contains its media tools.
- Preserves the permalink-first, HLS/DASH, direct-stream and optional Cobalt resolver order.
- Keeps DRM decryption deliberately unsupported.

## Installation notes

- Linux: install the architecture-matched `.flatpak`, extract the Firefox integration helper and run `./install-flatpak-firefox-integration.sh`.
- macOS: move `Parabolic.app` to `/Applications`, then open `Install Firefox integration.command`.
- Restart Firefox after installing or updating Parabolic's native integration.
- Firefox installed directly from Mozilla or a distribution package is supported. A confined Firefox Flatpak/Snap additionally depends on the WebExtensions XDG desktop portal.
- The macOS GitHub Actions artifacts are ad-hoc signed development bundles. Apple Developer ID signing and notarization remain necessary for polished public distribution.

## Known limitation

Some LinkedIn video players still expose no durable permalink or usable manifest/direct stream. They may return `Parabolic could not find downloadable media`; improved LinkedIn-specific extraction is deferred to a later release.

## Versions

- Firefox add-on: 0.8.2, Manifest V3, Native Messaging protocol v3.
- Desktop/service: 2026.8.6.
- Targets: Windows x64/ARM64, Linux Flatpak x86_64/aarch64, macOS Intel/Apple Silicon.
- N_m3u8DL-RE: 0.6.0-beta, architecture matched.
