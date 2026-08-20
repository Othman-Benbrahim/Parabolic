param(
    [string]$Repository = "C:\Users\othma\Desktop\Parabolic-release",
    [string]$Branch = "main",
    [switch]$CommitAndPush
)

$ErrorActionPreference = "Stop"
Set-Location $Repository

if ($CommitAndPush) {
    git add --all
    git commit -m "Add native Linux and macOS Firefox integration"
    git push origin $Branch
    Write-Host "The push started the workflows. Manual duplicate runs are not needed."
}
else {
    gh workflow run windows.yml --ref $Branch
    gh workflow run linux-native.yml --ref $Branch
    gh workflow run macos.yml --ref $Branch
    gh workflow run firefox.yml --ref $Branch
}

Start-Sleep -Seconds 3
gh run list --branch $Branch --limit 12

Write-Host "Use: gh run watch RUN_ID --exit-status"
Write-Host "Then: gh run download RUN_ID -D .\release-assets"
