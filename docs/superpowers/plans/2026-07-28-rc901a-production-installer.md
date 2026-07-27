# RC901A Production Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a one-click, production-signing-ready Windows installer that installs VibeController and the exact TCL RC901A compatibility driver with one UAC prompt, while refusing test-signed, mismatched, or unverifiable driver packages and preserving a deterministic rollback path.

**Architecture:** Keep Microsoft's returned, production-signed RC901A driver package outside source control and treat it as a release input. A read-only release validator verifies the exact INF boundary, Microsoft hardware signature, catalog membership, and file hashes before any installer is built. A small .NET driver-setup executable owns exact-device installation, state capture, repair, and rollback. WiX v5 Burn chains that helper with a per-machine MSI for the desktop app, providing standard Windows install/upgrade/uninstall behavior and one elevation boundary. Normal source ZIP builds remain available and never silently include a development driver.

**Tech Stack:** PowerShell 5.1 / Pester 3.4, .NET 8 / C#, xUnit, Windows PnPUtil, SignTool, WiX Toolset v5 Burn + MSI, GitHub Actions on `windows-latest`.

---

## Non-negotiable release boundary

- Exact supported hardware ID:
  `BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003`.
- The package may contain only `Rc901aHidFilter.inf`, `Rc901aHidFilter.cat`, the one binary referenced by `ServiceBinary`, and Microsoft-returned signing metadata files explicitly allowlisted by the release builder.
- A production installer must reject the current temporary subject
  `CN=VibeController RC901A Temporary Driver Test`, any unsigned catalog, a valid but non-Microsoft catalog, generic BLE/HID matches, wildcard IDs, Xbox/DualSense IDs, unexpected binaries, and mismatched catalog members.
- Production packaging requires SignTool kernel-policy verification plus catalog-membership verification for the INF and referenced binary. `Get-AuthenticodeSignature` is diagnostic evidence, not the sole trust decision.
- The repository never contains a private signing key, certificate password, Partner Center credential, or returned production driver binary.
- Installation never changes Secure Boot, BCD, test-signing, root certificate stores, Bluetooth pairing, or devices other than the exact RC901A service device.
- The Power button and remote microphone audio remain out of scope. Pairing guidance remains center `OK + Back` for about five seconds.
- If a production-signed package is not supplied, the installer build stops with a clear error. It must not fall back to the development package.

## External production-signing handoff

The implementation can be completed and tested with fake metadata and the existing development driver, but the final release gate needs an organization-controlled signing identity and the package returned by Microsoft Partner Center. Microsoft currently requires a valid registered signing certificate for dashboard submissions, always returns its own approved catalog, and documents Microsoft hardware EKUs for distinguishing attestation/HLK results:

- <https://learn.microsoft.com/en-gb/windows-hardware/drivers/dashboard/code-signing-reqs>
- <https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-validate>
- <https://learn.microsoft.com/en-us/windows-hardware/drivers/install/installing-a-release-signed-driver-package>

### Task 1: Freeze and test the production driver-package policy

**Files:**
- Create: `scripts/rc901a/Test-Rc901aProductionPackage.ps1`
- Create: `tests/scripts/Rc901aProductionPackage.Tests.ps1`
- Modify: `scripts/rc901a/Install-Rc901aCaptureFilter.ps1`

- [x] **Step 1: Write failing Pester policy tests**

Add tests for pure policy functions and the filesystem wrapper. Require:

1. `Valid` catalog status.
2. signer subject `CN=Microsoft Windows Hardware Compatibility Publisher`.
3. at least one accepted Windows Hardware Driver Verification EKU.
4. explicit rejection of the temporary VibeController signer.
5. exactly one model line and the exact RC901A ID.
6. provider `VibeController`, INF name `Rc901aHidFilter.inf`, catalog name `Rc901aHidFilter.cat`, and one basename-only `ServiceBinary`.
7. SignTool checks for kernel policy and catalog membership of both INF and binary.

Example:

```powershell
It 'rejects the temporary development signer even when status is Valid' {
    {
        Assert-Rc901aProductionSignatureMetadata `
            -Status Valid `
            -SignerSubject 'CN=VibeController RC901A Temporary Driver Test' `
            -EnhancedKeyUsageOids @('1.3.6.1.4.1.311.10.3.5.1')
    } | Should Throw
}
```

- [x] **Step 2: Run the new test and verify RED**

Run:

```powershell
Invoke-Pester -Script tests\scripts\Rc901aProductionPackage.Tests.ps1
```

Expected: tests fail because `Test-Rc901aProductionPackage.ps1` and its policy functions do not exist.

- [x] **Step 3: Implement a read-only, fail-closed validator**

Implement:

```powershell
Assert-Rc901aProductionSignatureMetadata
Assert-Rc901aProductionInfPolicy
Get-Rc901aProductionPackage
Invoke-Rc901aSignToolVerification
Test-Rc901aProductionPackage
```

The script entry point returns a serializable report containing the exact hardware ID, driver version, provider, file names, SHA-256 hashes, signer subject, signer thumbprint, accepted hardware EKU, and SignTool results. It performs no mutations.

Update `Install-Rc901aCaptureFilter.ps1` so `-Apply` accepts a package only after the production validator has returned `ProductionReady = $true`. Keep development installation available only through an explicit `-AllowTestPackage` switch that is absent from release/installer callers and whose plan output says `DevelopmentOnly = true`.

- [x] **Step 4: Run focused and existing script tests GREEN**

Run:

```powershell
Invoke-Pester -Script @(
    'tests\scripts\Rc901aProductionPackage.Tests.ps1',
    'tests\scripts\Rc901aCaptureFilterScripts.Tests.ps1'
)
```

Expected: every test passes, preview mode remains non-mutating, and no test invokes real PnPUtil.

- [x] **Step 5: Commit the production policy unit**

```powershell
git add scripts/rc901a/Test-Rc901aProductionPackage.ps1 scripts/rc901a/Install-Rc901aCaptureFilter.ps1 tests/scripts/Rc901aProductionPackage.Tests.ps1
git commit -m "feat: validate production RC901A driver packages"
```

### Task 2: Build a deterministic installer payload and manifest

**Files:**
- Create: `scripts/release/New-InstallerPayload.ps1`
- Create: `scripts/release/Test-InstallerPayload.ps1`
- Create: `tests/scripts/InstallerPayload.Tests.ps1`
- Modify: `scripts/package-release.ps1`
- Modify: `.gitignore`

- [ ] **Step 1: Write failing payload tests**

Use `$TestDrive` fixtures to prove that the builder:

- accepts an app publish directory and a production-ready RC901A validation report;
- copies only allowlisted app and driver files;
- emits stable, ordinally sorted relative paths and uppercase SHA-256 hashes;
- records schema version, product version, architecture, exact hardware ID, driver version, signer thumbprint, and build commit;
- refuses paths outside its staging root, symlinks/reparse points, duplicate case-insensitive names, test signers, missing files, and stale hashes;
- never writes secrets or absolute developer-machine paths into the manifest.

- [ ] **Step 2: Run the new test and verify RED**

```powershell
Invoke-Pester -Script tests\scripts\InstallerPayload.Tests.ps1
```

Expected: failures because the payload scripts do not exist.

- [ ] **Step 3: Implement staging and independent verification**

Create this layout under `artifacts/installer/<version>/payload`:

```text
app/
driver/
installer-manifest.json
LICENSE
```

`New-InstallerPayload.ps1` must call the Task 1 validator itself, not trust a caller-created JSON report. `Test-InstallerPayload.ps1` independently recalculates every hash and re-runs the driver policy. Add an explicit `-Installer` switch to `package-release.ps1`; without it, the existing ZIP remains driver-free and unchanged.

- [ ] **Step 4: Run payload and legacy release verification**

```powershell
Invoke-Pester -Script tests\scripts\InstallerPayload.Tests.ps1
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -Version 1.2.0
powershell -ExecutionPolicy Bypass -File scripts\verify-release.ps1 -Version 1.2.0
```

Expected: tests pass and the normal ZIP contains no `driver` directory.

- [ ] **Step 5: Commit the deterministic payload unit**

```powershell
git add scripts/release scripts/package-release.ps1 tests/scripts/InstallerPayload.Tests.ps1 .gitignore
git commit -m "feat: stage deterministic installer payloads"
```

### Task 3: Implement the reversible RC901A driver setup helper

**Files:**
- Create: `src/VibeController.DriverSetup/VibeController.DriverSetup.csproj`
- Create: `src/VibeController.DriverSetup/Program.cs`
- Create: `src/VibeController.DriverSetup/DriverInstallPlan.cs`
- Create: `src/VibeController.DriverSetup/DriverInstaller.cs`
- Create: `src/VibeController.DriverSetup/InstallerJournal.cs`
- Create: `src/VibeController.DriverSetup/WindowsDriverStore.cs`
- Create: `tests/VibeController.DriverSetup.Tests/VibeController.DriverSetup.Tests.csproj`
- Create: `tests/VibeController.DriverSetup.Tests/DriverInstallerTests.cs`
- Modify: `VibeController.sln`

- [ ] **Step 1: Write failing install/repair/uninstall state-machine tests**

Against fake filesystem, device-state, and process transports, require:

- `plan` is read-only and exact-device scoped;
- `install` captures the prior INF before staging;
- the journal is durably written before device binding;
- failure after staging removes only the newly published RC901A OEM INF;
- successful install verifies the exact device is `Started`, has problem code `0`, and uses the recorded published INF;
- `repair` is idempotent;
- `uninstall` removes only the recorded package and verifies the prior INF is restored;
- malformed, missing, or mismatched journals fail closed;
- all commands produce stable exit codes suitable for WiX Burn.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test tests\VibeController.DriverSetup.Tests\VibeController.DriverSetup.Tests.csproj
```

Expected: compilation fails because the project does not exist.

- [ ] **Step 3: Implement the helper**

Use these commands:

```text
VibeController.DriverSetup.exe plan --payload <path>
VibeController.DriverSetup.exe install --payload <path> --state <path>
VibeController.DriverSetup.exe repair --payload <path> --state <path>
VibeController.DriverSetup.exe uninstall --state <path>
```

The production executable accepts only a manifest already validated in Task 2, verifies it again before mutation, and stores its journal under `%ProgramData%\VibeController\installer\rc901a-driver-state.json`. Do not add a bypass flag to the production executable.

- [ ] **Step 4: Run helper and existing driver tests**

```powershell
dotnet test tests\VibeController.DriverSetup.Tests\VibeController.DriverSetup.Tests.csproj
Invoke-Pester -Script tests\scripts\Rc901aCaptureFilterScripts.Tests.ps1
```

Expected: all tests pass with no real driver mutation.

- [ ] **Step 5: Commit the setup helper**

```powershell
git add src/VibeController.DriverSetup tests/VibeController.DriverSetup.Tests VibeController.sln
git commit -m "feat: add reversible RC901A driver setup helper"
```

### Task 4: Create the WiX v5 MSI and one-click Burn bundle

**Files:**
- Create: `installer/VibeController.Package/VibeController.Package.wixproj`
- Create: `installer/VibeController.Package/Product.wxs`
- Create: `installer/VibeController.Bundle/VibeController.Bundle.wixproj`
- Create: `installer/VibeController.Bundle/Bundle.wxs`
- Create: `installer/VibeController.Bundle/Bundle.wxl`
- Create: `tests/scripts/WixInstallerContract.Tests.ps1`

- [ ] **Step 1: Write failing installer-contract tests**

Require a per-machine x64 MSI, stable upgrade code, `%ProgramFiles%\VibeController`, Start Menu shortcut, Add/Remove Programs metadata, major-upgrade behavior, and no PowerShell custom action. Require a compressed Burn bundle that chains:

1. the app MSI;
2. the self-contained driver helper as a per-machine `ExePackage` with explicit install/repair/uninstall commands and checked exit codes.

Also assert that WiX v5's removed `DifxApp` extension is not used.

- [ ] **Step 2: Run the contract test and verify RED**

```powershell
Invoke-Pester -Script tests\scripts\WixInstallerContract.Tests.ps1
```

Expected: failures because the WiX projects do not exist.

- [ ] **Step 3: Implement MSI and Burn authoring**

Use WiX v5 `WixStandardBootstrapperApplication`, keep the bundle per-machine, embed all payloads, and let Burn own elevation so the user sees one UAC prompt. The MSI installs only application files. The driver helper owns all PnP mutations and rollback; do not run arbitrary command strings from MSI custom actions.

- [ ] **Step 4: Build and inspect the unsigned development bundle**

```powershell
dotnet build installer\VibeController.Bundle\VibeController.Bundle.wixproj -c Release
Invoke-Pester -Script tests\scripts\WixInstallerContract.Tests.ps1
```

Expected: `VibeController-Setup-1.2.0.exe` builds and all static contracts pass. This artifact is development-only and cannot pass the production release gate.

- [ ] **Step 5: Commit installer authoring**

```powershell
git add installer tests/scripts/WixInstallerContract.Tests.ps1
git commit -m "feat: add one-click Windows installer bundle"
```

### Task 5: Add production signing and release gates without storing secrets

**Files:**
- Create: `scripts/release/Build-ProductionInstaller.ps1`
- Create: `scripts/release/Test-ProductionInstaller.ps1`
- Create: `tests/scripts/ProductionInstaller.Tests.ps1`
- Create: `.github/workflows/installer.yml`
- Modify: `scripts/verify-release.ps1`

- [ ] **Step 1: Write failing release-gate tests**

Require production mode to fail if any of these are absent or invalid:

- Microsoft-signed RC901A package;
- valid Authenticode signatures on app MSI, driver helper, Burn engine, and final bundle;
- trusted timestamp;
- exact product/file versions;
- manifest hashes;
- clean source commit and immutable build commit recorded in the manifest.

Require logs to redact certificate-provider secrets and never print environment values.

- [ ] **Step 2: Run tests and verify RED**

```powershell
Invoke-Pester -Script tests\scripts\ProductionInstaller.Tests.ps1
```

Expected: failures because production build and verification scripts do not exist.

- [ ] **Step 3: Implement fail-closed signing hooks**

`Build-ProductionInstaller.ps1` receives signing commands through a release-machine configuration file outside the repository, signs the MSI/helper/Burn engine/bundle in the correct order, and then invokes `Test-ProductionInstaller.ps1`. It must not accept a “skip signing” option in production mode.

The GitHub workflow is manual and accepts only a previously Microsoft-signed driver artifact plus configured repository/environment secrets for application signing. Pull requests run validation tests but never receive signing credentials.

- [ ] **Step 4: Verify unsigned rejection and configured dry build**

```powershell
Invoke-Pester -Script tests\scripts\ProductionInstaller.Tests.ps1
powershell -ExecutionPolicy Bypass -File scripts\release\Test-ProductionInstaller.ps1 -InstallerPath artifacts\installer\VibeController-Setup-1.2.0.exe
```

Expected: tests pass and the current unsigned artifact is deliberately rejected.

- [ ] **Step 5: Commit release gates**

```powershell
git add scripts/release tests/scripts/ProductionInstaller.Tests.ps1 .github/workflows/installer.yml scripts/verify-release.ps1
git commit -m "ci: gate production installer signing"
```

### Task 6: Document, install, roll back, and perform clean-machine acceptance

**Files:**
- Modify: `README.md`
- Rewrite: `docs/release/QUICKSTART.md`
- Create: `docs/release/INSTALLER.md`
- Modify: `docs/testing/RC901A-HID-FILTER.md`
- Create locally: `artifacts/installer-acceptance/<version>/`

- [ ] **Step 1: Add documentation contract tests**

Require bilingual instructions to distinguish:

- normal ZIP without RC901A driver;
- production installer with Microsoft-signed exact-device driver;
- pairing with `OK + Back`;
- repair/uninstall/rollback;
- Power and remote microphone audio exclusions;
- no Secure Boot, BCD, test-signing, or root-certificate changes.

- [ ] **Step 2: Rewrite the currently corrupted quick-start encoding**

Save `docs/release/QUICKSTART.md` as UTF-8 without BOM and verify both Chinese and English text render correctly.

- [ ] **Step 3: Run clean Windows acceptance**

On a Windows 11 x64 machine with Secure Boot enabled, test signing disabled, and no VibeController test root certificate:

1. pair the RC901A using `OK + Back`;
2. double-click the signed bundle;
3. observe one friendly-publisher UAC prompt;
4. verify the app, shortcut, Add/Remove Programs entry, driver state, 22 safe buttons, Xbox, and DualSense;
5. run repair;
6. uninstall and verify the prior inbox driver is restored;
7. reinstall and upgrade from the previous app version;
8. confirm neither the Power key nor microphone audio is captured.

- [ ] **Step 4: Record evidence**

Store installer log, manifest, SHA-256, Windows version, Secure Boot/test-signing state, driver signer/EKU, before/after driver state, hardware firmware, and rollback result. Do not store machine secrets, Bluetooth keys, certificate private material, or user paths.

- [ ] **Step 5: Run all automated verification and commit docs**

```powershell
powershell -ExecutionPolicy Bypass -File scripts\test.ps1
Invoke-Pester -Script tests\scripts
dotnet build installer\VibeController.Bundle\VibeController.Bundle.wixproj -c Release
```

Expected: all automated tests pass; production release remains blocked until the Microsoft-returned driver and application-signing identity are configured.

```powershell
git add README.md docs/release docs/testing/RC901A-HID-FILTER.md
git commit -m "docs: explain production RC901A installation"
```

## Verification checklist

- The normal ZIP stays driver-free.
- Production packaging cannot consume the temporary test-signed driver.
- Every driver mutation is exact-device scoped and journaled before binding.
- Install failure triggers rollback; uninstall verifies restoration.
- Burn owns the sole elevation boundary and standard Windows lifecycle.
- Production signatures and timestamps are independently verified.
- Current source and CI remain buildable without access to private signing material.
- Clean-machine acceptance uses normal Windows security settings.
