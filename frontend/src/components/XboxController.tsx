import type { CSSProperties } from "react";

interface XboxControllerProps {
  controls: Record<string, number>;
}

function pressed(controls: Record<string, number>, control: string) {
  return (controls[control] ?? 0) > 0.5;
}

interface HotspotProps {
  testId: string;
  label: string;
  isPressed: boolean;
  className?: string;
  isActive?: boolean;
  style: CSSProperties;
}

function Hotspot({ testId, label, isPressed, className = "", isActive, style }: HotspotProps) {
  return (
    <span
      className={`photo-hotspot ${className}`}
      data-testid={testId}
      data-pressed={isPressed}
      data-active={isActive ?? isPressed}
      aria-label={label}
      style={style}
    />
  );
}

export function XboxController({ controls }: XboxControllerProps) {
  const leftX = Math.round((controls.leftStickX ?? 0) * 12);
  const leftY = Math.round(-(controls.leftStickY ?? 0) * 12);
  const rightX = Math.round((controls.rightStickX ?? 0) * 12);
  const rightY = Math.round(-(controls.rightStickY ?? 0) * 12);

  return (
    <div className="controller-photo-stage" role="img" aria-label="Xbox 手柄实时状态">
      <img
        className="controller-photo"
        data-testid="controller-photo"
        src="/forza-controller-white-v1.png"
        alt=""
        draggable="false"
      />

      <Hotspot testId="control-y" label="Y 键" isPressed={pressed(controls, "y")} className="photo-hotspot--face" style={{ left: "70.9%", top: "19.2%" }} />
      <Hotspot testId="control-x" label="X 键" isPressed={pressed(controls, "x")} className="photo-hotspot--face" style={{ left: "65.6%", top: "28.1%" }} />
      <Hotspot testId="control-b" label="B 键" isPressed={pressed(controls, "b")} className="photo-hotspot--face" style={{ left: "76.5%", top: "28.7%" }} />
      <Hotspot testId="control-a" label="A 键" isPressed={pressed(controls, "a")} className="photo-hotspot--face" style={{ left: "71%", top: "37.3%" }} />

      <Hotspot
        testId="control-left-stick-button"
        label="左摇杆与 L3"
        isPressed={pressed(controls, "leftStickButton")}
        isActive={Math.abs(controls.leftStickX ?? 0) > 0.04 || Math.abs(controls.leftStickY ?? 0) > 0.04 || pressed(controls, "leftStickButton")}
        className="photo-hotspot--stick"
        style={{ left: "29.1%", top: "28.4%", transform: `translate(-50%, -50%) translate(${leftX}px, ${leftY}px)` }}
      />
      <Hotspot
        testId="control-right-stick-button"
        label="右摇杆与 R3"
        isPressed={pressed(controls, "rightStickButton")}
        isActive={Math.abs(controls.rightStickX ?? 0) > 0.04 || Math.abs(controls.rightStickY ?? 0) > 0.04 || pressed(controls, "rightStickButton")}
        className="photo-hotspot--stick"
        style={{ left: "60.5%", top: "48.5%", transform: `translate(-50%, -50%) translate(${rightX}px, ${rightY}px)` }}
      />

      <Hotspot testId="control-dpad-up" label="方向键上" isPressed={pressed(controls, "dPadUp")} className="photo-hotspot--dpad photo-hotspot--dpad-vertical" style={{ left: "38.9%", top: "44.2%" }} />
      <Hotspot testId="control-dpad-down" label="方向键下" isPressed={pressed(controls, "dPadDown")} className="photo-hotspot--dpad photo-hotspot--dpad-vertical" style={{ left: "38.9%", top: "55.1%" }} />
      <Hotspot testId="control-dpad-left" label="方向键左" isPressed={pressed(controls, "dPadLeft")} className="photo-hotspot--dpad photo-hotspot--dpad-horizontal" style={{ left: "34.8%", top: "49.7%" }} />
      <Hotspot testId="control-dpad-right" label="方向键右" isPressed={pressed(controls, "dPadRight")} className="photo-hotspot--dpad photo-hotspot--dpad-horizontal" style={{ left: "43.1%", top: "49.7%" }} />

      <Hotspot testId="control-view" label="View 键" isPressed={pressed(controls, "view")} className="photo-hotspot--utility" style={{ left: "44.1%", top: "28.5%" }} />
      <Hotspot testId="control-menu" label="Menu 键" isPressed={pressed(controls, "menu")} className="photo-hotspot--utility" style={{ left: "55.7%", top: "29.6%" }} />

      <Hotspot testId="control-left-bumper" label="LB" isPressed={pressed(controls, "leftBumper")} className="photo-hotspot--shoulder" style={{ left: "31%", top: "8.5%" }} />
      <Hotspot testId="control-right-bumper" label="RB" isPressed={pressed(controls, "rightBumper")} className="photo-hotspot--shoulder" style={{ left: "69%", top: "8.5%" }} />
      <Hotspot testId="control-left-trigger" label="LT" isPressed={pressed(controls, "leftTrigger")} className="photo-hotspot--trigger" style={{ left: "22%", top: "10.5%" }} />
      <Hotspot testId="control-right-trigger" label="RT" isPressed={pressed(controls, "rightTrigger")} className="photo-hotspot--trigger" style={{ left: "78%", top: "10.5%" }} />
    </div>
  );
}
