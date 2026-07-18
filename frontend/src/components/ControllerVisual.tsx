import type { ControllerType } from "../app/types";
import { PlayStationController } from "./PlayStationController";
import { XboxController } from "./XboxController";

interface ControllerVisualProps {
  controllerType: ControllerType;
  controls: Record<string, number>;
}

export function ControllerVisual({ controllerType, controls }: ControllerVisualProps) {
  return controllerType === "playStation5"
    ? <PlayStationController controls={controls} />
    : <XboxController controls={controls} />;
}
