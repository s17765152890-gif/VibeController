import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { RuntimeStatePayload } from "./app/types";

const mocked = vi.hoisted(() => ({
  send: vi.fn(),
  setState: vi.fn(),
  state: {
    connectionState: "connected",
    controllerIndex: 0,
    mappingEnabled: true,
    testMode: false,
    packetNumber: 1,
    controls: {},
    lastAction: null,
    configuration: {
      controllerType: "tclRc901a",
      activeControllerIndex: 0,
      codexOnly: true,
      dictationShortcut: "Ctrl+Alt+Shift+F12",
      mouseSpeed: 50,
      scrollSpeed: 50,
      deadZone: 0.18,
      startWithWindows: false,
      mappings: {},
      rc901a: {
        connectionState: "connectedLimited",
        deviceName: "BT_RC901A_B1",
        deviceId: "device-id",
        batteryPercent: null,
        subscribedCharacteristicCount: 1,
        message: null,
        samples: [],
      },
      rc901aInput: {
        bindings: [],
        lastUnknown: null,
        learning: {
          phase: "idle",
          sessionId: null,
          target: null,
          candidate: null,
          conflict: null,
          expiresAt: null,
        },
      },
    },
  } satisfies RuntimeStatePayload,
}));

vi.mock("./app/AppBridge", () => ({
  appBridge: { send: mocked.send },
}));

vi.mock("./app/useRuntimeState", () => ({
  useRuntimeState: () => [mocked.state, mocked.setState],
}));

vi.mock("./pages/Settings", () => ({
  Settings: (props: {
    onStartRc901aLearning(control: string, compatibilityOverride: true): void;
    onConfirmRc901aLearning(sessionId: string): void;
    onRetryRc901aLearning(sessionId: string): void;
    onCancelRc901aLearning(sessionId: string): void;
    onResetRc901aLearnedBindings(): void;
    onSave(values: {
      controllerType: string;
      activeControllerIndex: number;
      codexOnly: boolean;
      dictationShortcut: string;
      mouseSpeed: number;
      scrollSpeed: number;
      deadZone: number;
      startWithWindows: boolean;
      codexLightbarEnabled: boolean;
    }): void;
    persistedControllerType?: string;
    rc901aLearningReady?: boolean;
  }) => (
    <div>
      <output aria-label="persisted controller">{props.persistedControllerType}</output>
      <output aria-label="learning ready">{String(props.rc901aLearningReady)}</output>
      <button type="button" onClick={() => props.onStartRc901aLearning("remoteMic", true)}>start learning</button>
      <button type="button" onClick={() => props.onConfirmRc901aLearning("session-1")}>confirm learning</button>
      <button type="button" onClick={() => props.onRetryRc901aLearning("session-1")}>retry learning</button>
      <button type="button" onClick={() => props.onCancelRc901aLearning("session-1")}>cancel learning</button>
      <button type="button" onClick={props.onResetRc901aLearnedBindings}>reset learning</button>
      <button
        type="button"
        onClick={() => props.onSave({
          controllerType: "xbox",
          activeControllerIndex: 0,
          codexOnly: true,
          dictationShortcut: "Ctrl+Alt+Shift+F12",
          mouseSpeed: 50,
          scrollSpeed: 50,
          deadZone: 0.18,
          startWithWindows: false,
          codexLightbarEnabled: false,
        })}
      >
        save Xbox
      </button>
    </div>
  ),
}));

import App from "./App";

afterEach(() => {
  cleanup();
  mocked.send.mockClear();
  mocked.setState.mockClear();
});

describe("App RC901A bridge wiring", () => {
  it("passes all learning actions to the versioned app bridge", () => {
    render(<App />);
    fireEvent.click(
      within(screen.getByRole("navigation", { name: "主导航" }))
        .getByRole("button", { name: "设置" }),
    );

    fireEvent.click(screen.getByRole("button", { name: "start learning" }));
    fireEvent.click(screen.getByRole("button", { name: "confirm learning" }));
    fireEvent.click(screen.getByRole("button", { name: "retry learning" }));
    fireEvent.click(screen.getByRole("button", { name: "cancel learning" }));
    fireEvent.click(screen.getByRole("button", { name: "reset learning" }));

    expect(screen.getByLabelText("persisted controller")).toHaveTextContent("tclRc901a");
    expect(screen.getByLabelText("learning ready")).toHaveTextContent("true");
    expect(mocked.send.mock.calls).toEqual([
      ["startRc901aLearning", { control: "remoteMic", compatibilityOverride: true }],
      ["confirmRc901aLearning", { sessionId: "session-1" }],
      ["retryRc901aLearning", { sessionId: "session-1" }],
      ["cancelRc901aLearning", { sessionId: "session-1" }],
      ["resetRc901aLearnedBindings", {}],
    ]);
  });

  it("does not treat an optimistic device choice as backend-persisted", () => {
    render(<App />);
    fireEvent.click(
      within(screen.getByRole("navigation", { name: "主导航" }))
        .getByRole("button", { name: "设置" }),
    );

    fireEvent.click(screen.getByRole("button", { name: "save Xbox" }));

    const update = mocked.setState.mock.calls[0][0] as (
      current: RuntimeStatePayload,
    ) => RuntimeStatePayload;
    const optimisticState = update(mocked.state);
    expect(optimisticState.configuration?.controllerType).toBe("tclRc901a");
  });
});
