import type { ControllerType } from "../app/types";
import { PlayStationController } from "./PlayStationController";
import { TclRemoteController } from "./TclRemoteController";
import { XboxController } from "./XboxController";

interface ControllerVisualProps {
  controllerType: ControllerType;
  controls: Record<string, number>;
}

export function ControllerVisual({ controllerType, controls }: ControllerVisualProps) {
  if (controllerType === "playStation5") {
    return <PlayStationController controls={controls} />;
  }
  if (controllerType === "tclRc901a") {
    return <TclRemoteController controls={controls} />;
  }
  return <XboxController controls={controls} />;
}
