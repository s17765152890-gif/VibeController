import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MappingEditor } from "./MappingEditor";

afterEach(cleanup);

describe("MappingEditor", () => {
  it("selects a control and changes its action", () => {
    const onSave = vi.fn();
    render(<MappingEditor onSave={onSave} onReset={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "X 按键" }));
    fireEvent.change(screen.getByLabelText("绑定操作"), { target: { value: "commandPalette" } });
    fireEvent.click(screen.getByRole("button", { name: "保存映射" }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ x: "commandPalette" }));
    expect(screen.getByText("映射已保存")).toBeInTheDocument();
  });

  it("warns when two controls use the same exclusive shortcut", () => {
    render(<MappingEditor onSave={vi.fn()} onReset={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Y 按键" }));
    fireEvent.change(screen.getByLabelText("绑定操作"), { target: { value: "dictation" } });

    expect(screen.getByRole("alert")).toHaveTextContent("与 X 按键冲突");
  });

  it("can reset the default profile", () => {
    const onReset = vi.fn();
    render(<MappingEditor onSave={vi.fn()} onReset={onReset} />);
    fireEvent.click(screen.getByRole("button", { name: "恢复默认" }));
    expect(onReset).toHaveBeenCalledOnce();
  });

  it("records a custom keyboard shortcut", () => {
    const onSave = vi.fn();
    render(<MappingEditor onSave={onSave} onReset={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("绑定操作"), { target: { value: "keyboardShortcut" } });
    fireEvent.keyDown(screen.getByLabelText("自定义快捷键"), { key: "K", ctrlKey: true, shiftKey: true });
    fireEvent.click(screen.getByRole("button", { name: "保存映射" }));
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ x: "shortcut:Ctrl+Shift+K" }));
  });

  it("uses the text-editing defaults and keeps cancel available as an optional action", () => {
    const onSave = vi.fn();
    render(<MappingEditor onSave={onSave} onReset={vi.fn()} />);

    expect(screen.getByRole("button", { name: "右摇杆左" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "右摇杆右" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "右摇杆上" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "右摇杆下" })).toBeInTheDocument();

    expect(screen.queryByRole("option", { name: "上一个模型" })).not.toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "下一个模型" })).not.toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Esc（取消 / 返回）" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "保存映射" }));

    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      b: "shortcut:Backspace",
      dPadLeft: "decreaseReasoning",
      dPadRight: "increaseReasoning",
      rightStickLeft: "shortcut:ArrowLeft",
      rightStickRight: "shortcut:ArrowRight",
      rightStickUp: "shortcut:ArrowUp",
      rightStickDown: "shortcut:ArrowDown",
    }));
  });

  it("shows Codex-native actions with the names used by Codex shortcut settings", () => {
    render(<MappingEditor onSave={vi.fn()} onReset={vi.fn()} />);

    expect(screen.getByRole("option", { name: "切换听写快捷键" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "发送消息" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "打开命令菜单" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "上一个任务" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "下一项任务" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "降低推理强度" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "提高推理强度" })).toBeInTheDocument();
  });

  it("explains automatic Codex shortcut lookup only for Codex semantic actions", () => {
    render(<MappingEditor onSave={vi.fn()} onReset={vi.fn()} />);

    expect(screen.getByText("首次触发时读取 Codex 当前快捷键；修改 Codex 设置后会自动刷新。")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("绑定操作"), { target: { value: "keyboardShortcut" } });

    expect(screen.queryByText("首次触发时读取 Codex 当前快捷键；修改 Codex 设置后会自动刷新。")).not.toBeInTheDocument();
  });

  it("offers recent-task and tab navigation without changing the default bindings", () => {
    const onSave = vi.fn();
    render(<MappingEditor onSave={onSave} onReset={vi.fn()} />);

    expect(screen.getByRole("option", { name: "上一个最近查看的任务" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "下一个最近查看的任务" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "上一个标签页" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "下一个标签页" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "保存映射" }));

    const saved = onSave.mock.calls[0][0] as Record<string, string>;
    expect(saved.leftShoulder).toBe("previousChat");
    expect(saved.rightShoulder).toBe("nextChat");
    expect(Object.values(saved)).not.toContain("previousRecentThread");
    expect(Object.values(saved)).not.toContain("nextRecentThread");
    expect(Object.values(saved)).not.toContain("previousTab");
    expect(Object.values(saved)).not.toContain("nextTab");
  });

  it("uses PlayStation symbols, shoulder names and touchpad controls for PS5", () => {
    const onSave = vi.fn();
    render(
      <MappingEditor
        controllerType="playStation5"
        onSave={onSave}
        onReset={vi.fn()}
      />,
    );

    expect(screen.getByRole("button", { name: "□ 方块键" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "× 叉键" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "○ 圆圈键" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "△ 三角键" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "L1" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "R1" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "L2" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "R2" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "触控板横向滑动" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "触控板纵向滑动" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "触控板按下" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "保存映射" }));
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      touchpadX: "mouseMove",
      touchpadY: "mouseMove",
      touchpadButton: "mouseLeftClick",
    }));
  });
});
