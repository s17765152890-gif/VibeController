import type { BridgeCommand, CommandType, HostMessage } from "./types";

export interface WebViewLike {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (event: MessageEvent) => void): void;
  removeEventListener(type: "message", listener: (event: MessageEvent) => void): void;
}

export interface AppBridge {
  subscribe(listener: (message: HostMessage) => void): () => void;
  send<TPayload>(type: CommandType, payload: TPayload): void;
}

function isHostMessage(value: unknown): value is HostMessage {
  if (!value || typeof value !== "object") {
    return false;
  }

  const message = value as Partial<HostMessage>;
  return message.version === 1 && message.type === "runtimeState" && !!message.payload;
}

export function createAppBridge(webview: WebViewLike): AppBridge {
  return {
    subscribe(listener) {
      const handleMessage = (event: MessageEvent) => {
        if (isHostMessage(event.data)) {
          listener(event.data);
        }
      };

      webview.addEventListener("message", handleMessage);
      return () => webview.removeEventListener("message", handleMessage);
    },
    send<TPayload>(type: CommandType, payload: TPayload) {
      const command: BridgeCommand<TPayload> = {
        version: 1,
        type,
        payload,
      };
      webview.postMessage(command);
    },
  };
}

function createBrowserFallback(): WebViewLike {
  const target = new EventTarget();
  return {
    postMessage(message) {
      window.dispatchEvent(new CustomEvent("vibecontroller:command", { detail: message }));
    },
    addEventListener(_type, listener) {
      target.addEventListener("message", listener as EventListener);
    },
    removeEventListener(_type, listener) {
      target.removeEventListener("message", listener as EventListener);
    },
  };
}

declare global {
  interface Window {
    chrome?: {
      webview?: WebViewLike;
    };
  }
}

export const appBridge = createAppBridge(
  window.chrome?.webview ?? createBrowserFallback(),
);
