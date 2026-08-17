# Build and final release — Parabolic 2026.8.3 / Firefox 0.7.0

Do not create the GitHub Release until the step 3 validation checklist passes.

## Push the step 3 source

Overlay the supplied source ZIP on the existing repository without removing `.git`, then run:

```powershell
Set-Location "C:\Users\othma\Desktop\Parabolic-release"

git status --short
git add -A
git commit -m "feat: complete Firefox download manager step 3"
git push origin main
```

## Validate GitHub Actions artifacts

1. Wait for the Windows and Firefox Add-on workflows to succeed.
2. Download `NickvisionParabolicSetup-x64` for the current PC.
3. Download the unsigned Firefox 0.7.0 artifact.
4. Close Firefox and stop any old service before installing:

```powershell
Get-Process "Nickvision.Parabolic.DownloadService" -ErrorAction SilentlyContinue |
    Stop-Process -Force
```

5. Install Parabolic 2026.8.3 over the current build.
6. Load Firefox 0.7.0 temporarily through `about:debugging#/runtime/this-firefox`.
7. Complete every check in `STEP-3-VALIDATION.md`.

## Publish after validation

Create tag `2026.8.3`, use `RELEASE-NOTES-2026.8.3.md` as the release text, and attach the successful Windows installer artifacts plus the Firefox package intended for Mozilla submission. Do not present the unsigned XPI as permanently installable.
