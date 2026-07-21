[CmdletBinding()]
param(
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-security-state.json'
}

$auditScript = Join-Path $PSScriptRoot 'Get-Rc901aSecurityState.ps1'
$escapedAuditScript = $auditScript.Replace("'", "''")
$escapedReportPath = $ReportPath.Replace("'", "''")
$elevatedCommand = "& '$escapedAuditScript' -ReportPath '$escapedReportPath' | Out-Null"
$encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($elevatedCommand))

Write-Host 'Windows will request administrator approval for a read-only audit.' -ForegroundColor Cyan
$process = Start-Process `
    -FilePath 'powershell.exe' `
    -Verb RunAs `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encodedCommand) `
    -Wait `
    -PassThru

if ($process.ExitCode -ne 0) {
    throw "The elevated audit exited with code $($process.ExitCode)."
}
if (-not (Test-Path -LiteralPath $ReportPath)) {
    throw 'The elevated audit finished without creating its report.'
}

$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
if ($report.PSObject.Properties.Name -contains 'FatalError') {
    throw "The elevated audit reported: $($report.FatalError)"
}

Write-Host 'Read-only audit complete. No security setting was changed.' -ForegroundColor Green
Write-Host "Report: $ReportPath"
$report
