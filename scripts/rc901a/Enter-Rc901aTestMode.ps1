[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$SessionPath,
    [switch]$Apply,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SessionPath)) {
    $SessionPath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-test-session.json'
}

function New-Rc901aTestModeEntryPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,

        [Parameter(Mandatory)]
        [bool]$SecureBootEnabled,

        [switch]$Apply
    )

    if ($Apply -and $SecureBootEnabled) {
        throw 'Refusing to enter RC901A test mode while Secure Boot is enabled.'
    }

    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        SessionPath = $SessionPath
        SecureBootEnabled = $SecureBootEnabled
        TrustStores = @('Root', 'TrustedPublisher')
        BcdAction = 'Set TESTSIGNING ON for {current}'
        RestartRequired = $true
        RestoreScript = Join-Path $PSScriptRoot 'Restore-Rc901aTestMode.ps1'
    }
}

function Assert-Rc901aAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This operation requires an elevated PowerShell process.'
    }
}

function Get-Rc901aCurrentTestSigningState {
    $lines = @(& bcdedit.exe /enum '{current}' 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read the current BCD entry (exit code $LASTEXITCODE)."
    }
    $line = @($lines | Where-Object { $_ -match '^\s*testsigning\s+(.+?)\s*$' } | Select-Object -First 1)
    if ($line.Count -eq 0) {
        return 'NotPresent'
    }
    return ([regex]::Match($line[0], '^\s*testsigning\s+(.+?)\s*$')).Groups[1].Value
}

function Set-Rc901aSessionProperty {
    param(
        [Parameter(Mandatory)]
        [psobject]$Session,
        [Parameter(Mandatory)]
        [string]$Name,
        $Value
    )
    if ($Session.PSObject.Properties.Name -contains $Name) {
        $Session.$Name = $Value
    }
    else {
        $Session | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Remove-Rc901aTrustedCertificateCopies {
    param(
        [Parameter(Mandatory)]
        [string]$Thumbprint
    )
    foreach ($store in @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher')) {
        $path = Join-Path $store $Thumbprint
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

function Invoke-Rc901aTestModeEntry {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,
        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    if (-not $effectiveApply) {
        return New-Rc901aTestModeEntryPlan -SessionPath $SessionPath -SecureBootEnabled $true
    }

    Assert-Rc901aAdministrator
    $secureBootEnabled = [bool](Confirm-SecureBootUEFI)
    $plan = New-Rc901aTestModeEntryPlan `
        -SessionPath $SessionPath `
        -SecureBootEnabled $secureBootEnabled `
        -Apply

    $resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
    $session = Get-Content -LiteralPath $resolvedSessionPath -Raw | ConvertFrom-Json
    if ($session.CertificateSubject -ne 'CN=VibeController RC901A Temporary Driver Test' -or
        $session.CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The test-session certificate identity is invalid.'
    }
    if (-not (Test-Path -LiteralPath $session.CertificatePath) -or
        -not (Test-Path -LiteralPath $session.CatalogPath)) {
        throw 'The temporary certificate or catalog is missing.'
    }

    $initialTestSigning = Get-Rc901aCurrentTestSigningState
    if ($initialTestSigning -ne 'NotPresent' -and $initialTestSigning -notmatch '^(?i:no|off|false)$') {
        throw "Refusing to overwrite the existing TESTSIGNING state '$initialTestSigning'."
    }
    Set-Rc901aSessionProperty -Session $session -Name InitialTestSigning -Value $initialTestSigning
    Set-Rc901aSessionProperty -Session $session -Name EntryStartedAtUtc -Value ([DateTime]::UtcNow.ToString('o'))
    $session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedSessionPath -Encoding UTF8

    if (-not $PSCmdlet.ShouldProcess('{current}', 'Trust the recorded temporary certificate and enable TESTSIGNING')) {
        return $plan
    }

    try {
        Import-Certificate -FilePath $session.CertificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
        Import-Certificate -FilePath $session.CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null

        $catalogSignature = Get-AuthenticodeSignature -LiteralPath $session.CatalogPath
        if ($catalogSignature.Status -ne 'Valid' -or
            $catalogSignature.SignerCertificate.Thumbprint -ne $session.CertificateThumbprint) {
            throw "The temporary catalog is not valid after certificate import (status $($catalogSignature.Status))."
        }

        $bcdOutput = @(& bcdedit.exe /set '{current}' testsigning on 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "BCDEdit failed with exit code $LASTEXITCODE.`n$($bcdOutput -join [Environment]::NewLine)"
        }
        Set-Rc901aSessionProperty -Session $session -Name EnteredAtUtc -Value ([DateTime]::UtcNow.ToString('o'))
        $session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedSessionPath -Encoding UTF8
        return $plan
    }
    catch {
        Remove-Rc901aTrustedCertificateCopies -Thumbprint $session.CertificateThumbprint
        throw
    }
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aTestModeEntry -SessionPath $SessionPath -Apply:$Apply -WhatIf:$entryWhatIf
}
