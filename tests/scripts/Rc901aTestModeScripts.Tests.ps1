$prepareScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\New-Rc901aTestSignedPackage.ps1'
$enterScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Enter-Rc901aTestMode.ps1'
$restoreScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Restore-Rc901aTestMode.ps1'
$oneBootTrustScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Trust-Rc901aOneBootCertificate.ps1'
$oneBootTrustLauncher = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Start-Rc901aOneBootTrust.ps1'

Describe 'RC901A temporary test-mode workflow scripts' {
    It 'provides preparation, entry, and restore scripts' {
        Test-Path -LiteralPath $prepareScript | Should Be $true
        Test-Path -LiteralPath $enterScript | Should Be $true
        Test-Path -LiteralPath $restoreScript | Should Be $true
    }

    It 'provides the safer one-boot certificate trust script' {
        Test-Path -LiteralPath $oneBootTrustScript | Should Be $true
    }

    It 'provides an elevation launcher for one-boot trust only' {
        Test-Path -LiteralPath $oneBootTrustLauncher | Should Be $true
    }
}

if (Test-Path -LiteralPath $oneBootTrustScript) {
    . $oneBootTrustScript -FunctionsOnly

    Describe 'New-Rc901aOneBootTrustPlan' {
        It 'trusts only the recorded certificate and changes no boot policy' {
            $plan = New-Rc901aOneBootTrustPlan -SessionPath 'C:\session.json'

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.TrustStores | Should Be @('Root', 'TrustedPublisher')
            $plan.BcdAction | Should Be 'None'
            $plan.SecureBootAction | Should Be 'None'
            $plan.DriverAction | Should Be 'None'
            $plan.RestoreScript | Should Match 'Restore-Rc901aTestMode\.ps1'
        }
    }

    Describe 'RC901A one-boot trust mutation boundary' {
        It 'never changes BCD, Secure Boot, PnP, or restart state' {
            $content = Get-Content -LiteralPath $oneBootTrustScript -Raw

            $content | Should Not Match '(?i)\bbcdedit(?:\.exe)?\b'
            $content | Should Not Match '(?i)Confirm-SecureBootUEFI'
            $content | Should Not Match '(?i)\bpnputil(?:\.exe)?\b'
            $content | Should Not Match '(?i)\b(shutdown|Restart-Computer)\b'
        }

        It 'computes its default session path after startup' {
            $content = Get-Content -LiteralPath $oneBootTrustScript -Raw
            $content | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
            $content | Should Match 'IsNullOrWhiteSpace\(\$SessionPath\)'
        }
    }
}

if (Test-Path -LiteralPath $oneBootTrustLauncher) {
    Describe 'RC901A one-boot trust elevation launcher' {
        $launcherContent = Get-Content -LiteralPath $oneBootTrustLauncher -Raw

        It 'elevates the exact trust script and requests Apply' {
            $launcherContent | Should Match 'Trust-Rc901aOneBootCertificate\.ps1'
            $launcherContent | Should Match '-EncodedCommand'
            $launcherContent | Should Match '-Verb\s+RunAs'
            $launcherContent | Should Match '-Apply'
        }

        It 'never changes boot, PnP, or restart state itself' {
            $launcherContent | Should Not Match '(?i)\bbcdedit(?:\.exe)?\b'
            $launcherContent | Should Not Match '(?i)\bpnputil(?:\.exe)?\b'
            $launcherContent | Should Not Match '(?i)\b(shutdown|Restart-Computer)\b'
        }

        It 'computes its default session path after startup' {
            $launcherContent | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
            $launcherContent | Should Match 'IsNullOrWhiteSpace\(\$SessionPath\)'
        }
    }
}

if ((Test-Path -LiteralPath $prepareScript) -and
    (Test-Path -LiteralPath $enterScript) -and
    (Test-Path -LiteralPath $restoreScript)) {
    . $prepareScript -FunctionsOnly
    . $enterScript -FunctionsOnly
    . $restoreScript -FunctionsOnly

    Describe 'New-Rc901aTestPackagePlan' {
        It 'defaults to a non-mutating preview with exact package files and cleanup' {
            $plan = New-Rc901aTestPackagePlan `
                -SourcePackageDirectory 'C:\source' `
                -OutputDirectory 'C:\output' `
                -SessionPath 'C:\session.json'

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.RequiredFiles | Should Be @('Rc901aHidFilter.inf', 'Rc901aHidFilter.sys', 'Rc901aHidFilter.cat')
            $plan.CertificateSubject | Should Be 'CN=VibeController RC901A Temporary Driver Test'
            $plan.RestoreScript | Should Match 'Restore-Rc901aTestMode\.ps1'
        }

        It 'uses the installed architecture of each WDK signing tool' {
            (Get-Rc901aWdkTool -Name 'signtool.exe' -Architecture 'x64') | Should Match '\\x64\\signtool\.exe$'
            (Get-Rc901aWdkTool -Name 'Inf2Cat.exe' -Architecture 'x86') | Should Match '\\x86\\Inf2Cat\.exe$'
        }
    }

    Describe 'New-Rc901aTestModeEntryPlan' {
        It 'refuses to enter while Secure Boot is enabled' {
            { New-Rc901aTestModeEntryPlan -SessionPath 'C:\session.json' -SecureBootEnabled $true -Apply } |
                Should Throw
        }

        It 'limits entry to certificate trust and TESTSIGNING' {
            $plan = New-Rc901aTestModeEntryPlan `
                -SessionPath 'C:\session.json' `
                -SecureBootEnabled $false

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.TrustStores | Should Be @('Root', 'TrustedPublisher')
            $plan.BcdAction | Should Be 'Set TESTSIGNING ON for {current}'
            $plan.RestartRequired | Should Be $true
        }
    }

    Describe 'New-Rc901aTestModeRestorePlan' {
        It 'restores BCD and removes only the recorded temporary certificate' {
            $plan = New-Rc901aTestModeRestorePlan `
                -SessionPath 'C:\session.json' `
                -CertificateThumbprint 'ABC123' `
                -InitialTestSigning 'NotPresent'

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.CertificateThumbprint | Should Be 'ABC123'
            $plan.BcdAction | Should Be 'Delete TESTSIGNING from {current}'
            $plan.CertificateStores | Should Be @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher', 'Cert:\CurrentUser\My')
            $plan.RequiresDriverUninstalled | Should Be $true
            $plan.RestartRequired | Should Be $true
            $plan.ManualFinalStep | Should Match 'Secure Boot'
        }

        It 'can clean up a prepared session that never entered test mode' {
            $plan = New-Rc901aTestModeRestorePlan `
                -SessionPath 'C:\session.json' `
                -CertificateThumbprint 'ABC123' `
                -InitialTestSigning $null

            $plan.BcdAction | Should Be 'No BCD change; test mode was not entered'
            $plan.RestartRequired | Should Be $false
        }
    }

    Describe 'RC901A test-mode mutation boundary' {
        It 'never disables integrity checks or enables kernel debugging' {
            $content = (Get-Content -LiteralPath $prepareScript -Raw) +
                (Get-Content -LiteralPath $enterScript -Raw) +
                (Get-Content -LiteralPath $restoreScript -Raw)

            $content | Should Not Match '(?i)nointegritychecks'
            $content | Should Not Match '(?i)\bdebug\s+(on|yes|true)'
        }

        It 'keeps every default path out of the parameter block' {
            foreach ($path in @($prepareScript, $enterScript, $restoreScript)) {
                $content = Get-Content -LiteralPath $path -Raw
                $content | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
            }
        }
    }
}
