$scriptPath = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Get-Rc901aSecurityState.ps1'
$launcherPath = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Start-Rc901aSecurityAudit.ps1'

Describe 'RC901A elevated security-state audit' {
    It 'exists' {
        Test-Path -LiteralPath $scriptPath | Should Be $true
    }

    if (Test-Path -LiteralPath $scriptPath) {
        $content = Get-Content -LiteralPath $scriptPath -Raw

        It 'queries the required security boundaries' {
            $content | Should Match 'Confirm-SecureBootUEFI'
            $content | Should Match 'Get-BitLockerVolume'
            $content | Should Match 'Win32_DeviceGuard'
            $content | Should Match 'bcdedit\.exe\s+/enum'
        }

        It 'contains no boot, certificate, PnP, or restart mutation' {
            $content | Should Not Match '(?im)\bbcdedit(?:\.exe)?\s+/(set|deletevalue|delete|create|copy|import|export)\b'
            $content | Should Not Match '(?im)\b(certutil|Import-Certificate|New-SelfSignedCertificate)\b'
            $content | Should Not Match '(?im)\bpnputil(?:\.exe)?\s+/(add-driver|delete-driver|restart-device|disable-device|enable-device|remove-device)\b'
            $content | Should Not Match '(?im)\b(shutdown|Restart-Computer|Stop-Computer)\b'
            $content | Should Not Match '(?im)\bmanage-bde(?:\.exe)?\s+-protectors\s+-(disable|delete)\b'
        }

        It 'requires elevation and writes only the requested report' {
            $content | Should Match 'WindowsPrincipal'
            $content | Should Match 'IsInRole'
            $content | Should Match 'ConvertTo-Json'
            $content | Should Match 'Set-Content\s+-LiteralPath\s+\$ReportPath'
        }

        It 'computes default paths only after the script body starts' {
            $content | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
            $content | Should Match 'IsNullOrWhiteSpace\(\$ReportPath\)'
        }

        It 'persists a fatal error to the requested report' {
            $content | Should Match '\btrap\s*\{'
            $content | Should Match 'FatalError'
        }

        It 'treats an empty BCD response as a readable audit result' {
            $content | Should Match '\[AllowEmptyCollection\(\)\]'
            $content | Should Match '\[AllowEmptyString\(\)\]'
            $content | Should Match '\[AllowNull\(\)\]'
        }
    }
}

Describe 'RC901A security-audit elevation launcher' {
    It 'exists' {
        Test-Path -LiteralPath $launcherPath | Should Be $true
    }

    if (Test-Path -LiteralPath $launcherPath) {
        $launcherContent = Get-Content -LiteralPath $launcherPath -Raw

        It 'uses an encoded command and RunAs without changing security state' {
            $launcherContent | Should Match '-EncodedCommand'
            $launcherContent | Should Match "-Verb\s+RunAs"
            $launcherContent | Should Not Match '(?im)\bbcdedit(?:\.exe)?\s+/(set|deletevalue|delete|create|copy|import|export)\b'
            $launcherContent | Should Not Match '(?im)\b(certutil|Import-Certificate|New-SelfSignedCertificate)\b'
        }

        It 'computes its default report path after startup' {
            $launcherContent | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
            $launcherContent | Should Match 'IsNullOrWhiteSpace\(\$ReportPath\)'
        }
    }
}
