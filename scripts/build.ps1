param(
    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectDotnet = Join-Path $repositoryRoot ".tools\dotnet\dotnet.exe"
if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $repositoryRoot "artifacts\win-x64"
}
$publishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)

if (Test-Path -LiteralPath $projectDotnet) {
    $dotnet = $projectDotnet
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) {
        throw ".NET SDK not found. Install the SDK selected by global.json or place it at .tools\dotnet\dotnet.exe."
    }

    $dotnet = $dotnetCommand.Source
}

Push-Location (Join-Path $repositoryRoot "frontend")
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed" }
}
finally {
    Pop-Location
}

& $dotnet publish (Join-Path $repositoryRoot "src\VibeController.App\VibeController.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed" }

Write-Host "Publish completed: $publishDirectory"
