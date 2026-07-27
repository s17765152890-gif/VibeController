# RC901A HID compatibility filter

This document records the evidence and safety boundary for restoring Windows input on the TCL `BT_RC901A_B1`. Bluetooth pairing works, while the inbox HID-over-GATT parser rejects this firmware's report descriptor. The current experimental driver and physical acceptance state are recorded first; earlier capture milestones remain below as history.

## Current verified development state (2026-07-27)

The development machine has completed an end-to-end input acceptance pass for RC901A firmware `V1.0.192.6`.

| Field | Verified value |
| --- | --- |
| Driver provider | `VibeController` |
| Driver version | `1.0.0.6` dated `07/27/2026` |
| Installed INF | `oem182.inf` |
| Device state | Started, problem code `0x00000000` |
| Package used | `artifacts/rc901a-test-package-pass16-driver-channel` |
| Private interface | `{34826b0c-f006-44e1-ae98-a584b68c4ec1}`, exactly one present endpoint |
| Runtime probe | Parsed snapshot, `TotalReports=46`, `Records=32`, sequences `15..46` |

The package remains a temporary test-signed development build. It is not included in the public release and is not an end-user installation path. Public distribution requires Microsoft attestation signing or an equivalent production-signing route.

### Runtime transport

The app tries the driver's private snapshot interface first. On this HID stack, normal-user `CreateFile`/private-IOCTL access is rejected before the request reaches the UMDF filter (Win32 error 31). The managed transport therefore uses a bounded compatibility path:

1. enumerate the exact private interface and require it to be present;
2. try the private IOCTL;
3. if the upper HID stack blocks it, read the same driver-owned snapshot from the normal-user-readable UMDF Parameters key;
4. re-enumerate the interface once per second so unplugged hardware cannot be represented indefinitely by stale registry data.

The compatibility snapshot lives under:

```text
HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services\Rc901aUmdfCapture\Parameters
```

The reader validates protocol version, record size, count, history length, total count, and the final record sequence. A partially updated registry snapshot is rejected rather than decoded. The raw-input path remains a fallback only when the driver channel is unavailable; it is suppressed while the driver channel is healthy and can recover after a disconnect.

### Hardware-verified button table

These report-ID-`01` HID usage bytes were observed as physical press/release pairs and are the automatic default profile for firmware `V1.0.192.6`:

| Physical button | Usage byte |
| --- | --- |
| Up | `52` |
| Down | `51` |
| Left | `50` |
| Right | `4F` |
| OK | `28` |
| Menu | `65` |
| Back | `F1` |
| Home | `83` |
| Volume + | `ED` |
| Volume - | `EE` |
| Microphone | `AD` |
| Mute | `EF` |
| Input | `97` |
| Red | `99` |
| Green | `9A` |
| Blue | `9B` |
| Settings | `A8` |
| bilibili | `D1` |
| 奇异果 TV | `DE` |
| Side brightness + | `9E` |
| Side brightness - | `9F` |
| Side picture mode | `AA` |

The final ordered physical pass generated 44 report-ID-`01` records (22 press/release pairs) plus two report-ID-`E8` microphone auxiliary reports, for `TotalReports=46`. A deliberately inserted mouse left click between Menu and Back did not enter the RC901A stream. The first seven buttons were also verified independently before they rolled out of the 32-record ring buffer. Decoder tests confirm that the `E8` microphone auxiliary reports do not duplicate the logical `AD` microphone press/release.

Power is deliberately excluded because it can have system-level effects. The microphone button is an input button only: it may trigger Codex native dictation, but the remote's microphone audio transport is not implemented. Learning mode remains available only as an advanced compatibility override for a different firmware or an intentional custom binding.

### Verification and rollback evidence

- Managed tests cover snapshot parsing, stale-report suppression, raw fallback, driver recovery, microphone auxiliary filtering, the verified table, and inconsistent registry writes.
- Native descriptor/capture tests and the UMDF packaging contract pass independently of the managed suite.
- The normal-user production transport read the live driver snapshot successfully after the physical run.
- The pre-install rollback state is saved at `artifacts/rc901a-driver-state-before-pass16-driver-channel.json`; it records the previous `1.0.0.5` / `oem181.inf` selection.
- The install result is saved at `artifacts/rc901a-pass16-driver-channel-result.json` and records a successful install with no automatic rollback.

Preview rollback:

```powershell
.\scripts\rc901a\Uninstall-Rc901aCaptureFilter.ps1 `
  -StatePath .\artifacts\rc901a-driver-state-before-pass16-driver-channel.json
```

Apply rollback only from a visible elevated PowerShell after reviewing the preview:

```powershell
.\scripts\rc901a\Uninstall-Rc901aCaptureFilter.ps1 `
  -StatePath .\artifacts\rc901a-driver-state-before-pass16-driver-channel.json `
  -Apply
```

## Exact device boundary

| Field | Observed value |
| --- | --- |
| Device name | `BT_RC901A_B1` |
| HID service | `00001812-0000-1000-8000-00805f9b34fb` |
| Vendor ID | `0416` |
| Product ID | `0301` |
| Product revision | `0003` |
| Exact hardware ID | `BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003` |
| Inbox baseline | Microsoft `bthleenum.inf`, version `10.0.26100.8521` |
| Current development driver | VibeController `1.0.0.6`, Started, problem code `0x00000000` |

The Bluetooth address and full device instance ID are intentionally not recorded in source control.

## Failure evidence

With the Microsoft HID-over-GATT package (`hidbthle.inf`) selected, Windows stopped the HID service device with Code 10 and reported:

> A non constant main item was declared without a corresponding usage.

Switching that one service node to the generic Microsoft GATT driver (`bthleenum.inf`) removed the Device Manager error. It did not make button reports readable: WinRT and Win32 GATT report-map/report access still returned Access Denied. This proves the pairing, service enumeration, and PnP service PDO exist, but it does not prove that Windows can parse the remote's HID report descriptor.

## Initial capture architecture

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
.\scripts\rc901a\Start-Rc901aSecurityAudit.ps1
```

The second command requests administrator approval only so Windows will allow it to read Secure Boot, BitLocker, BCD, and Device Guard state. It writes an ignored local JSON report under `artifacts` and does not change boot, certificate, driver, device, or restart state.

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

The WDK package-verifier task expects an x86 `InfVerif.dll` that this x64 WDK installation does not contain. The project therefore skips that broken in-build task and runs the installed x64 `InfVerif.exe /w` explicitly. Inf2Cat signability checking remains enabled. At this historical checkpoint, the package was unsigned and had not yet been installed; the current installed development state is recorded at the top of this document.

## Controlled installation workflow

The installation and rollback entry points are:

```powershell
.\scripts\rc901a\Install-Rc901aCaptureFilter.ps1
.\scripts\rc901a\Uninstall-Rc901aCaptureFilter.ps1 -StatePath .\artifacts\rc901a-driver-state-before.json
```

Both commands default to a non-mutating preview. A real operation additionally requires `-Apply`; `-Apply -WhatIf` remains non-mutating. Installation rechecks the exact hardware ID immediately before `pnputil`, refuses any catalog whose Authenticode status is not `Valid`, and refuses to overwrite an existing rollback baseline. Rollback validates the recorded hardware ID plus the installed OEM package's original name and provider before removing it. Rollback deliberately does not require the broken package to remain trusted, because signature failure must never prevent recovery.

The live preview on 2026-07-22 found the exact RC901A device and returned `Mode=WhatIf`, `WillMutate=False`, and `CatalogSignatureStatus=NotSigned`. No rollback-state file was created and no PnP mutation was attempted. The full device instance ID remains local and uncommitted.

At the 2026-07-22 checkpoint, the signing gate was closed. The elevated read-only audit confirmed that Secure Boot was enabled, the system volume was fully decrypted with BitLocker protection off, `testsigning`, `nointegritychecks`, and kernel debugging were not configured in the current BCD entry, the hypervisor started automatically, and virtualization-based security plus kernel code-integrity enforcement were active. A later, explicitly approved one-boot test-signing workflow was used for physical development-machine validation. Do not repeat certificate, boot, or driver changes without explicit approval and a rollback plan.

## Preferred one-boot capture workflow

This workflow is for one controlled hardware capture on the development machine. It is not a release or end-user installation path.

```powershell
# Preview, then prepare an isolated signed package under ignored artifacts.
.\scripts\rc901a\New-Rc901aTestSignedPackage.ps1
.\scripts\rc901a\New-Rc901aTestSignedPackage.ps1 -Apply

# Trust only the exact temporary certificate recorded by that package.
# This launcher requests elevation but changes no boot or driver setting.
.\scripts\rc901a\Start-Rc901aOneBootTrust.ps1

# Then use Windows Settings > System > Recovery > Advanced startup.
# In Startup Settings choose 7/F7: Disable Driver Signature Enforcement.
# The relaxation applies only to the next Windows session.

# After capture-filter uninstall, run elevated:
.\scripts\rc901a\Restore-Rc901aTestMode.ps1
.\scripts\rc901a\Restore-Rc901aTestMode.ps1 -Apply
```

Preparation creates a seven-day, non-exportable certificate in `Cert:\CurrentUser\My`, signs the SYS, regenerates the catalog from the signed SYS, signs the catalog, and records the exact certificate thumbprint in an ignored session JSON. The one-boot trust script imports only that recorded public certificate into Local Machine Root and Trusted Publishers, verifies both signatures, and does not change BCD, Secure Boot, PnP state, or restart state. The operator then enters Windows Startup Settings and selects option 7/F7. Secure Boot stays enabled, and normal signature enforcement returns after the following ordinary reboot.

Restore refuses to run while the RC901A driver package remains installed and removes only the recorded certificate from the two machine stores plus Current User Personal. The capture driver must be uninstalled and certificate trust removed before the final ordinary reboot.

## Persistent test-mode fallback

`Enter-Rc901aTestMode.ps1` remains available only as an explicitly approved fallback if the one-boot route is rejected by this machine. It requires disabling Secure Boot in UEFI and changes `{current}` `TESTSIGNING`; do not use it for the current capture attempt. Never clear or replace Secure Boot PK/KEK/db keys for either workflow.

## Rollback outline

Before first installation, the install script records the service instance state shown by the read-only script. The rollback script removes only the validated RC901A OEM filter package and verifies that Windows returned to the previously recorded Microsoft driver selection. It does not delete the Bluetooth pairing, remove class-wide filters, or touch Xbox/DualSense devices.

## Primary references

- [Microsoft BLE overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/bluetooth-low-energy-overview)
- [Preprocessing and postprocessing IRPs](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/preprocessing-and-postprocessing-irps)
- [`IOCTL_HID_GET_REPORT_DESCRIPTOR`](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidport/ni-hidport-ioctl_hid_get_report_descriptor)
- [Using control device objects](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/using-control-device-objects)
- [`WdfControlDeviceInitAllocate`](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdfcontrol/nf-wdfcontrol-wdfcontroldeviceinitallocate)
- [Framework file objects](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/framework-file-objects)
- [`WdfDeviceCreateDeviceInterface`](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdfdevice/nf-wdfdevice-wdfdevicecreatedeviceinterface)
- [`WdfDriverOpenParametersRegistryKey`](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdfdriver/nf-wdfdriver-wdfdriveropenparametersregistrykey)
- [Introduction to registry keys for drivers](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/introduction-to-registry-keys-for-drivers)
- [Microsoft Firefly KMDF HID filter sample](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/firefly)
- [Microsoft vhidmini2 sample](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2)
