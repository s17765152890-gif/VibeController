# VibeController v1.0.0 Quick Start / 快速开始

## 简体中文

1. **完整解压**本 ZIP，不要在压缩包预览窗口中直接运行。
2. Windows 10/11 x64 需要 Microsoft Edge WebView2 Runtime；Windows 11 通常已经安装。
3. 通过蓝牙、USB 或 Xbox Wireless Adapter 连接 Xbox Series X|S 手柄；DualSense 支持 USB 或蓝牙。
4. 运行 `VibeController.App.exe`。当前程序未签名，如 SmartScreen 警告，请先确认文件来自官方 GitHub Release，并核对 SHA-256。
5. 在“设置”中选择手柄类型，然后打开“测试输入”确认按键和摇杆；测试模式不会注入键鼠事件。
6. 启动 Codex。在 Codex 的“设置 → 键盘快捷键”中，为“切换听写”和“提高/降低推理强度”设置普通组合键（这些动作通常没有默认绑定）。
7. 退出测试模式。按 Menu / Options 激活 Codex；X / □ 切换听写；A / × 发送。

VibeController 会读取 `%USERPROFILE%\.codex\keybindings.json`（或 `$CODEX_HOME\keybindings.json`）中的实际 Codex 快捷键。若动作没有绑定，会显示错误而不会发送固定的旧快捷键。

关闭窗口会最小化到系统托盘；需要彻底结束时，请右键托盘图标并选择“退出”。设置保存在 `%LOCALAPPDATA%\VibeController\settings.json`。应用不访问麦克风或对话文字。

校验下载文件：

```powershell
Get-FileHash .\VibeController-v1.0.0-win-x64.zip -Algorithm SHA256
Get-Content .\VibeController-v1.0.0-win-x64.zip.sha256
```

## English

1. **Extract the complete ZIP**. Do not launch the app from an archive preview.
2. Windows 10/11 x64 requires Microsoft Edge WebView2 Runtime; it is normally installed on Windows 11.
3. Connect an Xbox Series X|S controller over Bluetooth, USB, or Xbox Wireless Adapter. DualSense supports USB or Bluetooth.
4. Run `VibeController.App.exe`. The binary is unsigned; if SmartScreen warns, verify that it came from the official GitHub Release and compare its SHA-256.
5. Select the controller type in Settings and use Test Input to check buttons and sticks without injecting keyboard or mouse events.
6. In Codex Settings → Keyboard Shortcuts, bind ordinary key combinations for Toggle Dictation and Increase/Decrease Reasoning Effort, which commonly have no defaults.
7. Exit Test Input. Use Menu / Options to focus Codex, X / Square to toggle dictation, and A / Cross to submit.

VibeController reads the user's actual bindings from `%USERPROFILE%\.codex\keybindings.json` (or `$CODEX_HOME\keybindings.json`). An unbound action reports an error instead of sending a stale fixed shortcut.

Closing the window minimizes it to the system tray. To stop it completely, right-click the tray icon and choose Exit. Settings live at `%LOCALAPPDATA%\VibeController\settings.json`. The app does not access microphone audio or conversation text.
