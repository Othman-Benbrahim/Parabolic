$ErrorActionPreference = "Stop"

$sourceDirectory = $PSScriptRoot
$repositoryDirectory = (Resolve-Path (Join-Path $sourceDirectory "..\..")).Path
$outputDirectory = Join-Path $repositoryDirectory "dist"
$manifest = Get-Content (Join-Path $sourceDirectory "manifest.json") -Raw | ConvertFrom-Json
$packageBaseName = "parabolic-media-detector-$($manifest.version)-unsigned"
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "parabolic-firefox-addon-$PID"
$temporaryZip = Join-Path $outputDirectory "$packageBaseName.zip"
$outputXpi = Join-Path $outputDirectory "$packageBaseName.xpi"

$files = @(
    "manifest.json",
    "background.js",
    "utils.js",
    "media-detector.js",
    "media-overlay.js",
    "popup.html",
    "popup.css",
    "popup.js",
    "options.html",
    "options.css",
    "options.js"
)

try {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null

    foreach ($file in $files) {
        Copy-Item (Join-Path $sourceDirectory $file) (Join-Path $temporaryDirectory $file)
    }
    Copy-Item (Join-Path $sourceDirectory "icons") (Join-Path $temporaryDirectory "icons") -Recurse

    if (Test-Path $temporaryZip) {
        Remove-Item $temporaryZip -Force
    }
    if (Test-Path $outputXpi) {
        Remove-Item $outputXpi -Force
    }

    Compress-Archive -Path (Join-Path $temporaryDirectory "*") -DestinationPath $temporaryZip
    Move-Item $temporaryZip $outputXpi
    Write-Host "Firefox add-on created: $outputXpi"
}
finally {
    if (Test-Path $temporaryDirectory) {
        Remove-Item $temporaryDirectory -Recurse -Force
    }
}
