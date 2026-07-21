import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Dashboard } from "./Dashboard";
import type { RuntimeStatePayload } from "../app/types";

const connectedState: RuntimeStatePayload = {
  connectionState: "connected",
  controllerIndex: 0,
  mappingEnabled: true,
  testMode: false,
  packetNumber: 12,
  controls: { x: 1, a: 0, leftStickX: 0.25, leftTrigger: 1, leftStickButton: 1 },
  lastAction: "X → 切换听写快捷键",
};

describe("Dashboard", () => {
  it("shows connected runtime state and immediate controller feedback", () => {
    render(<Dashboard state={connectedState} onToggleMapping={vi.fn()} />);

    expect(screen.getByRole("heading", { name: "VibeController" })).toBeInTheDocument();
    expect(screen.getByText("Xbox 手柄已连接")).toBeInTheDocument();
    expect(screen.getByText("映射已启用")).toBeInTheDocument();
    expect(screen.getByText("X → 切换听写快捷键")).toBeInTheDocument();
    expect(screen.getByTestId("controller-photo")).toHaveAttribute(
      "src",
      expect.stringContaining("forza-controller-white-v1.png"),
    );
    expect(screen.getByTestId("control-x")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByTestId("control-left-trigger")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByTestId("control-left-stick-button")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByTestId("control-left-stick-button")).toHaveStyle({
      transform: "translate(-50%, -50%) translate(3px, 0px)",
    });
  });

  it("shows the DualSense artwork, symbols and touchpad feedback for PS5", () => {
    render(
      <Dashboard
        state={{
          ...connectedState,
          controls: { x: 1, touchpadButton: 1, touchpadX: 0.4, touchpadY: -0.2 },
          configuration: {
            controllerType: "playStation5",
            activeControllerIndex: 0,
            codexOnly: true,
            dictationShortcut: "Ctrl+Alt+Shift+F12",
            mouseSpeed: 50,
            scrollSpeed: 50,
            deadZone: 0.12,
            startWithWindows: false,
            mappings: {},
          },
        }}
        onToggleMapping={vi.fn()}
      />,
    );

    expect(screen.getByText("PS5 DualSense 已连接")).toBeInTheDocument();
    expect(screen.getByTestId("controller-photo")).toHaveAttribute(
      "src",
      expect.stringContaining("dualsense-black.png"),
    );
    expect(screen.getByTestId("control-square")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByTestId("control-touchpad")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByText("触控板滑动")).toBeInTheDocument();
    expect(screen.getByText("移动鼠标光标")).toBeInTheDocument();
  });

  it("shows the TCL remote workspace in direct BLE mode", () => {
    render(
      <Dashboard
        state={{
          ...connectedState,
          controls: { remoteOk: 1, remoteMic: 0 },
          configuration: {
            controllerType: "tclRc901a",
            activeControllerIndex: 0,
            codexOnly: true,
            dictationShortcut: "Ctrl+Alt+Shift+F12",
            mouseSpeed: 50,
            scrollSpeed: 50,
            deadZone: 0.12,
            startWithWindows: false,
            mappings: {},
          },
        }}
        onToggleMapping={vi.fn()}
      />,
    );

    expect(screen.getByText("TCL RC901A 已连接")).toBeInTheDocument();
    expect(screen.getByTestId("tcl-remote-visual")).toBeInTheDocument();
    expect(screen.getByTestId("control-remote-ok")).toHaveAttribute("data-pressed", "true");
    expect(screen.getByText("直接 BLE 模式")).toBeInTheDocument();
  });

  it("pauses mapping from the primary control", () => {
    const onToggleMapping = vi.fn();
    render(<Dashboard state={connectedState} onToggleMapping={onToggleMapping} />);

    fireEvent.click(screen.getByRole("button", { name: "暂停映射" }));

    expect(onToggleMapping).toHaveBeenCalledWith(false);
  });

  it("shows a clear recovery message when the controller is disconnected", () => {
    render(
      <Dashboard
        state={{ ...connectedState, connectionState: "disconnected" }}
        onToggleMapping={vi.fn()}
      />,
    );

    expect(screen.getByText("等待 Xbox 手柄")).toBeInTheDocument();
    expect(screen.getByText("连接后会自动恢复，无需重新配置")).toBeInTheDocument();
  });

  it("can enter input test mode without dispatching actions", () => {
    const onToggleTestMode = vi.fn();
    render(<Dashboard state={connectedState} onToggleMapping={vi.fn()} onToggleTestMode={onToggleTestMode} />);
    fireEvent.click(screen.getByRole("button", { name: "测试输入" }));
    expect(onToggleTestMode).toHaveBeenCalledWith(true);
  });

  it("shows the text-editing and reasoning defaults", () => {
    render(<Dashboard state={connectedState} onToggleMapping={vi.fn()} />);

    expect(screen.getByText("删除上一个字符（Backspace）")).toBeInTheDocument();
    expect(screen.getByText("B")).toBeInTheDocument();
    expect(screen.getByText("移动输入光标")).toBeInTheDocument();
    expect(screen.getByText("右摇杆 ↑ ↓ ← →")).toBeInTheDocument();
    expect(screen.getByText("降低 / 提高推理强度")).toBeInTheDocument();
    expect(screen.getByText("方向键 ← →")).toBeInTheDocument();
    expect(screen.queryByText("切换模型")).not.toBeInTheDocument();
  });
});
