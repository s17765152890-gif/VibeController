# RC901A HID Descriptor Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore standard Windows HID input for the TCL `BT_RC901A_B1` by capturing its real HID report descriptor, identifying the exact malformed item, and applying a byte-exact repair in a KMDF upper filter that can only bind to RC901A hardware.

**Architecture:** Keep the repair below VibeController's application layer: Windows keeps the inbox `hidbthle`/`mshidumdf` HID-over-GATT stack, while an exact-device KMDF upper filter post-processes only `IOCTL_HID_GET_REPORT_DESCRIPTOR`. Capture mode records the untouched descriptor first. Repair mode activates only when the descriptor SHA-256 and expected original bytes match a checked-in patch manifest; every mismatch is fail-open and forwards the original descriptor. A .NET descriptor analyzer provides deterministic user-mode diagnosis and generates evidence before any kernel patch is enabled.

**Tech Stack:** .NET 8, xUnit, C17, KMDF, WDK/Visual Studio Build Tools, Windows HIDClass and HID-over-GATT inbox drivers, PowerShell, Driver Verifier/InfVerif.

---

### Task 1: Record the device boundary, baseline, and rollback procedure

**Files:**
- Create: `docs/testing/RC901A-HID-FILTER.md`
- Create: `scripts/rc901a/Get-Rc901aDriverState.ps1`
- Test: `tests/scripts/Get-Rc901aDriverState.Tests.ps1`

- [x] **Step 1: Write the failing script contract test**

The test imports the script as functions only and verifies that `Test-Rc901aHardwareId` accepts exactly:

```text
BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003
```

It must reject a different VID, PID, revision, service UUID, and a generic `BTHLEDevice` ID.

- [x] **Step 2: Run the focused test and confirm RED**

Run:

```powershell
Invoke-Pester tests/scripts/Get-Rc901aDriverState.Tests.ps1
```

Expected: failure because the script/functions do not exist. If Pester is unavailable, install it only for the current user and record its version in the verification note.

- [x] **Step 3: Implement the read-only baseline script**

The script must use read-only `pnputil /enum-devices ... /format xml`, return a structured object, and never call a mutating `pnputil` verb, `devcon`, `Disable-PnpDevice`, `Enable-PnpDevice`, registry write APIs, or BCD tools. (`Get-PnpDevice` is intentionally avoided because it stalls for more than 30 seconds on the development machine.) Report:

- exact instance ID and hardware IDs;
- current status/problem code;
- selected INF and driver provider/version;
- `UpperFilters`/`LowerFilters` as read-only values;
- whether the exact RC901A hardware match passed.

- [x] **Step 4: Document baseline and rollback**

`docs/testing/RC901A-HID-FILTER.md` must record the observed VID/PID/revision, current generic `bthleenum.inf` state, the prior `hidbthle.inf` Code 10 message, and these safety gates:

1. Never bind a test package to the generic HID service ID.
2. Never enable `TESTSIGNING`, disable Secure Boot, or change boot policy without explicit user approval.
3. Capture first; do not enable a descriptor patch without captured bytes and SHA-256.
4. Before hardware installation, export the current device driver state and create an uninstall/rollback command file.

- [x] **Step 5: Run the test GREEN and commit**

```powershell
Invoke-Pester tests/scripts/Get-Rc901aDriverState.Tests.ps1
git add docs/testing/RC901A-HID-FILTER.md scripts/rc901a tests/scripts
git commit -m "docs: define RC901A driver safety boundary"
```

### Task 2: Build a deterministic HID report-descriptor analyzer

**Files:**
- Create: `src/VibeController.Core/Devices/HidReportDescriptor.cs`
- Create: `src/VibeController.Core/Devices/HidReportDescriptorIssue.cs`
- Test: `tests/VibeController.Core.Tests/Devices/HidReportDescriptorTests.cs`

- [x] **Step 1: Write failing parser tests**

Cover short items with 0/1/2/4-byte payloads, the `0xFE` long-item form, global Push/Pop, local Usage/Usage Minimum/Usage Maximum, Collection/End Collection, and truncated input. Every parsed item must retain its byte offset, prefix, data bytes, item type, tag, and signed/unsigned values.

```csharp
[Fact]
public void Parse_RetainsOffsetAndPayload()
{
    var items = HidReportDescriptor.Parse([0x05, 0x0C, 0x09, 0x01, 0xA1, 0x01]);
    Assert.Equal([0, 2, 4], items.Select(item => item.Offset));
    Assert.Equal(0x0C, items[0].UnsignedValue);
}
```

- [x] **Step 2: Run the focused test and confirm RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests --filter FullyQualifiedName~HidReportDescriptorTests
```

Expected: compilation fails because `HidReportDescriptor` does not exist.

- [x] **Step 3: Implement the minimum parser**

The parser must reject malformed/truncated descriptors with an exception containing the failing byte offset. It must not normalize, mutate, or infer missing bytes.

- [x] **Step 4: Write failing semantic-diagnostic tests**

Use a valid consumer-control fixture and a malformed fixture in which a non-constant Input/Output/Feature main item has no corresponding local Usage or Usage range. Verify that diagnostics identify the main-item byte offset and use the same plain-language reason Windows reported.

- [x] **Step 5: Implement diagnostics and run GREEN**

Local items reset after every Main item. Constant main items are not flagged. Data main items without a local Usage/Usage range emit `MissingUsageForDataMainItem`; diagnostics are candidates, not automatic repairs.

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests --filter FullyQualifiedName~HidReportDescriptorTests
```

- [x] **Step 6: Commit the analyzer**

```powershell
git add src/VibeController.Core/Devices tests/VibeController.Core.Tests/Devices
git commit -m "feat: analyze HID report descriptors"
```

### Task 3: Add a fail-closed exact-patch manifest

**Files:**
- Create: `src/VibeController.Core/Devices/HidDescriptorPatch.cs`
- Create: `src/VibeController.Core/Devices/Rc901aDescriptorPatchManifest.cs`
- Test: `tests/VibeController.Core.Tests/Devices/HidDescriptorPatchTests.cs`
- Create: `tools/VibeController.Rc901aDescriptorTool/VibeController.Rc901aDescriptorTool.csproj`
- Create: `tools/VibeController.Rc901aDescriptorTool/Program.cs`
- Create: `tools/VibeController.Rc901aDescriptorTool/Rc901aDescriptorToolApplication.cs`
- Create: `tests/VibeController.Rc901aDescriptorTool.Tests/VibeController.Rc901aDescriptorTool.Tests.csproj`
- Create: `tests/VibeController.Rc901aDescriptorTool.Tests/Rc901aDescriptorToolApplicationTests.cs`
- Modify: `VibeController.sln`

- [x] **Step 1: Write failing patch-safety tests**

Verify a patch applies only if all conditions match: full descriptor length, SHA-256, byte offset, and expected original bytes. Verify wrong hash, wrong length, out-of-range offset, and already-modified bytes return an unchanged copy plus a refusal reason.

- [x] **Step 2: Run RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests --filter FullyQualifiedName~HidDescriptorPatchTests
```

- [x] **Step 3: Implement immutable exact patching**

The implementation returns a new byte array and never edits caller-owned memory. The initial `Rc901aDescriptorPatchManifest` has no active patch because the real report map has not yet been captured.

- [x] **Step 4: Write the CLI acceptance test, then implement the CLI**

Given a binary descriptor path, the CLI prints length, uppercase hex, SHA-256, parsed items, and diagnostics. `--apply-rc901a-patch` must refuse while the manifest is inactive. It must never access Bluetooth or write the registry.

- [x] **Step 5: Run GREEN and commit**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests
.\.tools\dotnet\dotnet.exe run --project tools\VibeController.Rc901aDescriptorTool -- --help
git add src/VibeController.Core tests/VibeController.Core.Tests tools/VibeController.Rc901aDescriptorTool
git commit -m "feat: add safe RC901A descriptor diagnostics"
```

### Task 4: Scaffold the exact-device KMDF capture filter

**Files:**
- Create: `drivers/Rc901aHidFilter/Rc901aHidFilter.sln`
- Create: `drivers/Rc901aHidFilter/driver/Rc901aHidFilter.vcxproj`
- Create: `drivers/Rc901aHidFilter/driver/Driver.c`
- Create: `drivers/Rc901aHidFilter/driver/Device.c`
- Create: `drivers/Rc901aHidFilter/driver/Device.h`
- Create: `drivers/Rc901aHidFilter/driver/DescriptorCapture.c`
- Create: `drivers/Rc901aHidFilter/driver/DescriptorCapture.h`
- Create: `drivers/Rc901aHidFilter/driver/Rc901aHidFilter.inx`
- Create: `drivers/Rc901aHidFilter/driver/Rc901aHidFilter.vcxproj.filters`
- Create: `drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.vcxproj`
- Create: `drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.c`

- [x] **Step 1: Install or locate a supported driver build toolchain**

Use Visual Studio Build Tools plus the Windows SDK and WDK whose versions are supported together. Do not install the driver or alter boot configuration. Record exact versions in `docs/testing/RC901A-HID-FILTER.md`.

- [x] **Step 2: Write failing native tests for bounded capture copying**

Test zero length, maximum accepted descriptor length, oversized length, null source, and a normal descriptor. The portable capture helper copies into caller-owned storage and never allocates or touches kernel APIs.

- [x] **Step 3: Build native tests and confirm RED**

```powershell
msbuild drivers\Rc901aHidFilter\tests\DescriptorCaptureTests.vcxproj /p:Configuration=Debug /p:Platform=x64
```

- [x] **Step 4: Implement the minimum capture helper and filter**

The driver must:

- call `WdfFdoInitSetFilter`;
- register a WDM preprocess callback for `IRP_MJ_DEVICE_CONTROL`;
- inspect only `IOCTL_HID_GET_REPORT_DESCRIPTOR`;
- use `IoCopyCurrentIrpStackLocationToNext` plus `IoSetCompletionRoutine` for postprocessing;
- copy only a successful, bounded `Irp->UserBuffer` result;
- queue a PASSIVE_LEVEL work item that writes `Rc901aCapturedReportDescriptor`, length, SHA-256, and capture status under the exact device's PnP registry key;
- forward every other request unchanged;
- never block report reads, write GATT characteristics, synthesize input, or patch bytes in capture-only mode.

- [x] **Step 5: Create an exact-match extension INF**

The only model match is:

```text
BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003
```

The package includes/needs the inbox `hidbthle.inf` sections and adds `Rc901aHidFilter` as an `UpperFilters` entry. `ExcludeFromSelect=*` and `PnpLockdown=1` are required. No generic service UUID, name-only match, wildcard VID/PID, Xbox, or DualSense ID is allowed.

- [x] **Step 6: Run static/build verification GREEN**

```powershell
msbuild drivers\Rc901aHidFilter\Rc901aHidFilter.sln /p:Configuration=Debug /p:Platform=x64
InfVerif.exe /w drivers\Rc901aHidFilter\driver\x64\Debug\Rc901aHidFilter.inf
```

Run Code Analysis/SDV rules relevant to request completion and buffer lifetime. Treat every warning as a failure.

- [ ] **Step 7: Commit the capture filter**

```powershell
git add drivers/Rc901aHidFilter docs/testing/RC901A-HID-FILTER.md
git commit -m "feat: capture RC901A HID report descriptor"
```

### Task 5: Perform a controlled real-device capture

**Files:**
- Create: `scripts/rc901a/Install-Rc901aCaptureFilter.ps1`
- Create: `scripts/rc901a/Uninstall-Rc901aCaptureFilter.ps1`
- Create: `artifacts/rc901a-driver-state-before.json` (verification only; ignored)
- Create: `artifacts/rc901a-report-descriptor.bin` (verification only; ignored)
- Create: `artifacts/rc901a-report-descriptor-analysis.txt` (verification only; ignored)
- Modify: `docs/testing/RC901A-HID-FILTER.md`

- [x] **Step 1: Write script dry-run tests before installer code**

Tests verify both scripts default to `-WhatIf`, refuse any non-exact hardware ID, refuse an unsigned/untrusted package, and print the inverse rollback action. No test invokes `pnputil` or changes PnP state.

- [x] **Step 2: Implement dry-run-first installation scripts**

Installation requires an explicit `-Apply` switch and an exact instance-ID recheck immediately before each mutation. Uninstall removes only the RC901A filter package and restores the previously recorded driver selection.

- [ ] **Step 3: Stop at the signing/boot-policy gate**

Present the built package, dry-run output, and rollback procedure to the user. Obtain explicit approval before enabling test-signing or changing any boot setting. Prefer a normally trusted development certificate path if the machine permits it.

- [ ] **Step 4: Capture the real descriptor**

After approved installation, rebind RC901A to the inbox HID-over-GATT stack plus the filter. Read the registry capture, save the raw binary to ignored `artifacts`, run the descriptor CLI, and compare its diagnostic offset with the exact Windows Code 10 message.

- [ ] **Step 5: Roll back immediately if capture does not occur**

Do not add a guessed patch. Preserve logs, uninstall the filter, restore the previously recorded driver, and document whether interception occurred before or after the UMDF failure.

- [ ] **Step 6: Commit only scripts and redacted evidence notes**

Never commit device instance paths containing the Bluetooth address. Commit descriptor bytes only if they contain no unique device data and are needed for the public compatibility patch.

### Task 6: Implement and validate the byte-exact repair

**Files:**
- Modify: `src/VibeController.Core/Devices/Rc901aDescriptorPatchManifest.cs`
- Modify: `drivers/Rc901aHidFilter/driver/DescriptorCapture.c`
- Create: `drivers/Rc901aHidFilter/driver/Rc901aDescriptorPatch.h`
- Modify: `drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.c`
- Modify: `docs/testing/RC901A-HID-FILTER.md`

- [ ] **Step 1: Add failing tests from the captured descriptor**

The managed and native fixtures must use the captured descriptor verbatim. Tests assert the captured SHA-256, the exact diagnostic offset, expected original bytes, replacement bytes, unchanged descriptor length, and zero remaining parser issue of the targeted type.

- [ ] **Step 2: Confirm RED in both test suites**

The tests fail because the manifest is inactive and the driver returns the original bytes.

- [ ] **Step 3: Enable one exact repair**

Generate the C patch header from the reviewed manifest. In the completion routine, mutate only when VID/PID/revision were already constrained by the INF and the descriptor length, full SHA-256, offset, and original bytes all match. On any mismatch, keep the original descriptor and record `PatchRefused`.

- [ ] **Step 4: Run managed, native, driver, INF, and static verification GREEN**

```powershell
.\.tools\dotnet\dotnet.exe test VibeController.sln --configuration Release
msbuild drivers\Rc901aHidFilter\Rc901aHidFilter.sln /p:Configuration=Release /p:Platform=x64
InfVerif.exe /w drivers\Rc901aHidFilter\driver\x64\Release\Rc901aHidFilter.inf
```

- [ ] **Step 5: Validate on hardware with rollback ready**

Acceptance evidence:

1. RC901A device and child HID collections start without Code 10.
2. Windows Raw Input receives direction/OK/back/media key reports.
3. Xbox and DualSense inputs remain unchanged.
4. VibeController receives RC901A controls through the normal HID path or a documented thin adapter.
5. Uninstall restores the prior state without leaving a class-wide filter.

- [ ] **Step 6: Commit the repair only after hardware evidence**

```powershell
git add src/VibeController.Core tests drivers/Rc901aHidFilter docs/testing/RC901A-HID-FILTER.md
git commit -m "fix: repair RC901A HID report descriptor"
```

### Task 7: Package the compatibility component safely

**Files:**
- Modify: `README.md`
- Modify: `PRD.md`
- Create: `docs/RC901A-DRIVER-INSTALL.md`
- Modify: `.github/workflows/release.yml`

- [ ] **Step 1: Document support and limitations**

Explain that pairing success and HID input are different layers, the package supports only VID `0416`/PID `0301`/REV `0003`, remote microphone audio is still out of scope, and production Windows distribution requires a properly signed driver package.

- [ ] **Step 2: Add CI build verification without publishing unsigned drivers**

CI builds and runs managed/native tests plus InfVerif. Unsigned/test-signed `.sys`/`.cat` files remain workflow artifacts and are not added to the public VibeController release.

- [ ] **Step 3: Run complete release verification**

```powershell
.\.tools\dotnet\dotnet.exe test VibeController.sln --configuration Release
npm --prefix frontend test -- --run
npm --prefix frontend run build
.\.tools\dotnet\dotnet.exe publish src\VibeController.App -c Release -r win-x64 --self-contained true
```

Also run the x64 driver build, InfVerif, native tests, install-script dry run, physical-button smoke test, and uninstall/reinstall rollback test.

- [ ] **Step 4: Commit documentation and CI**

```powershell
git add README.md PRD.md docs/RC901A-DRIVER-INSTALL.md .github/workflows/release.yml
git commit -m "docs: add RC901A compatibility driver guide"
```
