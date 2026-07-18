param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "1.1.0",
    [string]$ReleaseDirectory
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) {
    $ReleaseDirectory = Join-Path $repositoryRoot "artifacts\release"
}

$archivePath = Join-Path $ReleaseDirectory "VibeController-v$Version-win-x64.zip"
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw "Release ZIP not found: $archivePath"
}

$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryRoot = Join-Path $temporaryBase "VibeController-smoke-$([guid]::NewGuid().ToString('N'))"
$temporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
if (-not $temporaryRoot.StartsWith(
        $temporaryBase,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use an unsafe smoke-test directory: $temporaryRoot"
}

$process = $null
$executablePath = $null
try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryRoot
    $executablePath = Join-Path $temporaryRoot "VibeController-v$Version-win-x64\VibeController.App.exe"

    $process = Start-Process `
        -FilePath $executablePath `
        -WorkingDirectory (Split-Path -Parent $executablePath) `
        -WindowStyle Hidden `
        -PassThru

    $deadline = (Get-Date).AddSeconds(25)
    $ready = $false
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            throw "Smoke-test process exited early with code $($process.ExitCode)."
        }

        if ($process.Responding -and
            $process.MainWindowHandle -ne 0 -and
            $process.MainWindowTitle -eq "VibeController") {
            $ready = $true
            break
        }
    } while ((Get-Date) -lt $deadline)

    if (-not $ready) {
        throw "VibeController did not expose a responsive main window within 25 seconds."
    }

    Write-Host "Release smoke test passed: PID $($process.Id), title '$($process.MainWindowTitle)'"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $runningProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)"
        if ($null -ne $runningProcess -and
            [string]::Equals(
                $runningProcess.ExecutablePath,
                $executablePath,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $process.Id -Force
            Wait-Process -Id $process.Id -ErrorAction SilentlyContinue
        }
    }

    if (Test-Path -LiteralPath $temporaryRoot) {
        for ($attempt = 1; $attempt -le 10; $attempt++) {
            try {
                Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
                break
            }
            catch {
                if ($attempt -eq 10) {
                    throw
                }

                Start-Sleep -Milliseconds 300
            }
        }
    }
}
