import { describe, expect, it, vi } from "vitest";
import { createAppBridge } from "./AppBridge";

describe("AppBridge", () => {
  it("publishes runtime state messages received from WebView2", () => {
    const listeners = new Set<(event: MessageEvent) => void>();
    const postMessage = vi.fn();
    const webview = {
      postMessage,
      addEventListener: (_type: "message", listener: (event: MessageEvent) => void) => {
        listeners.add(listener);
      },
      removeEventListener: (_type: "message", listener: (event: MessageEvent) => void) => {
        listeners.delete(listener);
      },
    };
    const bridge = createAppBridge(webview);
    const subscriber = vi.fn();
    const unsubscribe = bridge.subscribe(subscriber);

    const message = {
      version: 1,
      type: "runtimeState" as const,
      payload: {
        connectionState: "connected" as const,
        controllerIndex: 0,
        mappingEnabled: true,
        testMode: false,
        packetNumber: 4,
        controls: { x: 1 },
        lastAction: "X → 切换听写快捷键",
      },
    };
    listeners.forEach((listener) => listener(new MessageEvent("message", { data: message })));

    expect(subscriber).toHaveBeenCalledWith(message);
    unsubscribe();
    expect(listeners).toHaveLength(0);
  });

  it("sends versioned commands to the host", () => {
    const postMessage = vi.fn();
    const bridge = createAppBridge({
      postMessage,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    });

    bridge.send("setMappingEnabled", { enabled: false });

    expect(postMessage).toHaveBeenCalledWith({
      version: 1,
      type: "setMappingEnabled",
      payload: { enabled: false },
    });
  });
});
