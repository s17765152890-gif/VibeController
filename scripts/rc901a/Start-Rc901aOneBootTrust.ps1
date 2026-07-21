[CmdletBinding()]
param(
    [string]$SessionPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SessionPath)) {
    $SessionPath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-test-session.json'
}

$trustScript = Join-Path $PSScriptRoot 'Trust-Rc901aOneBootCertificate.ps1'
$resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
$escapedTrustScript = $trustScript.Replace("'", "''")
$escapedSessionPath = $resolvedSessionPath.Replace("'", "''")
$elevatedCommand = "& '$escapedTrustScript' -SessionPath '$escapedSessionPath' -Apply -Confirm:`$false | Out-Null"
$encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($elevatedCommand))

Write-Host 'Windows will request approval to trust one temporary RC901A test certificate.' -ForegroundColor Cyan
$process = Start-Process `
    -FilePath 'powershell.exe' `
    -Verb RunAs `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encodedCommand) `
    -Wait `
    -PassThru

if ($process.ExitCode -ne 0) {
    throw "The elevated one-boot trust step exited with code $($process.ExitCode)."
}

$session = Get-Content -LiteralPath $resolvedSessionPath -Raw | ConvertFrom-Json
if ($session.PSObject.Properties.Name -notcontains 'OneBootTrustAddedAtUtc' -or
    [string]::IsNullOrWhiteSpace([string]$session.OneBootTrustAddedAtUtc)) {
    throw 'The one-boot trust step finished without updating the session record.'
}
foreach ($store in @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher')) {
    if (-not (Test-Path -LiteralPath (Join-Path $store $session.CertificateThumbprint))) {
        throw "The recorded temporary certificate is missing from $store."
    }
}

Write-Host 'Temporary RC901A certificate trust is ready for the one-boot capture session.' -ForegroundColor Green
Write-Host 'No boot setting, device driver, or restart state was changed.'
$session
