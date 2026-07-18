export type ConnectionState = "unknown" | "connected" | "disconnected";
export type ControllerType = "xbox" | "playStation5";
export type MicrophoneDetectionState = "available" | "noDevices" | "error";
export type CodexActivityState = "idle" | "working" | "needsAttention" | "completed";

export interface MicrophoneStatus {
  state: MicrophoneDetectionState;
  defaultDeviceName: string | null;
  deviceNames: string[];
  dualSenseMicrophoneAvailable: boolean;
  message: string | null;
}

export interface CodexHookRegistrationStatus {
  enabled: boolean;
  installed: boolean;
  errorMessage: string | null;
}

export interface CodexActivityStatus {
  state: CodexActivityState;
  lastEventAt: string | null;
  activeSessionCount: number;
}

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
  codexLightbarEnabled?: boolean;
  microphone?: MicrophoneStatus;
  codexHook?: CodexHookRegistrationStatus;
  codexActivity?: CodexActivityStatus;
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
  | "refreshIntegrations"
  | "requestState";

export interface BridgeCommand<TPayload = unknown> {
  version: 1;
  type: CommandType;
  payload: TPayload;
}
