import { useMemo, useState } from "react";
import { AlertTriangle, RefreshCw, RotateCcw, Save } from "lucide-react";
import { ActionPicker, actionOptions } from "../components/ActionPicker";
import { ShortcutRecorder } from "../components/ShortcutRecorder";
import { getControllerPresentation } from "../app/controllerPresentation";
import type { ControllerType } from "../app/types";

export type MappingValues = Record<string, string>;

const defaults: MappingValues = {
  x: "dictation", a: "send", b: "keyboardShortcut", y: "commandPalette",
  menu: "activateCodex", leftShoulder: "previousChat", rightShoulder: "nextChat",
  dPadUp: "keyboardShortcut", dPadDown: "keyboardShortcut", dPadLeft: "decreaseReasoning", dPadRight: "increaseReasoning",
  leftTrigger: "mouseRightClick", rightTrigger: "mouseLeftClick", leftStickX: "mouseMove", leftStickY: "mouseMove",
  rightStickLeft: "keyboardShortcut", rightStickRight: "keyboardShortcut",
  rightStickUp: "keyboardShortcut", rightStickDown: "keyboardShortcut",
  view: "none", leftStickButton: "none", rightStickButton: "none",
  touchpadX: "mouseMove", touchpadY: "mouseMove", touchpadButton: "mouseLeftClick",
};

const defaultShortcuts: Record<string, string> = {
  b: "Backspace",
  dPadUp: "ArrowUp",
  dPadDown: "ArrowDown",
  rightStickLeft: "ArrowLeft",
  rightStickRight: "ArrowRight",
  rightStickUp: "ArrowUp",
  rightStickDown: "ArrowDown",
};
const exclusiveActions = new Set(["dictation", "send", "cancel", "commandPalette", "previousChat", "nextChat", "activateCodex"]);
const codexShortcutActions = new Set([
  "dictation", "send", "commandPalette", "previousChat", "nextChat",
  "previousRecentThread", "nextRecentThread", "previousTab", "nextTab",
  "increaseReasoning", "decreaseReasoning",
]);
const actionLabels = new Map<string, string>(actionOptions);

function describeShortcut(shortcut: string | undefined) {
  if (!shortcut) return "键盘：请录入快捷键";
  const displayName = {
    Backspace: "Backspace（删除上一个字符）",
    ArrowUp: "↑（ArrowUp）",
    ArrowDown: "↓（ArrowDown）",
    ArrowLeft: "←（ArrowLeft）",
    ArrowRight: "→（ArrowRight）",
  }[shortcut] ?? shortcut;
  return `键盘：${displayName}`;
}

function describeMapping(action: string, shortcut: string | undefined) {
  return action === "keyboardShortcut"
    ? describeShortcut(shortcut)
    : actionLabels.get(action) ?? "未设置";
}

interface MappingEditorProps {
  controllerType?: ControllerType;
  onSave(values: MappingValues): void;
  onReset(): void;
  initialValues?: MappingValues;
}

export function MappingEditor({ controllerType = "xbox", onSave, onReset, initialValues }: MappingEditorProps) {
  const presentation = getControllerPresentation(controllerType);
  const controls = presentation.controls;
  const incoming = initialValues ?? {};
  const incomingActions = Object.fromEntries(Object.entries(incoming).map(([key, value]) => [key, value.startsWith("shortcut:") ? "keyboardShortcut" : value]));
  const incomingShortcuts = Object.fromEntries(Object.entries(incoming).filter(([, value]) => value.startsWith("shortcut:")).map(([key, value]) => [key, value.slice(9)]));
  const [selected, setSelected] = useState("x");
  const [values, setValues] = useState({ ...defaults, ...incomingActions });
  const [shortcuts, setShortcuts] = useState({ ...defaultShortcuts, ...incomingShortcuts });
  const [saved, setSaved] = useState(false);
  const selectedControl = controls.find((control) => control.key === selected);
  const selectedLabel = selectedControl?.label ?? selected;
  const conflict = useMemo(() => {
    const action = values[selected];
    if (!exclusiveActions.has(action)) return null;
    const match = controls.find((control) => control.key !== selected && values[control.key] === action);
    return match?.label ?? null;
  }, [selected, values]);
  const actionLabel = describeMapping(values[selected], shortcuts[selected]);

  const changeAction = (action: string) => {
    setValues((current) => ({ ...current, [selected]: action }));
    setSaved(false);
  };

  return (
    <main className="workspace-page">
      <header className="page-heading">
        <div><p className="eyebrow">Input map</p><h1>按键映射</h1><p>选择手柄按键，再绑定 Codex 快捷操作或键鼠输入。</p></div>
        <div className="page-actions">
          <button className="quiet-button" type="button" onClick={() => { setValues(defaults); setShortcuts(defaultShortcuts); onReset(); }}><RotateCcw size={15}/>恢复默认</button>
          <button className="primary-button" type="button" onClick={() => { onSave(Object.fromEntries(Object.entries(values).map(([key, value]) => [key, value === "keyboardShortcut" ? `shortcut:${shortcuts[key] ?? ""}` : value]))); setSaved(true); }}><Save size={15}/>保存映射</button>
        </div>
      </header>

      <div className="editor-grid">
        <section className="mapping-list" aria-label={`${presentation.statusName}按键列表`}>
          {controls.map(({ key, label, keycap }) => (
            <button key={key} type="button" aria-label={label} data-selected={selected === key} onClick={() => setSelected(key)}>
              <span className="keycap">{keycap}</span>
              <span><strong>{label}</strong><small>{describeMapping(values[key], shortcuts[key])}</small></span>
            </button>
          ))}
        </section>

        <section className="inspector-card">
          <div className="inspector-title"><span className="large-keycap">{selectedControl?.keycap ?? selectedLabel.split(" ")[0]}</span><div><p>正在编辑</p><h2>{selectedLabel}</h2></div></div>
          <ActionPicker value={values[selected]} onChange={changeAction} />
          {codexShortcutActions.has(values[selected]) && (
            <p className="codex-shortcut-note">
              <RefreshCw size={14} aria-hidden="true" />
              <span>首次触发时读取 Codex 当前快捷键；修改 Codex 设置后会自动刷新。</span>
            </p>
          )}
          {values[selected] === "keyboardShortcut" && <ShortcutRecorder value={shortcuts[selected] ?? ""} onChange={(shortcut) => setShortcuts((current) => ({ ...current, [selected]: shortcut }))} />}
          <div className="mapping-preview"><span>触发结果</span><strong>{actionLabel}</strong></div>
          {conflict && <p className="inline-warning" role="alert"><AlertTriangle size={16}/>与 {conflict}冲突；实际使用时两个按键都会触发该操作。</p>}
          {saved && <p className="save-confirmation" role="status">映射已保存</p>}
        </section>
      </div>
    </main>
  );
}
