import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Rc901aControl, Rc901aInputStatus } from "../app/types";
import { Settings } from "./Settings";

afterEach(cleanup);

const verifiedControls: readonly Rc901aControl[] = [
  "remoteUp",
  "remoteDown",
  "remoteLeft",
  "remoteRight",
  "remoteOk",
  "remoteMenu",
  "remoteBack",
  "remoteHome",
  "remoteVolumeUp",
  "remoteVolumeDown",
  "remoteMic",
  "remoteMute",
  "remoteInput",
  "remoteRed",
  "remoteGreen",
  "remoteBlue",
  "remoteSettings",
  "remoteApp1",
  "remoteApp2",
  "remoteBrightnessUp",
  "remoteBrightnessDown",
  "remotePictureMode",
];

const verifiedRc901aInput: Rc901aInputStatus = {
  bindings: verifiedControls.map((control, index) => ({
    kind: "driverHidUsage",
    code: index + 1,
    control,
    source: "verifiedDefault",
  })),
  lastUnknown: null,
  learning: {
    phase: "idle",
    sessionId: null,
    target: null,
    candidate: null,
    conflict: null,
    expiresAt: null,
  },
};

describe("Settings", () => {
  it("explains that Codex shortcuts are read automatically instead of exposing a duplicate setting", () => {
    render(<Settings onSave={vi.fn()} onCopyDiagnostics={vi.fn()} />);

    expect(screen.getByText("自动同步 Codex 快捷键")).toBeInTheDocument();
    expect(screen.getByText(/首次触发 Codex 操作时读取当前用户的设置/)).toBeInTheDocument();
    expect(screen.queryByLabelText("听写快捷键")).not.toBeInTheDocument();
  });

  it("switches the active device family to PS5 DualSense", () => {
    const onSave = vi.fn();
    render(<Settings onSave={onSave} onCopyDiagnostics={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("PS5 DualSense"));
    fireEvent.click(screen.getByRole("button", { name: "保存设置" }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      controllerType: "playStation5",
    }));
  });

  it("switches to TCL RC901A and hides the gamepad slot selector", () => {
    const onSave = vi.fn();
    render(<Settings onSave={onSave} onCopyDiagnostics={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("TCL RC901A"));

    expect(screen.queryByLabelText("活动控制器")).not.toBeInTheDocument();
    expect(screen.getByText("Windows HID")).toBeInTheDocument();
    expect(screen.queryByText("直接 BLE 模式")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "保存设置" }));
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      controllerType: "tclRc901a",
    }));
  });

  it("shows RC901A Windows HID status and raw input controls", () => {
    const onRefreshRc901a = vi.fn();
    const onClearRc901aSamples = vi.fn();
    render(
      <Settings
        onSave={vi.fn()}
        onCopyDiagnostics={vi.fn()}
        onRefreshRc901a={onRefreshRc901a}
        onClearRc901aSamples={onClearRc901aSamples}
        initialValues={{
          controllerType: "tclRc901a",
          codexOnly: true,
          startWithWindows: false,
          deadZone: 0.18,
          mouseSpeed: 50,
          scrollSpeed: 50,
          activeControllerIndex: 0,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          codexLightbarEnabled: false,
        }}
        rc901a={{
          connectionState: "connected",
          deviceName: "BT_RC901A_B1",
          deviceId: "device-id",
          batteryPercent: 87,
          subscribedCharacteristicCount: 2,
          message: "VibeController Windows HID 已连接。",
          samples: [{
            timestamp: "2026-07-21T12:00:00Z",
            serviceUuid: "0000d0ff-3c17-d293-8e48-14fe2e4da212",
            characteristicUuid: "0000ffd4-0000-1000-8000-00805f9b34fb",
            dataHex: "00 A1 FF",
            length: 3,
          }],
        }}
      />,
    );

    expect(screen.getByText("BT_RC901A_B1")).toBeInTheDocument();
    expect(screen.getByText("87% 电量")).toBeInTheDocument();
    expect(screen.getByText("2 个数据通道")).toBeInTheDocument();
    expect(screen.getByText("00 A1 FF")).toBeInTheDocument();
    expect(screen.getByText(/22 个已验证按键会自动就绪/)).toBeInTheDocument();
    expect(screen.queryByText(/直接 BLE/)).not.toBeInTheDocument();
    expect(screen.getByText(/方向键中央 OK \+ 返回键/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "重新连接" }));
    fireEvent.click(screen.getByRole("button", { name: "清除记录" }));
    expect(onRefreshRc901a).toHaveBeenCalledOnce();
    expect(onClearRc901aSamples).toHaveBeenCalledOnce();
  });

  it("shows compatibility recognition only for TCL RC901A and marks the override request", () => {
    const onStartRc901aLearning = vi.fn();
    render(
      <Settings
        onSave={vi.fn()}
        onCopyDiagnostics={vi.fn()}
        initialValues={{
          controllerType: "xbox",
          codexOnly: true,
          startWithWindows: false,
          deadZone: 0.18,
          mouseSpeed: 50,
          scrollSpeed: 50,
          activeControllerIndex: 0,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          codexLightbarEnabled: false,
        }}
        rc901aInput={verifiedRc901aInput}
        persistedControllerType="tclRc901a"
        rc901aLearningReady
        onStartRc901aLearning={onStartRc901aLearning}
      />,
    );

    expect(screen.queryByRole("button", { name: "兼容性按键识别" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("TCL RC901A"));
    expect(screen.getByText("22 个已验证按键已自动就绪")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "兼容性按键识别" }));
    fireEvent.click(screen.getByRole("button", { name: "重新识别麦克风键" }));
    expect(onStartRc901aLearning).toHaveBeenCalledWith("remoteMic", true);
  });

  it("keeps learning visible but disabled until the TCL device selection is saved", () => {
    const onStartRc901aLearning = vi.fn();
    render(
      <Settings
        onSave={vi.fn()}
        onCopyDiagnostics={vi.fn()}
        initialValues={{
          controllerType: "tclRc901a",
          codexOnly: true,
          startWithWindows: false,
          deadZone: 0.18,
          mouseSpeed: 50,
          scrollSpeed: 50,
          activeControllerIndex: 0,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          codexLightbarEnabled: false,
        }}
        persistedControllerType="xbox"
        rc901aLearningReady
        onStartRc901aLearning={onStartRc901aLearning}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "兼容性按键识别" }));
    expect(screen.getByText("先保存设备设置")).toBeInTheDocument();
    const learnMic = screen.getByRole("button", { name: "识别麦克风键" });
    expect(learnMic).toBeDisabled();
    fireEvent.click(learnMic);
    expect(onStartRc901aLearning).not.toHaveBeenCalled();
  });

  it("keeps learning visible but disabled while the saved TCL remote is disconnected", () => {
    const onStartRc901aLearning = vi.fn();
    render(
      <Settings
        onSave={vi.fn()}
        onCopyDiagnostics={vi.fn()}
        initialValues={{
          controllerType: "tclRc901a",
          codexOnly: true,
          startWithWindows: false,
          deadZone: 0.18,
          mouseSpeed: 50,
          scrollSpeed: 50,
          activeControllerIndex: 0,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          codexLightbarEnabled: false,
        }}
        persistedControllerType="tclRc901a"
        rc901aLearningReady={false}
        onStartRc901aLearning={onStartRc901aLearning}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "兼容性按键识别" }));
    expect(screen.getByText("先连接遥控器")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "识别返回键" })).toBeDisabled();
  });

  it("saves guard, startup and input tuning", () => {
    const onSave = vi.fn();
    render(<Settings onSave={onSave} onCopyDiagnostics={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("仅在 Codex 前台时执行"));
    fireEvent.click(screen.getByLabelText("登录 Windows 后自动启动"));
    fireEvent.change(screen.getByLabelText("摇杆死区"), { target: { value: "22" } });
    fireEvent.change(screen.getByLabelText("活动控制器"), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "保存设置" }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      codexOnly: false,
      startWithWindows: true,
      deadZone: 0.22,
      activeControllerIndex: 2,
      dictationShortcut: "Ctrl+Alt+Shift+F12",
    }));
  });

  it("copies diagnostics", () => {
    const onCopyDiagnostics = vi.fn();
    render(<Settings onSave={vi.fn()} onCopyDiagnostics={onCopyDiagnostics} />);
    fireEvent.click(screen.getByRole("button", { name: "复制诊断信息" }));
    expect(onCopyDiagnostics).toHaveBeenCalledOnce();
  });

  it("shows the detected Windows dictation microphone and DualSense guidance", () => {
    const onRefreshIntegrations = vi.fn();
    render(
      <Settings
        onSave={vi.fn()}
        onCopyDiagnostics={vi.fn()}
        onRefreshIntegrations={onRefreshIntegrations}
        microphone={{
          state: "available",
          defaultDeviceName: "Microphone Array (Laptop)",
          deviceNames: ["Microphone Array (Laptop)"],
          dualSenseMicrophoneAvailable: false,
          message: null,
        }}
      />,
    );

    expect(screen.getByText("Microphone Array (Laptop)")).toBeInTheDocument();
    expect(screen.getByText(/普通蓝牙连接未暴露 DualSense 麦克风/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "刷新检测" }));
    expect(onRefreshIntegrations).toHaveBeenCalledOnce();
  });

  it("enables the Codex hook lightbar bridge explicitly", () => {
    const onSave = vi.fn();
    render(
      <Settings
        onSave={onSave}
        onCopyDiagnostics={vi.fn()}
        codexHook={{ enabled: false, installed: false, errorMessage: null }}
        codexActivity={{ state: "idle", lastEventAt: null, activeSessionCount: 0 }}
      />,
    );

    fireEvent.click(screen.getByLabelText("使用 DualSense 灯条显示 Codex 状态"));
    fireEvent.click(screen.getByRole("button", { name: "保存设置" }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      codexLightbarEnabled: true,
    }));
    expect(screen.getByText("工作中")).toBeInTheDocument();
    expect(screen.getByText("等待操作")).toBeInTheDocument();
    expect(screen.getByText("已完成")).toBeInTheDocument();
  });

  it("shows installed hook activity and can save the bridge as disabled", () => {
    const onSave = vi.fn();
    render(
      <Settings
        onSave={onSave}
        onCopyDiagnostics={vi.fn()}
        initialValues={{
          controllerType: "playStation5",
          codexOnly: true,
          startWithWindows: false,
          deadZone: 0.18,
          mouseSpeed: 50,
          scrollSpeed: 50,
          activeControllerIndex: 0,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          codexLightbarEnabled: true,
        }}
        codexHook={{ enabled: true, installed: true, errorMessage: null }}
        codexActivity={{ state: "needsAttention", lastEventAt: "2026-07-18T10:00:00Z", activeSessionCount: 1 }}
      />,
    );

    expect(screen.getByText("Hook 已安装 · 等待操作")).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("使用 DualSense 灯条显示 Codex 状态"));
    fireEvent.click(screen.getByRole("button", { name: "保存设置" }));
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      codexLightbarEnabled: false,
    }));
  });
});
