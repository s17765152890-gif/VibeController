$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectDotnet = Join-Path $repositoryRoot ".tools\dotnet\dotnet.exe"

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
    npm test -- --run
    if ($LASTEXITCODE -ne 0) { throw "Frontend tests failed" }
    npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw "Frontend typecheck failed" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed" }
}
finally {
    Pop-Location
}

& $dotnet test (Join-Path $repositoryRoot "VibeController.sln") --configuration Release
if ($LASTEXITCODE -ne 0) { throw ".NET tests failed" }
