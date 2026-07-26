$infPath = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\driver\Rc901aHidFilter.inx'
$exactHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'

Describe 'RC901A HID capture filter INF safety boundary' {
    It 'exists' {
        Test-Path -LiteralPath $infPath | Should Be $true
    }

    It 'binds exactly one model to the exact RC901A hardware ID' {
        $content = Get-Content -LiteralPath $infPath -Raw
        $modelLines = @($content -split "`r?`n" | Where-Object { $_ -match '^\s*%[^%]+%\s*=\s*[^,]+,\s*BTHLEDevice\\' })

        $modelLines.Count | Should Be 1
        $modelLines[0].ToLowerInvariant().Contains($exactHardwareId.ToLowerInvariant()) | Should Be $true
    }

    It 'does not contain generic, wildcard, Xbox, or DualSense hardware matches' {
        $content = Get-Content -LiteralPath $infPath -Raw
        $modelLines = @($content -split "`r?`n" | Where-Object { $_ -match '^\s*%[^%]+%\s*=\s*[^,]+,\s*BTHLEDevice\\' })
        $joined = $modelLines -join "`n"

        $joined | Should Not Match 'BTHLEDevice\\\{00001812-0000-1000-8000-00805f9b34fb\}\s*$'
        $joined | Should Not Match 'VID&01045e'
        $joined | Should Not Match 'VID&01054c'
        $joined | Should Not Match 'VID&\*|PID&\*|REV&\*'
    }

    It 'inherits every required inbox HID-over-GATT section' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Match '(?im)^\s*Include\s*=\s*hidbthle\.inf\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.hw\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.Services\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.Wdf\s*$'
    }

    It 'places the capture filter immediately below the function driver and keeps WUDFRd in the default lower level' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Match '(?im)^\s*HKR\s*,\s*,\s*LowerFilterLevels\s*,\s*0x00010000\s*,\s*"Rc901aCapture"\s*,\s*"Default"\s*$'
        $content | Should Match '(?im)^\s*HKR\s*,\s*,\s*LowerFilterDefaultLevel\s*,\s*,\s*"Default"\s*$'
        $content | Should Match '(?im)^\s*AddFilter\s*=\s*Rc901aHidFilter\s*,\s*,\s*Rc901aHidFilter\.LowerFilter\s*$'
        $content | Should Match '(?im)^\s*FilterLevel\s*=\s*Rc901aCapture\s*$'
        $content | Should Not Match '(?im)^\s*HKR\s*,\s*,\s*(?:UpperFilters|LowerFilters)\s*,.*Rc901aHidFilter'
        $content | Should Match '(?im)^\s*ExcludeFromSelect\s*=\s*\*\s*$'
        $content | Should Match '(?im)^\s*PnpLockdown\s*=\s*1\s*$'
    }

    It 'requires Windows 10 2004 and uses driver-store isolation' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Match '(?im)^\s*%ProviderName%\s*=\s*Rc901aModels,NTamd64\.10\.0\.\.\.19041\s*$'
        $content | Should Match '(?im)^\s*Rc901aHidFilter\.CopyFiles\s*=\s*13\s*$'
        $content | Should Match '(?im)^\s*ServiceBinary\s*=\s*%13%\\Rc901aHidFilter\.sys\s*$'
    }
}
