[CmdletBinding()]
param(
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-security-state.json'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdministrator) {
    throw 'This read-only audit requires an elevated PowerShell process.'
}

trap {
    $failureReport = [ordered]@{
        SchemaVersion = 1
        CapturedAtUtc = [DateTime]::UtcNow.ToString('o')
        IsAdministrator = $true
        FatalError = $_.Exception.Message
    }
    $failureDirectory = Split-Path -Parent $ReportPath
    if ($failureDirectory) {
        New-Item -ItemType Directory -Path $failureDirectory -Force | Out-Null
    }
    $failureReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Error $_.Exception.Message
    exit 1
}

function Get-BcdReadOnlyValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [AllowNull()]
        [string[]]$Lines,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $matchingLine = @($Lines | Where-Object { $_ -match "^\s*$([regex]::Escape($Name))\s+(.+?)\s*$" } | Select-Object -First 1)
    if ($matchingLine.Count -eq 0) {
        return 'NotPresent'
    }

    return ([regex]::Match($matchingLine[0], "^\s*$([regex]::Escape($Name))\s+(.+?)\s*$")).Groups[1].Value
}

$secureBoot = try {
    [pscustomobject]@{
        ReadSucceeded = $true
        Enabled = [bool](Confirm-SecureBootUEFI)
        Error = $null
    }
}
catch {
    [pscustomobject]@{
        ReadSucceeded = $false
        Enabled = $null
        Error = $_.Exception.Message
    }
}

$bitLocker = try {
    $volume = Get-BitLockerVolume -MountPoint $env:SystemDrive
    [pscustomobject]@{
        ReadSucceeded = $true
        MountPoint = [string]$volume.MountPoint
        VolumeStatus = [string]$volume.VolumeStatus
        ProtectionStatus = [string]$volume.ProtectionStatus
        EncryptionMethod = [string]$volume.EncryptionMethod
        EncryptionPercentage = [int]$volume.EncryptionPercentage
        AutoUnlockEnabled = [bool]$volume.AutoUnlockEnabled
        Error = $null
    }
}
catch {
    [pscustomobject]@{
        ReadSucceeded = $false
        MountPoint = $env:SystemDrive
        VolumeStatus = $null
        ProtectionStatus = $null
        EncryptionMethod = $null
        EncryptionPercentage = $null
        AutoUnlockEnabled = $null
        Error = $_.Exception.Message
    }
}

$bcdLines = @(& bcdedit.exe /enum '{current}' 2>&1)
$bcdExitCode = $LASTEXITCODE
$bcd = [pscustomobject]@{
    ReadSucceeded = ($bcdExitCode -eq 0)
    ExitCode = $bcdExitCode
    TestSigning = Get-BcdReadOnlyValue -Lines $bcdLines -Name 'testsigning'
    NoIntegrityChecks = Get-BcdReadOnlyValue -Lines $bcdLines -Name 'nointegritychecks'
    Debug = Get-BcdReadOnlyValue -Lines $bcdLines -Name 'debug'
    HypervisorLaunchType = Get-BcdReadOnlyValue -Lines $bcdLines -Name 'hypervisorlaunchtype'
}

$deviceGuard = try {
    $state = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace 'root\Microsoft\Windows\DeviceGuard'
    [pscustomobject]@{
        ReadSucceeded = $true
        VirtualizationBasedSecurityStatus = [int]$state.VirtualizationBasedSecurityStatus
        SecurityServicesConfigured = @($state.SecurityServicesConfigured)
        SecurityServicesRunning = @($state.SecurityServicesRunning)
        CodeIntegrityPolicyEnforcementStatus = [int]$state.CodeIntegrityPolicyEnforcementStatus
        UsermodeCodeIntegrityPolicyEnforcementStatus = [int]$state.UsermodeCodeIntegrityPolicyEnforcementStatus
        Error = $null
    }
}
catch {
    [pscustomobject]@{
        ReadSucceeded = $false
        VirtualizationBasedSecurityStatus = $null
        SecurityServicesConfigured = @()
        SecurityServicesRunning = @()
        CodeIntegrityPolicyEnforcementStatus = $null
        UsermodeCodeIntegrityPolicyEnforcementStatus = $null
        Error = $_.Exception.Message
    }
}

$report = [ordered]@{
    SchemaVersion = 1
    CapturedAtUtc = [DateTime]::UtcNow.ToString('o')
    IsAdministrator = $isAdministrator
    Windows = [ordered]@{
        ProductName = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').ProductName
        DisplayVersion = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').DisplayVersion
        CurrentBuild = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').CurrentBuild
        Ubr = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').UBR
    }
    SecureBoot = $secureBoot
    BitLocker = $bitLocker
    BootConfiguration = $bcd
    DeviceGuard = $deviceGuard
}

$reportDirectory = Split-Path -Parent $ReportPath
if ($reportDirectory) {
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8

Write-Host 'RC901A read-only security audit complete.' -ForegroundColor Green
Write-Host "Report: $ReportPath"
Write-Host 'No boot, certificate, driver, or device setting was changed.'
$report
