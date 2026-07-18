param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "1.0.0",
    [string]$ReleaseDirectory
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = Join-Path $repositoryRoot "artifacts\release"
}

$archiveName = "VibeController-v$Version-win-x64.zip"
$archivePath = Join-Path $ReleaseDirectory $archiveName
$checksumPath = "$archivePath.sha256"

if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw "Release ZIP not found: $archivePath"
}

if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
    throw "Release checksum not found: $checksumPath"
}

$checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
if ($checksumText -notmatch "^([A-Fa-f0-9]{64})\s+\*?$([regex]::Escape($archiveName))$") {
    throw "Release checksum file has an invalid format: $checksumPath"
}

$expectedHash = $Matches[1].ToUpperInvariant()
$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    throw "Release checksum mismatch. Expected $expectedHash, got $actualHash."
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "VibeController-release-$([guid]::NewGuid().ToString('N'))"
$extractedRoot = Join-Path $temporaryRoot "VibeController-v$Version-win-x64"

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryRoot

    $requiredPaths = @(
        "VibeController.App.exe",
        "VibeController.App.runtimeconfig.json",
        "WebView2Loader.dll",
        "wwwroot\index.html",
        "README.md",
        "LICENSE"
    )

    foreach ($relativePath in $requiredPaths) {
        $candidate = Join-Path $extractedRoot $relativePath
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Release is missing required file: $relativePath"
        }
    }

    $fileVersion = (Get-Item -LiteralPath (Join-Path $extractedRoot "VibeController.App.exe")).VersionInfo.FileVersion
    if (-not $fileVersion.StartsWith("$Version.")) {
        throw "Executable version '$fileVersion' does not match release version '$Version'."
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Release verified: $archivePath"
Write-Host "SHA-256: $actualHash"
