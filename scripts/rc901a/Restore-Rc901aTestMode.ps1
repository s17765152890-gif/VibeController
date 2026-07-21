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

function New-Rc901aTestModeRestorePlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,

        [Parameter(Mandatory)]
        [string]$CertificateThumbprint,

        [Parameter(Mandatory)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$InitialTestSigning,

        [switch]$Apply
    )

    $normalizedTestSigning = if ([string]::IsNullOrWhiteSpace($InitialTestSigning)) { 'NotEntered' } else { $InitialTestSigning }
    $bcdAction = switch -Regex ($normalizedTestSigning) {
        '^NotEntered$' { 'No BCD change; test mode was not entered'; break }
        '^NotPresent$' { 'Delete TESTSIGNING from {current}'; break }
        '^(?i:no|off|false)$' { 'Set TESTSIGNING OFF for {current}'; break }
        '^(?i:yes|on|true)$' { 'Set TESTSIGNING ON for {current}'; break }
        default { throw "Unsupported initial TESTSIGNING state '$InitialTestSigning'." }
    }

    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        SessionPath = $SessionPath
        CertificateThumbprint = $CertificateThumbprint
        BcdAction = $bcdAction
        CertificateStores = @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher', 'Cert:\CurrentUser\My')
        RequiresDriverUninstalled = $true
        RestartRequired = ($normalizedTestSigning -ne 'NotEntered')
        ManualFinalStep = 'Re-enable Secure Boot in UEFI firmware after TESTSIGNING is restored.'
    }
}

function Assert-Rc901aRestoreAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This restore operation requires an elevated PowerShell process.'
    }
}

function Assert-Rc901aCaptureDriverRemoved {
    $output = @(& pnputil.exe /enum-drivers /format xml 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate installed driver packages (exit code $LASTEXITCODE)."
    }
    if (($output -join [Environment]::NewLine) -match '(?i)rc901ahidfilter\.inf') {
        throw 'Uninstall the RC901A capture filter before restoring test mode and certificate trust.'
    }
}

function Remove-Rc901aRecordedCertificate {
    param(
        [Parameter(Mandatory)]
        [string]$Thumbprint,
        [Parameter(Mandatory)]
        [string]$ExpectedSubject
    )
    foreach ($store in @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher', 'Cert:\CurrentUser\My')) {
        $path = Join-Path $store $Thumbprint
        if (Test-Path -LiteralPath $path) {
            $certificate = Get-Item -LiteralPath $path
            if ($certificate.Subject -ne $ExpectedSubject) {
                throw "Refusing to remove certificate $Thumbprint from $store because its subject does not match."
            }
            Remove-Item -LiteralPath $path -Force
        }
    }
}

function Invoke-Rc901aTestModeRestore {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,
        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    if (-not (Test-Path -LiteralPath $SessionPath)) {
        if ($effectiveApply) {
            throw 'The RC901A test-session record is missing.'
        }
        return New-Rc901aTestModeRestorePlan `
            -SessionPath $SessionPath `
            -CertificateThumbprint 'FROM_SESSION_RECORD' `
            -InitialTestSigning 'NotPresent'
    }

    $resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath).Path
    $session = Get-Content -LiteralPath $resolvedSessionPath -Raw | ConvertFrom-Json
    if ($session.CertificateSubject -ne 'CN=VibeController RC901A Temporary Driver Test' -or
        $session.CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The test-session certificate identity is invalid.'
    }
    $plan = New-Rc901aTestModeRestorePlan `
        -SessionPath $resolvedSessionPath `
        -CertificateThumbprint $session.CertificateThumbprint `
        -InitialTestSigning $session.InitialTestSigning `
        -Apply:$effectiveApply
    if (-not $effectiveApply) {
        return $plan
    }

    Assert-Rc901aRestoreAdministrator
    Assert-Rc901aCaptureDriverRemoved
    if (-not $PSCmdlet.ShouldProcess('{current}', 'Restore TESTSIGNING and remove only the recorded RC901A test certificate')) {
        return $plan
    }

    $normalizedTestSigning = if ([string]::IsNullOrWhiteSpace([string]$session.InitialTestSigning)) { 'NotEntered' } else { [string]$session.InitialTestSigning }
    switch -Regex ($normalizedTestSigning) {
        '^NotEntered$' { $bcdArguments = $null; break }
        '^NotPresent$' { $bcdArguments = @('/deletevalue', '{current}', 'testsigning'); break }
        '^(?i:no|off|false)$' { $bcdArguments = @('/set', '{current}', 'testsigning', 'off'); break }
        '^(?i:yes|on|true)$' { $bcdArguments = @('/set', '{current}', 'testsigning', 'on'); break }
        default { throw "Unsupported initial TESTSIGNING state '$($session.InitialTestSigning)'." }
    }
    if ($null -ne $bcdArguments) {
        $bcdOutput = @(& bcdedit.exe @bcdArguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "BCDEdit restore failed with exit code $LASTEXITCODE.`n$($bcdOutput -join [Environment]::NewLine)"
        }
    }

    Remove-Rc901aRecordedCertificate `
        -Thumbprint $session.CertificateThumbprint `
        -ExpectedSubject $session.CertificateSubject
    if ($session.PSObject.Properties.Name -contains 'RestoredAtUtc') {
        $session.RestoredAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    else {
        $session | Add-Member -NotePropertyName RestoredAtUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o'))
    }
    $session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedSessionPath -Encoding UTF8
    return $plan
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aTestModeRestore -SessionPath $SessionPath -Apply:$Apply -WhatIf:$entryWhatIf
}
