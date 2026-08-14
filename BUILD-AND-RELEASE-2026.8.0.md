# Build and release Parabolic 2026.8.0

This adapted release contains two coordinated components:

- Parabolic for Windows `2026.8.0`, including the Firefox Native Messaging host;
- Parabolic Media Detector for Firefox `0.4.0`.

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
6. open the quality menu, select `Check and update yt-dlp`, and verify both the
   already-current and update-available paths;
7. close the Parabolic window and confirm that a new download starts without
   reopening it;
8. verify a failed or unsupported URL shows a safe user-facing error and offers
   the explicit yt-dlp update action.

## Automated Flatpak build

The `Flatpak` workflow uses the GNOME 50 build container required by the
manifest. A validation step first checks the runtime/container pairing, the
.NET 10 SDK extension, the x86_64 and aarch64 runtime mappings, and duplicate
offline NuGet/Python sources. The builder then produces
`org.nickvision.tubeconverter.flatpak` for both architectures.

If the validation step succeeds but the builder fails, capture the first error
inside the `flatpak-builder` step rather than the later cancelled matrix job.

## Release gate

Create the public GitHub release only when both workflows are green and the
manual Firefox/Windows matrix succeeds. Attach the signed installer artifacts,
publish the signed Firefox XPI separately through Mozilla Add-ons, and retain the
source commit used by both builds.
