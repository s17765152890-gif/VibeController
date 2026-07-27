import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type {
  Rc901aControl,
  Rc901aInputBinding,
  Rc901aInputStatus,
} from "../app/types";
import { Rc901aLearningPanel } from "./Rc901aLearningPanel";

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

const verifiedBindings: Rc901aInputBinding[] = verifiedControls.map(
  (control, index) => ({
    kind: "driverHidUsage",
    code: index + 1,
    control,
    source: "verifiedDefault",
  }),
);

const idleInput: Rc901aInputStatus = {
  bindings: verifiedBindings,
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

const learnedInput: Rc901aInputStatus = {
  ...idleInput,
  bindings: [
    ...idleInput.bindings,
    {
      kind: "consumerControl",
      code: 0x0224,
      control: "remoteBack",
      source: "learned",
    },
  ],
};

function renderPanel(inputStatus: Rc901aInputStatus = idleInput) {
  const callbacks = {
    onStart: vi.fn(),
    onConfirm: vi.fn(),
    onRetry: vi.fn(),
    onCancel: vi.fn(),
    onReset: vi.fn(),
  };

  render(<Rc901aLearningPanel inputStatus={inputStatus} {...callbacks} />);
  return callbacks;
}

function openPanel() {
  fireEvent.click(screen.getByRole("button", { name: "兼容性按键识别" }));
}

describe("Rc901aLearningPanel", () => {
  it("shows the automatic 22-key profile without exposing learning actions by default", () => {
    renderPanel();

    expect(screen.getByText("22 个已验证按键已自动就绪")).toBeInTheDocument();
    expect(screen.getByText(/电源键尚未验证/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "兼容性按键识别" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /识别/ })).toBe(
      screen.getByRole("button", { name: "兼容性按键识别" }),
    );
  });

  it("keeps the built-in profile summary stable while runtime bindings are loading", () => {
    render(<Rc901aLearningPanel />);

    expect(screen.getByText("22 个已验证按键已自动就绪")).toBeInTheDocument();
  });

  it("derives all verified badges from bindings and includes the three side controls", () => {
    renderPanel();
    openPanel();

    expect(screen.getAllByRole("listitem")).toHaveLength(23);
    expect(screen.getAllByText("已验证")).toHaveLength(22);
    expect(screen.getByText("尚未验证")).toBeInTheDocument();
    expect(screen.getByText("奇异果 TV 键")).toBeInTheDocument();
    expect(screen.getByText("亮度 +")).toBeInTheDocument();
    expect(screen.getByText("亮度 −")).toBeInTheDocument();
    expect(screen.getByText("图像模式")).toBeInTheDocument();
    expect(screen.queryByText("爱奇艺键")).not.toBeInTheDocument();
    expect(screen.queryByText("频道 +")).not.toBeInTheDocument();
    expect(screen.queryByText("数字键 0")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "识别电源键" })).not.toBeInTheDocument();
  });

  it("starts one advanced compatibility override at a time", () => {
    const { onStart } = renderPanel();
    openPanel();

    fireEvent.click(screen.getByRole("button", { name: "重新识别返回键" }));

    expect(onStart).toHaveBeenCalledWith("remoteBack", true);
  });

  it("guides the user through press and release without exposing action buttons too early", () => {
    const awaitingPress: Rc901aInputStatus = {
      ...idleInput,
      learning: {
        phase: "awaitingPress",
        sessionId: "session-1",
        target: "remoteMic",
        candidate: null,
        conflict: null,
        expiresAt: "2026-07-27T12:00:30Z",
      },
    };
    const { rerender } = render(
      <Rc901aLearningPanel
        inputStatus={awaitingPress}
        onStart={vi.fn()}
        onConfirm={vi.fn()}
        onRetry={vi.fn()}
        onCancel={vi.fn()}
        onReset={vi.fn()}
      />,
    );

    const status = screen.getByRole("status");
    expect(status).toHaveAttribute("aria-live", "polite");
    expect(status).toHaveAttribute("aria-atomic", "true");
    expect(status).toHaveTextContent("请按遥控器上的麦克风键");
    expect(screen.queryByRole("button", { name: "确认识别" })).not.toBeInTheDocument();

    rerender(
      <Rc901aLearningPanel
        inputStatus={{
          ...awaitingPress,
          learning: {
            ...awaitingPress.learning,
            phase: "awaitingRelease",
            candidate: { kind: "consumerControl", code: 0x0221 },
          },
        }}
        onStart={vi.fn()}
        onConfirm={vi.fn()}
        onRetry={vi.fn()}
        onCancel={vi.fn()}
        onReset={vi.fn()}
      />,
    );

    expect(screen.getByRole("status")).toHaveTextContent("已检测到按下，请松开麦克风键");
  });

  it("reviews the captured signal and sends confirm, retry and cancel with the session id", () => {
    const reviewInput: Rc901aInputStatus = {
      ...idleInput,
      learning: {
        phase: "review",
        sessionId: "session-2",
        target: "remoteMic",
        candidate: { kind: "consumerControl", code: 0x0221 },
        conflict: null,
        expiresAt: "2026-07-27T12:00:30Z",
      },
    };
    const callbacks = renderPanel(reviewInput);

    expect(screen.getByText("consumerControl · 0x0221")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "重新检测" }));
    fireEvent.click(screen.getByRole("button", { name: "取消识别" }));
    fireEvent.click(screen.getByRole("button", { name: "确认识别" }));

    expect(callbacks.onRetry).toHaveBeenCalledWith("session-2");
    expect(callbacks.onCancel).toHaveBeenCalledWith("session-2");
    expect(callbacks.onConfirm).toHaveBeenCalledWith("session-2");
  });

  it("requires a second explicit confirmation before overriding a verified signal", () => {
    const { onConfirm } = renderPanel({
      ...idleInput,
      learning: {
        phase: "review",
        sessionId: "session-3",
        target: "remoteMic",
        candidate: { kind: "keyboard", code: 0x0d },
        conflict: { control: "remoteOk", source: "verifiedDefault" },
        expiresAt: "2026-07-27T12:00:30Z",
      },
    });

    expect(screen.getByRole("alert")).toHaveTextContent("系统默认的确认键");
    expect(screen.queryByRole("button", { name: "确认覆盖默认识别" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "继续覆盖默认识别" }));
    expect(onConfirm).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "确认覆盖默认识别" }));
    expect(onConfirm).toHaveBeenCalledWith("session-3");
  });

  it("requires a second explicit confirmation before reassigning a learned signal", () => {
    const { onConfirm } = renderPanel({
      ...idleInput,
      learning: {
        phase: "review",
        sessionId: "session-4",
        target: "remoteMic",
        candidate: { kind: "consumerControl", code: 0x0224 },
        conflict: { control: "remoteBack", source: "learned" },
        expiresAt: "2026-07-27T12:00:30Z",
      },
    });

    expect(screen.getByRole("alert")).toHaveTextContent("目前分配给返回键");
    expect(screen.queryByRole("button", { name: "确认重新分配" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "继续重新分配" }));
    expect(onConfirm).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "确认重新分配" }));
    expect(onConfirm).toHaveBeenCalledWith("session-4");
  });

  it("treats relearning the same semantic control as a normal review", () => {
    const { onConfirm } = renderPanel({
      ...idleInput,
      learning: {
        phase: "review",
        sessionId: "session-self",
        target: "remoteBack",
        candidate: { kind: "consumerControl", code: 0x0224 },
        conflict: { control: "remoteBack", source: "learned" },
        expiresAt: "2026-07-27T12:00:30Z",
      },
    });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "继续重新分配" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "确认识别" }));
    expect(onConfirm).toHaveBeenCalledWith("session-self");
  });

  it("announces saving as the same atomic live status", () => {
    renderPanel({
      ...idleInput,
      learning: {
        phase: "saving",
        sessionId: "session-saving",
        target: "remoteMic",
        candidate: { kind: "consumerControl", code: 0x0221 },
        conflict: null,
        expiresAt: "2026-07-27T12:00:30Z",
      },
    });

    const status = screen.getByRole("status");
    expect(status).toHaveAttribute("aria-live", "polite");
    expect(status).toHaveAttribute("aria-atomic", "true");
    expect(status).toHaveTextContent("正在保存麦克风键的兼容性识别结果");
  });

  it("requires a clear confirmation before resetting learned bindings", () => {
    const { onReset } = renderPanel(learnedInput);
    openPanel();

    fireEvent.click(screen.getByRole("button", { name: "重置兼容性覆盖" }));
    expect(onReset).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "确认重置" }));

    expect(onReset).toHaveBeenCalledOnce();
  });

  it("clears reset confirmation and cannot reset while a learning phase is active", () => {
    const onReset = vi.fn();
    const props = {
      onStart: vi.fn(),
      onConfirm: vi.fn(),
      onRetry: vi.fn(),
      onCancel: vi.fn(),
      onReset,
    };
    const { rerender } = render(
      <Rc901aLearningPanel inputStatus={learnedInput} {...props} />,
    );
    openPanel();
    fireEvent.click(screen.getByRole("button", { name: "重置兼容性覆盖" }));
    expect(screen.getByRole("button", { name: "确认重置" })).toBeInTheDocument();

    rerender(
      <Rc901aLearningPanel
        inputStatus={{
          ...learnedInput,
          learning: {
            phase: "awaitingPress",
            sessionId: "session-active",
            target: "remoteMic",
            candidate: null,
            conflict: null,
            expiresAt: "2026-07-27T12:00:30Z",
          },
        }}
        {...props}
      />,
    );

    expect(screen.queryByRole("button", { name: "确认重置" })).not.toBeInTheDocument();
    expect(onReset).not.toHaveBeenCalled();
  });
});
