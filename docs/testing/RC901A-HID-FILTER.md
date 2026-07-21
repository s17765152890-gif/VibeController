# RC901A HID compatibility filter

This document records the evidence and safety boundary for restoring standard Windows HID input on the TCL `BT_RC901A_B1`. Pairing already works. The remaining failure is in the HID-over-GATT interpretation layer, not Bluetooth discovery.

## Observed device boundary

| Field | Observed value |
| --- | --- |
| Device name | `BT_RC901A_B1` |
| HID service | `00001812-0000-1000-8000-00805f9b34fb` |
| Vendor ID | `0416` |
| Product ID | `0301` |
| Product revision | `0003` |
| Exact hardware ID | `BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003` |
| Current driver | Microsoft `bthleenum.inf`, version `10.0.26100.8521` |
| Current state | Started, problem code `0x00000000`, no device UpperFilters/LowerFilters |

The Bluetooth address and full device instance ID are intentionally not recorded in source control.

## Failure evidence

With the Microsoft HID-over-GATT package (`hidbthle.inf`) selected, Windows stopped the HID service device with Code 10 and reported:

> A non constant main item was declared without a corresponding usage.

Switching that one service node to the generic Microsoft GATT driver (`bthleenum.inf`) removed the Device Manager error. It did not make button reports readable: WinRT and Win32 GATT report-map/report access still returned Access Denied. This proves the pairing, service enumeration, and PnP service PDO exist, but it does not prove that Windows can parse the remote's HID report descriptor.

## Selected architecture

The first driver package is a capture-only KMDF upper filter for the exact hardware ID above. Windows keeps the inbox `hidbthle`/`mshidumdf` stack. The filter post-processes only `IOCTL_HID_GET_REPORT_DESCRIPTOR`, copies the successful result into bounded nonpaged storage, and persists a diagnostic copy at PASSIVE_LEVEL.

The exact device registry key receives these diagnostic values after a successful interception:

- `Rc901aCapturedReportDescriptor` (`REG_BINARY`);
- `Rc901aCapturedReportDescriptorLength` (`REG_DWORD`);
- `Rc901aCapturedReportDescriptorSha256` (`REG_BINARY`, 32 bytes);
- `Rc901aCaptureStatus` (`REG_DWORD`, `RC901A_CAPTURE_RESULT`).

The filter forwards every other request without modification. Capture mode does not synthesize reports, write GATT characteristics, or change descriptor bytes.

Repair mode remains disabled until a physical capture provides all of:

1. raw descriptor bytes;
2. descriptor length and SHA-256;
3. the exact malformed main-item offset;
4. reviewed expected and replacement bytes;
5. managed and native regression tests.

When repair mode is eventually enabled, any mismatch must forward the original descriptor unchanged.

## Safety gates

1. Never bind a package to the generic HID service UUID, a name-only match, or a wildcard VID/PID. Only VID `0416`, PID `0301`, revision `0003` is permitted.
2. Never enable Windows `TESTSIGNING`, disable Secure Boot, or change BCD/boot policy without explicit user approval.
3. Capture first. Do not infer whether the malformed item should become Constant or receive a Usage.
4. Before installing a filter, save the current XML state, current INF, matching drivers, and uninstall/rollback commands.
5. An installation script must default to dry-run, recheck the exact hardware ID immediately before mutation, and have a tested inverse operation.
6. Do not touch RC901A's TCL DFU GATT service and do not write vendor characteristics.

## Baseline commands

Read-only state inspection:

```powershell
.\scripts\rc901a\Get-Rc901aDriverState.ps1 | Format-List *
```

Current test environment:

- Windows build reports `pnputil` version `10.0.26200`.
- Portable .NET SDK is used from the repository tool directory.
- Pester `3.4.0` is available system-wide.
- Visual Studio Build Tools 2022 `17.14.36` is installed at `D:\Dev\VisualStudio\2022\BuildTools` with MSVC `14.44.35228.0`, MSBuild `17.14.51`, and Windows SDK `10.0.26100.0`.
- The Build Tools installation is complete and launchable. A generic `PendingFileRenameOperations` marker remains after the completed restart, but RC901A and Build Tools state are healthy.
- WDK `10.1.26100.6584` is installed with KMDF through `1.35`, x64 libraries, Windows Driver MSBuild targets, and `InfVerif.exe`.
- Visual Studio components `Component.Microsoft.Windows.DriverKit.BuildTools` and `Microsoft.VisualStudio.Component.VC.14.44.17.14.x86.x64.Spectre` are installed. The `WindowsKernelModeDriver10.0` PlatformToolset and x64 Spectre libraries are present.
- A minimal KMDF project built successfully on 2026-07-22 and produced `KmdfToolchainProbe.sys` with signing disabled. This verifies compilation and linking only; no driver was installed and no boot or signing policy was changed.

Capture-filter verification on 2026-07-22:

- native bounded-copy and SHA-256 tests passed;
- the exact-match INF contract passed all six Pester tests;
- the KMDF IRP-forwarding contract passed all three Pester tests and enforces framework-owned dispatch for target and non-target requests;
- Debug x64 compilation, link, package signability, and catalog generation completed with no warnings;
- x64 `InfVerif.exe /w` completed with no findings;
- MSVC/WDK C/C++ Code Analysis completed with no findings;
- WDK 10.0.26100 reports that Static Driver Verifier is no longer included and is incompatible with Visual Studio 2022. No SDV result is claimed; using an older EWDK solely for SDV remains optional before distribution.

The WDK package-verifier task expects an x86 `InfVerif.dll` that this x64 WDK installation does not contain. The project therefore skips that broken in-build task and runs the installed x64 `InfVerif.exe /w` explicitly. Inf2Cat signability checking remains enabled. The package is unsigned and has not been installed.

## Controlled installation workflow

The installation and rollback entry points are:

```powershell
.\scripts\rc901a\Install-Rc901aCaptureFilter.ps1
.\scripts\rc901a\Uninstall-Rc901aCaptureFilter.ps1 -StatePath .\artifacts\rc901a-driver-state-before.json
```

Both commands default to a non-mutating preview. A real operation additionally requires `-Apply`; `-Apply -WhatIf` remains non-mutating. Installation rechecks the exact hardware ID immediately before `pnputil`, refuses any catalog whose Authenticode status is not `Valid`, and refuses to overwrite an existing rollback baseline. Rollback validates the recorded hardware ID plus the installed OEM package's original name and provider before removing it. Rollback deliberately does not require the broken package to remain trusted, because signature failure must never prevent recovery.

The live preview on 2026-07-22 found the exact RC901A device and returned `Mode=WhatIf`, `WillMutate=False`, and `CatalogSignatureStatus=NotSigned`. No rollback-state file was created and no PnP mutation was attempted. The full device instance ID remains local and uncommitted.

The current signing gate is therefore closed. Read-only inspection shows virtualization-based security enabled and kernel code-integrity policy enforced. Secure Boot and BCD test-signing state could not be read without elevation. Do not create trust certificates, enable `TESTSIGNING`, disable Secure Boot, or install the package without explicit user approval and a reboot/rollback plan.

## Rollback outline

Before first installation, the install script records the service instance state shown by the read-only script. The rollback script removes only the validated RC901A OEM filter package and verifies that Windows returned to the previously recorded Microsoft driver selection. It does not delete the Bluetooth pairing, remove class-wide filters, or touch Xbox/DualSense devices.

## Primary references

- [Microsoft BLE overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/bluetooth-low-energy-overview)
- [Preprocessing and postprocessing IRPs](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/preprocessing-and-postprocessing-irps)
- [`IOCTL_HID_GET_REPORT_DESCRIPTOR`](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidport/ni-hidport-ioctl_hid_get_report_descriptor)
- [Microsoft Firefly KMDF HID filter sample](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/firefly)
- [Microsoft vhidmini2 sample](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2)
