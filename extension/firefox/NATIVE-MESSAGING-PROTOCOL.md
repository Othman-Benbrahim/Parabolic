# Parabolic Firefox Native Messaging Protocol

This document is the implementation contract between Firefox add-on `0.4.x` and the adapted Parabolic desktop release. Protocol version `1` uses Firefox Native Messaging framing with JSON message bodies.

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
  "protocolVersion": 1,
  "requestId": "9ca24fa5-0aac-4ab0-a5fc-a368a13f5a21",
  "type": "download",
  "payload": {}
}
```

Every request receives one response with the same `requestId`.

## Successful response

```json
{
  "protocolVersion": 1,
  "requestId": "9ca24fa5-0aac-4ab0-a5fc-a368a13f5a21",
  "type": "response",
  "ok": true,
  "payload": {}
}
```

## Error response

```json
{
  "protocolVersion": 1,
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
  "extensionVersion": "0.4.0",
  "protocolVersion": 1
}
```

Response payload:

```json
{
  "appVersion": "2026.8.0",
  "protocolVersion": 1,
  "capabilities": ["formats", "download", "progress", "cancel", "open-folder", "ytdlp-update"]
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
  "title": "Example video",
  "preset": "best",
  "formatId": "",
  "sourceKind": "page",
  "frameUrl": "https://example.com/watch/123"
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

Response payload:

```json
{
  "downloadId": "download-a450d4",
  "status": "queued"
}
```

## Progress event

Events are not request responses and use `type: "event"`:

```json
{
  "protocolVersion": 1,
  "type": "event",
  "payload": {
    "downloadId": "download-a450d4",
    "tabId": 12,
    "status": "downloading",
    "progress": 42.5,
    "speed": "8.4 MiB/s",
    "eta": 18,
    "filename": "Example video.mp4"
  }
}
```

Status is one of `queued`, `analyzing`, `downloading`, `merging`, `completed`, `failed`, or `cancelled`. Progress is a number from 0 through 100 when known.

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

The first implementation is a dedicated host process that resolves Parabolic's existing configuration, discovery and download services directly. Firefox starts it over standard input/output and keeps one port open for commands and events. The WinUI application does not have to be open and is not activated.

The host cancels its remaining downloads when Firefox closes the port. A later implementation may move active downloads to a separately persistent broker if downloads must survive a complete Firefox shutdown.
