import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Settings } from "./Settings";

afterEach(cleanup);

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
