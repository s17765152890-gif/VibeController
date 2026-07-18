# PS5 DualSense Device Switch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persisted Xbox / PS5 device switch, native wired and Bluetooth DualSense input, device-correct control names and imagery, and touchpad-to-mouse behavior without changing the existing Xbox mappings.

**Architecture:** Keep one semantic controller model: the four physical face-button positions map to `X/A/B/Y`, so Square/Cross/Circle/Triangle inherit the existing Xbox actions. Add a `ControllerType` setting and select either XInput or a native Sony HID adapter when rebuilding the runtime. Parse DualSense USB report `0x01` and Bluetooth enhanced report `0x31`; expose relative touch motion as `TouchpadX/TouchpadY` and the physical click as `TouchpadButton`.

**Tech Stack:** .NET 8, WPF, Windows XInput, Windows HID/Configuration Manager P/Invoke, React 19, TypeScript, Vitest, Testing Library, CSS.

**Protocol references:** Linux `hid-playstation` defines the 64-byte USB and 78-byte Bluetooth report layouts, button masks, 1920x1080 touchpad, and Bluetooth payload offset. SDL's DualSense HID driver confirms that reading feature report `0x09` or `0x20` enables enhanced Bluetooth reports.

**Delivery note:** This repository is an uncommitted MVP worktree. Do not create commits or overwrite unrelated changes without explicit user authorization.

---

### Task 1: Persist the selected controller type and preserve the Xbox profile

**Files:**
- Create: `src/VibeController.Core/Domain/ControllerType.cs`
- Modify: `src/VibeController.Infrastructure/Settings/AppSettings.cs`
- Modify: `src/VibeController.Core/Mapping/DefaultProfileFactory.cs`
- Modify: `src/VibeController.Infrastructure/Settings/JsonSettingsStore.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/DefaultProfileFactoryTests.cs`

- [ ] **Step 1: Write failing persistence and default-profile tests**

```csharp
[Fact]
public async Task SaveAndLoad_RoundTripsControllerType()
{
    var store = new JsonSettingsStore(_directory);
    await store.SaveAsync(AppSettings.CreateDefault() with
    {
        ControllerType = ControllerType.PlayStation5,
    });

    var actual = await store.LoadAsync();

    Assert.Equal(ControllerType.PlayStation5, actual.ControllerType);
}

[Fact]
public void Create_AddsDualSenseTouchpadDefaultsWithoutChangingXboxButtons()
{
    var profile = DefaultProfileFactory.Create();

    Assert.Equal(MappedActionKind.CodexDictation, profile.Mappings[ControllerControl.X].Kind);
    Assert.Equal(MappedActionKind.MouseMove, profile.Mappings[ControllerControl.TouchpadX].Kind);
    Assert.Equal(MappedActionKind.MouseMove, profile.Mappings[ControllerControl.TouchpadY].Kind);
    Assert.Equal(MappedActionKind.MouseLeftClick, profile.Mappings[ControllerControl.TouchpadButton].Kind);
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~RoundTripsControllerType"
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~AddsDualSenseTouchpadDefaults"
```

Expected: compilation fails because `ControllerType` and the touchpad controls do not exist.

- [ ] **Step 3: Add the domain and defaults**

```csharp
public enum ControllerType
{
    Xbox,
    PlayStation5,
}
```

Add `TouchpadX`, `TouchpadY`, and `TouchpadButton` to `ControllerControl`. Add `ControllerType ControllerType { get; init; } = ControllerType.Xbox;` to `AppSettings`, and add these mappings:

```csharp
[ControllerControl.TouchpadX] = new(MappedActionKind.MouseMove),
[ControllerControl.TouchpadY] = new(MappedActionKind.MouseMove),
[ControllerControl.TouchpadButton] = new(MappedActionKind.MouseLeftClick),
```

During settings migration, add only missing touchpad mappings; never replace user mappings.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the two commands from Step 2. Expected: both focused tests pass.

### Task 2: Parse and translate DualSense USB/Bluetooth reports

**Files:**
- Create: `src/VibeController.Core/Devices/RawDualSenseState.cs`
- Create: `src/VibeController.Core/Devices/DualSenseReportParser.cs`
- Create: `src/VibeController.Core/Devices/DualSenseStateTranslator.cs`
- Modify: `src/VibeController.Core/Domain/ControllerEventDetector.cs`
- Modify: `src/VibeController.Core/Mapping/MappingEngine.cs`
- Test: `tests/VibeController.Core.Tests/Devices/DualSenseReportParserTests.cs`
- Test: `tests/VibeController.Core.Tests/Devices/DualSenseStateTranslatorTests.cs`
- Test: `tests/VibeController.Core.Tests/Domain/ControllerSnapshotTests.cs`

- [ ] **Step 1: Write failing report-layout tests**

Build synthetic 64-byte USB and 78-byte Bluetooth reports. Set the common payload at offset `1` for USB and `2` for Bluetooth. Assert that Square/Cross/Circle/Triangle become semantic X/A/B/Y, Options becomes Menu, Create becomes View, L1/R1 and L2/R2 map correctly, and the first touch contact decodes with:

```csharp
var x = xLow | ((packed & 0x0F) << 8);
var y = (packed >> 4) | (yHigh << 4);
```

Also assert that an unsupported report ID is rejected.

- [ ] **Step 2: Run parser tests and verify RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~DualSense"
```

Expected: compilation fails because the parser and raw state do not exist.

- [ ] **Step 3: Implement the parser and touch translation**

`DualSenseReportParser.TryParse(ReadOnlySpan<byte>, out RawDualSenseState)` must accept:

```csharp
var payloadOffset = report[0] switch
{
    0x01 when report.Length >= 64 => 1,
    0x31 when report.Length >= 78 => 2,
    _ => -1,
};
```

`DualSenseStateTranslator` normalizes 8-bit sticks around 128, preserves trigger hysteresis, and returns a touch state. The first frame of a contact yields zero movement. Subsequent frames yield clamped relative values:

```csharp
var deltaX = Math.Clamp((raw.TouchX - previousTouch.X) / 32f, -2f, 2f);
var deltaY = Math.Clamp((raw.TouchY - previousTouch.Y) / 32f, -2f, 2f);
```

Treat touch axes as relative controls: emit a `Changed` event for every non-zero new delta, even when two consecutive deltas are equal. Add them to `MappingEngine` continuous controls, but not to the runtime's held-stick replay list.

- [ ] **Step 4: Run DualSense core tests and verify GREEN**

Run the command from Step 2. Expected: all DualSense-focused tests pass.

### Task 3: Read a real DualSense through Windows HID

**Files:**
- Create: `src/VibeController.Infrastructure/Windows/IDualSenseHidApi.cs`
- Create: `src/VibeController.Infrastructure/Windows/DualSenseHidApi.cs`
- Create: `src/VibeController.Infrastructure/Windows/DualSenseControllerAdapter.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/DualSenseControllerAdapterTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/DualSenseHidApiTests.cs`

- [ ] **Step 1: Write a failing adapter test with a fake HID API**

```csharp
[Fact]
public void Read_ConnectedDualSenseTranslatesLatestReport()
{
    var api = new FakeDualSenseHidApi(CreateUsbReport(squarePressed: true));
    using var adapter = new DualSenseControllerAdapter(api);

    var result = adapter.Read(0, ControllerSnapshot.Empty, 0.12f);

    Assert.True(result.IsConnected);
    Assert.Equal(1f, result.Snapshot.GetValue(ControllerControl.X));
}
```

Add a second test proving disconnect returns `ControllerReadResult.Disconnected`, and a third proving a touch move is emitted once per new HID packet.

- [ ] **Step 2: Run adapter tests and verify RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~DualSense"
```

Expected: compilation fails because the HID API and adapter do not exist.

- [ ] **Step 3: Implement Configuration Manager enumeration and asynchronous reads**

Use `HidD_GetHidGuid`, `CM_Get_Device_Interface_List_SizeW`, and `CM_Get_Device_Interface_ListW` to enumerate present HID interfaces. Open Sony VID `0x054C`, PID `0x0CE6` (standard DualSense) or `0x0DF2` (DualSense Edge) with shared read/write access. Query `HidP_GetCaps` for the exact input-report length.

After opening the handle, request feature report `0x09` with `HidD_GetFeature`; this enables Bluetooth enhanced `0x31` reports without changing lights or haptics. Read reports on a cancellable background task, keep only the latest full report, increment a local packet number, and reconnect after I/O failure. Selecting another controller index cancels and restarts the reader.

- [ ] **Step 4: Implement the adapter lifecycle**

Cache the translated snapshot for a packet number, reset touch history on disconnect/index change, and dispose the HID reader. Make `ControllerRuntime` disposable when its adapter implements `IDisposable`, so runtime rebuilds release the old device handle.

- [ ] **Step 5: Run infrastructure DualSense tests and verify GREEN**

Run the command from Step 2. Expected: all DualSense infrastructure tests pass.

### Task 4: Switch adapters at runtime and execute touchpad mouse movement

**Files:**
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Modify: `src/VibeController.Core/Runtime/BridgeMessage.cs`
- Modify: `src/VibeController.Infrastructure/Windows/WindowsActionExecutor.cs`
- Test: `tests/VibeController.Core.Tests/Runtime/BridgeMessageTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/WindowsActionExecutorTests.cs`

- [ ] **Step 1: Write failing bridge and mouse tests**

Assert `RuntimeConfigurationPayload.ControllerType` serializes as `playStation5`. Assert `TouchpadX` sends `(positiveDelta, 0)` and `TouchpadY` sends `(0, positiveDelta)`, while left-stick Y remains inverted.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~BridgeMessage"
.\.tools\dotnet\dotnet.exe test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~MouseMove"
```

Expected: new controller type and touch controls are absent from production behavior.

- [ ] **Step 3: Add runtime switching**

Include `ControllerType` in the configuration payload. Parse `controllerType` during `updateSettings`; when the type changes, dispose/rebuild the runtime with:

```csharp
IControllerAdapter adapter = _settings.ControllerType switch
{
    ControllerType.PlayStation5 => new DualSenseControllerAdapter(),
    _ => new XInputControllerAdapter(),
};
```

Format recent actions with device-correct glyphs. Keep all existing Xbox key mappings untouched.

- [ ] **Step 4: Add touchpad mouse directions**

```csharp
ControllerControl.TouchpadX => (delta, 0),
ControllerControl.TouchpadY => (0, delta),
```

The configured mouse-speed slider remains the single sensitivity control.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the two commands from Step 2. Expected: both groups pass.

### Task 5: Add the device switch and device-correct controller UI

**Files:**
- Add binary asset: `frontend/public/dualsense-black.png`
- Create: `frontend/src/components/PlayStationController.tsx`
- Create: `frontend/src/components/ControllerVisual.tsx`
- Create: `frontend/src/app/controllerPresentation.ts`
- Modify: `frontend/src/app/types.ts`
- Modify: `frontend/src/app/useRuntimeState.ts`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/pages/Settings.tsx`
- Modify: `frontend/src/pages/Settings.test.tsx`
- Modify: `frontend/src/pages/Dashboard.tsx`
- Modify: `frontend/src/pages/Dashboard.test.tsx`
- Modify: `frontend/src/pages/MappingEditor.tsx`
- Modify: `frontend/src/pages/MappingEditor.test.tsx`
- Modify: `frontend/src/styles/global.css`

- [ ] **Step 1: Write failing frontend behavior tests**

Settings test:

```tsx
fireEvent.click(screen.getByRole("radio", { name: "PS5 DualSense" }));
fireEvent.click(screen.getByRole("button", { name: "保存设置" }));
expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ controllerType: "playStation5" }));
```

Dashboard test: with `configuration.controllerType = "playStation5"`, assert the image path is `dualsense-black.png`, status says `PS5 手柄已连接`, and the touchpad hotspot exists.

Mapping test: in PS5 mode assert the visible names are `□ 方块键`, `× 叉键`, `○ 圆圈键`, `△ 三角键`, `L1`, `R1`, `L2`, `R2`, `Create`, `Options`, `触控板滑动`, and `触控板按下`. Save and assert semantic defaults remain `x=dictation`, `a=send`, `b=shortcut:Backspace`, and `y=commandPalette`.

- [ ] **Step 2: Run frontend tests and verify RED**

```powershell
npm --prefix frontend test -- --run
```

Expected: the PS5 device selector, image, names, and touchpad controls are not present.

- [ ] **Step 3: Add the supplied image asset and presentation model**

Copy the exact supplied transparent PNG to `frontend/public/dualsense-black.png`. Centralize device display names, face-button glyphs, shoulder names, and utility-button names in `controllerPresentation.ts` so Dashboard and Mapping Editor cannot drift.

- [ ] **Step 4: Build a restrained device selector**

Use a two-choice radio-card group inside the existing device settings card. Selection changes should be immediate and interruptible, with only `transform`, border, background, and opacity transitions under 200ms. Buttons use `scale(0.97)` press feedback and respect `prefers-reduced-motion`.

- [ ] **Step 5: Build the PS5 visual with shared hotspot behavior**

Overlay hotspots on the supplied image for all face buttons, sticks, D-pad, Create/Options, L1/R1, L2/R2, and the touchpad. Reuse the existing `photo-hotspot` active/pressed system, so input-test feedback remains consistent. The touchpad receives a rectangular hotspot and a subtle motion marker while touch deltas are present.

- [ ] **Step 6: Run frontend tests, typecheck, and build**

```powershell
npm --prefix frontend test -- --run
npm --prefix frontend run typecheck
npm --prefix frontend run build
```

Expected: all frontend tests pass and production assets build without TypeScript errors.

### Task 6: Full verification, visual QA, and Windows publish

**Files:**
- Modify only if verification exposes a defect.

- [ ] **Step 1: Run the complete automated suite**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Expected: frontend tests, TypeScript, Vite build, Core tests, and Infrastructure tests all exit with zero failures.

- [ ] **Step 2: Render and inspect all three pages for both device types**

Run the app or browser preview and inspect Dashboard, Mapping, and Settings at the desktop window size. Verify no clipped labels, the PS5 image is sharp and transparent, hotspots align to the physical controls, the selected-device state is obvious, and reduced-motion styles remain valid.

- [ ] **Step 3: Publish and inspect the bundle**

Stop only the verified `artifacts\win-x64\VibeController.App.exe` process, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Verify the published `wwwroot` contains `dualsense-black.png` and the controller-type strings.

- [ ] **Step 4: Start the published app and verify runtime health**

Start `artifacts\win-x64\VibeController.App.exe`, confirm the process responds, save Xbox then PS5 selection, and confirm state returns through the bridge. If no physical DualSense is connected, report that protocol parsing and adapter behavior are automated-test verified but hardware pairing remains a user smoke test.
