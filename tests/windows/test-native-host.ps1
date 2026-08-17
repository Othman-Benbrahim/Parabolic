param(
    [Parameter(Mandatory = $true)]
    [string]$HostPath,

    [Parameter(Mandatory = $true)]
    [string]$ServicePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $HostPath -PathType Leaf)) {
    throw "Native host executable not found: $HostPath"
}
if (-not (Test-Path -LiteralPath $ServicePath -PathType Leaf)) {
    throw "Persistent download service executable not found: $ServicePath"
}

$resolvedServicePath = (Resolve-Path -LiteralPath $ServicePath).Path
$hostDirectory = Split-Path -Parent (Resolve-Path -LiteralPath $HostPath).Path
Copy-Item -Path "$(Split-Path -Parent $resolvedServicePath)\*" -Destination $hostDirectory -Recurse -Force

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = (Resolve-Path -LiteralPath $HostPath).Path
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
if (-not $process.Start()) {
    throw "Unable to start the Parabolic Native Messaging host."
}

try {
    $request = @{
        protocolVersion = 3
        requestId = "windows-smoke-test"
        type = "hello"
        payload = @{
            extensionId = "parabolic-media-detector@othmanbenbrahim.dev"
            extensionVersion = "0.8.0"
            protocolVersion = 3
        }
    } | ConvertTo-Json -Compress -Depth 5
    $payload = [System.Text.Encoding]::UTF8.GetBytes($request)
    $header = [System.BitConverter]::GetBytes([int]$payload.Length)
    $process.StandardInput.BaseStream.Write($header, 0, $header.Length)
    $process.StandardInput.BaseStream.Write($payload, 0, $payload.Length)
    $process.StandardInput.BaseStream.Flush()

    $responseHeader = [byte[]]::new(4)
    $headerRead = $process.StandardOutput.BaseStream.Read($responseHeader, 0, 4)
    if ($headerRead -ne 4) {
        throw "Native host returned an incomplete response header."
    }
    $responseLength = [System.BitConverter]::ToInt32($responseHeader, 0)
    if ($responseLength -le 0 -or $responseLength -gt 1048576) {
        throw "Native host returned an invalid response length: $responseLength"
    }
    $responseBytes = [byte[]]::new($responseLength)
    $offset = 0
    while ($offset -lt $responseLength) {
        $count = $process.StandardOutput.BaseStream.Read($responseBytes, $offset, $responseLength - $offset)
        if ($count -eq 0) {
            throw "Native host closed before returning the complete response."
        }
        $offset += $count
    }
    $response = [System.Text.Encoding]::UTF8.GetString($responseBytes) | ConvertFrom-Json
    if ($response.requestId -ne "windows-smoke-test" -or -not $response.ok) {
        throw "Native host hello request failed: $($response | ConvertTo-Json -Compress -Depth 5)"
    }
    $requiredCapabilities = @("formats", "download", "progress", "cancel", "open-folder", "ytdlp-update", "persistent-queue", "priority", "pause-resume", "list-downloads", "resolver-pipeline", "cobalt", "direct-media", "hls-dash", "n-m3u8dl-re", "permalink-first", "bandwidth-limit", "scheduling")
    $missingCapabilities = @($requiredCapabilities | Where-Object {
        $response.payload.capabilities -notcontains $_
    })
    if ($response.payload.protocolVersion -ne 3 -or $missingCapabilities.Count -gt 0) {
        throw "Native host returned incompatible capabilities."
    }
    Write-Host "Native Messaging host smoke test passed ($($response.payload.appVersion))."
}
finally {
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(10000)) {
        $process.Kill($true)
    }
    $process.Dispose()
    Get-Process -Name "Nickvision.Parabolic.DownloadService" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
