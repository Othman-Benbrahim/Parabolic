# Parabolic Firefox Native Messaging Protocol

This document is the implementation contract between Firefox add-on `0.8.x` and Parabolic `2026.8.5`. Protocol version `3` uses Firefox Native Messaging framing between Firefox and a lightweight relay. The relay forwards the same frames to the persistent per-user service over a secured named pipe.

## Host registration

The Firefox native host name is:

```text
com.nickvision.parabolic
```

The Windows installer must register a native-host manifest under either:

```text
HKEY_CURRENT_USER\Software\Mozilla\NativeMessagingHosts\com.nickvision.parabolic
HKEY_LOCAL_MACHINE\Software\Mozilla\NativeMessagingHosts\com.nickvision.parabolic
```

The registry default value points to the native-host JSON manifest. The manifest must use `type: "stdio"` and allow this extension ID:

```text
parabolic-media-detector@othmanbenbrahim.dev
```

## Request envelope

```json
{
  "protocolVersion": 3,
  "requestId": "9ca24fa5-0aac-4ab0-a5fc-a368a13f5a21",
  "type": "download",
  "payload": {}
}
```

Every request receives one response with the same `requestId`.

## Successful response

```json
{
  "protocolVersion": 3,
  "requestId": "9ca24fa5-0aac-4ab0-a5fc-a368a13f5a21",
  "type": "response",
  "ok": true,
  "payload": {}
}
```

## Error response

```json
{
  "protocolVersion": 3,
  "requestId": "9ca24fa5-0aac-4ab0-a5fc-a368a13f5a21",
  "type": "response",
  "ok": false,
  "error": {
    "code": "UNSUPPORTED_URL",
    "message": "This URL cannot be downloaded."
  }
}
```

Error messages are user-visible and must not include secrets, cookies or command-line contents.

## `hello`

Checks compatibility without opening the Parabolic window.

Request payload:

```json
{
  "extensionId": "parabolic-media-detector@othmanbenbrahim.dev",
  "extensionVersion": "0.8.1",
  "protocolVersion": 3
}
```

Response payload:

```json
{
  "appVersion": "2026.8.5",
  "protocolVersion": 3,
  "capabilities": ["formats", "download", "progress", "cancel", "open-folder", "ytdlp-update", "persistent-queue", "priority", "pause-resume", "list-downloads", "resolver-pipeline", "cobalt", "direct-media", "direct-stream-fallback", "hls-dash", "n-m3u8dl-re", "permalink-first", "bandwidth-limit", "scheduling", "url-renewal", "cdn-retry", "firefox-auth", "proxy-control"]
}
```

## `check-ytdlp-update`

Checks the executable currently selected by Parabolic against the latest stable
yt-dlp release. It does not install anything.

Response payload:

```json
{
  "currentVersion": "2026.03.17",
  "latestVersion": "2026.8.13",
  "updateAvailable": true,
  "updated": false,
  "message": "yt-dlp 2026.8.13 is available."
}
```

## `update-ytdlp`

Downloads and activates the latest stable yt-dlp release through Parabolic's
dependency updater. This command is user initiated and fails with
`DOWNLOADS_ACTIVE` while a download is running or queued.

Response payload:

```json
{
  "currentVersion": "2026.8.13",
  "latestVersion": "2026.8.13",
  "updateAvailable": false,
  "updated": true,
  "message": "yt-dlp was updated successfully to 2026.8.13."
}
```

## `get-formats`

Asks Parabolic/yt-dlp to inspect a page and return a concise user-facing format list.

Request payload:

```json
{
  "tabId": 12,
  "pageUrl": "https://example.com/watch/123",
  "mediaUrl": "",
  "manifestUrl": "https://cdn.example.com/master.m3u8",
  "manifestKind": "hls",
  "userAgent": "Mozilla/5.0 ... Firefox/153.0",
  "title": "Example video",
  "preset": "best",
  "formatId": "",
  "sourceKind": "page",
  "frameUrl": "https://example.com/watch/123",
  "priority": "normal",
  "resolverPreference": "auto",
  "cobaltEndpoint": "",
  "cobaltAuthScheme": "none",
  "cobaltAuthToken": "",
  "speedLimitKbps": 0,
  "scheduledAt": "",
  "networkStrategy": "balanced",
  "authenticationMode": "parabolic",
  "proxyMode": "parabolic",
  "sendPageReferer": false
}
```

Response payload:

```json
{
  "formats": [
    {
      "id": "137+140",
      "label": "1080p",
      "resolution": "1920×1080",
      "ext": "mp4",
      "filesizeLabel": "84 MB",
      "note": "video + audio"
    }
  ]
}
```

The host should return at most 20 useful combined choices rather than every low-level audio/video stream.

## `download`

Starts a download without activating the main desktop window.

The request uses the same payload as `get-formats`. Supported preset values are:

| Preset | Meaning |
| --- | --- |
| `best` | Best combined or mergeable video and audio. |
| `1080` | Best video no higher than 1080p plus audio. |
| `720` | Best video no higher than 720p plus audio. |
| `480` | Best video no higher than 480p plus audio. |
| `audio` | Best audio-only result. |

If `formatId` is non-empty, it takes precedence over the preset after the host validates it as a format returned for that URL.

In automatic mode, `pageUrl` is the durable permalink selected by Firefox. The host tries that page with yt-dlp first. If extraction fails and `manifestUrl` is a recent HLS/DASH request observed for the active tab, the persistent service retries it with N_m3u8DL-RE. The fallback forwards the page as `Referer` and the Firefox User-Agent, but never receives cookie values or DRM keys.

`resolverPreference` accepts `auto`, `yt-dlp`, or `cobalt`. Auto selects an HTTP media URL detected by Firefox first, then yt-dlp, and uses Cobalt only when an endpoint is configured and yt-dlp discovery returns no media. `speedLimitKbps` is `0` for unlimited or a value from 32 through 10,000,000. `scheduledAt` is empty or an ISO 8601 future date no more than one year away.

`networkStrategy` accepts `conservative`, `balanced`, or `aggressive` and controls fragment concurrency, socket timeouts, and retry counts. `authenticationMode` accepts `parabolic`, `firefox`, or `none`; Firefox mode is implemented locally by yt-dlp and does not transfer cookie values through Native Messaging. `proxyMode` accepts `parabolic` or `direct`. `sendPageReferer` is explicit and false by default.

Unauthenticated Cobalt tasks may be scheduled: the service stores the stable source and resolves a fresh temporary URL when the task actually starts. Authenticated Cobalt scheduling is rejected because the Cobalt token is deliberately not persisted. Direct temporary media URLs retain a stable-page fallback for HTTP 401/403/410 or signature-expiry failures.

Response payload:

```json
{
  "downloadId": "download-a450d4",
  "status": "queued",
  "priority": "normal",
  "resolver": "direct",
  "scheduledAt": "2026-08-18T06:00:00.0000000+00:00",
  "speedLimitKbps": 2048
}
```

## Progress event

Events are not request responses and use `type: "event"`:

```json
{
  "protocolVersion": 3,
  "type": "event",
  "payload": {
    "downloadId": "download-a450d4",
    "tabId": 12,
    "status": "downloading",
    "progress": 42.5,
    "speed": "8.4 MiB/s",
    "eta": 18,
    "filename": "Example video.mp4",
    "priority": "normal",
    "resolver": "direct",
    "scheduledAt": null,
    "speedLimitKbps": 2048
  }
}
```

Status is one of `scheduled`, `queued`, `analyzing`, `downloading`, `paused`, `merging`, `completed`, `failed`, or `cancelled`. Progress is a number from 0 through 100 when known.

## Persistent queue controls

`pause`, `resume`, and `cancel` use this payload:

```json
{
  "downloadId": "download-a450d4"
}
```

`set-priority` adds one of `high`, `normal`, or `low`:

```json
{
  "downloadId": "download-a450d4",
  "priority": "high"
}
```

`list-downloads` takes an empty payload and returns active queue snapshots. Firefox calls it after reconnecting so completion events and tab routing can continue without restarting the download.

## `cancel`

Request payload:

```json
{
  "downloadId": "download-a450d4"
}
```

The host acknowledges cancellation with an empty successful payload and later emits the `cancelled` event.

## `open-folder`

Request payload:

```json
{
  "downloadId": "download-a450d4"
}
```

The host opens the containing folder only for a download ID it created. It must not accept an arbitrary filesystem path from the extension.

## Windows lifecycle

Firefox starts `Nickvision.Parabolic.NativeHost.exe` over standard input/output. That executable only connects to `Parabolic.DownloadManager.v1` and copies framed messages in both directions. It starts `Nickvision.Parabolic.DownloadService.exe` when necessary.

The service owns discovery, the priority queue, downloads and the separate `browser_recovery_queue` SQLite table. Closing Firefox ends only the relay connection. The service and accepted downloads remain active, and interrupted items are restored when the service next starts.
