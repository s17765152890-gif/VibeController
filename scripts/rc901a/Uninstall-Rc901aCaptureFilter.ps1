[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$StatePath,
    [switch]$Apply,
    [switch]$FunctionsOnly
)

Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = Join-Path $PSScriptRoot '..\..\artifacts\rc901a-driver-state-before.json'
}

. (Join-Path $PSScriptRoot 'Get-Rc901aDriverState.ps1') -StateFunctionsOnly

if (-not (Get-Command Assert-Rc901aExactHardwareIds -ErrorAction SilentlyContinue)) {
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

        throw 'Refusing operation: the rollback state does not contain the exact RC901A hardware ID.'
    }
}

function New-Rc901aCaptureUninstallPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [psobject]$State,

        [Parameter(Mandatory)]
        [string]$StatePath,

        [switch]$Apply
    )

    Assert-Rc901aExactHardwareIds -HardwareIds @($State.Device.HardwareIds)
    if ([string]$State.Package.PublishedName -notmatch '^oem\d+\.inf$') {
        throw 'Refusing operation: the recorded published driver name is not an OEM INF.'
    }
    if ([System.IO.Path]::GetFileName([string]$State.Package.InfPath) -ine 'Rc901aHidFilter.inf') {
        throw 'Refusing operation: rollback state does not identify Rc901aHidFilter.inf.'
    }

    $installScript = Join-Path $PSScriptRoot 'Install-Rc901aCaptureFilter.ps1'
    [pscustomobject]@{
        Mode = if ($Apply) { 'Apply' } else { 'WhatIf' }
        WillMutate = [bool]$Apply
        InstanceId = [string]$State.Device.InstanceId
        HardwareId = $script:Rc901aHardwareId
        PublishedName = [string]$State.Package.PublishedName
        RestoreDriverInf = [string]$State.Device.DriverInf
        StatePath = $StatePath
        PlannedAction = "Remove only $($State.Package.PublishedName), then verify restoration of $($State.Device.DriverInf)."
        InverseCommand = "& '$installScript' -PackageDirectory '$([System.IO.Path]::GetDirectoryName([string]$State.Package.InfPath))' -InstanceId '$($State.Device.InstanceId)' -StatePath '$StatePath' -Apply"
    }
}

function Get-Rc901aRollbackPublishedDriver {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$PublishedName
    )

    $xmlText = (& pnputil.exe /enum-drivers /files /format xml | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "pnputil driver enumeration failed with exit code $LASTEXITCODE."
    }

    [xml]$document = $xmlText
    $matches = @($document.PnpUtil.Driver | Where-Object { $_.DriverName -ieq $PublishedName })
    if ($matches.Count -ne 1 -or
        $matches[0].OriginalName -ine 'rc901ahidfilter.inf' -or
        $matches[0].ProviderName -ine 'VibeController') {
        throw 'Refusing operation: the published package is not the VibeController RC901A filter.'
    }

    return $matches[0]
}

function Invoke-Rc901aRollbackPnpUtilMutation {
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

function Get-Rc901aCurrentExactState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$InstanceId
    )

    $matches = @(Get-Rc901aDriverState -DeviceInstanceId $InstanceId | Where-Object { $_.ExactMatch })
    if ($matches.Count -ne 1) {
        throw "Expected the recorded RC901A service device before rollback, found $($matches.Count)."
    }
    Assert-Rc901aExactHardwareIds -HardwareIds @($matches[0].HardwareIds)
    return $matches[0]
}

function Invoke-Rc901aCaptureFilterUninstall {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)]
        [string]$RollbackStatePath,

        [switch]$Apply
    )

    $effectiveApply = $Apply -and -not $WhatIfPreference
    $resolvedStatePath = (Resolve-Path -LiteralPath $RollbackStatePath -ErrorAction Stop).Path
    $state = Get-Content -LiteralPath $resolvedStatePath -Raw | ConvertFrom-Json
    $plan = New-Rc901aCaptureUninstallPlan -State $state -StatePath $resolvedStatePath -Apply:$effectiveApply

    if (-not $effectiveApply) {
        return $plan
    }

    $null = Get-Rc901aRollbackPublishedDriver -PublishedName $plan.PublishedName
    $current = Get-Rc901aCurrentExactState -InstanceId $plan.InstanceId
    if ($PSCmdlet.ShouldProcess($current.InstanceId, "Remove only RC901A filter package $($plan.PublishedName)")) {
        $deleteOutput = Invoke-Rc901aRollbackPnpUtilMutation -Arguments @('/delete-driver', $plan.PublishedName, '/uninstall', '/force')
        $restored = @(Get-Rc901aDriverState -DeviceInstanceId $plan.InstanceId | Where-Object { $_.ExactMatch } | Select-Object -First 1)
        $restoredInf = if ($restored.Count -gt 0) { [string]$restored[0].DriverInf } else { $null }

        [pscustomobject]@{
            Plan = $plan
            PnpUtilOutput = $deleteOutput
            RestoredDriverInf = $restoredInf
            RestoreVerified = ($restoredInf -ieq $plan.RestoreDriverInf)
            FollowUp = if ($restoredInf -ieq $plan.RestoreDriverInf) {
                'Rollback driver selection verified.'
            }
            else {
                'Driver selection is not yet restored; do not continue until the recorded prior INF is active.'
            }
        }
    }
}

if (-not $FunctionsOnly) {
    $entryWhatIf = $WhatIfPreference -or (-not $Apply)
    Invoke-Rc901aCaptureFilterUninstall `
        -RollbackStatePath $StatePath `
        -Apply:$Apply `
        -WhatIf:$entryWhatIf
}
