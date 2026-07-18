param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\release"))
$packageName = "VibeController-v$Version-win-x64"

function Assert-PathWithinReleaseRoot([string]$CandidatePath) {
    $fullCandidate = [System.IO.Path]::GetFullPath($CandidatePath)
    $rootWithSeparator = $releaseRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullCandidate.StartsWith(
            $rootWithSeparator,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the release directory: $fullCandidate"
    }

    return $fullCandidate
}

$publishDirectory = Assert-PathWithinReleaseRoot (Join-Path $releaseRoot ".publish-$Version")
$stagingDirectory = Assert-PathWithinReleaseRoot (Join-Path $releaseRoot $packageName)
$archivePath = Assert-PathWithinReleaseRoot (Join-Path $releaseRoot "$packageName.zip")
$checksumPath = Assert-PathWithinReleaseRoot "$archivePath.sha256"

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

foreach ($directory in @($publishDirectory, $stagingDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

foreach ($file in @($archivePath, $checksumPath)) {
    if (Test-Path -LiteralPath $file) {
        Remove-Item -LiteralPath $file -Force
    }
}

& (Join-Path $PSScriptRoot "build.ps1") -PublishDirectory $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Release publish failed"
}

$publishedExecutable = Join-Path $publishDirectory "VibeController.App.exe"
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publish output is incomplete: $publishedExecutable"
}

New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stagingDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination (Join-Path $stagingDirectory "LICENSE")
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs\release\QUICKSTART.md") -Destination (Join-Path $stagingDirectory "README.md")

Compress-Archive -LiteralPath $stagingDirectory -DestinationPath $archivePath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
$checksumContents = "$hash *$([System.IO.Path]::GetFileName($archivePath))`n"
[System.IO.File]::WriteAllText(
    $checksumPath,
    $checksumContents,
    [System.Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot "verify-release.ps1") `
    -Version $Version `
    -ReleaseDirectory $releaseRoot
if ($LASTEXITCODE -ne 0) {
    throw "Release verification failed"
}

Write-Host "Release package: $archivePath"
Write-Host "Checksum file: $checksumPath"
