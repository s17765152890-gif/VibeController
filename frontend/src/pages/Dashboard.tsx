import { Bluetooth, Crosshair, Pause, Play, Radio, Unplug } from "lucide-react";
import type { RuntimeStatePayload } from "../app/types";
import { getControllerPresentation } from "../app/controllerPresentation";
import { ControllerVisual } from "../components/ControllerVisual";
import { RecentAction } from "../components/RecentAction";
import { StatusPill } from "../components/StatusPill";

interface DashboardProps {
  state: RuntimeStatePayload;
  onToggleMapping(enabled: boolean): void;
  onToggleTestMode?(enabled: boolean): void;
}

export function Dashboard({ state, onToggleMapping, onToggleTestMode }: DashboardProps) {
  const connected = state.connectionState === "connected";
  const mappingLabel = state.mappingEnabled ? "映射已启用" : "映射已暂停";
  const controllerType = state.configuration?.controllerType ?? "xbox";
  const presentation = getControllerPresentation(controllerType);

  return (
    <main className="dashboard">
      <div className="dashboard-heading">
        <div>
          <p className="eyebrow">Controller workspace</p>
          <h1>VibeController</h1>
          <p className="subtitle">
            {presentation.subtitle}
          </p>
        </div>
        <div className="status-row" aria-label="运行状态">
          <StatusPill tone={connected ? "success" : "danger"}>
            {connected ? presentation.connectedLabel : "手柄未连接"}
          </StatusPill>
          <StatusPill tone={state.mappingEnabled ? "info" : "warning"}>
            {mappingLabel}
          </StatusPill>
          {state.testMode && <StatusPill tone="warning">测试模式</StatusPill>}
        </div>
      </div>

      <div className="dashboard-grid">
        <section
          className="controller-card"
          data-disconnected={!connected}
          aria-label="设备实时状态"
        >
          <div className="controller-meta">
            <span>{presentation.name}</span>
            <span>{controllerType === "tclRc901a"
              ? `直接 BLE · 数据包 ${state.packetNumber}`
              : `控制器 ${state.controllerIndex + 1} · 数据包 ${state.packetNumber}`}</span>
          </div>
          <ControllerVisual controllerType={controllerType} controls={state.controls} />
          {!connected && (
            <div className="disconnect-overlay">
              <Unplug size={22} aria-hidden="true" />
              <strong>等待 {presentation.waitingName}</strong>
              <p>连接后会自动恢复，无需重新配置</p>
            </div>
          )}
        </section>

        <aside className="side-stack">
          <section className="side-card">
            <div className="side-card-label">
              <span>映射控制</span>
              {connected ? <Bluetooth size={15} /> : <Radio size={15} />}
            </div>
            <div className="control-buttons">
              <button className={state.mappingEnabled ? "quiet-button" : "primary-button"} type="button" onClick={() => onToggleMapping(!state.mappingEnabled)}>
                {state.mappingEnabled ? <Pause size={15} /> : <Play size={15} />}
                {state.mappingEnabled ? "暂停映射" : "启用映射"}
              </button>
              {onToggleTestMode && <button className="quiet-button" type="button" onClick={() => onToggleTestMode(!state.testMode)}><Crosshair size={15}/>{state.testMode ? "退出测试" : "测试输入"}</button>}
            </div>
          </section>

          <section className="side-card">
            <div className="side-card-label"><span>最近动作</span><span>即时</span></div>
            <RecentAction action={state.lastAction} />
          </section>

          <section className="side-card">
            <div className="side-card-label"><span>默认快捷操作</span><span>Codex</span></div>
            <div className="control-list">
              {presentation.defaultActions.map(({ action, keycap }) => (
                <div className="control-row" key={action}>
                  <span>{action}</span><span className="keycap">{keycap}</span>
                </div>
              ))}
            </div>
          </section>
        </aside>
      </div>
    </main>
  );
}
