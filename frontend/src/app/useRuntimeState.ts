import { useEffect, useState } from "react";
import { appBridge } from "./AppBridge";
import type { RuntimeStatePayload } from "./types";

export const demoRuntimeState: RuntimeStatePayload = {
  connectionState: "connected",
  controllerIndex: 0,
  mappingEnabled: true,
  testMode: false,
  packetNumber: 12,
  controls: { x: 0, a: 0, b: 0, y: 0, leftStickX: 0, leftStickY: 0 },
  lastAction: "X → 切换听写快捷键",
};

export function useRuntimeState() {
  const [state, setState] = useState<RuntimeStatePayload>(demoRuntimeState);

  useEffect(() => appBridge.subscribe((message) => setState(message.payload)), []);

  return [state, setState] as const;
}
