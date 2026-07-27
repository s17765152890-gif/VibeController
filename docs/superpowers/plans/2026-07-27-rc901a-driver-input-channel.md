# RC901A Driver Input Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver every hardware-verified TCL RC901A button to VibeController's existing mapping and learning pipeline without leaking synthetic keyboard shortcuts to other Windows applications.

**Architecture:** The exact-device UMDF filter keeps its passive HID read-report observation and exposes a read-only device interface containing the newest bounded report snapshot. A Windows client polls that interface, de-duplicates reports by sequence number, decodes report ID `0x01` into press/release events, and makes this driver channel authoritative while it is available; existing Raw Input remains the no-driver fallback. The captured key table becomes the automatic profile, including the three side buttons, while the old learning panel moves into an advanced compatibility mode that may explicitly override a built-in signal for another firmware revision. Power and remote-microphone audio activation remain outside this change.

**Tech Stack:** UMDF 2 / WDF C17, Windows HID-over-GATT, `DeviceIoControl`, Configuration Manager API, .NET 8 / C#, WPF, React / TypeScript / Vitest, xUnit, Pester 3.4.

---

## Scope and verified hardware table

The input report has report ID `0x01`, the physical key usage at byte index `3`, and a zero usage on release. Microphone also emits `E8 82 03 01` on press and `E8 82 03 00` on release; the keyboard usage `0xAD` is the single authoritative microphone-button signal so the auxiliary report cannot double-trigger.

| Control | Usage |
| --- | ---: |
| Up | `0x52` |
| Down | `0x51` |
| Left | `0x50` |
| Right | `0x4F` |
| OK | `0x28` |
| Menu | `0x65` |
| Back | `0xF1` |
| Home | `0x83` |
| Volume + | `0xED` |
| Volume - | `0xEE` |
| Microphone button | `0xAD` |
| Mute | `0xEF` |
| Input source | `0x97` |
| Red | `0x99` |
| Green | `0x9A` |
| Blue | `0x9B` |
| Settings | `0xA8` |
| bilibili | `0xD1` |
| 奇异果 TV | `0xDE` |
| Brightness + | `0x9E` |
| Brightness - | `0x9F` |
| Picture mode | `0xAA` |

Power is not pressed or mapped in this plan. Remote microphone audio streaming is not enabled in this plan.

## File structure

- `drivers/Rc901aHidFilter/driver/InputReportCapture.h/.c`: bounded native history and stable snapshot wire format.
- `drivers/Rc901aHidFilter/driver/Rc901aCaptureProtocol.h`: interface GUID and custom read-only IOCTL shared by the UMDF project.
- `drivers/Rc901aHidFilter/umdf/Rc901aUmdfCapture.c`: device-interface registration and snapshot IOCTL completion.
- `src/VibeController.Infrastructure/Windows/Rc901aDriverSnapshot.cs`: managed wire-format parser and report decoder.
- `src/VibeController.Infrastructure/Windows/IRc901aDriverInputClient.cs`: testable client boundary.
- `src/VibeController.Infrastructure/Windows/WindowsRc901aDriverInputClient.cs`: Configuration Manager enumeration, handle lifetime, polling, and reconnect.
- `src/VibeController.Infrastructure/Windows/WindowsRc901aRawInputSource.cs`: driver-primary / Raw-Input-fallback orchestration and duplicate suppression.
- `src/VibeController.Core/Devices/Rc901aRawInput.cs`: driver usage kind and verified physical signal table.
- `src/VibeController.Core/Devices/Rc901aLearning.cs`: explicit advanced compatibility override gate.
- `src/VibeController.Core/Domain/ControllerControl.cs`: three side-button semantic controls.
- `frontend/src/app/types.ts`, `frontend/src/app/controllerPresentation.ts`, and `frontend/src/components/Rc901aLearningPanel.tsx`: side-button presentation and binding-driven verification state.

### Task 1: Freeze the native snapshot protocol

**Files:**
- Create: `drivers/Rc901aHidFilter/driver/Rc901aCaptureProtocol.h`
- Modify: `drivers/Rc901aHidFilter/driver/InputReportCapture.h`
- Modify: `drivers/Rc901aHidFilter/driver/InputReportCapture.c`
- Modify: `drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.c`
- Modify: `drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.vcxproj`

- [ ] **Step 1: Write the failing native snapshot tests**

Add tests which demand protocol version `1`, fixed record size `272`, chronological records after ring wrap, a stable `TotalReports`, and rejection of undersized output:

```c
static void TestBuildsStableInputReportSnapshot(void)
{
    RC901A_INPUT_REPORT_HISTORY history;
    RC901A_INPUT_REPORT_SNAPSHOT snapshot;
    const unsigned char press[] = {
        0x01U, 0x00U, 0x00U, 0xF1U, 0, 0, 0, 0, 0
    };
    size_t bytesWritten = 0U;

    Rc901aInitializeInputReportHistory(&history);
    EXPECT_TRUE(
        Rc901aRecordInputReport(&history, 0U, press, sizeof(press)) ==
        Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(
        Rc901aBuildInputReportSnapshot(
            &history,
            &snapshot,
            sizeof(snapshot),
            &bytesWritten) == Rc901aInputReportCaptureSuccess);
    EXPECT_TRUE(snapshot.Version == RC901A_CAPTURE_PROTOCOL_VERSION);
    EXPECT_TRUE(snapshot.RecordSize == sizeof(RC901A_INPUT_REPORT_RECORD));
    EXPECT_TRUE(snapshot.TotalReports == 1U);
    EXPECT_TRUE(snapshot.RecordCount == 1U);
    EXPECT_TRUE(snapshot.Records[0].Sequence == 1U);
    EXPECT_TRUE(snapshot.Records[0].Data[3] == 0xF1U);
    EXPECT_TRUE(bytesWritten == RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE +
        sizeof(RC901A_INPUT_REPORT_RECORD));
}
```

- [ ] **Step 2: Run the native test and verify RED**

Run:

```powershell
msbuild drivers\Rc901aHidFilter\tests\DescriptorCaptureTests.vcxproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

Expected: compilation fails because `RC901A_INPUT_REPORT_SNAPSHOT` and `Rc901aBuildInputReportSnapshot` do not exist.

- [ ] **Step 3: Add the shared read-only protocol**

Create `Rc901aCaptureProtocol.h` with a translation-unit-local constant so the UMDF DLL has no unresolved GUID symbol:

```c
#pragma once

#include <guiddef.h>
#include <winioctl.h>

static const GUID GUID_DEVINTERFACE_VIBECONTROLLER_RC901A_CAPTURE = {
    0x34826b0c, 0xf006, 0x44e1,
    { 0xae, 0x98, 0xa5, 0x84, 0xb6, 0x8c, 0x4e, 0xc1 }
};

#define RC901A_CAPTURE_DEVICE_TYPE 0x8010U
#define IOCTL_RC901A_GET_INPUT_REPORTS \
    CTL_CODE(RC901A_CAPTURE_DEVICE_TYPE, 0x800U, METHOD_BUFFERED, FILE_READ_ACCESS)
```

Add this stable snapshot structure to `InputReportCapture.h`:

```c
#define RC901A_CAPTURE_PROTOCOL_VERSION 1U
#define RC901A_INPUT_REPORT_SNAPSHOT_HEADER_SIZE 24U

typedef struct _RC901A_INPUT_REPORT_SNAPSHOT {
    uint32_t Version;
    uint32_t RecordSize;
    uint64_t TotalReports;
    uint32_t RecordCount;
    uint32_t Reserved;
    RC901A_INPUT_REPORT_RECORD Records[
        RC901A_INPUT_REPORT_HISTORY_CAPACITY];
} RC901A_INPUT_REPORT_SNAPSHOT;
```

Implement `Rc901aBuildInputReportSnapshot` so it zeroes the destination, copies records oldest-to-newest, writes only header plus populated records, and never exposes padding or uninitialized bytes.

- [ ] **Step 4: Run the native test and verify GREEN**

Run the command from Step 2, then:

```powershell
drivers\Rc901aHidFilter\tests\bin\x64\Debug\DescriptorCaptureTests.exe
```

Expected: build succeeds and the executable prints that all capture tests passed.

- [ ] **Step 5: Commit the protocol unit**

```powershell
git add drivers/Rc901aHidFilter/driver/Rc901aCaptureProtocol.h drivers/Rc901aHidFilter/driver/InputReportCapture.h drivers/Rc901aHidFilter/driver/InputReportCapture.c drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.c drivers/Rc901aHidFilter/tests/DescriptorCaptureTests.vcxproj
git commit -m "feat: define RC901A input snapshot protocol"
```

### Task 2: Expose the UMDF read-only device interface

**Files:**
- Modify: `drivers/Rc901aHidFilter/umdf/Rc901aUmdfCapture.c`
- Modify: `drivers/Rc901aHidFilter/umdf/Rc901aUmdfCapture.vcxproj`
- Modify: `drivers/Rc901aHidFilter/umdf/Rc901aHidFilter.inx`
- Modify: `tests/scripts/Rc901aUmdfCapture.Tests.ps1`

- [ ] **Step 1: Write failing static driver-contract tests**

Require the exact interface GUID header, `WdfDeviceCreateDeviceInterface`, a local custom-IOCTL branch, a bounded output buffer, and lower-HID pass-through behavior. Update expected package version to `1.0.0.6` dated `07/27/2026`.

```powershell
It 'exposes only a read-only RC901A snapshot interface' {
    $content = Get-Content -LiteralPath $sourcePath -Raw
    $content | Should Match 'GUID_DEVINTERFACE_VIBECONTROLLER_RC901A_CAPTURE'
    $content | Should Match 'WdfDeviceCreateDeviceInterface'
    $content | Should Match 'IOCTL_RC901A_GET_INPUT_REPORTS'
    $content | Should Match 'Rc901aBuildInputReportSnapshot'
    $content | Should Match 'WdfRequestRetrieveOutputBuffer'
    $content | Should Match 'WdfRequestCompleteWithInformation'
    $content | Should Not Match 'WdfRequestRetrieveInputBuffer'
}
```

- [ ] **Step 2: Run Pester and verify RED**

Run:

```powershell
Invoke-Pester -Script tests\scripts\Rc901aUmdfCapture.Tests.ps1
```

Expected: only the new interface/version assertions fail.

- [ ] **Step 3: Register the interface and serve snapshots**

After `WdfDeviceCreate`, call:

```c
status = WdfDeviceCreateDeviceInterface(
    device,
    &GUID_DEVINTERFACE_VIBECONTROLLER_RC901A_CAPTURE,
    NULL);
if (!NT_SUCCESS(status)) {
    return status;
}
```

Before the lower-HID forwarding branch, handle only `IOCTL_RC901A_GET_INPUT_REPORTS`: retrieve an output buffer of `sizeof(RC901A_INPUT_REPORT_SNAPSHOT)`, build the snapshot under `CaptureLock`, complete with the actual serialized byte count, and never send that private IOCTL to `HidOverGatt`. Keep all HID IOCTL completion status and buffers unchanged.

- [ ] **Step 4: Build and verify GREEN**

Run:

```powershell
Invoke-Pester -Script tests\scripts\Rc901aUmdfCapture.Tests.ps1
msbuild drivers\Rc901aHidFilter\umdf\Rc901aUmdfCapture.vcxproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

Expected: 12 or more Pester assertions pass, driver build has zero warnings/errors, and Inf2Cat signability succeeds.

- [ ] **Step 5: Commit the UMDF endpoint**

```powershell
git add drivers/Rc901aHidFilter/umdf tests/scripts/Rc901aUmdfCapture.Tests.ps1
git commit -m "feat: expose RC901A raw input channel"
```

### Task 3: Parse snapshots and decode physical reports in managed code

**Files:**
- Create: `src/VibeController.Infrastructure/Windows/Rc901aDriverSnapshot.cs`
- Create: `tests/VibeController.Infrastructure.Tests/Windows/Rc901aDriverSnapshotTests.cs`
- Modify: `src/VibeController.Core/Devices/Rc901aRawInput.cs`
- Modify: `tests/VibeController.Core.Tests/Devices/Rc901aRawInputTests.cs`

- [ ] **Step 1: Write failing parser and decoder tests**

Use an in-memory binary snapshot with press `01 00 00 F1 00 00 00 00 00`, release `01 00 00 00 00 00 00 00 00`, and an auxiliary `E8` report. Assert:

```csharp
Assert.Equal(
    [
        new Rc901aRawInputEvent(
            timestamp,
            Rc901aRawInputKind.DriverHidUsage,
            0xF1,
            true),
        new Rc901aRawInputEvent(
            timestamp,
            Rc901aRawInputKind.DriverHidUsage,
            0xF1,
            false),
    ],
    decoder.Decode(snapshot, lastSequence: 0));
```

Also assert that sequence numbers at or below `lastSequence`, malformed record sizes, protocol versions other than `1`, and the `E8` microphone auxiliary report do not create duplicate control events.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --filter Rc901aDriverSnapshot
```

Expected: compilation fails because the parser and `DriverHidUsage` kind do not exist.

- [ ] **Step 3: Implement the managed wire parser**

Implement these immutable types:

```csharp
public sealed record Rc901aDriverReport(
    ulong Sequence,
    uint IoControlCode,
    byte[] Data);

public sealed record Rc901aDriverSnapshot(
    ulong TotalReports,
    IReadOnlyList<Rc901aDriverReport> Reports);
```

Read little-endian fields with `BinaryPrimitives`, require `Version == 1`, `RecordSize == 272`, `RecordCount <= 32`, and bounds-check every record. The decoder tracks the one currently pressed report-ID-`0x01` usage and emits release before a changed press. It ignores report IDs other than `0x01` for control mapping while retaining them in diagnostic samples.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the command from Step 2.

Expected: all `Rc901aDriverSnapshot` tests pass.

- [ ] **Step 5: Commit parser and decoder**

```powershell
git add src/VibeController.Infrastructure/Windows/Rc901aDriverSnapshot.cs src/VibeController.Core/Devices/Rc901aRawInput.cs tests/VibeController.Infrastructure.Tests/Windows/Rc901aDriverSnapshotTests.cs tests/VibeController.Core.Tests/Devices/Rc901aRawInputTests.cs
git commit -m "feat: decode RC901A driver reports"
```

### Task 4: Add the reconnecting Windows driver client

**Files:**
- Create: `src/VibeController.Infrastructure/Windows/IRc901aDriverInputClient.cs`
- Create: `src/VibeController.Infrastructure/Windows/WindowsRc901aDriverInputClient.cs`
- Create: `tests/VibeController.Infrastructure.Tests/Windows/WindowsRc901aDriverInputClientTests.cs`

- [ ] **Step 1: Write failing lifecycle tests against a fake transport**

Define the client boundary:

```csharp
public interface IRc901aDriverInputClient : IAsyncDisposable
{
    event Action<Rc901aRawInputEvent>? InputReceived;
    event Action<bool>? AvailabilityChanged;
    bool IsAvailable { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}
```

Tests must prove initial snapshot delivery, sequence de-duplication, restart recovery when `TotalReports` goes backwards, reconnect after a missing interface, and disposal cancelling the polling loop.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --filter WindowsRc901aDriverInputClient
```

Expected: compilation fails because the interface and client do not exist.

- [ ] **Step 3: Implement the Windows transport**

Use `CM_Get_Device_Interface_List_SizeW` and `CM_Get_Device_Interface_ListW` with interface GUID `{34826b0c-f006-44e1-ae98-a584b68c4ec1}`. Open the first present interface using `CreateFileW(GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, OPEN_EXISTING)`, issue the computed `IOCTL_RC901A_GET_INPUT_REPORTS` with `DeviceIoControl`, and poll every 16 ms while connected. Back off to 500 ms only while the interface is absent. Do not request write access.

- [ ] **Step 4: Run focused and infrastructure tests**

Run:

```powershell
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj
```

Expected: all infrastructure tests pass.

- [ ] **Step 5: Commit the Windows client**

```powershell
git add src/VibeController.Infrastructure/Windows/IRc901aDriverInputClient.cs src/VibeController.Infrastructure/Windows/WindowsRc901aDriverInputClient.cs tests/VibeController.Infrastructure.Tests/Windows/WindowsRc901aDriverInputClientTests.cs
git commit -m "feat: read RC901A driver input on Windows"
```

### Task 5: Make the driver channel authoritative with Raw Input fallback

**Files:**
- Modify: `src/VibeController.Infrastructure/Windows/WindowsRc901aRawInputSource.cs`
- Modify: `src/VibeController.App/MainWindow.xaml.cs`
- Create: `tests/VibeController.Infrastructure.Tests/Windows/WindowsRc901aInputSourceTests.cs`

- [ ] **Step 1: Write failing source-priority tests**

Using fake Raw Input and fake driver events, assert:

1. Raw Input events are published when the driver interface is absent.
2. Driver events are published when the interface is available.
3. The matching Raw Input event is suppressed while the driver channel is authoritative.
4. Losing the driver returns to Raw Input without leaving a pressed control stuck.
5. Refresh triggers both device discovery paths.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --filter WindowsRc901aInputSource
```

Expected: assertions fail because the source does not know about a driver client.

- [ ] **Step 3: Integrate the client without changing adapter contracts**

Allow `WindowsRc901aRawInputSource` to own or receive an `IRc901aDriverInputClient`. Start it when the WPF window attaches, forward driver events through the existing `InputReceived` event, and gate keyboard/consumer Raw Input publication on `!driverClient.IsAvailable`. Status should say either:

```text
RC901A 专用驱动已连接，可识别 22 个已验证按键。
```

or:

```text
RC901A 专用驱动不可用，正在使用 Windows 标准按键回退。
```

Keep `IRc901aRawInputSource`, `Rc901aControllerAdapter`, and `ControllerRuntimeService` public behavior unchanged.

- [ ] **Step 4: Run source and adapter tests GREEN**

Run:

```powershell
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --filter "WindowsRc901aInputSource|Rc901aRawInputAdapter"
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit source orchestration**

```powershell
git add src/VibeController.Infrastructure/Windows/WindowsRc901aRawInputSource.cs src/VibeController.App/MainWindow.xaml.cs tests/VibeController.Infrastructure.Tests/Windows/WindowsRc901aInputSourceTests.cs
git commit -m "feat: prefer RC901A driver input"
```

### Task 6: Install the verified table and compatibility override semantics

**Files:**
- Modify: `src/VibeController.Core/Domain/ControllerControl.cs`
- Modify: `src/VibeController.Core/Devices/Rc901aRawInput.cs`
- Modify: `src/VibeController.Core/Devices/Rc901aLearning.cs`
- Modify: `tests/VibeController.Core.Tests/Devices/Rc901aRawInputTests.cs`
- Modify: `tests/VibeController.Core.Tests/Devices/Rc901aLearningSessionTests.cs`
- Modify: `tests/VibeController.Infrastructure.Tests/Windows/Rc901aRawInputInterpreterTests.cs`

- [ ] **Step 1: Write failing verified-table tests**

Assert all 22 `(DriverHidUsage, usage)` pairs map to the semantic controls in the hardware table, no usage is duplicated, Power has no verified binding, and the three side controls exist. Add a second test proving an explicitly learned compatibility binding replaces the automatic binding for the same semantic control:

```csharp
Assert.Contains(
    Rc901aInputBindings.VerifiedDefaults,
    item => item.Kind == Rc901aRawInputKind.DriverHidUsage &&
            item.Code == 0x9E &&
            item.Control == ControllerControl.RemoteBrightnessUp);
Assert.DoesNotContain(
    Rc901aInputBindings.VerifiedDefaults,
    item => item.Control == ControllerControl.RemotePower);

var effective = Rc901aInputBindings.CombineWithVerifiedDefaults(
[
    new(
        Rc901aRawInputKind.DriverHidUsage,
        0xE1,
        ControllerControl.RemoteBack,
        Rc901aBindingSource.Learned),
]);
Assert.DoesNotContain(
    effective,
    item => item.Control == ControllerControl.RemoteBack &&
            item.Source == Rc901aBindingSource.VerifiedDefault);
```

- [ ] **Step 2: Run core tests and verify RED**

Run:

```powershell
dotnet test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj --filter Rc901aRawInput
```

Expected: the side controls and driver defaults are missing.

- [ ] **Step 3: Add semantic controls and all verified defaults**

Append:

```csharp
RemoteBrightnessUp,
RemoteBrightnessDown,
RemotePictureMode,
```

Keep the current Virtual-Key fallback defaults, add `VK_APPS` as the Menu fallback, and add the 22 `DriverHidUsage` defaults from the table at the top of this plan. Do not add Power. Normal startup uses the automatic table without learning. `NormalizeLearned` accepts a valid explicit compatibility override, and `CombineWithVerifiedDefaults` removes any built-in binding with the overridden semantic control or signal before appending learned bindings.

Add `allowVerifiedOverride = false` to `Rc901aLearningSession.Start`. Normal callers retain the current protection. The advanced compatibility command passes `true`, records that gate for the active session, and allows confirmation even when `Conflict.Source == VerifiedDefault`. Reset the gate on cancel, expiry, disconnect, and successful save.

- [ ] **Step 4: Run core and interpreter tests GREEN**

Run:

```powershell
dotnet test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj
dotnet test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --filter Rc901aRawInputInterpreter
```

Expected: all tests pass.

- [ ] **Step 5: Commit the key table**

```powershell
git add src/VibeController.Core/Domain/ControllerControl.cs src/VibeController.Core/Devices/Rc901aRawInput.cs src/VibeController.Core/Devices/Rc901aLearning.cs tests/VibeController.Core.Tests/Devices/Rc901aRawInputTests.cs tests/VibeController.Core.Tests/Devices/Rc901aLearningSessionTests.cs tests/VibeController.Infrastructure.Tests/Windows/Rc901aRawInputInterpreterTests.cs
git commit -m "feat: verify RC901A hardware button table"
```

### Task 7: Present all verified and side buttons in the UI

**Files:**
- Modify: `frontend/src/app/types.ts`
- Modify: `frontend/src/app/controllerPresentation.ts`
- Modify: `frontend/src/components/Rc901aLearningPanel.tsx`
- Modify: `frontend/src/components/Rc901aLearningPanel.test.tsx`
- Modify: `frontend/src/pages/Settings.test.tsx`
- Modify: `frontend/src/styles/global.css`
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Modify: `tests/VibeController.Infrastructure.Tests/App/ControllerRuntimeServiceRc901aTests.cs`

- [ ] **Step 1: Write failing frontend tests**

Require labels and keycaps for `亮度＋`, `亮度－`, and `图像模式`; require the normal settings view to show the automatic 22-key profile without per-key learning buttons; require “兼容性按键识别” to live inside an advanced disclosure; and require starting a session there to send `compatibilityOverride: true`. Power remains disabled until safe capture is available.

- [ ] **Step 2: Run focused frontend tests and verify RED**

Run:

```powershell
npm test -- --run src/components/Rc901aLearningPanel.test.tsx src/pages/Settings.test.tsx
```

Expected: tests fail because side controls and binding-driven verification are absent.

- [ ] **Step 3: Update types and presentation**

Extend `Rc901aControl` with:

```ts
| "remoteBrightnessUp"
| "remoteBrightnessDown"
| "remotePictureMode";
```

Render the side controls as a compact three-item group beside the remote body. Build `verifiedControls` from `inputStatus.bindings.filter(binding => binding.source === "verifiedDefault")`, and use that set for status badges. The primary view states that the automatic 22-key profile is ready and contains no learning workflow. Move `Rc901aLearningPanel` under an advanced “兼容性按键识别” disclosure; its start command includes `compatibilityOverride: true`, and `ControllerRuntimeService` passes that flag to `Rc901aLearningSession.Start`. Update the explanatory copy to state that Power remains intentionally unverified.

- [ ] **Step 4: Run frontend tests and production build GREEN**

Run:

```powershell
npm test -- --run
npm run build
```

Expected: all Vitest tests pass and Vite production build succeeds.

- [ ] **Step 5: Commit the UI model**

```powershell
git add frontend/src/app/types.ts frontend/src/app/controllerPresentation.ts frontend/src/components/Rc901aLearningPanel.tsx frontend/src/components/Rc901aLearningPanel.test.tsx frontend/src/pages/Settings.test.tsx frontend/src/styles/global.css src/VibeController.App/Services/ControllerRuntimeService.cs tests/VibeController.Infrastructure.Tests/App/ControllerRuntimeServiceRc901aTests.cs
git commit -m "feat: show verified RC901A side controls"
```

### Task 8: Package, install, and perform hardware acceptance

**Files:**
- Modify: `docs/testing/RC901A-HID-FILTER.md`
- Generate locally: `artifacts/rc901a-test-package-pass16-driver-channel/`
- Generate locally: `artifacts/rc901a-driver-state-before-pass16-driver-channel.json`
- Generate locally: `artifacts/rc901a-pass16-driver-channel-result.json`

- [ ] **Step 1: Run all automated verification**

Run:

```powershell
msbuild drivers\Rc901aHidFilter\tests\DescriptorCaptureTests.vcxproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
drivers\Rc901aHidFilter\tests\bin\x64\Debug\DescriptorCaptureTests.exe
Invoke-Pester -Script tests\scripts\Rc901aUmdfCapture.Tests.ps1
dotnet test VibeController.sln
npm --prefix frontend test -- --run
npm --prefix frontend run build
```

Expected: every command exits `0`, native and managed builds have no warnings, and all test counts are recorded in the handoff.

- [ ] **Step 2: Build and test-sign driver `1.0.0.6`**

Embed-sign `Rc901aUmdfCapture.dll`, regenerate `Rc901aHidFilter.cat`, sign the catalog with the already trusted temporary certificate, and verify both signatures are `Valid`. Do not modify Secure Boot, BCD, test-signing, pairing, or any non-RC901A device.

- [ ] **Step 3: Install with exact-device rollback**

Use `Install-Rc901aCaptureFilter.ps1` with a new rollback-state path. Require:

```text
DriverProvider = VibeController
DriverVersion = 07/27/2026 1.0.0.6
Status = Started
ProblemCode = 0x00000000
```

Automatically run the existing uninstall/restore script if any requirement fails.

- [ ] **Step 4: Verify the interface as a normal user**

Launch VibeController without elevation. Confirm the custom interface opens with `GENERIC_READ`, the status page reports the dedicated channel, and no registry polling is required for normal input.

- [ ] **Step 5: Perform physical button acceptance**

Press in order:

```text
Up, OK, Back, Home, Volume+, Volume-, Mic,
Mute, Input, Red, Green, Blue, Settings,
bilibili, 奇异果TV, Brightness+, Brightness-, Picture mode
```

For every button require exactly one press and one release in the app's raw sample list, the correct semantic `ControllerControl`, and the configured action only when not in learning mode. Confirm `Mic` toggles Codex native dictation once and auxiliary report `E8` does not double-trigger.

- [ ] **Step 6: Verify fallback and recovery**

Temporarily stop using the driver client in a test build and confirm directions/OK still arrive through Raw Input. Restore the normal build and confirm the dedicated channel reconnects without re-pairing or restarting Windows.

- [ ] **Step 7: Update hardware evidence and commit**

Record the verified firmware `V1.0.192.6`, driver `1.0.0.6`, interface GUID, 22-key table, excluded Power key, microphone-button/audio distinction, rollback location, and exact verification commands in `docs/testing/RC901A-HID-FILTER.md`.

```powershell
git add docs/testing/RC901A-HID-FILTER.md
git commit -m "docs: record RC901A driver channel acceptance"
```

## Self-review

- Spec coverage: native endpoint, managed transport, de-duplication, fallback, all 22 safe physical buttons, side controls, UI, packaging, rollback, and hardware acceptance are each assigned to a task.
- Explicit exclusions: Power remains unpressed/unmapped; remote microphone audio activation remains out of scope; no unknown BLE writes occur.
- Placeholder scan: the plan contains concrete types, protocol values, commands, expected failures, expected successes, and physical acceptance sequences.
- Type consistency: native record size is `272`; protocol version is `1`; interface GUID is `{34826b0c-f006-44e1-ae98-a584b68c4ec1}`; managed events use `DriverHidUsage`; side controls use `RemoteBrightnessUp`, `RemoteBrightnessDown`, and `RemotePictureMode`; automatic bindings are overridden only through an explicit advanced compatibility session.
