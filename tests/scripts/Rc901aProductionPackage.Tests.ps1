$validatorScript = Join-Path $PSScriptRoot '..\..\scripts\rc901a\Test-Rc901aProductionPackage.ps1'
$exactHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'
$microsoftHardwarePublisher = 'CN=Microsoft Windows Hardware Compatibility Publisher'
$attestationEku = '1.3.6.1.4.1.311.10.3.5.1'

function New-Rc901aInfFixture {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [string]$Provider = 'VibeController',

        [string[]]$ModelHardwareIds = @($exactHardwareId),

        [string]$BinaryName = 'Rc901aUmdfCapture.dll'
    )

    $modelLines = @($ModelHardwareIds | ForEach-Object {
        "%Rc901aDeviceDescription% = Rc901aCapture_Install, $_"
    }) -join [Environment]::NewLine

    @"
[Version]
Signature = "`$WINDOWS NT`$"
Class = HIDClass
ClassGuid = {745a17a0-74d3-11d0-b6fe-00a0c90f57da}
Provider = %ProviderName%
CatalogFile = Rc901aHidFilter.cat
DriverVer = 07/27/2026,1.0.0.6
PnpLockdown = 1

[Manufacturer]
%ProviderName% = Rc901aModels,NTamd64.10.0...19041

[Rc901aModels.NTamd64.10.0...19041]
$modelLines

[Rc901aCapture_Install.NT.Wdf]
UmdfService = Rc901aUmdfCapture, Rc901aUmdfCapture.Wdf

[Rc901aUmdfCapture.Wdf]
UmdfLibraryVersion = `$UMDFVERSION`$
ServiceBinary = "%13%\$BinaryName"

[Strings]
ProviderName = "$Provider"
Rc901aDeviceDescription = "TCL RC901A report-map capture filter"
"@ | Set-Content -LiteralPath $Path -Encoding ASCII
}

Describe 'RC901A production driver package validator' {
    It 'exists as a dedicated read-only release gate' {
        Test-Path -LiteralPath $validatorScript | Should Be $true
    }
}

if (Test-Path -LiteralPath $validatorScript) {
    . $validatorScript -FunctionsOnly

    Describe 'Assert-Rc901aProductionSignatureMetadata' {
        It 'accepts a valid Microsoft hardware publisher with a hardware-driver EKU' {
            $result = Assert-Rc901aProductionSignatureMetadata `
                -Status Valid `
                -SignerSubject $microsoftHardwarePublisher `
                -SignerThumbprint '001122AABB' `
                -EnhancedKeyUsageOids @($attestationEku)

            $result.ProductionSignature | Should Be $true
            $result.AcceptedHardwareEku | Should Be $attestationEku
        }

        It 'rejects the temporary development signer even when status is Valid' {
            {
                Assert-Rc901aProductionSignatureMetadata `
                    -Status Valid `
                    -SignerSubject 'CN=VibeController RC901A Temporary Driver Test' `
                    -SignerThumbprint 'TEST' `
                    -EnhancedKeyUsageOids @($attestationEku)
            } | Should Throw
        }

        It 'rejects a valid non-Microsoft publisher' {
            {
                Assert-Rc901aProductionSignatureMetadata `
                    -Status Valid `
                    -SignerSubject 'CN=Contoso Driver Publisher' `
                    -SignerThumbprint 'CONTOSO' `
                    -EnhancedKeyUsageOids @($attestationEku)
            } | Should Throw
        }

        It 'rejects a Microsoft subject without a Windows hardware-driver EKU' {
            {
                Assert-Rc901aProductionSignatureMetadata `
                    -Status Valid `
                    -SignerSubject $microsoftHardwarePublisher `
                    -SignerThumbprint '001122AABB' `
                    -EnhancedKeyUsageOids @('1.3.6.1.5.5.7.3.3')
            } | Should Throw
        }

        It 'rejects any catalog status other than Valid' {
            {
                Assert-Rc901aProductionSignatureMetadata `
                    -Status NotSigned `
                    -SignerSubject $microsoftHardwarePublisher `
                    -SignerThumbprint '001122AABB' `
                    -EnhancedKeyUsageOids @($attestationEku)
            } | Should Throw
        }
    }

    Describe 'Assert-Rc901aProductionInfPolicy' {
        It 'accepts the exact UMDF package boundary' {
            $infPath = Join-Path $TestDrive 'Rc901aHidFilter.inf'
            New-Rc901aInfFixture -Path $infPath

            $policy = Assert-Rc901aProductionInfPolicy -InfPath $infPath

            $policy.HardwareId | Should Be $exactHardwareId
            $policy.Provider | Should Be 'VibeController'
            $policy.CatalogName | Should Be 'Rc901aHidFilter.cat'
            $policy.BinaryName | Should Be 'Rc901aUmdfCapture.dll'
            $policy.DriverVersion | Should Be '1.0.0.6'
        }

        It 'rejects an additional generic BLE HID model line' {
            $infPath = Join-Path $TestDrive 'Rc901aHidFilter.inf'
            New-Rc901aInfFixture `
                -Path $infPath `
                -ModelHardwareIds @(
                    $exactHardwareId,
                    'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}'
                )

            { Assert-Rc901aProductionInfPolicy -InfPath $infPath } | Should Throw
        }

        It 'rejects an additional non-BLE HID or controller model line' {
            $infPath = Join-Path $TestDrive 'Rc901aHidFilter.inf'
            New-Rc901aInfFixture `
                -Path $infPath `
                -ModelHardwareIds @(
                    $exactHardwareId,
                    'HID\VID_054C&PID_0CE6'
                )

            { Assert-Rc901aProductionInfPolicy -InfPath $infPath } | Should Throw
        }

        It 'rejects a different provider' {
            $infPath = Join-Path $TestDrive 'Rc901aHidFilter.inf'
            New-Rc901aInfFixture -Path $infPath -Provider 'OtherVendor'

            { Assert-Rc901aProductionInfPolicy -InfPath $infPath } | Should Throw
        }

        It 'rejects a path-bearing or unbounded ServiceBinary value' {
            $infPath = Join-Path $TestDrive 'Rc901aHidFilter.inf'
            New-Rc901aInfFixture -Path $infPath -BinaryName '..\Unexpected.dll'

            { Assert-Rc901aProductionInfPolicy -InfPath $infPath } | Should Throw
        }
    }

    Describe 'Get-Rc901aProductionPackageFiles' {
        It 'accepts only the INF, catalog, and INF-referenced binary' {
            $packageDirectory = Join-Path $TestDrive 'package'
            New-Item -ItemType Directory -Path $packageDirectory | Out-Null
            New-Rc901aInfFixture -Path (Join-Path $packageDirectory 'Rc901aHidFilter.inf')
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'rc901ahidfilter.cat') | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'Rc901aUmdfCapture.dll') | Out-Null

            $package = Get-Rc901aProductionPackageFiles -Directory $packageDirectory

            [System.IO.Path]::GetFileName($package.InfPath) | Should Be 'Rc901aHidFilter.inf'
            [System.IO.Path]::GetFileName($package.CatalogPath) | Should Be 'rc901ahidfilter.cat'
            [System.IO.Path]::GetFileName($package.BinaryPath) | Should Be 'Rc901aUmdfCapture.dll'
        }

        It 'rejects an unexpected executable or metadata payload' {
            $packageDirectory = Join-Path $TestDrive 'package-with-extra-file'
            New-Item -ItemType Directory -Path $packageDirectory | Out-Null
            New-Rc901aInfFixture -Path (Join-Path $packageDirectory 'Rc901aHidFilter.inf')
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'Rc901aHidFilter.cat') | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'Rc901aUmdfCapture.dll') | Out-Null
            New-Item -ItemType File -Path (Join-Path $packageDirectory 'setup.exe') | Out-Null

            { Get-Rc901aProductionPackageFiles -Directory $packageDirectory } | Should Throw
        }
    }

    Describe 'Invoke-Rc901aSignToolVerification' {
        BeforeEach {
            Mock Invoke-Rc901aExternalTool {
                [pscustomobject]@{
                    ExitCode = 0
                    Output = @('Successfully verified')
                }
            }
        }

        It 'checks kernel policy and catalog membership for the INF and binary' {
            $checks = Invoke-Rc901aSignToolVerification `
                -SignToolPath 'C:\tools\signtool.exe' `
                -CatalogPath 'C:\package\Rc901aHidFilter.cat' `
                -InfPath 'C:\package\Rc901aHidFilter.inf' `
                -BinaryPath 'C:\package\Rc901aUmdfCapture.dll'

            $checks.Count | Should Be 3
            @($checks | Where-Object { $_.Name -eq 'KernelPolicy' }).Count | Should Be 1
            @($checks | Where-Object { $_.Name -eq 'InfCatalogMembership' }).Count | Should Be 1
            @($checks | Where-Object { $_.Name -eq 'BinaryCatalogMembership' }).Count | Should Be 1
            Assert-MockCalled Invoke-Rc901aExternalTool -Times 3
        }

        It 'fails closed when any SignTool verification fails' {
            Mock Invoke-Rc901aExternalTool {
                param($FilePath, $ArgumentList)

                if ($ArgumentList -contains 'C:\package\Rc901aUmdfCapture.dll') {
                    return [pscustomobject]@{
                        ExitCode = 1
                        Output = @('Catalog membership failed')
                    }
                }

                [pscustomobject]@{
                    ExitCode = 0
                    Output = @('Successfully verified')
                }
            }

            {
                Invoke-Rc901aSignToolVerification `
                    -SignToolPath 'C:\tools\signtool.exe' `
                    -CatalogPath 'C:\package\Rc901aHidFilter.cat' `
                    -InfPath 'C:\package\Rc901aHidFilter.inf' `
                    -BinaryPath 'C:\package\Rc901aUmdfCapture.dll'
            } | Should Throw
        }
    }

    Describe 'Get-Rc901aProductionPackage' {
        It 'returns a hash-addressed production-ready report after every gate passes' {
            $packageDirectory = Join-Path $TestDrive 'production-package'
            New-Item -ItemType Directory -Path $packageDirectory | Out-Null
            New-Rc901aInfFixture -Path (Join-Path $packageDirectory 'Rc901aHidFilter.inf')
            [System.IO.File]::WriteAllBytes(
                (Join-Path $packageDirectory 'Rc901aHidFilter.cat'),
                [byte[]](1, 2, 3))
            [System.IO.File]::WriteAllBytes(
                (Join-Path $packageDirectory 'Rc901aUmdfCapture.dll'),
                [byte[]](4, 5, 6))

            Mock Get-Rc901aCatalogSignatureMetadata {
                [pscustomobject]@{
                    Status = 'Valid'
                    SignerSubject = $microsoftHardwarePublisher
                    SignerThumbprint = '001122AABB'
                    EnhancedKeyUsageOids = @($attestationEku)
                }
            }
            Mock Resolve-Rc901aSignToolPath { 'C:\tools\signtool.exe' }
            Mock Invoke-Rc901aSignToolVerification {
                @(
                    [pscustomobject]@{ Name = 'KernelPolicy'; ExitCode = 0 },
                    [pscustomobject]@{ Name = 'InfCatalogMembership'; ExitCode = 0 },
                    [pscustomobject]@{ Name = 'BinaryCatalogMembership'; ExitCode = 0 }
                )
            }

            $report = Get-Rc901aProductionPackage -Directory $packageDirectory

            $report.ProductionReady | Should Be $true
            $report.HardwareId | Should Be $exactHardwareId
            $report.SignerSubject | Should Be $microsoftHardwarePublisher
            $report.Files.Count | Should Be 3
            @($report.Files | Where-Object { $_.Sha256 -match '^[A-F0-9]{64}$' }).Count | Should Be 3
            $report.SignToolChecks.Count | Should Be 3
        }
    }
}
