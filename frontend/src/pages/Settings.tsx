import { useState } from "react";
import { Clipboard, Gamepad2, RefreshCw, Save } from "lucide-react";
import { SliderField } from "../components/SliderField";
import type { ControllerType } from "../app/types";

export interface SettingsValues {
  controllerType: ControllerType;
  codexOnly: boolean;
  startWithWindows: boolean;
  deadZone: number;
  mouseSpeed: number;
  scrollSpeed: number;
  activeControllerIndex: number;
  dictationShortcut: string;
}

interface SettingsProps {
  onSave(values: SettingsValues): void;
  onCopyDiagnostics(): void;
  initialValues?: SettingsValues;
}

const defaultSettings: SettingsValues = {
  controllerType: "xbox",
  codexOnly: true,
  startWithWindows: false,
  deadZone: 0.18,
  mouseSpeed: 50,
  scrollSpeed: 50,
  activeControllerIndex: 0,
  dictationShortcut: "Ctrl+Alt+Shift+F12",
};

export function Settings({ onSave, onCopyDiagnostics, initialValues }: SettingsProps) {
  const [values, setValues] = useState<SettingsValues>(initialValues ?? defaultSettings);
  const update = <K extends keyof SettingsValues>(key: K, value: SettingsValues[K]) => setValues((current) => ({ ...current, [key]: value }));

  return (
    <main className="workspace-page settings-page">
      <header className="page-heading"><div><p className="eyebrow">Preferences</p><h1>设置</h1><p>调整安全边界、摇杆手感和启动行为。</p></div></header>
      <div className="settings-grid">
        <section className="settings-card">
          <h2>安全与启动</h2>
          <label className="switch-row"><span><strong>仅在 Codex 前台时执行</strong><small>避免手柄操作误输入到其他应用。</small></span><input aria-label="仅在 Codex 前台时执行" type="checkbox" checked={values.codexOnly} onChange={(event) => update("codexOnly", event.target.checked)} /></label>
          <label className="switch-row"><span><strong>登录 Windows 后自动启动</strong><small>后台等待手柄连接，可随时从托盘打开。</small></span><input aria-label="登录 Windows 后自动启动" type="checkbox" checked={values.startWithWindows} onChange={(event) => update("startWithWindows", event.target.checked)} /></label>
        </section>
        <section className="settings-card">
          <h2>输入手感</h2>
          <SliderField label="摇杆死区" value={Math.round(values.deadZone * 100)} min={5} max={35} onChange={(value) => update("deadZone", value / 100)} />
          <SliderField label="鼠标速度" value={values.mouseSpeed} min={10} max={100} onChange={(value) => update("mouseSpeed", value)} />
          <SliderField label="滚动速度" value={values.scrollSpeed} min={10} max={100} onChange={(value) => update("scrollSpeed", value)} />
        </section>
        <section className="settings-card device-settings-card">
          <h2>设备与 Codex</h2>
          <fieldset className="device-family-picker">
            <legend>手柄类型</legend>
            <div className="device-family-options">
              <label className="device-family-option" data-selected={values.controllerType === "xbox"}>
                <input
                  aria-label="Xbox 无线手柄"
                  type="radio"
                  name="controllerType"
                  value="xbox"
                  checked={values.controllerType === "xbox"}
                  onChange={() => update("controllerType", "xbox")}
                />
                <span className="device-family-icon"><Gamepad2 size={20} /></span>
                <span><strong>Xbox 无线手柄</strong><small>XInput · Xbox Series X|S</small></span>
                <span className="selection-dot" aria-hidden="true" />
              </label>
              <label className="device-family-option" data-selected={values.controllerType === "playStation5"}>
                <input
                  aria-label="PS5 DualSense"
                  type="radio"
                  name="controllerType"
                  value="playStation5"
                  checked={values.controllerType === "playStation5"}
                  onChange={() => update("controllerType", "playStation5")}
                />
                <span className="device-family-icon device-family-icon--ps">△○×□</span>
                <span><strong>PS5 DualSense</strong><small>USB / 蓝牙 · 支持触控板</small></span>
                <span className="selection-dot" aria-hidden="true" />
              </label>
            </div>
          </fieldset>
          <label className="field-stack"><span>活动控制器</span><select aria-label="活动控制器" value={values.activeControllerIndex} onChange={(event) => update("activeControllerIndex", Number(event.target.value))}>{[0, 1, 2, 3].map((index) => <option key={index} value={index}>控制器 {index + 1}</option>)}</select></label>
          <div className="codex-sync-card" role="note">
            <span className="codex-sync-icon" aria-hidden="true"><RefreshCw size={17} /></span>
            <span>
              <strong>自动同步 Codex 快捷键</strong>
              <small>首次触发 Codex 操作时读取当前用户的设置；之后修改会自动刷新，无需重新映射。</small>
            </span>
          </div>
        </section>
        <section className="settings-card diagnostics-card">
          <h2>诊断</h2><p>复制当前版本、设备状态和运行配置，便于排查连接问题。</p>
          <button className="quiet-button" type="button" onClick={onCopyDiagnostics}><Clipboard size={15}/>复制诊断信息</button>
        </section>
      </div>
      <footer className="settings-footer"><button className="primary-button" type="button" onClick={() => onSave(values)}><Save size={15}/>保存设置</button></footer>
    </main>
  );
}
