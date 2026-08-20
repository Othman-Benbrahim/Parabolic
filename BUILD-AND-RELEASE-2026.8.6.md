# Build and release — Parabolic 2026.8.6 / Firefox 0.8.2

## GitHub Actions

Push the updated source to `main`. The following workflows generate the release files:

- `windows.yml`: Windows x64 and ARM64 installers and portable packages;
- `flatpak.yml`: Linux x86_64 and aarch64 Flatpak bundles plus the Firefox integration helper;
- `macos.yml`: Intel x64 and Apple Silicon ARM64 application ZIPs;
- `firefox.yml`: unsigned Firefox XPI for testing and Mozilla review.

The Flatpak workflow builds against GNOME 50 and packages the architecture-matched N_m3u8DL-RE binary. Windows and macOS continue to build on their target runners.

## PowerShell: commit and launch builds

```powershell
Set-Location "C:\Users\othma\Desktop\Parabolic-release"

git add --all
git commit -m "Migrate Linux distribution to Flatpak"
git push origin main

# A push to main already starts all four workflows. These commands are useful
# only to rerun them manually without a new commit.
gh workflow run windows.yml --ref main
gh workflow run flatpak.yml --ref main
gh workflow run macos.yml --ref main
gh workflow run firefox.yml --ref main

gh run list --branch main --limit 12
```

To watch one run, copy its numeric ID from `gh run list`:

```powershell
gh run watch RUN_ID --exit-status
```

Download all successful artifacts into one directory:

```powershell
New-Item -ItemType Directory -Force .\release-assets | Out-Null
gh run download WINDOWS_RUN_ID -D .\release-assets\windows
gh run download FLATPAK_RUN_ID -D .\release-assets\linux
gh run download MACOS_RUN_ID -D .\release-assets\macos
gh run download FIREFOX_RUN_ID -D .\release-assets\firefox
```

## Suggested release assets

- `Parabolic-2026.8.6-Windows-x64-Setup.exe`
- `Parabolic-2026.8.6-Windows-arm64-Setup.exe`
- `Parabolic-2026.8.6-x86_64.flatpak`
- `Parabolic-2026.8.6-aarch64.flatpak`
- `Parabolic-2026.8.6-firefox-flatpak-integration.tar.gz`
- `Parabolic-2026.8.6-macos-x64.zip`
- `Parabolic-2026.8.6-macos-arm64.zip`
- `parabolic-download-manager-0.8.2-unsigned.xpi`
- `parabolic-download-manager-0.8.2-amo.zip`

Create the tag and a draft release only after the workflows are green:

```powershell
git pull --ff-only origin main
git tag -a 2026.8.6 -m "Parabolic 2026.8.6"
git push origin 2026.8.6

gh release create 2026.8.6 `
  --title "Parabolic 2026.8.6" `
  --notes-file ".\RELEASE-NOTES-2026.8.6.md" `
  --verify-tag `
  --draft

gh release view 2026.8.6 --web
```

Attach the eight platform/add-on files to the draft, verify their architecture labels, then publish it as the latest release. Upload the AMO ZIP as version 0.8.2 of the existing Firefox listing, not as a new extension.
