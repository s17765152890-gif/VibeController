# Global Dictation and Right-Stick Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Xbox X toggle Codex dictation globally and use four discrete right-stick flicks to switch models and adjust reasoning effort.

**Architecture:** Replace the app-scoped dictation shortcut with Codex's OS-global toggle-dictation command, using a rare accelerator shared by Codex and VibeController. Add a stateful right-stick gesture detector with activation, dominance, and re-arm thresholds; map horizontal flicks to adjacent-model selection and vertical flicks to native reasoning-effort commands. Keep raw stick axes for the live controller animation while dispatching one semantic action per flick.

**Tech Stack:** .NET 8, C# records and xUnit, WPF/WebView2, React 19, TypeScript, Vitest, Windows `SendInput`, Codex desktop `keybindings.json`.

---

### Task 1: Lock down the Codex shortcut contract

**Files:**
- Modify: `src/VibeController.Infrastructure/Settings/AppSettings.cs`
- Modify: `src/VibeController.Infrastructure/Windows/KeyboardInputBuilder.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/KeyboardInputBuilderTests.cs`

- [ ] **Step 1: Write failing tests for the rare function-key shortcuts**

Add theory cases proving `F10`, `F11`, and `F12` become the correct Windows virtual keys and preserve modifier down/up order.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
& .\.tools\dotnet\dotnet.exe test .\tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~KeyboardInputBuilderTests
```

Expected: FAIL because function keys are not parsed.

- [ ] **Step 3: Add the shortcut defaults and virtual keys**

Use these exact contracts:

```csharp
DictationShortcut = Ctrl + Alt + Shift + F12;
DecreaseReasoning = Ctrl + Alt + Shift + F10;
IncreaseReasoning = Ctrl + Alt + Shift + F11;
OpenModelPicker = Ctrl + Shift + M;
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Expected: all `KeyboardInputBuilderTests` pass.

### Task 2: Convert the right stick into four re-armed gestures

**Files:**
- Create: `src/VibeController.Core/Mapping/RightStickGestureDetector.cs`
- Modify: `src/VibeController.Core/Domain/ControllerControl.cs`
- Modify: `src/VibeController.Core/Runtime/ControllerRuntime.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/RightStickGestureDetectorTests.cs`
- Test: `tests/VibeController.Core.Tests/Runtime/ControllerRuntimeTests.cs`

- [ ] **Step 1: Write failing detector tests**

Cover all four directions plus these invariants:

```text
activation threshold = 0.72
neutral re-arm threshold = 0.35
dominance margin = 0.12
one action while held
no diagonal action when neither axis dominates
```

- [ ] **Step 2: Run the focused detector tests and verify RED**

Expected: FAIL because `RightStickGestureDetector` and semantic controls do not exist.

- [ ] **Step 3: Implement the minimal state machine**

Expose semantic controls `RightStickLeft`, `RightStickRight`, `RightStickUp`, and `RightStickDown`. Emit one `Pressed` event after crossing the threshold; remain disarmed until both axes return within the neutral threshold.

- [ ] **Step 4: Integrate with `ControllerRuntime`**

Keep raw `RightStickX`/`RightStickY` events for visual state. Append semantic gesture events before mapping and reset the detector on disconnect.

- [ ] **Step 5: Run focused Core tests and verify GREEN**

Expected: direction, hold, diagonal, neutral re-arm, and runtime dispatch tests pass.

### Task 3: Dispatch global dictation, model, and reasoning actions

**Files:**
- Modify: `src/VibeController.Core/Domain/MappedAction.cs`
- Modify: `src/VibeController.Core/Mapping/DefaultProfileFactory.cs`
- Modify: `src/VibeController.Core/Execution/ActionDispatcher.cs`
- Modify: `src/VibeController.Infrastructure/Windows/WindowsActionExecutor.cs`
- Test: `tests/VibeController.Core.Tests/Execution/ActionDispatcherTests.cs`
- Test: `tests/VibeController.Core.Tests/Mapping/DefaultProfileFactoryTests.cs`
- Test: `tests/VibeController.Infrastructure.Tests/Windows/WindowsActionExecutorTests.cs`

- [ ] **Step 1: Write failing action tests**

Require:

```text
X -> global toggle dictation accelerator, even when Codex is not foreground
Right -> open model picker, ArrowDown, Enter
Left -> open model picker, ArrowUp, Enter
Up -> increase reasoning accelerator
Down -> decrease reasoning accelerator
```

- [ ] **Step 2: Run focused tests and verify RED**

Expected: FAIL because the semantic actions and global-dictation foreground exemption do not exist.

- [ ] **Step 3: Implement minimal action kinds and mappings**

Add `PreviousModel`, `NextModel`, `IncreaseReasoning`, and `DecreaseReasoning`. Make only `ActivateCodex` and `CodexDictation` bypass the Codex-foreground guard. Remove right-stick scrolling from the default profile.

- [ ] **Step 4: Implement model picker macros**

Open the picker with `Ctrl+Shift+M`, allow the picker to settle, send one arrow key, then `Enter`. Use the native Codex reasoning commands through the rare accelerators.

- [ ] **Step 5: Run focused tests and verify GREEN**

Expected: action, shortcut sequence, and foreground-guard tests pass.

### Task 4: Expose the new mappings in the UI

**Files:**
- Modify: `frontend/src/components/ActionPicker.tsx`
- Modify: `frontend/src/pages/MappingEditor.tsx`
- Modify: `frontend/src/pages/Dashboard.tsx`
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Test: `frontend/src/pages/MappingEditor.test.tsx`
- Test: `frontend/src/pages/Dashboard.test.tsx`

- [ ] **Step 1: Write failing UI tests**

Require four right-stick direction rows, semantic action labels, and dashboard hints for horizontal model and vertical reasoning control.

- [ ] **Step 2: Run Vitest and verify RED**

Run:

```powershell
Set-Location frontend
npm test -- --run src/pages/MappingEditor.test.tsx src/pages/Dashboard.test.tsx
```

Expected: FAIL because the new controls and labels are absent.

- [ ] **Step 3: Implement bridge formatting and UI labels**

Round-trip the four new action names through `TryAction`/`FormatAction`, show them in the mapping editor, and keep the existing physical right-stick animation.

- [ ] **Step 4: Run focused UI tests and verify GREEN**

Expected: both page test files pass.

### Task 5: Install and verify Codex keybindings safely

**Files:**
- Create or merge: `%USERPROFILE%\.codex\keybindings.json`

- [ ] **Step 1: Re-check the live keymap file immediately before writing**

If it exists, copy it to `keybindings.backup-<timestamp>.json`. Preserve unrelated commands.

- [ ] **Step 2: Merge the three VibeController bindings**

```json
[
  { "command": "globalDictationToggle", "key": "Ctrl+Alt+Shift+F12" },
  { "command": "composer.decreaseReasoningEffort", "key": "Ctrl+Alt+Shift+F10" },
  { "command": "composer.increaseReasoningEffort", "key": "Ctrl+Alt+Shift+F11" }
]
```

- [ ] **Step 3: Parse and inspect the resulting JSON**

Verify the three exact bindings and the preservation of any unrelated entries. Do not restart Codex from this task because it owns the active development thread; report that one user restart is required to load externally edited global accelerators.

### Task 6: Full verification and Windows publish

**Files:**
- Verify: `scripts/test.ps1`
- Verify: `scripts/build.ps1`
- Verify: `artifacts/win-x64/VibeController.App.exe`

- [ ] **Step 1: Run the complete test suite**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Expected: frontend tests, typecheck, frontend build, Core tests, and Infrastructure tests all pass.

- [ ] **Step 2: Publish the self-contained Windows build**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Expected: `artifacts/win-x64` is refreshed successfully.

- [ ] **Step 3: Launch and verify the published process**

Confirm `VibeController.App` remains alive, is responding, and has the expected window title. Hardware validation remains a separate physical-controller check after Codex restarts and loads the new keymap.

---

**Safety note:** This repository is already a user-owned dirty feature branch. Do not create a new worktree, reset, or commit the unrelated baseline during this plan.
