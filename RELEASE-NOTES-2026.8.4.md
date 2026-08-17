# Parabolic 2026.8.4 — permalink-first Firefox fallback

Parabolic 2026.8.4 coordinates with Firefox add-on 0.8.0 and remains Firefox-only for the browser integration.

## Highlights

- Prefers the real Facebook Reel/video/post or LinkedIn activity/post permalink found around the active player.
- Removes Facebook `_fb_noscript=1` noise and unwraps safe Facebook `login/?next=...` redirects before sending a page to yt-dlp.
- Keeps the most recent Firefox-detected HLS/DASH manifest as a separate fallback instead of replacing the durable page URL.
- Retries eligible yt-dlp extraction failures with bundled N_m3u8DL-RE 0.6.0-beta.
- Forwards the Firefox User-Agent and the selected page as stream `Referer`; cookie values are never captured by the add-on or passed to N_m3u8DL-RE.
- Supports the existing bandwidth, retry, proxy, queue, scheduling, pause/resume, and recovery controls for the new stream engine.
- Refuses DRM decryption by design: no key, PSSH, or decryption-engine option is supplied.
- Bundles the upstream N_m3u8DL-RE MIT license in installer and portable artifacts.

## Resolver order

1. Durable page permalink with yt-dlp.
2. Recent non-DRM HLS/DASH manifest with N_m3u8DL-RE when yt-dlp reports an extraction/access failure.
3. Optional authorized Cobalt instance when configured and no usable manifest was detected.

## Versions

- Firefox add-on: 0.8.0, Manifest V3, Native Messaging protocol v3.
- Desktop/service: 2026.8.4.
- Bundled N_m3u8DL-RE: 0.6.0-beta for Windows x64 and ARM64.
