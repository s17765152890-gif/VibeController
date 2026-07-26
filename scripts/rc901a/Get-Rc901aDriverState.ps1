[CmdletBinding()]
param(
    [string]$InstanceId,
    [Alias('FunctionsOnly')]
    [switch]$StateFunctionsOnly
)

Set-StrictMode -Version Latest

$script:Rc901aHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'

function Test-Rc901aHardwareId {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$HardwareId
    )

    return $HardwareId.Equals($script:Rc901aHardwareId, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PnpXmlPropertyData {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlElement]$Device,

        [Parameter(Mandatory)]
        [string]$KeyName
    )

    return @($Device.Properties.Property | Where-Object { $_.Key -eq $KeyName } | ForEach-Object {
        @($_.Value) | ForEach-Object { [string]$_ }
    })
}

function Get-Rc901aPnpEnumerationArguments {
    [CmdletBinding()]
    param(
        [string]$DeviceInstanceId
    )

    $arguments = @('/enum-devices')
    if ([string]::IsNullOrWhiteSpace($DeviceInstanceId)) {
        $arguments += '/connected'
    }
    $arguments += @('/class', 'HIDClass', '/deviceids', '/drivers', '/stack', '/properties', '/format', 'xml')

    return $arguments
}

function Get-Rc901aDriverState {
    [CmdletBinding()]
    param(
        [string]$DeviceInstanceId
    )

    $arguments = @(Get-Rc901aPnpEnumerationArguments -DeviceInstanceId $DeviceInstanceId)

    $xmlText = (& pnputil.exe @arguments | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "pnputil failed with exit code $LASTEXITCODE."
    }

    [xml]$document = $xmlText
    $devices = @($document.PnpUtil.Device | Where-Object {
        if ($DeviceInstanceId) {
            $_.InstanceId -ieq $DeviceInstanceId
        }
        else {
            $_.InstanceId -like 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003*'
        }
    })

    foreach ($device in $devices) {
        $hardwareIds = @($device.HardwareIds.HardwareId | ForEach-Object { [string]$_ })
        $exactMatch = $false
        foreach ($hardwareId in $hardwareIds) {
            if ($null -ne $hardwareId -and (Test-Rc901aHardwareId -HardwareId ([string]$hardwareId))) {
                $exactMatch = $true
                break
            }
        }

        $installedDriver = @($device.MatchingDrivers.DriverName | Where-Object {
            ([string]$_.Status) -match 'Installed'
        } | Select-Object -First 1)

        $problemCode = @(Get-PnpXmlPropertyData -Device $device -KeyName 'DEVPKEY_Device_ProblemCode' | Select-Object -First 1)
        $upperFilters = @(Get-PnpXmlPropertyData -Device $device -KeyName 'DEVPKEY_Device_UpperFilters')
        $lowerFilters = @(Get-PnpXmlPropertyData -Device $device -KeyName 'DEVPKEY_Device_LowerFilters')

        [pscustomobject]@{
            FriendlyName    = [string]$device.DeviceDescription
            InstanceId     = [string]$device.InstanceId
            Status         = [string]$device.Status
            ProblemCode    = if ($problemCode.Count -gt 0) { $problemCode[0] } else { $null }
            HardwareIds    = $hardwareIds
            ExactMatch     = $exactMatch
            DriverInf      = [string]$device.DriverName
            DriverProvider = if ($installedDriver.Count -gt 0) { [string]$installedDriver[0].ProviderName } else { $null }
            DriverVersion  = if ($installedDriver.Count -gt 0) { [string]$installedDriver[0].DriverVersion } else { $null }
            UpperFilters   = $upperFilters
            LowerFilters   = $lowerFilters
        }
    }
}

if (-not $StateFunctionsOnly) {
    Get-Rc901aDriverState -DeviceInstanceId $InstanceId
}
