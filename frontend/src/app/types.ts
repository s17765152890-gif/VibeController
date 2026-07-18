export type ConnectionState = "unknown" | "connected" | "disconnected";
export type ControllerType = "xbox" | "playStation5";

export interface RuntimeStatePayload {
  connectionState: ConnectionState;
  controllerIndex: number;
  mappingEnabled: boolean;
  testMode: boolean;
  packetNumber: number;
  controls: Record<string, number>;
  lastAction: string | null;
  configuration?: RuntimeConfiguration;
}

export interface RuntimeConfiguration {
  controllerType: ControllerType;
  activeControllerIndex: number;
  codexOnly: boolean;
  dictationShortcut: string;
  mouseSpeed: number;
  scrollSpeed: number;
  deadZone: number;
  startWithWindows: boolean;
  mappings: Record<string, string>;
}

export interface RuntimeStateMessage {
  version: 1;
  type: "runtimeState";
  payload: RuntimeStatePayload;
}

export type HostMessage = RuntimeStateMessage;

export type CommandType =
  | "setMappingEnabled"
  | "setTestMode"
  | "updateMapping"
  | "updateSettings"
  | "resetDefaults"
  | "requestState";

export interface BridgeCommand<TPayload = unknown> {
  version: 1;
  type: CommandType;
  payload: TPayload;
}
