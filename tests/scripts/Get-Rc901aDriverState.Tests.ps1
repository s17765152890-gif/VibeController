$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Get-Rc901aDriverState.ps1'
. $scriptPath -FunctionsOnly

Describe 'Test-Rc901aHardwareId' {
    It 'accepts the exact RC901A HID service hardware ID' {
        $hardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'

        Test-Rc901aHardwareId -HardwareId $hardwareId | Should Be $true
    }

    $invalidHardwareIds = @(
        'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&01045e_PID&0301_REV&0003',
        'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0999_REV&0003',
        'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0004',
        'BTHLEDevice\{0000180f-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003',
        'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}'
    )

    foreach ($hardwareId in $invalidHardwareIds) {
        It "rejects $hardwareId" {
            Test-Rc901aHardwareId -HardwareId $hardwareId | Should Be $false
        }
    }
}

Describe 'Get-Rc901aPnpEnumerationArguments' {
    It 'discovers the connected RC901A through its HID class' {
        $arguments = @(Get-Rc901aPnpEnumerationArguments)

        ($arguments -contains '/connected') | Should Be $true
        ($arguments -contains '/class') | Should Be $true
        ($arguments -contains 'HIDClass') | Should Be $true
        ($arguments -contains 'Bluetooth') | Should Be $false
    }

    It 'avoids the malformed pnputil instance query while filtering an explicit instance in memory' {
        $arguments = @(Get-Rc901aPnpEnumerationArguments -DeviceInstanceId 'BTHLEDevice\exact-instance')

        ($arguments -contains '/class') | Should Be $true
        ($arguments -contains 'HIDClass') | Should Be $true
        ($arguments -contains '/instanceid') | Should Be $false
        ($arguments -contains '/connected') | Should Be $false
    }
}
