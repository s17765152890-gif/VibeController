[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\driver\x64\Debug\Rc901aHidFilter'),
    [string]$InstanceId,
    [string]$StatePath = (Join-Path $PSScriptRoot '..\..\artifacts\rc901a-driver-state-before.json'),
    [switch]$Apply,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'Get-Rc901aDriverState.ps1') -StateFunctionsOnly

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

    $uninstallScript = Join-Path $PSScriptRoot 'Uninstall-Rc901aCaptureFilter.ps1'
    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        InstanceId = $InstanceId
        HardwareId = $script:Rc901aHardwareId
        InfPath = $InfPath
        CatalogSignatureStatus = $CatalogSignatureStatus
        PackageTrusted = $packageTrusted
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
    $driverFiles = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter 'Rc901aHidFilter.sys' -File)
    if ($infFiles.Count -ne 1 -or $catalogFiles.Count -ne 1 -or $driverFiles.Count -ne 1) {
        throw 'The package directory must contain exactly one RC901A INF, catalog, and SYS file.'
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $catalogFiles[0].FullName
    [pscustomobject]@{
        Directory = $resolvedDirectory
        InfPath = $infFiles[0].FullName
        CatalogPath = $catalogFiles[0].FullName
        DriverPath = $driverFiles[0].FullName
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

function Get-Rc901aPublishedDriver {
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
    if ($matches.Count -ne 1) {
        throw "Expected one published RC901A filter package, found $($matches.Count)."
    }

    [pscustomobject]@{
        PublishedName = [string]$matches[0].DriverName
        OriginalName = [string]$matches[0].OriginalName
        ProviderName = [string]$matches[0].ProviderName
        DriverVersion = [string]$matches[0].DriverVersion
        SignerName = [string]$matches[0].SignerName
    }
}

function Invoke-Rc901aCaptureFilterInstall {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$DriverPackageDirectory,

        [string]$DeviceInstanceId,

        [Parameter(Mandatory)]
        [string]$RollbackStatePath,

        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    $package = Get-Rc901aCapturePackage -Directory $DriverPackageDirectory
    $device = Get-Rc901aExactDeviceState -DeviceInstanceId $DeviceInstanceId
    $plan = New-Rc901aCaptureInstallPlan `
        -InstanceId $device.InstanceId `
        -HardwareIds @($device.HardwareIds) `
        -InfPath $package.InfPath `
        -CatalogSignatureStatus $package.CatalogSignatureStatus `
        -StatePath $RollbackStatePath `
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
        }
    }
    $stateRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RollbackStatePath -Encoding UTF8

    $device = Get-Rc901aExactDeviceState -DeviceInstanceId $device.InstanceId
    if ($PSCmdlet.ShouldProcess($device.InstanceId, "Install trusted RC901A capture filter from $($package.InfPath)")) {
        $installOutput = Invoke-Rc901aPnpUtilMutation -Arguments @('/add-driver', $package.InfPath, '/install')
        $published = Get-Rc901aPublishedDriver
        $stateRecord.Package.PublishedName = $published.PublishedName
        $stateRecord.Package.DriverVersion = $published.DriverVersion
        $stateRecord.Package.SignerName = $published.SignerName
        $stateRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RollbackStatePath -Encoding UTF8

        [pscustomobject]@{
            Plan = $plan
            PublishedDriver = $published
            PnpUtilOutput = $installOutput
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
        -WhatIf:$entryWhatIf
}
