interface TclRemoteControllerProps {
  controls: Record<string, number>;
}

function pressed(controls: Record<string, number>, control: string) {
  return (controls[control] ?? 0) > 0.5;
}

interface RemoteKeyProps {
  control: string;
  label: string;
  children: string;
  controls: Record<string, number>;
  className?: string;
}

function RemoteKey({ control, label, children, controls, className = "" }: RemoteKeyProps) {
  return (
    <span
      className={`remote-key ${className}`}
      aria-label={label}
      data-testid={`control-${control.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)}`}
      data-pressed={pressed(controls, control)}
    >
      {children}
    </span>
  );
}

export function TclRemoteController({ controls }: TclRemoteControllerProps) {
  return (
    <div
      className="tcl-remote-stage"
      data-testid="tcl-remote-visual"
      role="img"
      aria-label="TCL RC901A 遥控器实时状态"
    >
      <div className="tcl-remote-shell">
        <div className="remote-brand"><span>TCL</span><small>RC901A</small></div>
        <div className="remote-top-row">
          <RemoteKey control="remoteHome" label="主页键" controls={controls}>⌂</RemoteKey>
          <RemoteKey control="remoteMic" label="麦克风键" controls={controls} className="remote-key--mic">●</RemoteKey>
        </div>
        <div className="remote-dpad" aria-label="方向键">
          <RemoteKey control="remoteUp" label="方向键上" controls={controls} className="remote-key--up">↑</RemoteKey>
          <RemoteKey control="remoteLeft" label="方向键左" controls={controls} className="remote-key--left">←</RemoteKey>
          <RemoteKey control="remoteOk" label="确认键" controls={controls} className="remote-key--ok">OK</RemoteKey>
          <RemoteKey control="remoteRight" label="方向键右" controls={controls} className="remote-key--right">→</RemoteKey>
          <RemoteKey control="remoteDown" label="方向键下" controls={controls} className="remote-key--down">↓</RemoteKey>
        </div>
        <div className="remote-action-row">
          <RemoteKey control="remoteBack" label="返回键" controls={controls}>↩</RemoteKey>
          <RemoteKey control="remoteMenu" label="菜单键" controls={controls}>☰</RemoteKey>
          <RemoteKey control="remoteMute" label="静音键" controls={controls}>M</RemoteKey>
        </div>
        <div className="remote-rockers">
          <div><small>VOL</small><RemoteKey control="remoteVolumeUp" label="音量加" controls={controls}>＋</RemoteKey><RemoteKey control="remoteVolumeDown" label="音量减" controls={controls}>−</RemoteKey></div>
          <div><small>CH</small><RemoteKey control="remoteChannelUp" label="频道加" controls={controls}>＋</RemoteKey><RemoteKey control="remoteChannelDown" label="频道减" controls={controls}>−</RemoteKey></div>
        </div>
      </div>
      <div className="remote-connection-caption">
        <span aria-hidden="true" />直接 BLE
      </div>
    </div>
  );
}
