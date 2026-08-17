# Build and release — Parabolic 2026.8.5 / Firefox 0.8.1

## GitHub Actions

1. Overlay this source archive on the repository root.
2. Commit and push to `main`; both the Windows and Firefox workflows run on push.
3. Wait for the Windows x64 job and Firefox add-on job to pass.
4. Download the Windows installer and `parabolic-download-manager-firefox` artifacts.
5. Install Parabolic 2026.8.5 before loading Firefox add-on 0.8.1.

The Windows workflow downloads the official N_m3u8DL-RE 0.6.0-beta executable matching the target architecture and includes its MIT license. The Firefox workflow packages only `extension/firefox`; Chrome and Edge are outside this release.

Create tag `2026.8.5` only after the Facebook and LinkedIn recovery paths have been validated. Use `RELEASE-NOTES-2026.8.5.md` as the release text.
