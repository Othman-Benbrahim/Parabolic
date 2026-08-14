# Parabolic Firefox Native Host

`Nickvision.Parabolic.NativeHost` implements protocol version 1 from
`extension/firefox/NATIVE-MESSAGING-PROTOCOL.md` for Windows.

It is a windowless executable launched by Firefox. The host reuses Parabolic's
configuration, yt-dlp discovery and download services, so downloads use the same
save folder and media preferences as the desktop application.

## Supported commands

- `hello`
- `get-formats`
- `download`
- `cancel`
- `open-folder`

Download progress and final state are emitted as asynchronous events.

## Publish and smoke test

```powershell
dotnet restore .\Nickvision.Parabolic.NativeHost --runtime win-x64
dotnet publish .\Nickvision.Parabolic.NativeHost -c Release --no-restore --runtime win-x64
.\tests\windows\test-native-host.ps1 -HostPath .\Nickvision.Parabolic.NativeHost\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\Nickvision.Parabolic.NativeHost.exe
```

The normal Windows workflow publishes this project for x64 and ARM64 before
building the installer. Inno Setup creates the absolute-path native-host
manifest and registers it in both Firefox registry views.

The installed host is the supported integration path. A portable archive alone
does not register a Native Messaging manifest.
