# VibeController MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use test-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop MVP that detects an Xbox Series X|S controller, maps its inputs to safe Codex keyboard/mouse actions, and provides a polished Apple-inspired visual mapping interface.

**Architecture:** A .NET 8 WPF host owns XInput polling, mapping, foreground safety, SendInput output, persistence, tray behavior, and WebView2 messaging. A React/TypeScript frontend renders the controller, live device state, mapping editor, settings, and diagnostics. Device adapters emit normalized input events so a future TCL HID adapter can reuse the mapping and output layers.

**Tech Stack:** .NET 8, C# 12, WPF, WebView2, xUnit, React 19, TypeScript, Vite, Vitest, Testing Library, CSS, Windows XInput/SendInput APIs.

---

## File map

- `src/VibeController.Core/Domain/*`: normalized controller, action, profile, and runtime state types.
- `src/VibeController.Core/Mapping/*`: default profile and deterministic event-to-action mapping.
- `src/VibeController.Core/Devices/*`: device adapter boundary, XInput state translation, and polling adapter.
- `src/VibeController.Core/Execution/*`: foreground guard and platform-neutral action dispatcher.
- `src/VibeController.Infrastructure/Windows/*`: XInput, SendInput, Codex window activation, vibration, startup, and tray-facing Windows services.
- `src/VibeController.Infrastructure/Settings/*`: local JSON settings persistence.
- `src/VibeController.App/*`: WPF lifetime, WebView2 bridge, controller orchestration, tray icon, and bundled frontend.
- `frontend/src/*`: Apple-inspired React interface and controller visualization.
- `tests/VibeController.Core.Tests/*`: mapping, state translation, safety, and orchestration tests.
- `tests/VibeController.Infrastructure.Tests/*`: settings and Windows payload construction tests.
- `frontend/src/**/*.test.tsx`: frontend component and interaction tests.

## Task 1: Bootstrap the repository and local toolchain

**Files:**
- Create: `.gitignore`
- Create: `global.json`
- Create: `VibeController.sln`
- Create: `src/VibeController.Core/VibeController.Core.csproj`
- Create: `src/VibeController.Infrastructure/VibeController.Infrastructure.csproj`
- Create: `src/VibeController.App/VibeController.App.csproj`
- Create: `tests/VibeController.Core.Tests/VibeController.Core.Tests.csproj`
- Create: `tests/VibeController.Infrastructure.Tests/VibeController.Infrastructure.Tests.csproj`

- [ ] Install .NET 8 SDK into `.tools/dotnet` with Microsoft's non-admin install script.
- [ ] Initialize Git and switch to `codex/mvp-implementation` before implementation.
- [ ] Create the solution and projects with `dotnet new`, then add project references.
- [ ] Add WebView2 only to the WPF project and xUnit packages only to test projects.
- [ ] Run `dotnet test VibeController.sln` and confirm the empty baseline passes.

## Task 2: Define the normalized input and action model

**Files:**
- Create: `src/VibeController.Core/Domain/ControllerControl.cs`
- Create: `src/VibeController.Core/Domain/ControllerSnapshot.cs`
- Create: `src/VibeController.Core/Domain/InputEvent.cs`
- Create: `src/VibeController.Core/Domain/MappedAction.cs`
- Create: `src/VibeController.Core/Domain/MappingProfile.cs`
- Test: `tests/VibeController.Core.Tests/Domain/ControllerSnapshotTests.cs`

- [ ] Write a failing test proving two snapshots emit one `Pressed` event on a rising edge and one `Released` event on a falling edge.
- [ ] Run the focused test and confirm it fails because the domain types do not exist.
- [ ] Implement immutable records/enums for controls, snapshots, edges, actions, and profile entries.
- [ ] Run the focused test and confirm it passes.
- [ ] Add failing tests for trigger hysteresis and stick dead-zone normalization.
- [ ] Implement deterministic normalization helpers and confirm the full Core test suite passes.

The action union must support these exact kinds: `ActivateCodex`, `CodexDictation`, `Send`, `Cancel`, `CommandPalette`, `PreviousChat`, `NextChat`, `KeyboardShortcut`, `MouseMove`, `MouseLeftClick`, `MouseRightClick`, `MouseScroll`, and `None`.

## Task 3: Implement the default profile and mapping engine

**Files:**
- Create: `src/VibeController.Core/Mapping/DefaultProfileFactory.cs`
- Create: `src/VibeController.Core/Mapping/MappingEngine.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/DefaultProfileFactoryTests.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/MappingEngineTests.cs`

- [ ] Write failing tests for the PRD default map: Menu activates Codex, X dictates, A sends, B cancels, Y opens commands, LB/RB switch chats, D-pad emits arrows, sticks/triggers emit mouse actions.
- [ ] Verify the tests fail for missing production types.
- [ ] Implement the default profile factory and mapping lookup.
- [ ] Verify default mapping tests pass.
- [ ] Write failing tests proving ordinary buttons do not auto-repeat, D-pad may repeat after a configured delay, and paused mapping emits no actions.
- [ ] Implement minimal repeat policy and pause behavior; run all Core tests.

## Task 4: Persist settings safely

**Files:**
- Create: `src/VibeController.Infrastructure/Settings/AppSettings.cs`
- Create: `src/VibeController.Infrastructure/Settings/ISettingsStore.cs`
- Create: `src/VibeController.Infrastructure/Settings/JsonSettingsStore.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`

- [ ] Write a failing round-trip test using a temporary directory and a custom mapping.
- [ ] Verify the test fails because the store is missing.
- [ ] Implement UTF-8 JSON save via temporary file plus atomic replace/move.
- [ ] Verify the round-trip test passes.
- [ ] Write failing tests for missing and malformed files.
- [ ] Implement default recovery while preserving a `.corrupt-<timestamp>` copy of malformed settings; verify tests pass.

Settings must contain active controller index, mapping enabled state, Codex-only guard, dictation shortcut, mouse speed, scroll speed, dead zone, repeat timing, startup preference, and current mapping profile.

## Task 5: Translate and poll XInput

**Files:**
- Create: `src/VibeController.Core/Devices/IControllerAdapter.cs`
- Create: `src/VibeController.Core/Devices/RawXboxState.cs`
- Create: `src/VibeController.Core/Devices/XboxStateTranslator.cs`
- Create: `src/VibeController.Infrastructure/Windows/XInputNative.cs`
- Create: `src/VibeController.Infrastructure/Windows/XInputControllerAdapter.cs`
- Test: `tests/VibeController.Core.Tests/Devices/XboxStateTranslatorTests.cs`

- [ ] Write failing translator tests for A/B/X/Y, D-pad, shoulders, Menu/View, L3/R3, triggers, and both sticks.
- [ ] Verify failures are caused by the missing translator.
- [ ] Implement the pure translator and confirm tests pass.
- [ ] Implement `XInputGetState` polling behind `IControllerAdapter` for indices 0-3 without putting P/Invoke code in Core.
- [ ] Add a fake adapter integration test proving disconnect/reconnect state transitions are emitted once.
- [ ] Run all .NET tests.

## Task 6: Build safe action execution

**Files:**
- Create: `src/VibeController.Core/Execution/IActionExecutor.cs`
- Create: `src/VibeController.Core/Execution/IForegroundAppService.cs`
- Create: `src/VibeController.Core/Execution/ActionDispatcher.cs`
- Create: `src/VibeController.Infrastructure/Windows/KeyboardInputBuilder.cs`
- Create: `src/VibeController.Infrastructure/Windows/WindowsActionExecutor.cs`
- Create: `src/VibeController.Infrastructure/Windows/CodexWindowService.cs`
- Test: `tests/VibeController.Core.Tests/Execution/ActionDispatcherTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/KeyboardInputBuilderTests.cs`

- [ ] Write failing dispatcher tests proving Codex-only mode blocks actions when Codex is not foreground, except `ActivateCodex`.
- [ ] Verify the expected failure.
- [ ] Implement the guard and dispatcher; verify tests pass.
- [ ] Write failing tests for key-down/key-up payload order for `Ctrl+Shift+D`, Enter, Escape, arrows, and custom shortcuts.
- [ ] Implement pure SendInput payload construction and verify tests pass.
- [ ] Implement Windows execution with `SendInput`, relative mouse input, scroll input, `EnumWindows`, process-name/title matching, and `SetForegroundWindow`.
- [ ] Ensure all modifier keys are released in `finally` when dispatch fails; run the full suite.

## Task 7: Orchestrate polling, mapping, diagnostics, and WebView messages

**Files:**
- Create: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Create: `src/VibeController.App/Services/RuntimeState.cs`
- Create: `src/VibeController.App/Bridge/BridgeMessage.cs`
- Create: `src/VibeController.App/Bridge/WebViewBridge.cs`
- Test: `tests/VibeController.Core.Tests/Runtime/ControllerRuntimeTests.cs`

- [ ] Write a failing runtime test using a fake adapter, fake executor, and in-memory settings store.
- [ ] Prove one X-button rising edge dispatches one dictation action and the release dispatches none.
- [ ] Implement the runtime loop as a testable single-tick service, then wrap it in a 60 Hz background loop.
- [ ] Add reconnect, pause, test-mode, recent-event, and controller-selection tests before their implementations.
- [ ] Define versioned JSON bridge messages for state snapshots, mapping updates, settings updates, pause/resume, reset defaults, and test mode.
- [ ] Run all .NET tests.

## Task 8: Scaffold and test the React frontend

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/vitest.config.ts`
- Create: `frontend/src/main.tsx`
- Create: `frontend/src/App.tsx`
- Create: `frontend/src/app/AppBridge.ts`
- Create: `frontend/src/app/types.ts`
- Create: `frontend/src/app/useRuntimeState.ts`
- Test: `frontend/src/app/AppBridge.test.ts`

- [ ] Scaffold React/TypeScript/Vite configuration and install dependencies.
- [ ] Write a failing bridge test proving a backend state message updates subscribed React state.
- [ ] Verify the test fails for the missing bridge.
- [ ] Implement the browser/WebView2 bridge with a demo fallback for normal browsers.
- [ ] Verify frontend tests pass.
- [ ] Add `npm run test`, `npm run build`, and `npm run typecheck` scripts and confirm each succeeds.

## Task 9: Build the Apple-inspired dashboard and controller visualization

**Files:**
- Create: `frontend/src/styles/tokens.css`
- Create: `frontend/src/styles/global.css`
- Create: `frontend/src/components/AppShell.tsx`
- Create: `frontend/src/components/StatusPill.tsx`
- Create: `frontend/src/components/XboxController.tsx`
- Create: `frontend/src/components/RecentAction.tsx`
- Create: `frontend/src/pages/Dashboard.tsx`
- Test: `frontend/src/pages/Dashboard.test.tsx`

- [ ] Write failing tests for connected, disconnected, paused, and test-mode states.
- [ ] Implement a calm light theme using Segoe UI Variable, neutral materials, one green status accent, one amber pause accent, and one red error accent.
- [ ] Draw an original Xbox-style controller with semantic SVG groups and immediate pressed-state feedback.
- [ ] Keep hardware input feedback unanimated; pointer press feedback may scale to `0.97` for 100-160 ms.
- [ ] Add reduced-motion, reduced-transparency, high-contrast, and keyboard focus styles.
- [ ] Run tests, typecheck, and build.

## Task 10: Build the mapping editor and settings UI

**Files:**
- Create: `frontend/src/pages/MappingEditor.tsx`
- Create: `frontend/src/pages/Settings.tsx`
- Create: `frontend/src/components/ActionPicker.tsx`
- Create: `frontend/src/components/ShortcutRecorder.tsx`
- Create: `frontend/src/components/SliderField.tsx`
- Test: `frontend/src/pages/MappingEditor.test.tsx`
- Test: `frontend/src/pages/Settings.test.tsx`

- [ ] Write failing tests for selecting a physical control, changing its action, recording a shortcut, conflict warning, reset defaults, and save message.
- [ ] Implement the editor with direct labels and a controller-to-panel spatial relationship.
- [ ] Write failing tests for guard mode, dead zone, mouse speed, scroll speed, startup, and diagnostics copy.
- [ ] Implement settings and local bridge messages.
- [ ] Keep frequent tab/navigation transitions instant; animate only occasional panels with transform/opacity under 250 ms and a strong ease-out curve.
- [ ] Run frontend tests, typecheck, and build.

## Task 11: Integrate WPF, WebView2, tray, and startup

**Files:**
- Create: `src/VibeController.App/App.xaml`
- Create: `src/VibeController.App/App.xaml.cs`
- Create: `src/VibeController.App/MainWindow.xaml`
- Create: `src/VibeController.App/MainWindow.xaml.cs`
- Create: `src/VibeController.Infrastructure/Windows/StartupManager.cs`
- Create: `src/VibeController.App/Services/TrayIconService.cs`
- Modify: `src/VibeController.App/VibeController.App.csproj`

- [ ] Configure Vite output to copy into the WPF build as `wwwroot`.
- [ ] Load local `index.html` through WebView2 with no remote navigation permission.
- [ ] Connect bridge messages to the runtime service and publish state updates only when changed.
- [ ] Add tray commands for Open, Enable/Pause, and Exit; close-to-tray by default.
- [ ] Add opt-in HKCU startup management and surface permission failures.
- [ ] Ensure application shutdown releases all injected keys and stops polling.
- [ ] Build the WPF app and launch it against browser demo state before physical-controller testing.

## Task 12: Verify hardware, visual quality, and packaging

**Files:**
- Create: `README.md`
- Create: `docs/testing/hardware-checklist.md`
- Create: `scripts/build.ps1`
- Create: `scripts/test.ps1`

- [ ] Run all .NET and frontend automated tests with zero failures.
- [ ] Run a production frontend build and a Release WPF build.
- [ ] Use Playwright to capture dashboard, mapping, settings, disconnected, paused, and high-contrast screenshots.
- [ ] Review animation code with `review-animations/STANDARDS.md`; record findings in the required Before/After/Why table and fix all blocking findings.
- [ ] Connect the user's Xbox controller and verify X→dictation, A→Enter, B→Escape, Y→command palette, LB/RB chat switching, and mouse controls.
- [ ] Sleep/reconnect the controller 20 times and record results.
- [ ] Publish a self-contained Windows x64 build and document WebView2/runtime requirements.
- [ ] Re-read `PRD.md` line-by-line and record any unimplemented P0/P1 requirement before claiming MVP completion.

## Execution decision

The user explicitly approved starting implementation in the current session. Execute inline with checkpoints after the Core vertical slice, the interactive frontend, and hardware verification. Do not dispatch subagents.
