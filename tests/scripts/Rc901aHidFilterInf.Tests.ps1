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

    It 'adds only the RC901A capture filter and remains hidden from manual selection' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Match '(?im)^\s*HKR\s*,\s*,\s*UpperFilters\s*,\s*0x00010008\s*,\s*"Rc901aHidFilter"\s*$'
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
