# Build and release Parabolic 2026.8.1

This is milestone 1 of the validated download-manager architecture. It adds the
persistent Windows engine and Firefox protocol v2 while keeping yt-dlp, aria2c
and FFmpeg as the existing execution pipeline.

## GitHub Actions

Pushing this source to `main` starts the Windows, Flatpak, macOS, Firefox and
spelling workflows. The Windows workflow now publishes three coordinated
projects for x64 and ARM64:

- `Nickvision.Parabolic.WinUI`;
- `Nickvision.Parabolic.NativeHost`, the short-lived Firefox relay;
- `Nickvision.Parabolic.DownloadService`, the persistent per-user engine.

The installer includes all three components, registers Native Messaging,
registers the service at user logon, and starts it after a normal interactive
installation. The portable package contains the service but still does not
register Firefox Native Messaging automatically.

The Firefox workflow validates protocol v2 and produces the artifact
`parabolic-download-manager-firefox`, containing the unsigned 0.5.0 XPI.

## Required verification

After the x64 Windows workflow succeeds:

1. install `NickvisionParabolicSetup.exe` over version 2026.8.0;
2. load the unsigned 0.5.0 XPI temporarily in Firefox;
3. confirm the popup reports protocol v2 and the persistent queue capability;
4. start a large download, close Firefox completely, and confirm the file keeps growing;
5. reopen Firefox and confirm the bridge reconnects;
6. interrupt the service or restart Windows, then verify the partial download resumes;
7. queue at least three downloads with different default priorities and confirm High starts before Normal and Low;
8. test audio, video, exact formats, cancellation, and open-folder;
9. confirm the Windows x64 and ARM64 matrix jobs are green.

## Compatibility

Firefox add-on 0.5.0 requires desktop version 2026.8.1 because Native Messaging
protocol v2 is intentionally incompatible with the old process-owned protocol.
The 2026.8.0 add-on and installer should be replaced together.

## Later milestones

This release does not yet add Cobalt, direct fragment recovery, DNS/CDN retry,
scheduled downloads, or the daemon queue inside the WinUI list. Those remain
separate milestones so the persistent lifecycle can be tested first.
