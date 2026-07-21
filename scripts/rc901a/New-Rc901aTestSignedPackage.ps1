[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$SourcePackageDirectory,
    [string]$OutputDirectory,
    [string]$SessionPath,
    [switch]$Apply,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Rc901aTestCertificateSubject = 'CN=VibeController RC901A Temporary Driver Test'
$script:Rc901aRequiredPackageFiles = @('Rc901aHidFilter.inf', 'Rc901aHidFilter.sys', 'Rc901aHidFilter.cat')

if ([string]::IsNullOrWhiteSpace($SourcePackageDirectory)) {
    $SourcePackageDirectory = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\driver\x64\Debug\Rc901aHidFilter'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-test-package'
}
if ([string]::IsNullOrWhiteSpace($SessionPath)) {
    $SessionPath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-test-session.json'
}

function New-Rc901aTestPackagePlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SourcePackageDirectory,

        [Parameter(Mandatory)]
        [string]$OutputDirectory,

        [Parameter(Mandatory)]
        [string]$SessionPath,

        [switch]$Apply
    )

    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        SourcePackageDirectory = $SourcePackageDirectory
        OutputDirectory = $OutputDirectory
        SessionPath = $SessionPath
        RequiredFiles = $script:Rc901aRequiredPackageFiles
        CertificateSubject = $script:Rc901aTestCertificateSubject
        PlannedAction = 'Create a temporary user certificate, embed-sign SYS, regenerate CAT, and sign CAT.'
        RestoreScript = Join-Path $PSScriptRoot 'Restore-Rc901aTestMode.ps1'
    }
}

function Assert-Rc901aTestPackageSource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    $resolved = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    foreach ($name in $script:Rc901aRequiredPackageFiles) {
        $matches = @(Get-ChildItem -LiteralPath $resolved -Filter $name -File)
        if ($matches.Count -ne 1) {
            throw "The source package must contain exactly one $name."
        }
    }

    $infContent = Get-Content -LiteralPath (Join-Path $resolved 'Rc901aHidFilter.inf') -Raw
    $exactHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'
    if ($infContent -notmatch [regex]::Escape($exactHardwareId)) {
        throw 'The source INF does not target the exact RC901A hardware ID.'
    }

    return $resolved
}

function Get-Rc901aWdkTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateSet('x64', 'x86')]
        [string]$Architecture
    )

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
    $matches = @(Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter $Name -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\$([regex]::Escape($Architecture))\\" } |
        Sort-Object FullName -Descending)
    if ($matches.Count -eq 0) {
        throw "Unable to locate the x64 WDK tool $Name."
    }
    return $matches[0].FullName
}

function Invoke-Rc901aSigningTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& $FilePath @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "$([IO.Path]::GetFileName($FilePath)) failed with exit code $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

function Invoke-Rc901aTestPackagePreparation {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$SourcePackageDirectory,

        [Parameter(Mandatory)]
        [string]$OutputDirectory,

        [Parameter(Mandatory)]
        [string]$SessionPath,

        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    $plan = New-Rc901aTestPackagePlan `
        -SourcePackageDirectory $SourcePackageDirectory `
        -OutputDirectory $OutputDirectory `
        -SessionPath $SessionPath `
        -Apply:$effectiveApply
    if (-not $effectiveApply) {
        return $plan
    }

    $source = Assert-Rc901aTestPackageSource -Directory $SourcePackageDirectory
    $outputFullPath = [IO.Path]::GetFullPath($OutputDirectory)
    $sessionFullPath = [IO.Path]::GetFullPath($SessionPath)
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\artifacts'))
    if (-not $outputFullPath.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $sessionFullPath.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The temporary package and session record must stay under the repository artifacts directory.'
    }
    if ((Test-Path -LiteralPath $outputFullPath) -or (Test-Path -LiteralPath $sessionFullPath)) {
        throw 'Refusing to overwrite an existing RC901A test package or session record.'
    }

    if (-not $PSCmdlet.ShouldProcess($outputFullPath, 'Create an isolated, temporary RC901A test-signed package')) {
        return $plan
    }

    $signTool = Get-Rc901aWdkTool -Name 'signtool.exe' -Architecture 'x64'
    $inf2Cat = Get-Rc901aWdkTool -Name 'Inf2Cat.exe' -Architecture 'x86'
    $certificate = $null
    try {
        New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $source 'Rc901aHidFilter.inf') -Destination $outputFullPath
        Copy-Item -LiteralPath (Join-Path $source 'Rc901aHidFilter.sys') -Destination $outputFullPath

        $certificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $script:Rc901aTestCertificateSubject `
            -FriendlyName 'VibeController RC901A temporary driver test certificate' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -KeyExportPolicy NonExportable `
            -NotAfter (Get-Date).AddDays(7)

        $certificatePath = Join-Path $outputFullPath 'VibeController-RC901A-Test.cer'
        Export-Certificate -Cert $certificate -FilePath $certificatePath -Type CERT | Out-Null

        $driverPath = Join-Path $outputFullPath 'Rc901aHidFilter.sys'
        Invoke-Rc901aSigningTool -FilePath $signTool -Arguments @(
            'sign', '/v', '/fd', 'SHA256', '/s', 'My', '/sha1', $certificate.Thumbprint, $driverPath
        ) | Out-Null

        Invoke-Rc901aSigningTool -FilePath $inf2Cat -Arguments @(
            "/driver:$outputFullPath", '/os:10_X64', '/uselocaltime'
        ) | Out-Null

        $catalogPath = Join-Path $outputFullPath 'Rc901aHidFilter.cat'
        Invoke-Rc901aSigningTool -FilePath $signTool -Arguments @(
            'sign', '/v', '/fd', 'SHA256', '/s', 'My', '/sha1', $certificate.Thumbprint, $catalogPath
        ) | Out-Null

        foreach ($signedPath in @($driverPath, $catalogPath)) {
            $signature = Get-AuthenticodeSignature -LiteralPath $signedPath
            if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
                throw "The expected temporary certificate did not sign $signedPath."
            }
        }

        $sessionDirectory = Split-Path -Parent $sessionFullPath
        if ($sessionDirectory) {
            New-Item -ItemType Directory -Path $sessionDirectory -Force | Out-Null
        }
        [ordered]@{
            SchemaVersion = 1
            CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
            CertificateSubject = $certificate.Subject
            CertificateThumbprint = $certificate.Thumbprint
            CertificatePath = $certificatePath
            CertificateStore = 'Cert:\CurrentUser\My'
            PackageDirectory = $outputFullPath
            InfPath = Join-Path $outputFullPath 'Rc901aHidFilter.inf'
            DriverPath = $driverPath
            CatalogPath = $catalogPath
            InitialTestSigning = $null
            EnteredAtUtc = $null
            RestoredAtUtc = $null
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $sessionFullPath -Encoding UTF8

        return Get-Content -LiteralPath $sessionFullPath -Raw | ConvertFrom-Json
    }
    catch {
        if ($null -ne $certificate -and (Test-Path -LiteralPath ("Cert:\CurrentUser\My\{0}" -f $certificate.Thumbprint))) {
            Remove-Item -LiteralPath ("Cert:\CurrentUser\My\{0}" -f $certificate.Thumbprint) -Force
        }
        throw
    }
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aTestPackagePreparation `
        -SourcePackageDirectory $SourcePackageDirectory `
        -OutputDirectory $OutputDirectory `
        -SessionPath $SessionPath `
        -Apply:$Apply `
        -WhatIf:$entryWhatIf
}
