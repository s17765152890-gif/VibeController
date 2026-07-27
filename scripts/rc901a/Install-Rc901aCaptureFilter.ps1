[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$PackageDirectory,
    [string]$InstanceId,
    [string]$StatePath,
    [switch]$Apply,
    [switch]$AllowTestPackage,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\umdf\bin\x64\Debug\Rc901aUmdfCapture'
}
if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-driver-state-before.json'
}

. (Join-Path $PSScriptRoot 'Get-Rc901aDriverState.ps1') -StateFunctionsOnly
. (Join-Path $PSScriptRoot 'Test-Rc901aProductionPackage.ps1') -FunctionsOnly

function Assert-Rc901aExactHardwareIds {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$HardwareIds
    )

    foreach ($hardwareId in $HardwareIds) {
        if (Test-Rc901aHardwareId -HardwareId ([string]$hardwareId)) {
            return
        }
    }

    throw 'Refusing operation: the device does not expose the exact RC901A hardware ID.'
}

function New-Rc901aCaptureInstallPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$InstanceId,

        [Parameter(Mandatory)]
        [string[]]$HardwareIds,

        [Parameter(Mandatory)]
        [string]$InfPath,

        [Parameter(Mandatory)]
        [string]$CatalogSignatureStatus,

        [Parameter(Mandatory)]
        [string]$StatePath,

        [switch]$ProductionReady,

        [switch]$AllowTestPackage,

        [switch]$Apply
    )

    Assert-Rc901aExactHardwareIds -HardwareIds $HardwareIds

    if ([System.IO.Path]::GetFileName($InfPath) -ine 'Rc901aHidFilter.inf') {
        throw 'Refusing operation: the package INF name is not Rc901aHidFilter.inf.'
    }

    $packageTrusted = $CatalogSignatureStatus -eq 'Valid'
    if ($Apply -and -not $packageTrusted) {
        throw "Refusing Apply: the driver catalog signature status is '$CatalogSignatureStatus', not 'Valid'."
    }
    if ($ProductionReady -and $AllowTestPackage) {
        throw 'Refusing operation: production-ready and development-only package modes are mutually exclusive.'
    }
    if ($Apply -and -not $ProductionReady -and -not $AllowTestPackage) {
        throw 'Refusing Apply: the RC901A package has not passed the production release gate. Development packages require the explicit AllowTestPackage switch.'
    }

    $uninstallScript = Join-Path $PSScriptRoot 'Uninstall-Rc901aCaptureFilter.ps1'
    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        InstanceId = $InstanceId
        HardwareId = $script:Rc901aHardwareId
        InfPath = $InfPath
        CatalogSignatureStatus = $CatalogSignatureStatus
        PackageTrusted = $packageTrusted
        ProductionReady = [bool]$ProductionReady
        DevelopmentOnly = [bool]$AllowTestPackage
        ReleaseGate = if ($ProductionReady) {
            'Production'
        }
        elseif ($AllowTestPackage) {
            'ExplicitDevelopmentOverride'
        }
        else {
            'NotEvaluatedInPreview'
        }
        StatePath = $StatePath
        PlannedAction = "Stage and install only $InfPath for the exact RC901A device."
        RollbackCommand = "& '$uninstallScript' -StatePath '$StatePath' -Apply"
    }
}

function Get-Rc901aExactDeviceState {
    [CmdletBinding()]
    param(
        [string]$DeviceInstanceId
    )

    $matches = @(Get-Rc901aDriverState -DeviceInstanceId $DeviceInstanceId | Where-Object { $_.ExactMatch })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one connected RC901A service device, found $($matches.Count)."
    }

    Assert-Rc901aExactHardwareIds -HardwareIds @($matches[0].HardwareIds)
    return $matches[0]
}

function Get-Rc901aCapturePackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Directory
    )

    $resolvedDirectory = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    $infFiles = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter 'Rc901aHidFilter.inf' -File)
    $catalogFiles = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter 'Rc901aHidFilter.cat' -File)
    if ($infFiles.Count -ne 1 -or $catalogFiles.Count -ne 1) {
        throw 'The package directory must contain exactly one RC901A INF and catalog.'
    }

    $infContent = Get-Content -LiteralPath $infFiles[0].FullName -Raw
    $binaryMatches = @([regex]::Matches(
        $infContent,
        '(?im)^\s*ServiceBinary\s*=\s*"?%13%\\([^"\r\n]+\.(?:sys|dll))"?\s*$'
    ))
    $binaryNames = @($binaryMatches |
        ForEach-Object { $_.Groups[1].Value } |
        Select-Object -Unique)
    if ($binaryNames.Count -ne 1 -or
        [IO.Path]::GetFileName($binaryNames[0]) -ine $binaryNames[0]) {
        throw 'The exact RC901A INF must reference one driver-store SYS or DLL binary.'
    }

    $binaryFiles = @(Get-ChildItem -LiteralPath $resolvedDirectory -File |
        Where-Object { $_.Name -ieq $binaryNames[0] })
    if ($binaryFiles.Count -ne 1) {
        throw "The package must contain exactly one INF-referenced binary named '$($binaryNames[0])'."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $catalogFiles[0].FullName
    $binaryKind = if ($binaryFiles[0].Extension -ieq '.dll') { 'UMDF' } else { 'KMDF' }
    [pscustomobject]@{
        Directory = $resolvedDirectory
        InfPath = $infFiles[0].FullName
        CatalogPath = $catalogFiles[0].FullName
        BinaryPath = $binaryFiles[0].FullName
        BinaryKind = $binaryKind
        DriverPath = $binaryFiles[0].FullName
        CatalogSignatureStatus = [string]$signature.Status
        CatalogSigner = if ($null -ne $signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { $null }
    }
}

function Assert-Rc901aNewRollbackStatePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$StatePath
    )

    if (Test-Path -LiteralPath $StatePath) {
        throw "Refusing Apply: rollback baseline '$StatePath' already exists. Preserve or remove it explicitly first."
    }
}

function Invoke-Rc901aPnpUtilMutation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& pnputil.exe @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "pnputil failed with exit code $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }

    return $output
}

function Get-Rc901aPublishedDrivers {
    [CmdletBinding()]
    param()

    $xmlText = (& pnputil.exe /enum-drivers /files /format xml | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "pnputil driver enumeration failed with exit code $LASTEXITCODE."
    }

    [xml]$document = $xmlText
    $matches = @($document.PnpUtil.Driver | Where-Object {
        $_.OriginalName -ieq 'rc901ahidfilter.inf' -and $_.ProviderName -ieq 'VibeController'
    })

    foreach ($match in $matches) {
        [pscustomobject]@{
            PublishedName = [string]$match.DriverName
            OriginalName = [string]$match.OriginalName
            ProviderName = [string]$match.ProviderName
            DriverVersion = [string]$match.DriverVersion
            SignerName = [string]$match.SignerName
        }
    }
}

function Resolve-Rc901aNewPublishedDriver {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [psobject[]]$BeforeDrivers
    )

    $beforeNames = @($BeforeDrivers | ForEach-Object { [string]$_.PublishedName })
    $afterDrivers = @(Get-Rc901aPublishedDrivers)
    $newDrivers = @($afterDrivers | Where-Object {
        $beforeNames -inotcontains [string]$_.PublishedName
    })
    if ($newDrivers.Count -ne 1) {
        throw "Expected staging to publish exactly one new RC901A package, found $($newDrivers.Count)."
    }

    return $newDrivers[0]
}

function Invoke-Rc901aCaptureFilterInstall {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$DriverPackageDirectory,

        [string]$DeviceInstanceId,

        [Parameter(Mandatory)]
        [string]$RollbackStatePath,

        [switch]$AllowTestPackage,

        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    $package = Get-Rc901aCapturePackage -Directory $DriverPackageDirectory
    $productionReport = $null
    if ($effectiveApply -and -not $AllowTestPackage) {
        $productionReport = Get-Rc901aProductionPackage -Directory $DriverPackageDirectory
    }
    $device = Get-Rc901aExactDeviceState -DeviceInstanceId $DeviceInstanceId
    $plan = New-Rc901aCaptureInstallPlan `
        -InstanceId $device.InstanceId `
        -HardwareIds @($device.HardwareIds) `
        -InfPath $package.InfPath `
        -CatalogSignatureStatus $package.CatalogSignatureStatus `
        -StatePath $RollbackStatePath `
        -ProductionReady:($null -ne $productionReport -and $productionReport.ProductionReady) `
        -AllowTestPackage:$AllowTestPackage `
        -Apply:$effectiveApply

    if (-not $effectiveApply) {
        return $plan
    }

    Assert-Rc901aNewRollbackStatePath -StatePath $RollbackStatePath
    $stateDirectory = Split-Path -Parent $RollbackStatePath
    if ($stateDirectory) {
        New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    }

    $stateRecord = [ordered]@{
        SchemaVersion = 1
        CapturedAtUtc = [DateTime]::UtcNow.ToString('o')
        Device = $device
        Package = [ordered]@{
            InfPath = $package.InfPath
            PublishedName = $null
            OriginalName = 'rc901ahidfilter.inf'
            ProviderName = 'VibeController'
            CatalogSignatureStatus = $package.CatalogSignatureStatus
            CatalogSigner = $package.CatalogSigner
            ProductionReady = $plan.ProductionReady
            DevelopmentOnly = $plan.DevelopmentOnly
            ProductionValidation = $productionReport
        }
    }
    $stateRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RollbackStatePath -Encoding UTF8

    $device = Get-Rc901aExactDeviceState -DeviceInstanceId $device.InstanceId
    if ($PSCmdlet.ShouldProcess($device.InstanceId, "Install trusted RC901A capture filter from $($package.InfPath)")) {
        $beforeDrivers = @(Get-Rc901aPublishedDrivers)
        $stageOutput = Invoke-Rc901aPnpUtilMutation -Arguments @('/add-driver', $package.InfPath)
        $published = Resolve-Rc901aNewPublishedDriver -BeforeDrivers $beforeDrivers
        $stateRecord.Package.PublishedName = $published.PublishedName
        $stateRecord.Package.DriverVersion = $published.DriverVersion
        $stateRecord.Package.SignerName = $published.SignerName
        $stateRecord.Package.StagedAtUtc = [DateTime]::UtcNow.ToString('o')
        $stateRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RollbackStatePath -Encoding UTF8

        $installOutput = Invoke-Rc901aPnpUtilMutation -Arguments @('/add-driver', $package.InfPath, '/install')
        $activeDevice = Get-Rc901aExactDeviceState -DeviceInstanceId $device.InstanceId
        if ($activeDevice.DriverInf -ine $published.PublishedName) {
            throw "The staged package is rollback-ready, but the exact RC901A device did not bind to $($published.PublishedName)."
        }

        [pscustomobject]@{
            Plan = $plan
            PublishedDriver = $published
            StageOutput = $stageOutput
            PnpUtilOutput = $installOutput
            ActiveDevice = $activeDevice
        }
    }
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aCaptureFilterInstall `
        -DriverPackageDirectory $PackageDirectory `
        -DeviceInstanceId $InstanceId `
        -RollbackStatePath $StatePath `
        -Apply:$Apply `
        -AllowTestPackage:$AllowTestPackage `
        -WhatIf:$entryWhatIf
}
