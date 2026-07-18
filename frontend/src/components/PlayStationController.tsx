import type { CSSProperties } from "react";

interface PlayStationControllerProps {
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

export function PlayStationController({ controls }: PlayStationControllerProps) {
  const leftX = Math.round((controls.leftStickX ?? 0) * 10);
  const leftY = Math.round(-(controls.leftStickY ?? 0) * 10);
  const rightX = Math.round((controls.rightStickX ?? 0) * 10);
  const rightY = Math.round(-(controls.rightStickY ?? 0) * 10);
  const touchX = controls.touchpadX ?? 0;
  const touchY = controls.touchpadY ?? 0;
  const touchPressed = pressed(controls, "touchpadButton");
  const touchActive = touchPressed || Math.abs(touchX) > 0.001 || Math.abs(touchY) > 0.001;

  return (
    <div className="controller-photo-stage controller-photo-stage--dualsense" role="img" aria-label="PS5 DualSense 实时状态">
      <img
        className="controller-photo"
        data-testid="controller-photo"
        src="/dualsense-black.png"
        alt=""
        draggable="false"
      />

      <Hotspot testId="control-triangle" label="三角键" isPressed={pressed(controls, "y")} className="photo-hotspot--face" style={{ left: "81.4%", top: "32.5%" }} />
      <Hotspot testId="control-square" label="方块键" isPressed={pressed(controls, "x")} className="photo-hotspot--face" style={{ left: "74.6%", top: "39.2%" }} />
      <Hotspot testId="control-circle" label="圆圈键" isPressed={pressed(controls, "b")} className="photo-hotspot--face" style={{ left: "88.3%", top: "39.2%" }} />
      <Hotspot testId="control-cross" label="叉键" isPressed={pressed(controls, "a")} className="photo-hotspot--face" style={{ left: "81.4%", top: "46.1%" }} />

      <Hotspot
        testId="control-left-stick-button"
        label="左摇杆与 L3"
        isPressed={pressed(controls, "leftStickButton")}
        isActive={Math.abs(controls.leftStickX ?? 0) > 0.04 || Math.abs(controls.leftStickY ?? 0) > 0.04 || pressed(controls, "leftStickButton")}
        className="photo-hotspot--stick"
        style={{ left: "36.9%", top: "52.5%", transform: `translate(-50%, -50%) translate(${leftX}px, ${leftY}px)` }}
      />
      <Hotspot
        testId="control-right-stick-button"
        label="右摇杆与 R3"
        isPressed={pressed(controls, "rightStickButton")}
        isActive={Math.abs(controls.rightStickX ?? 0) > 0.04 || Math.abs(controls.rightStickY ?? 0) > 0.04 || pressed(controls, "rightStickButton")}
        className="photo-hotspot--stick"
        style={{ left: "66.8%", top: "52.5%", transform: `translate(-50%, -50%) translate(${rightX}px, ${rightY}px)` }}
      />

      <Hotspot testId="control-dpad-up" label="方向键上" isPressed={pressed(controls, "dPadUp")} className="photo-hotspot--dpad photo-hotspot--dpad-vertical" style={{ left: "22.9%", top: "34.0%" }} />
      <Hotspot testId="control-dpad-down" label="方向键下" isPressed={pressed(controls, "dPadDown")} className="photo-hotspot--dpad photo-hotspot--dpad-vertical" style={{ left: "22.9%", top: "45.1%" }} />
      <Hotspot testId="control-dpad-left" label="方向键左" isPressed={pressed(controls, "dPadLeft")} className="photo-hotspot--dpad photo-hotspot--dpad-horizontal" style={{ left: "17.4%", top: "39.5%" }} />
      <Hotspot testId="control-dpad-right" label="方向键右" isPressed={pressed(controls, "dPadRight")} className="photo-hotspot--dpad photo-hotspot--dpad-horizontal" style={{ left: "28.2%", top: "39.5%" }} />

      <Hotspot testId="control-create" label="Create 键" isPressed={pressed(controls, "view")} className="photo-hotspot--utility" style={{ left: "30.3%", top: "29.0%" }} />
      <Hotspot testId="control-options" label="Options 键" isPressed={pressed(controls, "menu")} className="photo-hotspot--utility" style={{ left: "74.1%", top: "29.0%" }} />

      <Hotspot testId="control-left-bumper" label="L1" isPressed={pressed(controls, "leftBumper")} className="photo-hotspot--shoulder" style={{ left: "23.2%", top: "22.8%" }} />
      <Hotspot testId="control-right-bumper" label="R1" isPressed={pressed(controls, "rightBumper")} className="photo-hotspot--shoulder" style={{ left: "79.4%", top: "22.8%" }} />
      <Hotspot testId="control-left-trigger" label="L2" isPressed={pressed(controls, "leftTrigger")} className="photo-hotspot--trigger" style={{ left: "23.2%", top: "20.1%" }} />
      <Hotspot testId="control-right-trigger" label="R2" isPressed={pressed(controls, "rightTrigger")} className="photo-hotspot--trigger" style={{ left: "79.4%", top: "20.1%" }} />

      <Hotspot
        testId="control-touchpad"
        label="触控板滑动与按下"
        isPressed={touchPressed}
        isActive={touchActive}
        className="photo-hotspot--touchpad"
        style={{ left: "52.6%", top: "32.2%" }}
      />
    </div>
  );
}
