# Parabolic Firefox Native Host

`Nickvision.Parabolic.NativeHost` implements protocol version 3 from
`extension/firefox/NATIVE-MESSAGING-PROTOCOL.md` for Windows, native Linux and macOS.

It is a short-lived windowless relay launched by Firefox. It starts the
persistent per-user download service when needed, connects to the secured named
pipe on Windows or Unix domain socket on Linux/macOS, then copies Firefox
Native Messaging frames in both directions.

## Supported commands

- `hello`
- `get-formats`
- `download`
- `cancel`
- `pause`
- `resume`
- `set-priority`
- `list-downloads`
- `open-folder`
- `check-ytdlp-update`
- `update-ytdlp`

The persistent service emits progress and final state as asynchronous events.
The update commands remain explicit and are refused while downloads are active
or queued.

For automatic Firefox downloads, the service tries the durable page permalink
with yt-dlp first. If extraction fails and Firefox supplied a recent HLS/DASH
manifest, it starts the bundled N_m3u8DL-RE executable without requesting DRM
keys. Each native package supplies the architecture-matched executable.

## Publish and smoke test

```powershell
dotnet restore .\Nickvision.Parabolic.NativeHost --runtime win-x64
dotnet restore .\Nickvision.Parabolic.DownloadService --runtime win-x64
dotnet publish .\Nickvision.Parabolic.NativeHost -c Release --no-restore --runtime win-x64
dotnet publish .\Nickvision.Parabolic.DownloadService -c Release --no-restore --runtime win-x64
```

The Windows workflow publishes x64 and ARM64 installers. The native Linux
workflow publishes x64 and ARM64 tarballs with a per-user installer. The macOS
workflow publishes Intel and Apple Silicon ZIPs with a Firefox and LaunchAgent
integration command.

The installed host is the supported integration path. Every Native Messaging
manifest contains an absolute executable path, so moving an installed bundle
requires reinstalling its Firefox integration.
