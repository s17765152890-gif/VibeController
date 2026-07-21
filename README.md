# VibeController

<div align="center">

**把 Xbox / PS5 手柄和 TCL 遥控器变成 Codex 的轻量控制器。**<br>
**Turn Xbox / PS5 controllers and a TCL remote into lightweight controls for Codex.**

[![CI](https://github.com/Mrjie7205/VibeController/actions/workflows/ci.yml/badge.svg)](https://github.com/Mrjie7205/VibeController/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Mrjie7205/VibeController)](https://github.com/Mrjie7205/VibeController/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-111111.svg)](LICENSE)

[简体中文](#简体中文) · [English](#english)

</div>

![VibeController Xbox dashboard](docs/assets/screenshots/dashboard-xbox.png)

<a id="简体中文"></a>

## 简体中文

VibeController 是一款面向 Windows 的开源桌面工具。它读取 Xbox Series X|S、PS5 DualSense 或 TCL BT_RC901A_B1 的输入，再将按键、摇杆和触控板转换为 Codex 操作、键盘快捷键或鼠标事件。它不会接管语音识别：听写仍由 Codex 桌面端和电脑麦克风完成。

### 功能亮点

- 支持 Xbox Wireless Controller（XInput，控制器 1–4）和 PS5 DualSense（USB / 蓝牙 HID）。
- 实验性支持 TCL BT_RC901A_B1：即使 Windows 的通用 HID 子驱动报错，也可通过专用 BLE GATT 后端读取连接状态、电量和原始通知。
- Codex 语义动作不是写死的快捷键：首次触发时读取 Codex 当前快捷键，并在配置变化后自动刷新。
- 支持听写、发送、命令菜单、任务/最近任务/标签页切换和推理强度调节。
- 左摇杆移动鼠标，扳机键点击；DualSense 触控板移动鼠标，按下执行左键单击。
- Xbox 与 DualSense 摇杆都按运行帧持续输出，按住移动时不会因 HID 报告抖动阈值而断续。
- 右摇杆模拟方向键，可按住连续移动输入框光标。
- 设置页可检查 Windows 默认录音端点，并提示 DualSense 麦克风是否实际暴露给系统。
- 可选的 Codex Hook 状态桥接：DualSense 灯条用蓝色、琥珀色和绿色表示工作中、等待操作和已完成。
- 默认只在 Codex 位于前台时注入输入，另有暂停映射和无注入测试模式。
- 本地 JSON 配置、系统托盘、自动重连和开机启动选项。

### TCL RC901A 实验性直连

当前源码包含 BT_RC901A_B1 的专用 BLE 后端、设备切换界面、遥控器示意图、默认动作配置和原始数据检查器；已发布的 v1.1.0 二进制尚不包含该功能。RC901A 的逻辑按键解释器默认保持空白，必须先从实体遥控器采集并验证每个按键的真实数据包，项目不会猜测厂商协议。

Windows 可能为该遥控器显示“驱动程序错误”。已确认这是 Windows 通用 BLE HID 驱动无法解析遥控器固件中的 HID 报告描述符，不代表整个蓝牙设备不可用。VibeController 会绕过失败的 HID 子驱动，直接检查标准 HID 服务和 TCL 的 D0FF/D1FF GATT 通知服务。

如果 Windows 显示“已配对”但 VibeController 报告 `Unreachable`，通常是电脑与遥控器保存的绑定密钥不同步：先在 Windows 蓝牙设置中删除 `BT_RC901A_B1`，再靠近电脑同时长按方向键中央的 `OK + 返回键` 约 5 秒并重新配对。完整步骤和安全采集规则见 [RC901A BLE 采集指南](docs/testing/RC901A-BLE-CAPTURE.md)。

该后端不会写入 TCL 厂商特征或 DFU 服务；唯一允许的写操作是 BLE 标准要求的通知订阅描述符。Mic 键目前只能映射为 Codex 原生听写快捷键，遥控器麦克风音频尚未接入。

| PS5 DualSense | 按键映射 |
| --- | --- |
| ![PS5 dashboard](docs/assets/screenshots/dashboard-ps5.png) | ![Mapping editor](docs/assets/screenshots/mapping.png) |

### 下载与安装

要求：Windows 10 22H2 或 Windows 11，x64。发布包已包含 .NET 运行时，但仍需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)；Windows 11 通常已预装。

1. 从 [Releases](https://github.com/Mrjie7205/VibeController/releases/latest) 下载 `VibeController-v1.1.0-win-x64.zip` 和同名 `.sha256` 文件。
2. 可选但推荐：执行 `Get-FileHash .\VibeController-v1.1.0-win-x64.zip -Algorithm SHA256`，与校验文件比较。
3. 完整解压 ZIP，不要直接在压缩包内运行。
4. 在 Windows 中通过蓝牙、USB 或 Xbox Wireless Adapter 连接手柄。
5. 运行 `VibeController.App.exe`，在“设置”中选择 Xbox 或 PS5，再用“测试输入”确认按键。TCL RC901A 当前需从源码构建实验分支。
6. 启动 Codex；按 Menu / Options 激活窗口，退出测试模式后即可执行映射。

当前二进制尚未进行代码签名，因此 Windows SmartScreen 可能显示警告。请只从本仓库 Release 下载并核对 SHA-256。

### Codex 快捷键自动同步

当映射选择的是 Codex 内置操作时，VibeController 会在该操作首次使用时读取：

- `$CODEX_HOME\keybindings.json`；或
- 未设置 `CODEX_HOME` 时的 `%USERPROFILE%\.codex\keybindings.json`。

用户自定义绑定优先于 Codex 默认绑定；文件改变后会自动重新读取。如果 Codex 中明确取消了某项绑定、该操作没有默认快捷键，或快捷键格式暂不支持，VibeController 会在界面显示错误，而不会发送一个过时的固定按键。

Codex 当前通常未给“切换听写”和“提高/降低推理强度”分配默认快捷键。第一次使用这些动作前，请在 **Codex → 设置 → 键盘快捷键** 中为它们设置普通组合键。其他用户可以使用不同组合键，无需在 VibeController 里重复配置。

### 听写麦克风与 DualSense 状态灯

设置页的“听写与状态灯”区域会列出 Windows 当前默认录音端点，并检查活动录音设备中是否存在 DualSense / Wireless Controller。该检测只读取设备 ID 和显示名称，不打开麦克风、不录音；真正的语音识别仍由 Codex 原生听写完成。标准蓝牙连接通常不会向 Windows 暴露 DualSense 麦克风，此时请使用电脑麦克风，或改用能暴露该音频端点的有线连接。

“使用 DualSense 灯条显示 Codex 状态”默认关闭。开启并保存后，VibeController 会把自己的命令处理器合并到 `$CODEX_HOME\hooks.json`；未设置 `CODEX_HOME` 时使用 `%USERPROFILE%\.codex\hooks.json`。首次修改已有文件时会保留 `hooks.json.vibecontroller.bak`：

| 灯条 | Codex 状态 |
| --- | --- |
| 蓝色 | 正在处理任务 |
| 琥珀色 | 等待权限或用户操作 |
| 绿色（8 秒） | 本轮任务已完成 |
| 暗蓝 | 空闲 |

首次触发非托管 Hook 时，Codex 可能要求信任配置。关闭该开关会只移除 VibeController 自己的处理器，不改动其他 Hook。Hook 状态文件只保存会话 ID、工作目录、状态和时间戳，不保存提示词、回复或工具输入输出。

### 默认映射

| Xbox | PS5 | 输出 / Codex 功能 |
| --- | --- | --- |
| X | □ 方块 | 切换听写（读取 Codex 当前绑定） |
| A | × 叉 | 发送消息（读取 `composer.submit`，默认 `Enter`） |
| B | ○ 圆圈 | `Backspace`，删除上一个字符 |
| Y | △ 三角 | 打开命令菜单（读取 Codex 当前绑定） |
| Menu | Options | 激活正在运行的 Codex 窗口 |
| LB / RB | L1 / R1 | 上一个任务 / 下一个任务（读取 Codex 当前绑定） |
| 十字键 ← / → | 十字键 ← / → | 降低 / 提高推理强度（读取 Codex 当前绑定） |
| 十字键 ↑ / ↓ | 十字键 ↑ / ↓ | 键盘 `ArrowUp` / `ArrowDown` |
| 右摇杆 | 右摇杆 | 键盘四方向键，按住自动重复 |
| 左摇杆 | 左摇杆 | 移动鼠标光标 |
| RT / LT | R2 / L2 | 鼠标左键 / 右键单击 |
| — | 触控板滑动 / 按下 | 移动鼠标 / 鼠标左键单击 |
| View、L3、R3 | Create、L3、R3 | 默认未分配，可在映射页选择动作 |

“上一个/下一个最近查看的任务”和“上一个/下一个标签页”也在映射动作列表中，但默认不占用手柄按键。

### 工作原理

```text
Xbox XInput / DualSense HID / RC901A Direct BLE
          ↓
统一的手柄输入事件
          ↓
可编辑映射 + Codex 快捷键解析
          ↓
Codex 前台安全检查
          ↓
Windows 键盘 / 鼠标 / 窗口操作
```

这是 Windows 输入映射工具，不依赖 Codex 私有 API。设置保存于 `%LOCALAPPDATA%\VibeController\settings.json`。应用只枚举 Windows 录音端点的名称，不打开麦克风、不录音，也不读取或保存 Codex 对话文本。只有用户主动开启状态灯功能时，应用才会安装上述 Codex Hook。

### 从源码构建

需要 .NET SDK `8.0.423`（或 `global.json` 允许的兼容补丁版本）、Node.js 20+ 和 npm。

```powershell
git clone https://github.com/Mrjie7205/VibeController.git
cd VibeController
npm ci --prefix frontend
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

自包含输出位于 `artifacts\win-x64`。生成与官方 Release 相同结构的 ZIP：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 1.1.0
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-release.ps1 -Version 1.1.0
```

架构、开发约定和 PR 要求见 [CONTRIBUTING.md](CONTRIBUTING.md)。实体设备测试见 [硬件验收清单](docs/testing/hardware-checklist.md)。

### 当前限制与路线图

- 仅支持 Windows x64；暂未提供安装器、自动更新或代码签名。
- Xbox 走 XInput；部分系统保留键（如 Guide）不作为核心输入。
- DualSense 的 USB/蓝牙报告解析和触控板行为已有自动化测试，但不同固件与蓝牙适配器仍需要更多实体设备反馈。
- DualSense 灯条的 USB/蓝牙输出报告与 CRC 已有自动化测试；不同固件、蓝牙栈以及共享 HID 写权限仍需按硬件清单验收。
- 普通 DualSense 蓝牙配对通常只提供手柄 HID，不提供麦克风音频端点；VibeController 不尝试绕过 Windows 的音频设备能力。
- RC901A 直连仍处于实体数据包采集阶段；在签名完成前，界面可以显示连接和原始通知，但不会猜测按键动作。
- 未来计划包括配置档案、组合/长按动作以及更多通用 HID 设备。

### 许可证与声明

源代码以 [MIT License](LICENSE) 发布。控制器图片、商标和第三方材料的权利说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。VibeController 是非官方社区项目，与 OpenAI、Microsoft 或 Sony 无隶属、赞助或背书关系。

---

<a id="english"></a>

## English

VibeController is an open-source Windows desktop utility that turns input from Xbox Series X|S, PS5 DualSense, or TCL BT_RC901A_B1 into Codex actions, keyboard shortcuts, and mouse events. It does not perform speech recognition itself: dictation remains entirely inside the Codex desktop app and uses your computer's microphone.

### Highlights

- Xbox Wireless Controller support through XInput (controller slots 1–4).
- PS5 DualSense support through native Windows USB/Bluetooth HID, including the touchpad.
- Experimental TCL BT_RC901A_B1 support through a dedicated BLE GATT backend, independent of the failed generic Windows HID child driver.
- Codex semantic actions resolve the user's current Codex shortcuts on first use and refresh after the file changes—no universal hard-coded shortcut assumptions.
- Dictation, submit, command menu, thread/recent-thread/tab navigation, and reasoning-effort actions.
- Left stick mouse movement, trigger clicks, right-stick arrow-key repeat, and DualSense touchpad mouse control.
- Continuous per-frame stick output for both Xbox and DualSense, avoiding choppy held movement caused by HID jitter thresholds.
- A read-only Settings check for the Windows default recording endpoint and whether a DualSense microphone endpoint is actually exposed.
- Optional Codex Hook status bridging to the DualSense lightbar: blue for working, amber for attention, and green for completion.
- Codex-foreground guard by default, plus pause and non-injecting input-test modes.
- Local JSON settings, system tray behavior, device reconnect, and optional start with Windows.

### Experimental TCL RC901A direct BLE

The current source tree includes a dedicated BT_RC901A_B1 backend, device picker, remote visual, default action profile, and raw-notification inspector. The published v1.1.0 binaries do not include it yet. The logical report registry intentionally starts empty: physical button signatures must be captured and verified before the project assigns meanings to vendor packets.

Windows may show “Driver error” for this remote. Hardware probing confirmed that the generic BLE HID child driver rejects the firmware's HID report descriptor; that does not make the complete BLE device unusable. VibeController bypasses that child driver and inspects the standard HID plus TCL D0FF/D1FF notification services directly.

If Windows says the remote is paired while VibeController reports `Unreachable`, remove `BT_RC901A_B1` from Windows Bluetooth settings, hold the center D-pad `OK + Back` buttons near the computer for about five seconds, and pair it again. This repairs stale bond keys. See the [RC901A BLE capture guide](docs/testing/RC901A-BLE-CAPTURE.md) for the full safe workflow.

The backend never writes TCL vendor characteristics or the DFU service. Its only permitted write is the standard BLE notification-subscription descriptor. The Mic button can trigger Codex native dictation, but remote microphone audio is not supported yet.

### Download and install

Requirements: Windows 10 22H2 or Windows 11, x64. The release is self-contained and does not require a separate .NET runtime, but it still requires [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), which is normally present on Windows 11.

1. Download `VibeController-v1.1.0-win-x64.zip` and its `.sha256` file from [Releases](https://github.com/Mrjie7205/VibeController/releases/latest).
2. Optionally verify it with `Get-FileHash .\VibeController-v1.1.0-win-x64.zip -Algorithm SHA256`.
3. Extract the complete archive; do not run the app from inside the ZIP.
4. Pair or connect the controller through Bluetooth, USB, or Xbox Wireless Adapter.
5. Run `VibeController.App.exe`, choose Xbox or PS5 in Settings, and confirm inputs in Test Input mode. TCL RC901A currently requires a source build of the experimental branch.
6. Start Codex. Use Menu / Options to focus it, exit Test Input mode, and use the mappings.

The v1.1.0 binaries are currently unsigned, so Windows SmartScreen may warn. Download only from this repository's Release page and verify the SHA-256 checksum.

### Codex shortcut synchronization

For a Codex action, VibeController reads the shortcut on first use from `$CODEX_HOME\keybindings.json`, or `%USERPROFILE%\.codex\keybindings.json` when `CODEX_HOME` is unset. Custom Codex bindings override defaults and the file is reloaded after it changes.

If an action is explicitly unbound, has no Codex default, or uses an unsupported accelerator format, VibeController reports the problem instead of injecting a stale fallback. Codex commonly has no default binding for **Toggle Dictation** or **Increase/Decrease Reasoning Effort**; bind those actions once under **Codex → Settings → Keyboard Shortcuts**. Each user may choose different combinations.

### Dictation microphone and DualSense status light

The “Dictation & status light” card in Settings reports the current Windows default recording endpoint and checks active endpoints for a DualSense / Wireless Controller microphone. This is metadata-only detection: VibeController does not open or record from the microphone. Codex native dictation still performs all speech recognition. A standard Bluetooth controller connection normally does not expose the DualSense microphone to Windows, so use the computer microphone or a wired mode that exposes the audio endpoint.

“Use the DualSense lightbar for Codex status” is off by default. When explicitly enabled and saved, VibeController merges its command handlers into `$CODEX_HOME\hooks.json`, falling back to `%USERPROFILE%\.codex\hooks.json` when `CODEX_HOME` is unset, and preserves an existing file once as `hooks.json.vibecontroller.bak`.

| Lightbar | Codex state |
| --- | --- |
| Blue | Working |
| Amber | Waiting for permission or user action |
| Green for 8 seconds | Turn completed |
| Dim blue | Idle |

Codex may ask you to trust a non-managed Hook the first time it runs. Turning the option off removes only VibeController's handlers. The bridge state stores only session ID, working directory, state, and timestamp—never prompts, responses, or tool input/output.

### Default mappings

| Xbox | PS5 | Output / Codex action |
| --- | --- | --- |
| X | Square □ | Toggle dictation (current Codex binding) |
| A | Cross × | Submit (`composer.submit`; `Enter` by default) |
| B | Circle ○ | `Backspace` |
| Y | Triangle △ | Open command menu (current Codex binding) |
| Menu | Options | Focus the running Codex window |
| LB / RB | L1 / R1 | Previous / next thread (current Codex binding) |
| D-pad ← / → | D-pad ← / → | Decrease / increase reasoning effort (current Codex binding) |
| D-pad ↑ / ↓ | D-pad ↑ / ↓ | Keyboard `ArrowUp` / `ArrowDown` |
| Right stick | Right stick | Four arrow keys with hold-to-repeat |
| Left stick | Left stick | Move the mouse pointer |
| RT / LT | R2 / L2 | Left / right mouse click |
| — | Touchpad move / press | Move mouse / left click |
| View, L3, R3 | Create, L3, R3 | Unassigned by default |

Previous/next recently viewed thread and previous/next tab are also available in the action picker without occupying a default controller button.

### How it works, privacy, and safety

VibeController normalizes XInput/HID reports, applies the editable mapping, resolves Codex shortcuts, checks the foreground-window policy, and injects ordinary Windows keyboard/mouse input. It does not use a private Codex API.

Settings stay in `%LOCALAPPDATA%\VibeController\settings.json`. VibeController only enumerates recording-endpoint names; it does not open the microphone, record audio, or read/store Codex conversation text. It installs the Codex Hook only after the user explicitly enables the status-light option. The default foreground guard prevents accidental input into other apps; global mode is available but should be used deliberately.

### Build from source

Install .NET SDK `8.0.423` (or a compatible patch allowed by `global.json`), Node.js 20+, and npm.

```powershell
git clone https://github.com/Mrjie7205/VibeController.git
cd VibeController
npm ci --prefix frontend
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

The self-contained publish output is written to `artifacts\win-x64`. To create the distributable ZIP and checksum:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version 1.1.0
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test-release.ps1 -Version 1.1.0
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for architecture and pull-request guidance, and the [hardware checklist](docs/testing/hardware-checklist.md) for physical-device validation.

### Limitations and roadmap

- Windows x64 only; no installer, automatic updater, or code-signing certificate yet.
- Xbox uses XInput, so OS-reserved buttons such as Guide are not core inputs.
- DualSense USB/Bluetooth parsing and touchpad behavior are covered by automated tests, while more real-world firmware and adapter reports are welcome.
- DualSense USB/Bluetooth lightbar packets and Bluetooth CRC are covered by automated tests, but firmware, Bluetooth stacks, and shared HID write access still require physical-device acceptance.
- Standard DualSense Bluetooth pairing normally exposes controller HID only, not its microphone audio endpoint; VibeController does not bypass Windows audio-device capabilities.
- RC901A direct BLE is still in physical packet-capture validation. Until signatures are verified, the UI can report connection and raw notifications but will not guess button actions.
- Planned directions include profiles, chord/hold actions, and additional generic HID devices.

### License and notice

Source code is available under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for controller imagery, trademarks, and dependency notices. VibeController is an unofficial community project and is not affiliated with, sponsored by, or endorsed by OpenAI, Microsoft, or Sony.
