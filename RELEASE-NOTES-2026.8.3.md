# Parabolic 2026.8.3 — Firefox download-manager roadmap

Parabolic 2026.8.3 completes the three-step download-manager roadmap for Firefox with coordinated add-on 0.7.0.

## New in step 3

- Scheduled unauthenticated Cobalt tasks now resolve a fresh media URL at their actual start time.
- Direct media tasks retain a stable page fallback when a temporary CDN URL returns HTTP 401, 403, 410, or a signature-expiry error.
- Transient network failures receive bounded process-level retries with exponential delay, in addition to yt-dlp fragment retries and partial-file continuation.
- Conservative, balanced, and aggressive network/CDN strategies control fragment concurrency, retry count, and socket timeout.
- Website authentication is explicit: inherit Parabolic settings, use the local Firefox session through yt-dlp, or use no cookies.
- Proxy behavior is explicit: inherit the Parabolic proxy or force a direct connection.
- HTTP referrer forwarding is opt-in and disabled by default.

## Security and privacy

- The add-on never extracts, receives, or transmits Firefox cookie values.
- Cobalt tokens remain in Firefox local storage and are never written to the Parabolic recovery database or logs.
- Authenticated Cobalt tasks cannot be scheduled because persisting that token would violate the storage rule.
- No shared public Cobalt instance is configured automatically.
- DRM decryption is not supported.

## Compatibility

- Firefox add-on: 0.7.0, Manifest V3, Native Messaging protocol v3.
- Desktop/service: 2026.8.3.
- Firefox integration is packaged for Windows x64 and ARM64 through the installer.
- Chrome and Edge adaptations are outside this release.

Unsigned XPI packages are for temporary testing and Mozilla submission. Permanent Firefox installation requires Mozilla signing.
