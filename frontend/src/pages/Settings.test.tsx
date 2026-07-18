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
});
