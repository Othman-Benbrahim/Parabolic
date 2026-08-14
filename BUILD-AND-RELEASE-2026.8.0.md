# Build and release Parabolic 2026.8.0

This adapted release contains two coordinated components:

- Parabolic for Windows `2026.8.0`, including the Firefox Native Messaging host;
- Parabolic Media Detector for Firefox `0.3.0`.

## Automated Windows build

The recommended build path is the `Windows` GitHub Actions workflow. For each
supported architecture it:

1. restores and publishes the WinUI application;
2. restores and publishes `Nickvision.Parabolic.NativeHost`;
3. runs the framed `hello` protocol smoke test against the native executable;
4. includes the host in the Inno Setup installer;
5. creates x64 or ARM64 installer and portable artifacts.

Run it from **Actions → Windows → Run workflow**, or push the reviewed commit to
`main`. Download these artifacts after both matrix jobs succeed:

- `NickvisionParabolicSetup-x64`
- `NickvisionParabolicSetup-arm64`
- `Nickvision.Parabolic.WinUI-portable-x64`
- `Nickvision.Parabolic.WinUI-portable-arm64`

The installer is the supported Firefox integration package because it writes the
absolute native-host manifest and the required Mozilla registry entries. The
portable archive contains the executable but does not register it.

## Automated Firefox build

The `Firefox Add-on` workflow validates the add-on, runs the JavaScript protocol
smoke test and produces:

- `parabolic-media-detector-firefox`

Its XPI is unsigned and intended for temporary testing or Mozilla Add-ons
submission. Regular Firefox installations require Mozilla signing.

## Manual Windows verification

After installing the x64 or ARM64 setup package:

1. confirm that `Nickvision.Parabolic.NativeHost.exe` and
   `com.nickvision.parabolic.json` exist in the installed `Release` directory;
2. load the Firefox add-on temporarily from `about:debugging`;
3. open its toolbar diagnostics and confirm `App ready`;
4. test `Best quality`, one capped preset and `Audio only`;
5. test exact format loading, progress, cancellation and `Open folder`;
6. close the Parabolic window and confirm that a new download starts without
   reopening it;
7. verify a failed or unsupported URL shows a safe user-facing error.

## Release gate

Create the public GitHub release only when both workflows are green and the
manual Firefox/Windows matrix succeeds. Attach the signed installer artifacts,
publish the signed Firefox XPI separately through Mozilla Add-ons, and retain the
source commit used by both builds.
