# Linux migration to Flatpak — Parabolic 2026.8.6

Linux distribution now uses Flatpak for both x86_64 and aarch64. Windows and macOS remain native packages.

## What changed

- the Linux GitHub Actions job builds against the GNOME 50 runtime;
- the Flatpak contains Parabolic, the Firefox native host, the background download service, yt-dlp, FFmpeg, aria2, Deno and the architecture-matched N_m3u8DL-RE binary;
- a separate helper installs the Firefox Native Messaging manifest in the current user profile;
- the native Ubuntu tarball workflow and its install scripts are retired.

## Files to remove from the branch

These obsolete native-Linux files must be removed when applying the migration ZIP:

- `.github/workflows/linux-native.yml`
- `resources/linux/install-native.sh`
- `resources/linux/package-native.sh`
- `resources/linux/uninstall-native.sh`
- `deliverables/BUILD-NATIVE-2026.8.6.ps1`

## Expected GitHub Actions artifacts

- `Parabolic-2026.8.6-x86_64.flatpak`
- `Parabolic-2026.8.6-aarch64.flatpak`
- `Parabolic-2026.8.6-firefox-flatpak-integration.tar.gz`

The Firefox helper targets a standard, unconfined Firefox installation. Firefox distributed as Flatpak or Snap additionally requires a working WebExtensions XDG desktop portal on the Linux distribution.
