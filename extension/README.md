![](resources/banner.png)

# Features
- **In-Player Download Button (Firefox)**: The primary download control appears automatically over the active video, so the toolbar popup is not required.
- **One-Click Download Presets (Firefox)**: Download the best quality, cap video at 1080p/720p/480p, or extract audio from the page itself.
- **Native Background Downloads (Firefox)**: Adapted Windows, Linux Flatpak and macOS builds start downloads and return progress through Native Messaging without switching away from Firefox.
- **Persistent Queue (Firefox)**: Accepted downloads continue after Firefox closes and recover from a dedicated SQLite queue after interruption.
- **Priority Scheduling**: New browser downloads can use High, Normal or Low priority.
- **Scheduled Downloads**: Queue a future start time that is persisted by the per-user background service.
- **Permalink-First Resolver Pipeline**: The real page or Facebook post/Reel permalink is tried with yt-dlp first; a detected non-DRM HLS/DASH manifest is retried with bundled N_m3u8DL-RE, then a detected direct MP4/video response can be used as a final local fallback. A self-hosted Cobalt endpoint remains optional.
- **Bandwidth Limit**: Apply a KiB/s limit to each new browser task.
- **Temporary URL Renewal**: Resolve scheduled Cobalt links at start time and fall back to the stable page when a direct CDN link expires.
- **Network/CDN Strategies**: Choose conservative, balanced, or aggressive fragment concurrency and retries.
- **Controlled Access**: Explicitly inherit Parabolic cookies/proxy settings, use the local Firefox session, or disable them for a task.
- **Context Menu Integration**: Right-click and select: `Open link in Parabolic` on any specific link to send that exact URL without needing to open the page. Otherwise, use the current tab's URL.
- **Keyboard shortcut**: Press `Alt` + `P` to send the URL of your active tab to Parabolic.
- **Lightweight & Fast**: Designed to be fast, unobtrusive, easy to use, and respects your privacy.
- **Firefox Media Detection**: The Firefox version detects HTML5 video/audio, HLS playlists and DASH manifests, including media loaded in embedded frames.
- **Diagnostics Popup**: The toolbar popup shows native-bridge status and detected sources as a secondary diagnostic interface.
- **YouTube Adaptive Detection**: Firefox recognizes YouTube's `googlevideo.com` media traffic and groups audio/video requests under the stable watch-page URL.
- **Compatibility Mode**: The old `parabolic://` integration remains available as an explicit fallback while the native bridge is unavailable.

# Installation — Firefox
[![get-the-addon](resources/firefox.png)](https://addons.mozilla.org/en-US/firefox/addon/parabolic/)

The enhanced detector is Firefox-only and documented in [`firefox/ARCHITECTURE.md`](firefox/ARCHITECTURE.md). It can be loaded temporarily from `about:debugging` by selecting `firefox/manifest.json`. The legacy Chromium source directory is not built or supported by this roadmap.

Facebook recovery is validated in version 0.8.2. Some LinkedIn video players still expose no durable or usable media address and are a documented limitation for this release. The same Firefox package is used on Windows, Linux Flatpak and macOS; each platform supplies its bridge, with a separate host-side registration helper for Linux.
