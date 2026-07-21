# RC901A BLE Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a safe, read-only-first TCL BT_RC901A_B1 BLE backend that can connect independently of the failed Windows HID driver, expose connection/battery/raw-notification diagnostics, and feed verified remote buttons into VibeController's existing mapping pipeline.

**Architecture:** Keep `VibeController.Core` independent of WinRT by defining RC901A protocol constants, status records, and a bounded notification queue there. Put a testable BLE session coordinator plus a thin Windows GATT implementation in `VibeController.Infrastructure`; the synchronous `IControllerAdapter.Read` method drains queued snapshots produced by the asynchronous BLE session. Extend the existing WebView bridge and React presentation with a third controller family and a direct-BLE diagnostic panel. The first release subscribes only to HID and known vendor notification characteristics, never writes TCL's DFU service, and does not invent button packet mappings before they are captured from the real remote.

**Tech Stack:** .NET 8, WPF, Windows Runtime Bluetooth/GATT APIs, xUnit, React 19, TypeScript, Vitest, WebView2.

---

### Task 1: Define the RC901A protocol and bridge contracts

**Files:**
- Modify: `src/VibeController.Core/Domain/ControllerType.cs`
- Modify: `src/VibeController.Core/Domain/ControllerControl.cs`
- Create: `src/VibeController.Core/Devices/Rc901aGattProfile.cs`
- Create: `src/VibeController.Core/Devices/Rc901aStatus.cs`
- Modify: `src/VibeController.Core/Runtime/BridgeMessage.cs`
- Test: `tests/VibeController.Core.Tests/Devices/Rc901aGattProfileTests.cs`
- Test: `tests/VibeController.Core.Tests/Runtime/BridgeMessageTests.cs`

- [ ] **Step 1: Write failing protocol safety tests**

```csharp
[Theory]
[InlineData("00001812-0000-1000-8000-00805f9b34fb")]
[InlineData("0000d0ff-3c17-d293-8e48-14fe2e4da212")]
[InlineData("0000d1ff-3c17-d293-8e48-14fe2e4da212")]
public void IsInspectableService_AllowsInputServices(string value) =>
    Assert.True(Rc901aGattProfile.IsInspectableService(Guid.Parse(value)));

[Fact]
public void IsInspectableService_BlocksTclDfuService() =>
    Assert.False(Rc901aGattProfile.IsInspectableService(Rc901aGattProfile.DfuService));
```

- [ ] **Step 2: Run the focused test and confirm RED**

Run: `dotnet test tests/VibeController.Core.Tests --filter FullyQualifiedName~Rc901aGattProfileTests`

Expected: compilation fails because `Rc901aGattProfile` does not exist.

- [ ] **Step 3: Implement the minimal protocol contract**

Add `ControllerType.TclRc901a`; add neutral remote controls for OK, Back, Home, Menu, Mic, volume, mute, channel, and digits; define the exact HID, battery, D0FF, D1FF and DFU UUIDs. `IsInspectableService` must return true only for HID/D0FF/D1FF and never for DFU.

```csharp
public static bool IsInspectableService(Guid serviceUuid) =>
    serviceUuid == HidService ||
    serviceUuid == VendorD0Service ||
    serviceUuid == VendorD1Service;
```

Add immutable `Rc901aStatus` and `Rc901aPacketSample` records with connection state, device name/id, battery, message, subscribed characteristic count, and the newest bounded packet samples. Add the optional status to `RuntimeConfigurationPayload`.

- [ ] **Step 4: Add and run bridge serialization test**

The JSON must serialize `controllerType` as `tclRc901a` and packet bytes as an uppercase space-separated hex string. Run all Core tests and expect green.

- [ ] **Step 5: Commit the contract**

```powershell
git add src/VibeController.Core tests/VibeController.Core.Tests
git commit -m "feat: define RC901A BLE contracts"
```

### Task 2: Build the testable RC901A BLE session and input queue

**Files:**
- Create: `src/VibeController.Infrastructure/Windows/IRc901aGattClient.cs`
- Create: `src/VibeController.Infrastructure/Windows/Rc901aBleSession.cs`
- Create: `src/VibeController.Infrastructure/Windows/Rc901aControllerAdapter.cs`
- Create: `src/VibeController.Core/Devices/Rc901aReportInterpreter.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/Rc901aBleSessionTests.cs`
- Test: `tests/VibeController.Core.Tests/Devices/Rc901aReportInterpreterTests.cs`

- [ ] **Step 1: Write failing session lifecycle tests**

Use a fake `IRc901aGattClient` that returns a paired remote and emits notifications. Verify `StartAsync` publishes Scanning -> Connecting -> Connected, keeps only the newest 32 samples, and `DisposeAsync` unsubscribes and disconnects.

- [ ] **Step 2: Run the focused tests and confirm RED**

Run: `dotnet test tests/VibeController.Infrastructure.Tests --filter FullyQualifiedName~Rc901aBleSessionTests`

Expected: compilation fails because the session and client interface do not exist.

- [ ] **Step 3: Implement the minimal session coordinator**

```csharp
public interface IRc901aGattClient : IAsyncDisposable
{
    event Action<Rc901aGattNotification>? NotificationReceived;
    Task<Rc901aGattConnection> ConnectAsync(string? preferredDeviceId, CancellationToken token);
}
```

The session owns cancellation, publishes immutable status snapshots, deduplicates exact consecutive notifications, caps samples at 32, and never exposes a write operation.

- [ ] **Step 4: Write failing report-interpreter tests**

Verify an unknown packet produces no logical control, while a registered captured signature produces a press snapshot followed by a release snapshot. This registry starts empty in production so no packet mapping is guessed.

- [ ] **Step 5: Implement the adapter queue and run tests GREEN**

`Rc901aControllerAdapter` starts the asynchronous session, queues translated snapshots, and returns one queued snapshot per existing 16ms runtime read. When no new packet exists it returns the last snapshot; when disconnected it returns `ControllerReadResult.Disconnected`.

- [ ] **Step 6: Commit the session**

```powershell
git add src/VibeController.Core src/VibeController.Infrastructure tests
git commit -m "feat: add RC901A BLE session queue"
```

### Task 3: Implement the Windows GATT client

**Files:**
- Modify: `src/VibeController.Infrastructure/VibeController.Infrastructure.csproj`
- Modify: `src/VibeController.App/VibeController.App.csproj`
- Modify: `tests/VibeController.Infrastructure.Tests/VibeController.Infrastructure.Tests.csproj`
- Create: `src/VibeController.Infrastructure/Windows/WindowsRc901aGattClient.cs`
- Create: `src/VibeController.Infrastructure/Windows/Rc901aGattDiscoveryPolicy.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/Rc901aGattDiscoveryPolicyTests.cs`

- [ ] **Step 1: Write failing discovery-policy tests**

Verify exact-name preference (`BT_RC901A_B1`), case-insensitive RC901A fallback, and rejection of unrelated paired BLE devices. Verify notification properties select Notify before Indicate and skip characteristics with neither property.

- [ ] **Step 2: Run the focused test and confirm RED**

Run: `dotnet test tests/VibeController.Infrastructure.Tests --filter FullyQualifiedName~Rc901aGattDiscoveryPolicyTests`

Expected: compilation fails because the discovery policy does not exist.

- [ ] **Step 3: Implement the policy and thin WinRT client**

Target the Windows projects at `net8.0-windows10.0.19041.0`. Use `BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)` and `DeviceInformation.FindAllAsync` to find the paired remote. Open uncached GATT services; read Battery Level when available; subscribe only to notification/indication characteristics under HID, D0FF, and D1FF; continue if Windows denies one service; and surface per-service errors in the connection message. Do not call any GATT write API except the standard client-characteristic notification descriptor required to subscribe.

- [ ] **Step 4: Verify build and all infrastructure tests**

Run: `dotnet test tests/VibeController.Infrastructure.Tests`

Run: `dotnet build VibeController.sln --configuration Debug`

Expected: zero warnings and zero errors.

- [ ] **Step 5: Commit the Windows client**

```powershell
git add src/VibeController.Infrastructure src/VibeController.App tests/VibeController.Infrastructure.Tests
git commit -m "feat: connect RC901A through Windows GATT"
```

### Task 4: Wire RC901A into runtime settings and commands

**Files:**
- Modify: `src/VibeController.Infrastructure/Windows/WindowsControllerAdapterFactory.cs`
- Modify: `src/VibeController.Infrastructure/Settings/AppSettings.cs`
- Modify: `src/VibeController.Infrastructure/Settings/JsonSettingsStore.cs`
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Modify: `src/VibeController.Core/Mapping/DefaultProfileFactory.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/DefaultProfileFactoryTests.cs`

- [ ] **Step 1: Write failing settings migration and default-mapping tests**

Verify existing v1.1 settings load unchanged while missing RC901A mappings are added: OK -> Send, Back -> Backspace, Mic -> CodexDictation, Home -> ActivateCodex, Menu -> CommandPalette, directions -> arrow shortcuts, and volume/channel/digits -> None.

- [ ] **Step 2: Run focused tests and confirm RED**

Run both test classes and expect assertions to fail because RC901A defaults are absent.

- [ ] **Step 3: Implement runtime wiring**

The factory creates `Rc901aControllerAdapter`; `ControllerRuntimeService` subscribes to its status changes, includes `Rc901aStatus` in bridge configuration, rebuilds when selecting TCL, and handles `refreshRc901a` plus `clearRc901aSamples`. Existing Xbox/PS5 behavior remains unchanged.

- [ ] **Step 4: Run backend tests GREEN**

Run: `dotnet test VibeController.sln --configuration Debug`

- [ ] **Step 5: Commit runtime integration**

```powershell
git add src tests
git commit -m "feat: integrate RC901A with controller runtime"
```

### Task 5: Add the TCL direct-BLE frontend experience

**Files:**
- Modify: `frontend/src/app/types.ts`
- Modify: `frontend/src/app/controllerPresentation.ts`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/components/ControllerVisual.tsx`
- Create: `frontend/src/components/TclRemoteController.tsx`
- Modify: `frontend/src/pages/Settings.tsx`
- Modify: `frontend/src/pages/MappingEditor.tsx`
- Modify: `frontend/src/styles/global.css`
- Test: `frontend/src/pages/Settings.test.tsx`
- Test: `frontend/src/pages/Dashboard.test.tsx`
- Test: `frontend/src/pages/MappingEditor.test.tsx`

- [ ] **Step 1: Write failing UI tests**

Verify selecting `TCL RC901A` saves `controllerType: "tclRc901a"`; the controller-index selector is hidden for TCL; the direct-BLE panel displays device name, state, battery, subscribed-characteristic count, latest packet samples, and refresh/clear buttons; Dashboard renders a remote visual; MappingEditor lists neutral remote controls.

- [ ] **Step 2: Run Vitest and confirm RED**

Run: `npm test -- --run`

Expected: TCL labels and controls cannot be found.

- [ ] **Step 3: Implement the minimal third-device UI**

Extend the TypeScript bridge contract and presentation table. Add a neutral remote silhouette rather than inventing product artwork. In Settings explain: `Windows HID 驱动不可用不影响 VibeController 直接 BLE 连接`. Show packet hex only in an Advanced/diagnostic area. Keep microphone wording explicit: the Mic button can trigger Codex dictation, but remote audio is not yet supported.

- [ ] **Step 4: Run frontend tests and production build GREEN**

Run: `npm test -- --run`

Run: `npm run build`

- [ ] **Step 5: Commit the frontend**

```powershell
git add frontend
git commit -m "feat: add TCL RC901A direct BLE UI"
```

### Task 6: Verify on the real remote and document the capture workflow

**Files:**
- Modify: `README.md`
- Modify: `PRD.md`
- Create: `docs/testing/RC901A-BLE-CAPTURE.md`

- [ ] **Step 1: Document the safe capture sequence**

Document pairing in Windows, selecting TCL in VibeController, expected direct-BLE states, pressing one key at a time, copying diagnostics, and the rule that no DFU service writes are allowed.

- [ ] **Step 2: Run full automated verification**

Run: `dotnet test VibeController.sln --configuration Release`

Run: `npm test -- --run`

Run: `npm run build`

Run: `dotnet publish src/VibeController.App -c Release -r win-x64 --self-contained true`

Expected: all tests pass; frontend and self-contained Windows build complete without warnings.

- [ ] **Step 3: Run the app with the paired BT_RC901A_B1**

Expected acceptance evidence: the UI identifies the paired device, reaches Connected or ConnectedLimited, reports battery when available, subscribes to at least one non-DFU characteristic, and displays timestamped packet samples when physical buttons are pressed. If Windows denies all input characteristics, preserve the exact error message and stop before introducing writes or a custom driver.

- [ ] **Step 4: Commit documentation and verification notes**

```powershell
git add README.md PRD.md docs/testing/RC901A-BLE-CAPTURE.md
git commit -m "docs: add RC901A BLE capture workflow"
```

