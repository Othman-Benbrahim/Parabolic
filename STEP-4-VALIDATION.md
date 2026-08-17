# Step 4 validation — Parabolic 2026.8.4 / Firefox 0.8.0

Install the 2026.8.4 Windows setup before loading Firefox 0.8.0 temporarily from `about:debugging`. The popup must report the `n-m3u8dl-re` and `permalink-first` capabilities.

## Source validation already completed

- Firefox JavaScript syntax checks: passed.
- Native protocol and permalink/manifest routing smoke test: passed.
- Mozilla `addons-linter`: 0 errors, 0 notices, 0 warnings.
- Windows persistent-service source smoke test: passed.
- Workflow YAML, JSON, XML, Flatpak inputs, XPI, and source ZIP integrity: passed.

A complete .NET/WinUI build is intentionally left to the Windows GitHub Actions matrix because the packaging environment used for this source archive does not contain the .NET SDK or Inno Setup.

## Facebook

1. Open a public or account-accessible Reel/video and start playback.
2. Click the in-player Parabolic button.
3. Confirm the error no longer contains the generic `https://www.facebook.com/?_fb_noscript=1` URL when a Reel/post permalink is present around the player.
4. If yt-dlp fails, confirm the progress message says it is retrying the detected HLS/DASH stream with N_m3u8DL-RE.
5. Confirm the final MP4 opens and contains both video and audio.

## LinkedIn

1. Open a video post, start playback, and click the in-player button.
2. Confirm the request uses `/feed/update/urn:li:activity:...` or another stable post permalink when available.
3. If yt-dlp reports `Unable to extract video`, confirm N_m3u8DL-RE starts from the detected manifest.

## Safety and regressions

- YouTube still uses its stable watch/Reel/live page and yt-dlp.
- A normal direct MP4 still downloads without invoking N_m3u8DL-RE.
- Audio-only tasks remain on yt-dlp and do not use the stream fallback.
- A DRM-protected manifest ends with a clear unsupported-DRM message and never requests a key.
- Closing Firefox after enqueue does not stop the persistent service.
- Installer and portable artifacts contain `N_m3u8DL-RE.exe` and `N_m3u8DL-RE-LICENSE.txt`.
