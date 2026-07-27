# RC901A Photo and Button Learning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the user’s real TCL RC901A remote on the dashboard, preserve live button feedback, and add a guided, persistent button-learning flow for signals that Windows exposes but VibeController does not yet know.

**Architecture:** Keep the existing pipeline `verified Raw Input device -> normalized RC901A input event -> semantic ControllerControl -> configurable action`. Known standard signals remain available without setup. Learning mode captures one pressed signal for a user-selected semantic button, persists the binding, suppresses action dispatch during capture, then rebuilds the adapter with the learned bindings.

**Tech Stack:** .NET 8, WPF/WebView2 bridge, Windows Raw Input, React 19, TypeScript, Vitest, xUnit, CSS.

---

### Task 1: Replace the synthetic remote with the real RC901A photo

**Files:**
- Add: `frontend/public/tcl-rc901a.jpg`
- Modify: `frontend/src/components/TclRemoteController.tsx`
- Modify: `frontend/src/pages/Dashboard.tsx`
- Modify: `frontend/src/app/controllerPresentation.ts`
- Modify: `frontend/src/styles/global.css`
- Test: `frontend/src/pages/Dashboard.test.tsx`

- [ ] **Step 1: Write the failing dashboard test**

Add assertions that TCL mode renders the supplied photo, the verified direction/OK hotspots, and Windows HID wording:

```tsx
expect(screen.getByTestId("controller-photo")).toHaveAttribute(
  "src",
  expect.stringContaining("tcl-rc901a.jpg"),
);
expect(screen.getByTestId("control-remote-ok")).toHaveAttribute(
  "data-pressed",
  "true",
);
expect(screen.getByText("Windows HID")).toBeInTheDocument();
```

- [ ] **Step 2: Run the test and confirm RED**

Run:

```powershell
npm --prefix frontend test -- Dashboard.test.tsx
```

Expected: failure because TCL mode still renders the synthetic shell and “直接 BLE”.

- [ ] **Step 3: Add the supplied photo and photo-based hotspots**

Copy the exact user-provided JPEG to `frontend/public/tcl-rc901a.jpg`. Render it with the shared `controller-photo` test id and overlay non-moving, immediate-feedback hotspots for every physical button represented by a `ControllerControl`. Do not animate repeated remote presses; only change color/opacity.

- [ ] **Step 4: Update product wording**

Replace “直接 BLE” with “Windows HID” on the dashboard and settings status. Keep pairing guidance as center OK + Back for about five seconds.

- [ ] **Step 5: Run the dashboard tests and build**

Run:

```powershell
npm --prefix frontend test -- Dashboard.test.tsx
npm --prefix frontend run build
```

Expected: all selected tests pass and Vite builds successfully.

### Task 2: Define persistent physical-signal bindings

**Files:**
- Create: `src/VibeController.Core/Devices/Rc901aRawInput.cs`
- Modify: `src/VibeController.Core/Domain/ControllerControl.cs`
- Modify: `src/VibeController.Core/Mapping/DefaultProfileFactory.cs`
- Modify: `src/VibeController.Infrastructure/Settings/AppSettings.cs`
- Modify: `src/VibeController.Infrastructure/Settings/JsonSettingsStore.cs`
- Modify: `src/VibeController.Infrastructure/Windows/IRc901aRawInputSource.cs`
- Modify: `src/VibeController.Infrastructure/Windows/Rc901aRawInputInterpreter.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/Rc901aRawInputInterpreterTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/DefaultProfileFactoryTests.cs`

- [ ] **Step 1: Write failing binding and migration tests**

Specify a stable model:

```csharp
public enum Rc901aRawInputKind
{
    Keyboard,
    ConsumerControl,
}

public sealed record Rc901aInputBinding(
    Rc901aRawInputKind Kind,
    ushort Code,
    ControllerControl Control,
    Rc901aBindingSource Source);
```

Tests must prove:

1. The only built-in bindings are the five hardware-verified direction/OK signals.
2. Back/Home/Menu/Mic/volume candidate codes remain inactive until learned.
3. A learned binding overrides no unrelated standard binding.
4. Relearning a semantic control replaces its old signal.
5. Assigning an already-used signal moves it to the new semantic control.
6. JSON settings round-trip learned bindings only.
7. Existing settings load with an empty learned-binding list.
8. New physical controls have `None` defaults.

- [ ] **Step 2: Run the tests and confirm RED**

Run:

```powershell
& 'D:\AI Projects\VibeController\.tools\dotnet\dotnet.exe' test `
  tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj `
  --filter "FullyQualifiedName~Rc901aRawInput|FullyQualifiedName~JsonSettingsStore"
```

Expected: compile/test failure because bindings and new controls do not exist.

- [ ] **Step 3: Add semantic controls for the photographed remote**

Append these controls without renumbering existing values:

```csharp
RemotePower,
RemoteInput,
RemoteRed,
RemoteGreen,
RemoteBlue,
RemoteSettings,
RemoteApp1,
RemoteApp2,
```

Add each to the default profile as `MappedActionKind.None`. Keep the already verified direction/OK defaults unchanged.

- [ ] **Step 4: Add binding normalization**

Implement one helper that upserts a binding while enforcing both uniqueness rules:

```csharp
public static IReadOnlyList<Rc901aInputBinding> Upsert(
    IEnumerable<Rc901aInputBinding> current,
    Rc901aInputBinding replacement) =>
    current
        .Where(item =>
            item.Control != replacement.Control &&
            (item.Kind != replacement.Kind || item.Code != replacement.Code))
        .Append(replacement)
        .ToArray();
```

Use learned bindings plus these immutable verified defaults in `Rc901aRawInputInterpreter`:

```text
Keyboard 0x26 -> RemoteUp
Keyboard 0x28 -> RemoteDown
Keyboard 0x25 -> RemoteLeft
Keyboard 0x27 -> RemoteRight
Keyboard 0x0D -> RemoteOk
```

Verified defaults cannot be silently overwritten. Other previous hard-coded candidates are removed from the active map.

- [ ] **Step 5: Persist bindings**

Add this property to `AppSettings`:

```csharp
public IReadOnlyList<Rc901aInputBinding> Rc901aLearnedBindings { get; init; } = [];
```

Preserve custom mappings and learned bindings during migration.

- [ ] **Step 6: Run the focused tests and confirm GREEN**

Run the focused command from Step 2. Expected: all selected tests pass with no warnings.

### Task 3: Add a safe one-button-at-a-time learning state machine

**Files:**
- Modify: `src/VibeController.Core/Runtime/BridgeMessage.cs`
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Modify: `src/VibeController.Infrastructure/Windows/WindowsControllerAdapterFactory.cs`
- Modify: `src/VibeController.Infrastructure/Windows/Rc901aControllerAdapter.cs`
- Test: `tests/VibeController.Core.Tests/Runtime/BridgeMessageTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/WindowsControllerAdapterFactoryTests.cs`
- Add: `tests/VibeController.App.Tests/Services/Rc901aLearningStateTests.cs` if an app test project exists; otherwise extract the state machine into Infrastructure and test it there.

- [ ] **Step 1: Write failing state-machine tests**

Specify these transitions:

```text
Idle -> Start(target control) -> Listening
Listening + pressed input -> AwaitingMatchingRelease
AwaitingMatchingRelease + same release -> Review
Review + Confirm -> Saving -> Idle
Review + Retry -> Listening
Listening + Cancel -> Idle
```

Tests must prove that action dispatch is suppressed throughout learning, only the matching release advances the state, a confirmed binding is persisted once, and cancellation/timeout/disconnect changes no settings. A conflict with a verified default is shown and cannot be silently overwritten.

- [ ] **Step 2: Run and confirm RED**

Run the smallest owning test project. Expected: failure because learning commands/status do not exist.

- [ ] **Step 3: Add bridge state**

Expose a compact status in runtime configuration:

```csharp
public sealed record Rc901aLearningStatus(
    Rc901aLearningPhase Phase,
    string? SessionId,
    ControllerControl? TargetControl,
    Rc901aRawInputKind? CapturedKind,
    ushort? CapturedCode,
    ControllerControl? ConflictControl,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<Rc901aInputBinding> Bindings);
```

Add command handling for:

```text
startRc901aLearning { control }
confirmRc901aLearning { sessionId }
retryRc901aLearning { sessionId }
cancelRc901aLearning { sessionId }
resetRc901aLearnedBindings {}
```

- [ ] **Step 4: Capture without dispatching an action**

Subscribe the runtime to the already hardware-verified `IRc901aRawInputSource.InputReceived`. While listening:

```csharp
MappingEnabled = _settings.MappingEnabled && !_rc901aLearning.IsListening
```

Capture the first press, wait for the matching release, then publish a review state. Only `confirmRc901aLearning` upserts and saves the binding. Rebuild the RC901A adapter after confirmation, then return to idle.

- [ ] **Step 5: Inject learned bindings**

Pass `AppSettings.Rc901aLearnedBindings` through `WindowsControllerAdapterFactory` into `Rc901aRawInputInterpreter`, where they are combined with the five immutable verified defaults.

- [ ] **Step 6: Run state, factory, bridge, and full .NET tests**

Run:

```powershell
& 'D:\AI Projects\VibeController\.tools\dotnet\dotnet.exe' test VibeController.sln
```

Expected: all projects pass with zero failures.

### Task 4: Build the guided learning interface

**Files:**
- Modify: `frontend/src/app/types.ts`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/pages/Settings.tsx`
- Modify: `frontend/src/pages/Settings.test.tsx`
- Modify: `frontend/src/app/controllerPresentation.ts`
- Modify: `frontend/src/styles/global.css`

- [ ] **Step 1: Write failing UI tests**

Tests must prove:

1. The learning panel is shown only for TCL RC901A.
2. Clicking “学习按键” reveals physical buttons rather than immediately starting an irreversible sequence.
3. Clicking “返回键” sends `startRc901aLearning` with `remoteBack`.
4. Listening shows “请按遥控器上的返回键” and a cancel action.
5. After matching release, review shows the signal and explicit Confirm/Retry actions.
6. Learned, verified-standard, conflict, and unlearned states are visually distinguishable.
7. A user can skip any button; there is no requirement to complete the full remote.

- [ ] **Step 2: Run and confirm RED**

Run:

```powershell
npm --prefix frontend test -- Settings.test.tsx
```

Expected: failure because learning props and commands do not exist.

- [ ] **Step 3: Implement the inline learning sheet**

Use an occasional, origin-aware 180–220 ms ease-out reveal. The listening state gets an immediate blue focus ring and live status text; actual remote presses do not animate. Buttons use a subtle `scale(.97)` active state and honor reduced motion.

The primary explanation is:

```text
方向键和 OK 已由 Windows 标准识别，无需重学。
其他按键可按需学习：先在这里选一个按键，再按遥控器上的对应键。
```

- [ ] **Step 4: Wire bridge commands**

Add the five command types from Task 3 and route them from `App.tsx`.

- [ ] **Step 5: Run frontend tests, typecheck, and build**

Run:

```powershell
npm --prefix frontend test
npm --prefix frontend run typecheck
npm --prefix frontend run build
```

Expected: all commands succeed.

### Task 5: Hardware acceptance and documentation

**Files:**
- Modify: `README.md`
- Modify: `PRD.md`
- Modify: `docs/testing/RC901A-HID-FILTER.md`

- [ ] **Step 1: Launch the feature build**

Run the built WPF app from this worktree and select TCL RC901A.

- [ ] **Step 2: Verify standard controls**

Confirm Up, Down, Left, Right, and OK produce immediate photo hotspot feedback and the existing semantic actions.

- [ ] **Step 3: Learn unknown controls**

Use the UI to learn Back, Home, Menu, Mic, Volume+, and Volume−. Verify each learned signal survives an app restart.

- [ ] **Step 4: Verify safety**

Confirm:

1. A normal PC keyboard cannot enter RC901A learning or trigger remote controls.
2. No Codex action fires while learning is listening.
3. Cancel leaves bindings unchanged.
4. The app still works while hidden to the tray.
5. Holding a learned or verified direction key repeats according to the existing delay/interval and stops on release.

- [ ] **Step 5: Document the recommended default layout**

Document this conservative recommendation without forcing it on existing profiles:

| Physical button | Recommended action |
| --- | --- |
| Direction ring | Arrow Up/Down/Left/Right |
| OK | Send |
| Back | Backspace |
| Home | Activate Codex |
| Menu | Command palette |
| Microphone | Toggle Codex dictation |
| Volume + / − | Increase / decrease reasoning strength |
| bilibili / iQIYI | Previous / next task |
| Power, input, colored, settings | Unassigned until the user chooses |

- [ ] **Step 6: Run complete verification**

Run .NET tests, frontend tests/typecheck/build, native descriptor tests, driver analysis/signability checks, and `git diff --check`. Do not restore the one-boot driver trust state until hardware acceptance is complete.
