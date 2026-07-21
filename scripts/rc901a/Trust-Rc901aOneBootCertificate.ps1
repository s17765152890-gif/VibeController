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

function New-Rc901aOneBootTrustPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,
        [switch]$Apply
    )

    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        SessionPath = $SessionPath
        TrustStores = @('Root', 'TrustedPublisher')
        BcdAction = 'None'
        SecureBootAction = 'None'
        DriverAction = 'None'
        PlannedAction = 'Trust only the temporary certificate recorded by the RC901A test session.'
        RestoreScript = Join-Path $PSScriptRoot 'Restore-Rc901aTestMode.ps1'
    }
}

function Assert-Rc901aOneBootAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Trusting the temporary RC901A certificate requires an elevated PowerShell process.'
    }
}

function Remove-Rc901aOneBootTrustCopies {
    [CmdletBinding()]
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

function Set-Rc901aOneBootSessionProperty {
    [CmdletBinding()]
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

function Invoke-Rc901aOneBootTrust {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$SessionPath,
        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    $plan = New-Rc901aOneBootTrustPlan -SessionPath $SessionPath -Apply:$effectiveApply
    if (-not $effectiveApply) {
        return $plan
    }

    Assert-Rc901aOneBootAdministrator
    $resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
    $session = Get-Content -LiteralPath $resolvedSessionPath -Raw | ConvertFrom-Json
    if ($session.CertificateSubject -ne 'CN=VibeController RC901A Temporary Driver Test' -or
        $session.CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The test-session certificate identity is invalid.'
    }
    if ($session.PSObject.Properties.Name -notcontains 'InitialTestSigning' -or
        $null -ne $session.InitialTestSigning) {
        throw 'This session is not eligible for the one-boot route because persistent test mode was already prepared.'
    }
    foreach ($requiredPath in @($session.CertificatePath, $session.DriverPath, $session.CatalogPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "The recorded temporary file is missing: $requiredPath"
        }
    }

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($session.CertificatePath)
    if ($certificate.Thumbprint -ne $session.CertificateThumbprint -or
        $certificate.Subject -ne $session.CertificateSubject) {
        throw 'The exported temporary certificate does not match the session record.'
    }
    if (-not $PSCmdlet.ShouldProcess($session.CertificateThumbprint, 'Trust only the recorded RC901A temporary certificate')) {
        return $plan
    }

    try {
        Import-Certificate -FilePath $session.CertificatePath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
        Import-Certificate -FilePath $session.CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null

        foreach ($signedPath in @($session.DriverPath, $session.CatalogPath)) {
            $signature = Get-AuthenticodeSignature -LiteralPath $signedPath
            if ($signature.Status -ne 'Valid' -or
                $null -eq $signature.SignerCertificate -or
                $signature.SignerCertificate.Thumbprint -ne $session.CertificateThumbprint) {
                throw "The recorded signature is not valid after trust was added: $signedPath"
            }
        }

        Set-Rc901aOneBootSessionProperty `
            -Session $session `
            -Name OneBootTrustAddedAtUtc `
            -Value ([DateTime]::UtcNow.ToString('o'))
        $session | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedSessionPath -Encoding UTF8
        return $plan
    }
    catch {
        Remove-Rc901aOneBootTrustCopies -Thumbprint $session.CertificateThumbprint
        throw
    }
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aOneBootTrust -SessionPath $SessionPath -Apply:$Apply -WhatIf:$entryWhatIf
}
