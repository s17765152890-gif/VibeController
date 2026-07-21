$installScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Install-Rc901aCaptureFilter.ps1'
$uninstallScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Uninstall-Rc901aCaptureFilter.ps1'
$exactHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'

Describe 'RC901A capture-filter scripts' {
    It 'provides an install script' {
        Test-Path -LiteralPath $installScript | Should Be $true
    }

    It 'provides an uninstall script' {
        Test-Path -LiteralPath $uninstallScript | Should Be $true
    }

    It 'keeps the shared state-module entry switch isolated from both script entry points' {
        $stateScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Get-Rc901aDriverState.ps1'
        $stateContent = Get-Content -LiteralPath $stateScript -Raw
        $installContent = Get-Content -LiteralPath $installScript -Raw
        $uninstallContent = Get-Content -LiteralPath $uninstallScript -Raw

        $stateContent | Should Match '\$StateFunctionsOnly'
        $installContent | Should Match '-StateFunctionsOnly'
        $uninstallContent | Should Match '-StateFunctionsOnly'
    }

    It 'computes install and rollback default paths after script startup' {
        $installContent = Get-Content -LiteralPath $installScript -Raw
        $uninstallContent = Get-Content -LiteralPath $uninstallScript -Raw

        $installContent | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
        $uninstallContent | Should Not Match '=\s*\(Join-Path\s+\$PSScriptRoot'
        $installContent | Should Match 'IsNullOrWhiteSpace\(\$PackageDirectory\)'
        $installContent | Should Match 'IsNullOrWhiteSpace\(\$StatePath\)'
        $uninstallContent | Should Match 'IsNullOrWhiteSpace\(\$StatePath\)'
    }
}

if ((Test-Path -LiteralPath $installScript) -and (Test-Path -LiteralPath $uninstallScript)) {
    . $installScript -FunctionsOnly
    . $uninstallScript -FunctionsOnly

    Describe 'New-Rc901aCaptureInstallPlan' {
        It 'defaults to a non-mutating preview and prints the inverse rollback command' {
            $plan = New-Rc901aCaptureInstallPlan `
                -InstanceId 'BTHLEDevice\exact-instance' `
                -HardwareIds @($exactHardwareId) `
                -InfPath 'C:\package\Rc901aHidFilter.inf' `
                -CatalogSignatureStatus 'NotSigned' `
                -StatePath 'C:\state\before.json'

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.PackageTrusted | Should Be $false
            $plan.RollbackCommand | Should Match 'Uninstall-Rc901aCaptureFilter\.ps1'
            $plan.RollbackCommand | Should Match 'before\.json'
        }

        It 'refuses a non-exact hardware ID' {
            { New-Rc901aCaptureInstallPlan `
                -InstanceId 'BTHLEDevice\other-instance' `
                -HardwareIds @('BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}') `
                -InfPath 'C:\package\Rc901aHidFilter.inf' `
                -CatalogSignatureStatus 'Valid' `
                -StatePath 'C:\state\before.json' } | Should Throw
        }

        It 'refuses Apply for an unsigned or untrusted catalog' {
            { New-Rc901aCaptureInstallPlan `
                -InstanceId 'BTHLEDevice\exact-instance' `
                -HardwareIds @($exactHardwareId) `
                -InfPath 'C:\package\Rc901aHidFilter.inf' `
                -CatalogSignatureStatus 'NotSigned' `
                -StatePath 'C:\state\before.json' `
                -Apply } | Should Throw
        }
    }

    Describe 'Get-Rc901aCapturePackage' {
        It 'accepts the lowercase catalog filename emitted by the WDK' {
            $packageDirectory = Join-Path $TestDrive 'package'
            New-Item -ItemType Directory -Path $packageDirectory | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'Rc901aHidFilter.inf') | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'rc901ahidfilter.cat') | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'Rc901aHidFilter.sys') | Out-Null
            Mock Get-AuthenticodeSignature {
                [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null }
            }

            $package = Get-Rc901aCapturePackage -Directory $packageDirectory

            [System.IO.Path]::GetFileName($package.CatalogPath) | Should Be 'rc901ahidfilter.cat'
            $package.CatalogSignatureStatus | Should Be 'NotSigned'
        }
    }

    Describe 'Invoke-Rc901aCaptureFilterInstall preview safety' {
        It 'does not write rollback state or invoke pnputil for Apply plus WhatIf' {
            $statePath = Join-Path $TestDrive 'before.json'
            Mock Get-Rc901aCapturePackage {
                [pscustomobject]@{
                    InfPath = 'C:\package\Rc901aHidFilter.inf'
                    CatalogSignatureStatus = 'Valid'
                    CatalogSigner = 'CN=Test'
                }
            }
            Mock Get-Rc901aExactDeviceState {
                [pscustomobject]@{
                    InstanceId = 'BTHLEDevice\exact-instance'
                    HardwareIds = @($exactHardwareId)
                    DriverInf = 'bthleenum.inf'
                }
            }
            Mock Invoke-Rc901aPnpUtilMutation { throw 'pnputil must not run' }

            $preview = Invoke-Rc901aCaptureFilterInstall `
                -DriverPackageDirectory 'C:\package' `
                -RollbackStatePath $statePath `
                -Apply `
                -WhatIf

            $preview.Mode | Should Be 'WhatIf'
            $preview.WillMutate | Should Be $false
            Test-Path -LiteralPath $statePath | Should Be $false
            Assert-MockCalled Invoke-Rc901aPnpUtilMutation -Times 0
        }
    }

    Describe 'Assert-Rc901aNewRollbackStatePath' {
        It 'is implemented as an explicit safety gate' {
            (Get-Command Assert-Rc901aNewRollbackStatePath -ErrorAction SilentlyContinue).Name | Should Be 'Assert-Rc901aNewRollbackStatePath'
        }

        It 'refuses to overwrite an existing rollback baseline' {
            $statePath = Join-Path $TestDrive 'existing-baseline.json'
            New-Item -ItemType File -Path $statePath | Out-Null

            try {
                Assert-Rc901aNewRollbackStatePath -StatePath $statePath
                throw 'Expected rollback baseline refusal.'
            }
            catch {
                $_.Exception.Message | Should Match 'already exists'
            }
        }
    }

    Describe 'New-Rc901aCaptureUninstallPlan' {
        It 'defaults to a non-mutating preview and prints its inverse install command' {
            $state = [pscustomobject]@{
                Device = [pscustomobject]@{
                    InstanceId = 'BTHLEDevice\exact-instance'
                    HardwareIds = @($exactHardwareId)
                    DriverInf = 'bthleenum.inf'
                }
                Package = [pscustomobject]@{
                    PublishedName = 'oem42.inf'
                    InfPath = 'C:\package\Rc901aHidFilter.inf'
                }
            }

            $plan = New-Rc901aCaptureUninstallPlan -State $state -StatePath 'C:\state\before.json'

            $plan.Mode | Should Be 'WhatIf'
            $plan.WillMutate | Should Be $false
            $plan.PublishedName | Should Be 'oem42.inf'
            $plan.RestoreDriverInf | Should Be 'bthleenum.inf'
            $plan.InverseCommand | Should Match 'Install-Rc901aCaptureFilter\.ps1'
        }

        It 'refuses rollback state for a non-exact device' {
            $state = [pscustomobject]@{
                Device = [pscustomobject]@{
                    InstanceId = 'BTHLEDevice\other-instance'
                    HardwareIds = @('BTHLEDevice\generic')
                    DriverInf = 'bthleenum.inf'
                }
                Package = [pscustomobject]@{
                    PublishedName = 'oem42.inf'
                    InfPath = 'C:\package\Rc901aHidFilter.inf'
                }
            }

            { New-Rc901aCaptureUninstallPlan -State $state -StatePath 'C:\state\before.json' } | Should Throw
        }

        It 'refuses an arbitrary driver package name' {
            $state = [pscustomobject]@{
                Device = [pscustomobject]@{
                    InstanceId = 'BTHLEDevice\exact-instance'
                    HardwareIds = @($exactHardwareId)
                    DriverInf = 'bthleenum.inf'
                }
                Package = [pscustomobject]@{
                    PublishedName = 'not-a-driver.txt'
                    InfPath = 'C:\package\Rc901aHidFilter.inf'
                }
            }

            { New-Rc901aCaptureUninstallPlan -State $state -StatePath 'C:\state\before.json' } | Should Throw
        }
    }
}
