# Step 3 validation — Parabolic 2026.8.3 / Firefox 0.7.0

Install the 2026.8.3 Windows setup before loading Firefox 0.7.0. The bridge popup must report protocol v3 and the new capabilities `url-renewal`, `cdn-retry`, `firefox-auth`, and `proxy-control`.

## 1. Regression checks

- Start a normal one-click download and verify progress and completion.
- Close Firefox while it downloads; verify that the download continues.
- Pause/resume, change priority, cancel, schedule, and apply a bandwidth limit.
- Confirm direct MP4 and at least one HLS or DASH source still work.

## 2. Network/CDN strategies

Open Firefox add-on settings and test `Conservative`, `Balanced`, and `Aggressive` on a resumable media source. Confirm each task starts and partial files remain reusable after an interruption. Balanced is the recommended default.

## 3. Stable fallback for temporary URLs

Start a direct network media task from a page whose media URL contains an expiry/signature query. If the direct URL expires or returns 401/403/410, verify the task reports a network/CDN retry and continues from the stable page through yt-dlp.

This behavior cannot bypass DRM or a website that does not expose a downloadable media source.

## 4. Scheduled Cobalt renewal

Use only a self-hosted instance or one whose owner authorizes this client.

1. Configure an unauthenticated Cobalt endpoint.
2. Select Cobalt and schedule a task several minutes ahead.
3. Close Firefox.
4. Confirm the service resolves Cobalt at the scheduled time and begins downloading.

Then configure a Cobalt token and try to schedule another task. It must be rejected with an explanation that the token is not persisted. An immediate authenticated Cobalt download should still work.

## 5. Controlled authentication and proxy

- With `Use Firefox session`, test authorized content that yt-dlp can access from the local Firefox profile.
- With `No cookies`, confirm the same protected source fails normally rather than silently using a browser session.
- With `Use Parabolic settings`, confirm the desktop application's cookie and proxy choices are inherited.
- With `Direct connection`, confirm the Parabolic proxy is bypassed for the new task.
- Enable page-referrer forwarding only on a CDN that requires it, then disable it again.

The add-on must never display, log, or transfer raw cookie values.

## 6. Release gate

The final GitHub Release is approved only when Windows x64, Firefox, protocol/source smoke tests, and the manual Firefox checks above pass. Chrome and Edge are not part of this validation.
