# Codex Shortcut Auto-Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve every Codex-specific mapped action from the current Windows user's Codex keyboard-shortcut settings when that action is first used, while leaving plain keyboard and mouse mappings direct and deterministic.

**Architecture:** Add a lazy, file-backed Codex shortcut resolver in the Windows infrastructure layer. It maps VibeController semantic actions to Codex command IDs, applies the same custom-binding precedence used by Codex (`keybindings.json` entries replace defaults; a `null` key unbinds the command), chooses the first supported Windows binding, and reloads when the file changes. `WindowsActionExecutor` asks this resolver only for Codex-specific actions; simple Enter/Escape/custom keyboard/mouse actions bypass it.

**Tech Stack:** .NET 8, WPF, `System.Text.Json`, Windows `SendInput`, xUnit, React 19, TypeScript, Vitest.

**Codex compatibility evidence:** The installed Codex 26.715.2305.0 command registry uses `globalDictationToggle`, `openCommandMenu`, `previousThread`, `nextThread`, `previousRecentThread`, `nextRecentThread`, `previousTab`, `nextTab`, `composer.increaseReasoningEffort`, and `composer.decreaseReasoningEffort`. Its keymap loader reads `%USERPROFILE%\.codex\keybindings.json` as an array of `{ command, key }`, where `key` is nullable.

**Delivery note:** This repository is an uncommitted MVP worktree. Preserve all existing work, do not create commits, and publish only after the full verification suite passes.

---

### Task 1: Specify the Codex shortcut compatibility layer with failing tests

**Files:**
- Create: `tests/VibeController.Infrastructure.Tests/Windows/CodexShortcutResolverTests.cs`
- Modify: `tests/VibeController.Infrastructure.Tests/Windows/WindowsActionExecutorTests.cs`

- [ ] **Step 1: Add resolver tests for Codex-compatible precedence**

Cover custom bindings, default bindings when no override exists, multiple custom bindings (first supported wins), `null` unbinding, unknown/unsupported keys, malformed JSON, a missing file, and reload after the file changes.

- [ ] **Step 2: Add executor boundary tests**

Assert that Codex semantic actions call the resolver, while `Send`, `Cancel`, custom `KeyboardShortcut`, and mouse actions never read Codex settings. Assert that a failed resolution becomes a clear action failure rather than sending the old hardcoded shortcut.

- [ ] **Step 3: Run focused tests and verify RED**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~CodexShortcutResolver|FullyQualifiedName~WindowsActionExecutor"
```

Expected: compilation fails because the resolver contract and implementation do not exist.

### Task 2: Implement lazy parsing and Codex command resolution

**Files:**
- Create: `src/VibeController.Infrastructure/Windows/ICodexShortcutResolver.cs`
- Create: `src/VibeController.Infrastructure/Windows/CodexShortcutResolver.cs`
- Create: `src/VibeController.Infrastructure/Windows/CodexShortcutCatalog.cs`
- Create: `src/VibeController.Infrastructure/Windows/KeyboardShortcutParser.cs`
- Modify: `src/VibeController.Infrastructure/Windows/KeyboardInputBuilder.cs`

- [ ] **Step 1: Add the semantic command catalog**

Map only these Codex actions:

```text
CodexDictation       -> globalDictationToggle
Send                 -> composer.submit (Enter fallback, matching Codex composer behavior)
CommandPalette       -> openCommandMenu
PreviousChat         -> previousThread
NextChat             -> nextThread
PreviousRecentThread -> previousRecentThread
NextRecentThread     -> nextRecentThread
PreviousTab          -> previousTab
NextTab              -> nextTab
IncreaseReasoning    -> composer.increaseReasoningEffort
DecreaseReasoning    -> composer.decreaseReasoningEffort
```

Keep known Windows defaults only for commands that Codex itself currently defines. Do not invent fallbacks for global dictation toggle or reasoning-strength commands; if those are unbound, instruct the user to bind them in Codex Settings > Keyboard Shortcuts.

- [ ] **Step 2: Parse Codex accelerators safely**

Support `Ctrl`, `Control`, `CmdOrCtrl` (Control on Windows), `Shift`, `Alt`, `Win`, `Windows`, `Meta`, letters, digits, arrows, brackets, Enter, Escape, Backspace, Tab, Home/End, PageUp/PageDown, Delete, Space, and F1-F24. Reject modifier-only and multi-step/chord strings rather than emitting the wrong input.

- [ ] **Step 3: Implement lazy, change-aware file loading**

Do not touch the Codex keymap file at application startup or for plain input actions. On first Codex action, read `$CODEX_HOME\keybindings.json` (`%USERPROFILE%\.codex\keybindings.json` when `CODEX_HOME` is unset); cache the parsed result and reload when file existence, last-write time, or length changes. Preserve Codex precedence: any matching custom entries replace all defaults, and any matching `null` entry leaves the command unbound.

- [ ] **Step 4: Run resolver tests and verify GREEN**

Run the focused command from Task 1 and confirm resolver tests pass.

### Task 3: Route semantic Codex actions through the resolver

**Files:**
- Modify: `src/VibeController.Infrastructure/Windows/WindowsActionExecutor.cs`
- Modify: `src/VibeController.App/Services/ControllerRuntimeService.cs`
- Modify: `tests/VibeController.Infrastructure.Tests/Windows/WindowsActionExecutorTests.cs`

- [ ] **Step 1: Inject the resolver into the executor**

Replace every hardcoded Codex shortcut branch with one shared `SendCodexShortcut(MappedActionKind)` path. Keep `ActivateCodex` on its native window service and keep simple keyboard/mouse actions direct.

- [ ] **Step 2: Wire the real user profile path**

Construct `CodexShortcutResolver` with the current process's `CODEX_HOME` when configured, otherwise `Environment.SpecialFolder.UserProfile` plus `.codex\keybindings.json`, when rebuilding the runtime.

- [ ] **Step 3: Surface actionable failures**

When a Codex command is unbound, malformed, unsupported, or unavailable, throw a concise Chinese error containing the Codex action and Settings > Keyboard Shortcuts guidance. Let `ActionDispatcher` expose it through the existing recent-action/status path.

- [ ] **Step 4: Run executor and complete .NET tests**

```powershell
.\.tools\dotnet\dotnet.exe test tests\VibeController.Infrastructure.Tests\VibeController.Infrastructure.Tests.csproj --no-restore
.\.tools\dotnet\dotnet.exe test tests\VibeController.Core.Tests\VibeController.Core.Tests.csproj --no-restore
```

Expected: all tests pass.

### Task 4: Make automatic shortcut ownership clear in the UI

**Files:**
- Modify: `frontend/src/pages/Settings.tsx`
- Modify: `frontend/src/pages/Settings.test.tsx`
- Modify: `frontend/src/pages/MappingEditor.tsx`
- Modify: `frontend/src/pages/MappingEditor.test.tsx`
- Modify: `frontend/src/styles/global.css` only if the existing note style is insufficient

- [ ] **Step 1: Write failing UI expectation tests**

Assert that settings no longer imply VibeController owns the dictation shortcut and that selecting a Codex semantic action explains it will read the active shortcut from Codex on first use. Keep action option names identical to Codex terminology.

- [ ] **Step 2: Replace the editable legacy dictation field with an auto-sync explanation**

Retain the old serialized setting for backward compatibility, but stop presenting it as an active source of truth. Explain that custom Codex shortcuts are picked up automatically and later edits do not require remapping.

- [ ] **Step 3: Add contextual mapping help**

For semantic Codex actions, show a restrained note: `首次触发时读取 Codex 当前快捷键；修改 Codex 设置后会自动刷新。` Do not show it for plain keyboard/mouse actions.

- [ ] **Step 4: Run frontend tests, typecheck, and build**

```powershell
npm --prefix frontend test -- --run
npm --prefix frontend run typecheck
npm --prefix frontend run build
```

Expected: all frontend checks pass.

### Task 5: Full verification, publish, and runtime smoke test

**Files:**
- Modify only if verification exposes a defect.

- [ ] **Step 1: Run the complete repository suite**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

- [ ] **Step 2: Test against a temporary Codex keymap**

Use automated tests—not the user's real file—to prove two different users' bindings resolve differently, explicit unbinding sends nothing, and ordinary keyboard/mouse actions do not read the file.

- [ ] **Step 3: Publish the Windows bundle**

Stop only the verified `artifacts\win-x64\VibeController.App.exe` process, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

- [ ] **Step 4: Restart and verify runtime health**

Start `artifacts\win-x64\VibeController.App.exe`, confirm it remains responsive, and confirm the current user's existing Codex bindings resolve without changing `%USERPROFILE%\.codex\keybindings.json`.
