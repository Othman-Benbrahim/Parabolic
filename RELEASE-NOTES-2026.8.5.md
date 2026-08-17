# Parabolic 2026.8.5 — Firefox social-video recovery

Parabolic 2026.8.5 coordinates with Firefox add-on 0.8.1. No Chrome or Edge package is produced.

## Highlights

- Searches deeper inside the Facebook article/dialog/player container for a real Reel, video, post, group permalink, or embedded Facebook video id.
- Keeps the stable social permalink as the first yt-dlp candidate.
- Retains the existing HLS/DASH fallback through bundled N_m3u8DL-RE.
- Adds a final fallback to a recent direct MP4/video response observed by Firefox when page extraction and manifest handling cannot proceed.
- Shows the connected Parabolic application version in the overlay menu, making extension/service mismatches visible.
- Never requests DRM keys or attempts DRM decryption.

## Resolver order

1. Real Facebook/LinkedIn permalink with yt-dlp.
2. Detected HLS/DASH manifest with N_m3u8DL-RE.
3. Detected direct MP4/video stream with yt-dlp.
4. Optional authorized Cobalt instance where configured.

## Versions

- Firefox add-on: 0.8.1, Manifest V3, Native Messaging protocol v3.
- Desktop/service: 2026.8.5.
- Bundled N_m3u8DL-RE: 0.6.0-beta for Windows x64 and ARM64.
