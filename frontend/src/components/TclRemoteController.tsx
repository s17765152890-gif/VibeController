import type { CSSProperties } from "react";

interface TclRemoteControllerProps {
  controls: Record<string, number>;
}

function pressed(controls: Record<string, number>, control: string) {
  return (controls[control] ?? 0) > 0.5;
}

type RemoteHotspotShape = "round" | "direction" | "ok" | "rocker" | "app";

interface RemoteHotspotDefinition {
  control: string;
  label: string;
  left: string;
  top: string;
  shape: RemoteHotspotShape;
}

const remoteHotspots: readonly RemoteHotspotDefinition[] = [
  { control: "remotePower", label: "电源键", left: "51.6%", top: "6.0%", shape: "round" },
  { control: "remoteMute", label: "静音键", left: "36.0%", top: "10.0%", shape: "round" },
  { control: "remoteInput", label: "输入源键", left: "67.2%", top: "10.1%", shape: "round" },
  { control: "remoteRed", label: "红色功能键", left: "36.2%", top: "16.3%", shape: "round" },
  { control: "remoteGreen", label: "绿色功能键", left: "51.8%", top: "16.3%", shape: "round" },
  { control: "remoteBlue", label: "蓝色功能键", left: "67.2%", top: "16.4%", shape: "round" },
  { control: "remoteUp", label: "方向键上", left: "51.7%", top: "22.8%", shape: "direction" },
  { control: "remoteLeft", label: "方向键左", left: "36.2%", top: "29.2%", shape: "direction" },
  { control: "remoteOk", label: "确认键", left: "51.7%", top: "29.2%", shape: "ok" },
  { control: "remoteRight", label: "方向键右", left: "67.2%", top: "29.2%", shape: "direction" },
  { control: "remoteDown", label: "方向键下", left: "51.7%", top: "35.7%", shape: "direction" },
  { control: "remoteBack", label: "返回键", left: "36.0%", top: "42.2%", shape: "round" },
  { control: "remoteVolumeUp", label: "音量加", left: "51.7%", top: "42.5%", shape: "rocker" },
  { control: "remoteHome", label: "主页键", left: "67.3%", top: "42.3%", shape: "round" },
  { control: "remoteMenu", label: "菜单键", left: "36.0%", top: "48.7%", shape: "round" },
  { control: "remoteVolumeDown", label: "音量减", left: "51.7%", top: "48.7%", shape: "rocker" },
  { control: "remoteSettings", label: "设置键", left: "67.3%", top: "48.7%", shape: "round" },
  { control: "remoteApp1", label: "哔哩哔哩键", left: "40.0%", top: "54.6%", shape: "app" },
  { control: "remoteApp2", label: "奇异果 TV 键", left: "62.5%", top: "54.6%", shape: "app" },
  { control: "remoteMic", label: "麦克风键", left: "51.7%", top: "60.5%", shape: "round" },
];

const sideControls = [
  { control: "remoteBrightnessUp", label: "亮度 +", keycap: "☀+" },
  { control: "remoteBrightnessDown", label: "亮度 −", keycap: "☀−" },
  { control: "remotePictureMode", label: "图像模式", keycap: "PIC" },
] as const;

function controlTestId(control: string) {
  return `control-${control.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)}`;
}

function RemoteHotspot({
  controls,
  definition,
}: {
  controls: Record<string, number>;
  definition: RemoteHotspotDefinition;
}) {
  const isPressed = pressed(controls, definition.control);
  const style: CSSProperties = { left: definition.left, top: definition.top };

  return (
    <span
      className={`tcl-remote-hotspot tcl-remote-hotspot--${definition.shape}`}
      role="img"
      aria-label={`${definition.label}${isPressed ? "，已按下" : ""}`}
      data-testid={controlTestId(definition.control)}
      data-pressed={isPressed}
      data-verified={definition.control !== "remotePower"}
      style={style}
    />
  );
}

export function TclRemoteController({ controls }: TclRemoteControllerProps) {
  return (
    <div
      className="tcl-remote-stage"
      data-testid="tcl-remote-visual"
      role="group"
      aria-label="TCL RC901A 遥控器实时状态"
    >
      <div className="tcl-remote-visual-row">
        <div className="tcl-remote-photo-frame">
          <img
            className="tcl-remote-photo"
            data-testid="controller-photo"
            src="/tcl-rc901a.jpg"
            alt="TCL RC901A 遥控器正面"
            draggable="false"
          />
          {remoteHotspots.map((definition) => (
            <RemoteHotspot
              key={definition.control}
              controls={controls}
              definition={definition}
            />
          ))}
        </div>
        <div
          className="tcl-remote-side-controls"
          role="group"
          aria-label="遥控器侧边按键"
        >
          <small>侧边</small>
          {sideControls.map((definition) => {
            const isPressed = pressed(controls, definition.control);
            return (
              <span
                className="tcl-remote-side-control"
                role="img"
                aria-label={`${definition.label}${isPressed ? "，已按下" : ""}`}
                data-testid={controlTestId(definition.control)}
                data-pressed={isPressed}
                data-verified="true"
                key={definition.control}
              >
                <span>{definition.keycap}</span>
                <small>{definition.label}</small>
              </span>
            );
          })}
        </div>
      </div>
      <div className="remote-connection-caption">
        <span className="remote-connection-dot" aria-hidden="true" />
        <span>Windows HID</span>
        <small>22 键自动就绪</small>
      </div>
    </div>
  );
}
