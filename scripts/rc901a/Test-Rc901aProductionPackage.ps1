[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [string]$SignToolPath,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest

$script:Rc901aProductionHardwareId =
    'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'
$script:Rc901aProductionInfName = 'Rc901aHidFilter.inf'
$script:Rc901aProductionCatalogName = 'Rc901aHidFilter.cat'
$script:Rc901aProductionProvider = 'VibeController'
$script:Rc901aTemporarySignerSubject = 'CN=VibeController RC901A Temporary Driver Test'
$script:Rc901aMicrosoftHardwarePublisher =
    'Microsoft Windows Hardware Compatibility Publisher'
$script:Rc901aAcceptedHardwareEkus = @(
    '1.3.6.1.4.1.311.10.3.5',
    '1.3.6.1.4.1.311.10.3.5.1'
)

function Assert-Rc901aProductionSignatureMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Status,

        [Parameter(Mandatory)]
        [string]$SignerSubject,

        [Parameter(Mandatory)]
        [string]$SignerThumbprint,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$EnhancedKeyUsageOids
    )

    if ($Status -ne 'Valid') {
        throw "Production RC901A catalog signature status is '$Status', not 'Valid'."
    }

    if ([string]::IsNullOrWhiteSpace($SignerSubject) -or
        [string]::IsNullOrWhiteSpace($SignerThumbprint)) {
        throw 'Production RC901A catalog signature metadata is incomplete.'
    }

    if ([string]::Equals(
            $SignerSubject.Trim(),
            $script:Rc901aTemporarySignerSubject,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The temporary VibeController RC901A test signer is development-only.'
    }

    $publisherPattern =
        '(^|,\s*)CN=' +
        [regex]::Escape($script:Rc901aMicrosoftHardwarePublisher) +
        '(,|$)'
    if ($SignerSubject -notmatch $publisherPattern) {
        throw "The RC901A catalog signer '$SignerSubject' is not the Microsoft Windows hardware publisher."
    }

    $acceptedEku = @($EnhancedKeyUsageOids | Where-Object {
        $script:Rc901aAcceptedHardwareEkus -contains [string]$_
    } | Select-Object -First 1)
    if ($acceptedEku.Count -ne 1) {
        throw 'The RC901A catalog does not contain an accepted Windows hardware-driver verification EKU.'
    }

    [pscustomobject]@{
        ProductionSignature = $true
        Status = $Status
        SignerSubject = $SignerSubject
        SignerThumbprint = $SignerThumbprint.ToUpperInvariant()
        AcceptedHardwareEku = [string]$acceptedEku[0]
    }
}

function Assert-Rc901aProductionInfPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$InfPath
    )

    $resolvedInfPath = (Resolve-Path -LiteralPath $InfPath -ErrorAction Stop).Path
    if ([System.IO.Path]::GetFileName($resolvedInfPath) -ine $script:Rc901aProductionInfName) {
        throw "Production RC901A INF must be named '$($script:Rc901aProductionInfName)'."
    }

    $content = Get-Content -LiteralPath $resolvedInfPath -Raw
    if ($content -notmatch '(?im)^\s*Provider\s*=\s*%ProviderName%\s*$') {
        throw 'Production RC901A INF must resolve its provider through ProviderName.'
    }

    $providerMatch = [regex]::Match(
        $content,
        '(?im)^\s*ProviderName\s*=\s*"([^"]+)"\s*$')
    if (-not $providerMatch.Success -or
        $providerMatch.Groups[1].Value -ine $script:Rc901aProductionProvider) {
        throw "Production RC901A INF provider must be '$($script:Rc901aProductionProvider)'."
    }

    $catalogMatch = [regex]::Match(
        $content,
        '(?im)^\s*CatalogFile\s*=\s*"?([^"\r\n]+)"?\s*$')
    if (-not $catalogMatch.Success -or
        $catalogMatch.Groups[1].Value -ine $script:Rc901aProductionCatalogName) {
        throw "Production RC901A INF catalog must be '$($script:Rc901aProductionCatalogName)'."
    }

    $driverVersionMatch = [regex]::Match(
        $content,
        '(?im)^\s*DriverVer\s*=\s*([^,\r\n]+)\s*,\s*(\d+\.\d+\.\d+\.\d+)\s*$')
    if (-not $driverVersionMatch.Success) {
        throw 'Production RC901A INF must contain a four-part DriverVer.'
    }

    $modelMatches = @([regex]::Matches(
        $content,
        '(?im)^\s*%[^%]+%\s*=\s*[^,\r\n]+,\s*([A-Za-z][A-Za-z0-9_-]*\\[^\r\n;]+?)\s*$'))
    if ($modelMatches.Count -ne 1) {
        throw "Production RC901A INF must contain exactly one PnP model line; found $($modelMatches.Count)."
    }

    $hardwareId = $modelMatches[0].Groups[1].Value.Trim()
    if (-not [string]::Equals(
            $hardwareId,
            $script:Rc901aProductionHardwareId,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Production RC901A INF does not bind only the exact supported hardware ID."
    }

    $binaryMatches = @([regex]::Matches(
        $content,
        '(?im)^\s*ServiceBinary\s*=\s*"?%13%\\([^"\r\n]+)"?\s*$'))
    if ($binaryMatches.Count -ne 1) {
        throw "Production RC901A INF must reference exactly one driver-store binary; found $($binaryMatches.Count)."
    }

    $binaryName = $binaryMatches[0].Groups[1].Value.Trim()
    if ([System.IO.Path]::GetFileName($binaryName) -ine $binaryName -or
        $binaryName.Contains('\') -or
        $binaryName.Contains('/') -or
        [System.IO.Path]::GetExtension($binaryName) -inotmatch '^\.(dll|sys)$') {
        throw 'Production RC901A ServiceBinary must be a basename-only DLL or SYS.'
    }

    [pscustomobject]@{
        InfPath = $resolvedInfPath
        HardwareId = $script:Rc901aProductionHardwareId
        Provider = $script:Rc901aProductionProvider
        CatalogName = $script:Rc901aProductionCatalogName
        DriverDate = $driverVersionMatch.Groups[1].Value.Trim()
        DriverVersion = $driverVersionMatch.Groups[2].Value
        BinaryName = $binaryName
    }
}

function Get-Rc901aProductionPackageFiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    $resolvedDirectory = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    $directoryItem = Get-Item -LiteralPath $resolvedDirectory -Force
    if (-not $directoryItem.PSIsContainer -or
        ($directoryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'Production RC901A package path must be a real directory, not a reparse point.'
    }

    $children = @(Get-ChildItem -LiteralPath $resolvedDirectory -Force)
    if (@($children | Where-Object {
                $_.PSIsContainer -or
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint)
            }).Count -gt 0) {
        throw 'Production RC901A package must not contain directories or reparse points.'
    }

    $infFiles = @($children | Where-Object { $_.Name -ieq $script:Rc901aProductionInfName })
    $catalogFiles = @($children | Where-Object { $_.Name -ieq $script:Rc901aProductionCatalogName })
    if ($infFiles.Count -ne 1 -or $catalogFiles.Count -ne 1) {
        throw 'Production RC901A package must contain exactly one expected INF and catalog.'
    }

    $policy = Assert-Rc901aProductionInfPolicy -InfPath $infFiles[0].FullName
    $binaryFiles = @($children | Where-Object { $_.Name -ieq $policy.BinaryName })
    if ($binaryFiles.Count -ne 1) {
        throw "Production RC901A package must contain exactly one '$($policy.BinaryName)'."
    }

    $expectedNames = @(
        $script:Rc901aProductionInfName,
        $script:Rc901aProductionCatalogName,
        $policy.BinaryName
    )
    $unexpectedFiles = @($children | Where-Object {
        $expectedNames -inotcontains $_.Name
    })
    if ($unexpectedFiles.Count -gt 0 -or $children.Count -ne 3) {
        $unexpectedNames = @($unexpectedFiles | ForEach-Object { $_.Name }) -join ', '
        throw "Production RC901A package contains unexpected files: $unexpectedNames"
    }

    [pscustomobject]@{
        Directory = $resolvedDirectory
        InfPath = $infFiles[0].FullName
        CatalogPath = $catalogFiles[0].FullName
        BinaryPath = $binaryFiles[0].FullName
        Policy = $policy
    }
}

function Get-Rc901aCatalogSignatureMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$CatalogPath
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $CatalogPath
    $certificate = $signature.SignerCertificate
    $ekuOids = @()
    if ($null -ne $certificate) {
        foreach ($extension in $certificate.Extensions) {
            if ($extension.Oid.Value -ne '2.5.29.37') {
                continue
            }

            $enhancedKeyUsage = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
                $extension,
                $extension.Critical)
            $ekuOids = @($enhancedKeyUsage.EnhancedKeyUsages | ForEach-Object { $_.Value })
        }
    }

    [pscustomobject]@{
        Status = [string]$signature.Status
        SignerSubject = if ($null -ne $certificate) { [string]$certificate.Subject } else { $null }
        SignerThumbprint = if ($null -ne $certificate) { [string]$certificate.Thumbprint } else { $null }
        EnhancedKeyUsageOids = $ekuOids
    }
}

function Resolve-Rc901aSignToolPath {
    [CmdletBinding()]
    param(
        [string]$Path
    )

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw 'SignTool is required to verify a production RC901A driver package.'
    }

    return $command.Source
}

function Invoke-Rc901aExternalTool {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    $output = @(& $FilePath @ArgumentList 2>&1)
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = @($output | ForEach-Object { [string]$_ })
    }
}

function Invoke-Rc901aSignToolVerification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SignToolPath,

        [Parameter(Mandatory)]
        [string]$CatalogPath,

        [Parameter(Mandatory)]
        [string]$InfPath,

        [Parameter(Mandatory)]
        [string]$BinaryPath
    )

    $checks = @(
        [pscustomobject]@{
            Name = 'KernelPolicy'
            Arguments = @('verify', '/v', '/kp', $CatalogPath)
        },
        [pscustomobject]@{
            Name = 'InfCatalogMembership'
            Arguments = @('verify', '/v', '/pa', '/c', $CatalogPath, $InfPath)
        },
        [pscustomobject]@{
            Name = 'BinaryCatalogMembership'
            Arguments = @('verify', '/v', '/pa', '/c', $CatalogPath, $BinaryPath)
        }
    )

    $results = @()
    foreach ($check in $checks) {
        $toolResult = Invoke-Rc901aExternalTool `
            -FilePath $SignToolPath `
            -ArgumentList @($check.Arguments)
        if ($toolResult.ExitCode -ne 0) {
            throw "SignTool check '$($check.Name)' failed with exit code $($toolResult.ExitCode).`n$($toolResult.Output -join [Environment]::NewLine)"
        }

        $results += [pscustomobject]@{
            Name = $check.Name
            ExitCode = $toolResult.ExitCode
            Arguments = @($check.Arguments)
            Output = @($toolResult.Output)
        }
    }

    return $results
}

function Get-Rc901aProductionPackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [string]$SignToolPath
    )

    $package = Get-Rc901aProductionPackageFiles -Directory $Directory
    $metadata = Get-Rc901aCatalogSignatureMetadata -CatalogPath $package.CatalogPath
    $signature = Assert-Rc901aProductionSignatureMetadata `
        -Status ([string]$metadata.Status) `
        -SignerSubject ([string]$metadata.SignerSubject) `
        -SignerThumbprint ([string]$metadata.SignerThumbprint) `
        -EnhancedKeyUsageOids @($metadata.EnhancedKeyUsageOids)
    $resolvedSignTool = Resolve-Rc901aSignToolPath -Path $SignToolPath
    $signToolChecks = @(Invoke-Rc901aSignToolVerification `
        -SignToolPath $resolvedSignTool `
        -CatalogPath $package.CatalogPath `
        -InfPath $package.InfPath `
        -BinaryPath $package.BinaryPath)

    $files = @(
        $package.InfPath,
        $package.CatalogPath,
        $package.BinaryPath
    ) | ForEach-Object {
        [pscustomobject]@{
            Name = [System.IO.Path]::GetFileName($_)
            Sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToUpperInvariant()
        }
    }

    [pscustomobject]@{
        SchemaVersion = 1
        ProductionReady = $true
        Directory = $package.Directory
        HardwareId = $package.Policy.HardwareId
        Provider = $package.Policy.Provider
        DriverDate = $package.Policy.DriverDate
        DriverVersion = $package.Policy.DriverVersion
        InfName = [System.IO.Path]::GetFileName($package.InfPath)
        CatalogName = [System.IO.Path]::GetFileName($package.CatalogPath)
        BinaryName = [System.IO.Path]::GetFileName($package.BinaryPath)
        SignerSubject = $signature.SignerSubject
        SignerThumbprint = $signature.SignerThumbprint
        AcceptedHardwareEku = $signature.AcceptedHardwareEku
        Files = @($files)
        SignToolChecks = @($signToolChecks)
    }
}

function Test-Rc901aProductionPackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Directory,

        [string]$SignToolPath
    )

    Get-Rc901aProductionPackage -Directory $Directory -SignToolPath $SignToolPath
}

if (-not $FunctionsOnly) {
    if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
        throw 'PackageDirectory is required unless FunctionsOnly is specified.'
    }

    Test-Rc901aProductionPackage `
        -Directory $PackageDirectory `
        -SignToolPath $SignToolPath
}
