# Firefox Add-ons listing — Parabolic Download Manager 0.8.2

## Short description

Download detected web videos from Firefox through Parabolic on Windows, native Linux and macOS.

## Full description

Parabolic Download Manager adds a download button directly to compatible video players in Firefox. Choose a quick quality preset, inspect exact formats, schedule a task and follow its progress without leaving the page.

Downloads are handed to the installed Parabolic desktop application through Firefox Native Messaging. The per-user background service keeps accepted tasks running after Firefox closes and can recover interrupted browser downloads.

When a website does not expose a straightforward download, Parabolic first tries the real page or post permalink with yt-dlp. It can then retry a recent non-DRM HLS/DASH manifest with N_m3u8DL-RE or a detected direct video stream. An authorized self-hosted Cobalt endpoint remains optional.

Version 0.8.2 works with Parabolic 2026.8.6 packages for Windows x64/ARM64, native Linux x64/ARM64 and macOS Intel/Apple Silicon. Chrome and Edge are not supported by this package. Some LinkedIn players remain a known limitation.

Parabolic does not request DRM keys or perform DRM decryption. Only download media you are authorized to save and follow the website's terms and applicable law.

## Required companion application

Install Parabolic 2026.8.6 for your operating system before using the extension. Windows registers the bridge through its setup program. Linux uses the included `install.sh`. On macOS, move `Parabolic.app` to `/Applications` and run the included Firefox integration command.
