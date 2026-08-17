# Build and release — Parabolic 2026.8.4 / Firefox 0.8.0

## GitHub Actions

1. Push the source to `main`.
2. Wait for the Windows and Firefox workflows to pass.
3. Download the Windows x64 or ARM64 installer and the unsigned Firefox artifact.
4. Validate Facebook and LinkedIn with `STEP-4-VALIDATION.md` before creating a public release.

The Windows workflow downloads the official N_m3u8DL-RE 0.6.0-beta asset matching the target architecture, extracts `N_m3u8DL-RE.exe`, and includes it in both installer and portable outputs. The upstream MIT license is included beside the executable.

Create tag `2026.8.4`, use `RELEASE-NOTES-2026.8.4.md` as the release text, and attach the successful Windows installer artifacts plus the Firefox package intended for Mozilla submission. Do not present the unsigned XPI as permanently installable.
