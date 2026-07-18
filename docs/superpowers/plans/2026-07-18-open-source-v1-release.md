# VibeController Open-Source v1.0.0 Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the complete VibeController source as a public `Mrjie7205/VibeController` GitHub repository with bilingual documentation, reproducible CI, and a self-contained Windows x64 v1.0.0 release.

**Architecture:** Keep the existing WPF/WebView2 + React application unchanged except for release metadata. Add public-facing documentation, portable build scripts, Windows CI, and a deterministic packaging script that wraps the self-contained publish output with quick-start and license files, then emits a ZIP and SHA-256 checksum. Create one intentional initial commit on `main`, tag it `v1.0.0`, and attach the verified artifacts to a GitHub Release.

**Tech Stack:** Git, GitHub CLI, GitHub Actions, .NET 8, WPF, WebView2, React 19, TypeScript, PowerShell, ZIP/SHA-256 release artifacts.

**Repository condition:** This is an unborn repository containing the complete user-authorized MVP worktree. A separate worktree cannot be created before the initial commit; the user explicitly authorized publishing this whole project, so execute in the current non-default branch and rename it to `main` only after local verification.

---

### Task 1: Add open-source project metadata and bilingual documentation

**Files:**
- Create: `LICENSE`
- Create: `THIRD_PARTY_NOTICES.md`
- Create: `CONTRIBUTING.md`
- Replace: `README.md`
- Create: `docs/assets/screenshots/dashboard-xbox.png`
- Create: `docs/assets/screenshots/dashboard-ps5.png`
- Create: `docs/assets/screenshots/mapping.png`
- Create: `docs/assets/screenshots/settings.png`
- Modify: `.gitignore`
- Modify: `docs/superpowers/plans/2026-07-18-codex-shortcut-auto-sync.md`
- Modify: `docs/superpowers/plans/2026-07-18-global-dictation-right-stick-controls.md`

- [ ] **Step 1: Add MIT license and third-party notice**

Use the standard MIT license with `Copyright (c) 2026 Mrjie7205`. State that Xbox, PlayStation, DualSense, Codex, OpenAI, Microsoft, and Sony marks belong to their owners; VibeController is an unofficial community project. Exclude user-supplied controller product images from the MIT grant unless their original rights permit reuse.

- [ ] **Step 2: Write a bilingual README**

Include language anchors, overview, screenshots, feature list, supported devices, requirements, download/install, first-run Codex shortcut behavior, default mapping table, architecture, privacy/security, build/test commands, limitations, roadmap, contributing, license, and non-affiliation notice. Make these facts explicit:

```text
- Windows 10/11 x64
- Xbox via XInput; DualSense via USB/Bluetooth HID
- Self-contained .NET release; Microsoft Edge WebView2 Runtime still required
- Codex semantic actions read $CODEX_HOME/keybindings.json on first use and refresh after edits
- Commands without a Codex binding fail visibly instead of sending a stale hardcoded key
- Release binaries are currently unsigned; verify the SHA-256 asset
```

- [ ] **Step 3: Add contributor instructions and screenshots**

Document prerequisites (`.NET SDK 8.0.423`, Node.js 20+), `npm ci`, tests, build, architecture boundaries, and pull-request expectations. Capture fresh Xbox dashboard/mapping/settings screenshots and reuse the already verified PS5 dashboard screenshot.

- [ ] **Step 4: Sanitize local-only paths and expand ignore rules**

Replace author-specific home-directory paths with `%USERPROFILE%`; ignore OS/editor detritus and release archives while keeping source screenshots tracked.

### Task 2: Make public builds reproducible

**Files:**
- Modify: `scripts/test.ps1`
- Modify: `scripts/build.ps1`
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`
- Modify: `src/VibeController.App/VibeController.App.csproj`
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Set v1.0.0 metadata**

Set the frontend package and app assembly/package version to `1.0.0`. Keep `global.json` at SDK `8.0.423` with latest-patch roll-forward.

- [ ] **Step 2: Make PowerShell scripts portable**

Prefer `.tools\dotnet\dotnet.exe` when present, otherwise resolve system `dotnet`; fail with an actionable message only when neither exists. Preserve current frontend and .NET verification steps.

- [ ] **Step 3: Add Windows CI**

Use `actions/checkout@v4`, `actions/setup-node@v4` with Node 22 and npm cache, and `actions/setup-dotnet@v4` with `global.json`. Run `npm ci`, all frontend tests/typecheck/build, and `dotnet test VibeController.sln --configuration Release` on pushes and pull requests.

- [ ] **Step 4: Normalize npm registry metadata and verify clean install inputs**

Use `https://registry.npmjs.org` URLs in `package-lock.json`, then run `npm ci --ignore-scripts` in a temporary cache context or otherwise verify the lockfile is accepted without modifying source.

### Task 3: Build a self-contained release artifact

**Files:**
- Create: `docs/release/QUICKSTART.md`
- Create: `docs/release/v1.0.0.md`
- Create: `scripts/package-release.ps1`

- [ ] **Step 1: Write bilingual quick-start and release notes**

Explain extract-and-run usage, controller pairing, Codex shortcut binding, SmartScreen/unsigned status, checksum verification, data locations, exit-from-tray behavior, known limitations, and the v1.0.0 feature list.

- [ ] **Step 2: Implement deterministic packaging**

Run `scripts/build.ps1`, stage `artifacts/win-x64` under `artifacts/release/VibeController-v1.0.0-win-x64`, copy `LICENSE` and quick-start, create `VibeController-v1.0.0-win-x64.zip`, and write `VibeController-v1.0.0-win-x64.zip.sha256`. Validate every destructive staging path stays under `artifacts/release` before removing a prior build.

- [ ] **Step 3: Validate the package**

Expand the ZIP into a temporary directory, assert `VibeController.App.exe`, `README.md`, `LICENSE`, WebView2 loader files, and `wwwroot/index.html` exist, recompute SHA-256, then launch the extracted executable and confirm a responsive `VibeController` window.

### Task 4: Verify public-source safety and release quality

**Files:**
- Modify only if checks expose a defect.

- [ ] **Step 1: Run the complete test suite**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Expected: 21 frontend tests, 103 Core tests, and 97 Infrastructure tests pass; TypeScript and Vite production build succeed.

- [ ] **Step 2: Scan public files**

Search all non-generated text sources for credentials, private keys, API tokens, author-specific absolute paths, and accidental build output. Confirm `参考视频.mp4`, `.tools`, `artifacts`, `output`, `.playwright-cli`, `node_modules`, `bin`, and `obj` are not staged.

- [ ] **Step 3: Review the exact initial commit scope**

Use `git status`, `git diff --check`, and `git diff --cached --stat`. Stage the complete project only after confirming every staged path belongs to VibeController.

### Task 5: Create and publish the GitHub repository

**Files:**
- Git metadata only.

- [ ] **Step 1: Create the initial commit on main**

Rename the unborn branch to `main`, stage the verified project, and commit:

```text
feat: release VibeController v1.0.0
```

- [ ] **Step 2: Create the public repository and push**

Create `Mrjie7205/VibeController` with description `Use Xbox and PS5 controllers as a lightweight Codex remote on Windows.`, add `origin`, and push `main`. Set topics: `codex`, `controller`, `xbox`, `dualsense`, `windows`, `wpf`, `react`, `input-remapping`.

- [ ] **Step 3: Confirm remote integrity**

Verify the remote default branch is `main`, visibility is `PUBLIC`, remote HEAD equals the local commit, and the README/license render from the repository.

### Task 6: Tag and publish GitHub Release v1.0.0

**Files:**
- Git tag and GitHub Release metadata only.

- [ ] **Step 1: Tag the verified commit**

Create annotated tag `v1.0.0` with message `VibeController v1.0.0` and push it.

- [ ] **Step 2: Create the release**

Create a non-draft, non-prerelease GitHub Release titled `VibeController v1.0.0`, using `docs/release/v1.0.0.md`, and upload the ZIP plus SHA-256 file.

- [ ] **Step 3: Verify public delivery**

Query the release and assets through GitHub, confirm the tag targets the initial commit, both assets have non-zero sizes, and the repository/release URLs are publicly reachable.
