export type ConnectionState = "unknown" | "connected" | "disconnected";
export type ControllerType = "xbox" | "playStation5" | "tclRc901a";
export type MicrophoneDetectionState = "available" | "noDevices" | "error";
export type CodexActivityState = "idle" | "working" | "needsAttention" | "completed";
export type Rc901aConnectionState =
  | "idle"
  | "scanning"
  | "connecting"
  | "connected"
  | "connectedLimited"
  | "disconnected"
  | "error";
export type Rc901aRawInputKind =
  | "keyboard"
  | "consumerControl"
  | "driverHidUsage";
export type Rc901aBindingSource = "verifiedDefault" | "learned";
export type Rc901aLearningPhase =
  | "idle"
  | "awaitingPress"
  | "awaitingRelease"
  | "review"
  | "saving";

export type Rc901aControl =
  | "remotePower"
  | "remoteMute"
  | "remoteInput"
  | "remoteRed"
  | "remoteGreen"
  | "remoteBlue"
  | "remoteUp"
  | "remoteLeft"
  | "remoteOk"
  | "remoteRight"
  | "remoteDown"
  | "remoteBack"
  | "remoteVolumeUp"
  | "remoteHome"
  | "remoteMenu"
  | "remoteVolumeDown"
  | "remoteSettings"
  | "remoteApp1"
  | "remoteApp2"
  | "remoteMic"
  | "remoteBrightnessUp"
  | "remoteBrightnessDown"
  | "remotePictureMode";

export type Rc901aLearnableControl = Exclude<
  Rc901aControl,
  "remotePower"
>;

export interface Rc901aInputSignal {
  kind: Rc901aRawInputKind;
  code: number;
}

export interface Rc901aUnknownInputSignal extends Rc901aInputSignal {
  timestamp: string;
}

export interface Rc901aInputBinding extends Rc901aInputSignal {
  control: string;
  source: Rc901aBindingSource;
}

export interface Rc901aLearningConflict {
  control: string;
  source: Rc901aBindingSource;
}

export interface Rc901aLearningStatus {
  phase: Rc901aLearningPhase;
  sessionId: string | null;
  target: string | null;
  candidate: Rc901aInputSignal | null;
  conflict: Rc901aLearningConflict | null;
  expiresAt: string | null;
}

export interface Rc901aInputStatus {
  bindings: Rc901aInputBinding[];
  lastUnknown: Rc901aUnknownInputSignal | null;
  learning: Rc901aLearningStatus;
}

export interface Rc901aPacketSample {
  timestamp: string;
  serviceUuid: string;
  characteristicUuid: string;
  dataHex: string;
  length: number;
}

export interface Rc901aStatus {
  connectionState: Rc901aConnectionState;
  deviceName: string | null;
  deviceId: string | null;
  batteryPercent: number | null;
  subscribedCharacteristicCount: number;
  message: string | null;
  samples: Rc901aPacketSample[];
}

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
  rc901a?: Rc901aStatus;
  rc901aInput?: Rc901aInputStatus;
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
  | "refreshRc901a"
  | "clearRc901aSamples"
  | "startRc901aLearning"
  | "confirmRc901aLearning"
  | "retryRc901aLearning"
  | "cancelRc901aLearning"
  | "resetRc901aLearnedBindings"
  | "requestState";

export interface BridgeCommand<TPayload = unknown> {
  version: 1;
  type: CommandType;
  payload: TPayload;
}
