import type { ControllerType } from "./types";

export interface ControllerControlPresentation {
  key: string;
  label: string;
  keycap: string;
}

export interface DefaultActionPresentation {
  action: string;
  keycap: string;
}

export interface ControllerPresentation {
  name: string;
  statusName: string;
  connectedLabel: string;
  waitingName: string;
  subtitle: string;
  controls: readonly ControllerControlPresentation[];
  defaultActions: readonly DefaultActionPresentation[];
}

const commonDirectionalControls: readonly ControllerControlPresentation[] = [
  { key: "dPadUp", label: "方向键上", keycap: "D↑" },
  { key: "dPadDown", label: "方向键下", keycap: "D↓" },
  { key: "dPadLeft", label: "方向键左", keycap: "D←" },
  { key: "dPadRight", label: "方向键右", keycap: "D→" },
  { key: "leftStickX", label: "左摇杆横轴", keycap: "LS↔" },
  { key: "leftStickY", label: "左摇杆纵轴", keycap: "LS↕" },
  { key: "rightStickLeft", label: "右摇杆左", keycap: "RS←" },
  { key: "rightStickRight", label: "右摇杆右", keycap: "RS→" },
  { key: "rightStickUp", label: "右摇杆上", keycap: "RS↑" },
  { key: "rightStickDown", label: "右摇杆下", keycap: "RS↓" },
];

const xbox: ControllerPresentation = {
  name: "Xbox Wireless Controller",
  statusName: "Xbox 手柄",
  connectedLabel: "Xbox 手柄已连接",
  waitingName: "Xbox 手柄",
  subtitle: "用 Xbox 手柄控制 Codex。按 X 听写，按 A 发送，保持思路不断线。",
  controls: [
    { key: "x", label: "X 按键", keycap: "X" },
    { key: "a", label: "A 按键", keycap: "A" },
    { key: "b", label: "B 按键", keycap: "B" },
    { key: "y", label: "Y 按键", keycap: "Y" },
    { key: "menu", label: "菜单键", keycap: "☰" },
    { key: "leftShoulder", label: "LB", keycap: "LB" },
    { key: "rightShoulder", label: "RB", keycap: "RB" },
    { key: "leftTrigger", label: "LT", keycap: "LT" },
    { key: "rightTrigger", label: "RT", keycap: "RT" },
    ...commonDirectionalControls,
    { key: "view", label: "View", keycap: "View" },
    { key: "leftStickButton", label: "L3", keycap: "L3" },
    { key: "rightStickButton", label: "R3", keycap: "R3" },
  ],
  defaultActions: [
    { action: "切换听写快捷键", keycap: "X" },
    { action: "发送消息", keycap: "A" },
    { action: "删除上一个字符（Backspace）", keycap: "B" },
    { action: "打开命令菜单", keycap: "Y" },
    { action: "上一个任务", keycap: "LB" },
    { action: "下一项任务", keycap: "RB" },
    { action: "降低 / 提高推理强度", keycap: "方向键 ← →" },
    { action: "移动输入光标", keycap: "右摇杆 ↑ ↓ ← →" },
  ],
};

const playStation5: ControllerPresentation = {
  name: "PlayStation 5 DualSense",
  statusName: "PS5 DualSense",
  connectedLabel: "PS5 DualSense 已连接",
  waitingName: "PS5 DualSense",
  subtitle: "用 PS5 DualSense 控制 Codex。按方块听写，按叉发送，触控板直接移动鼠标。",
  controls: [
    { key: "x", label: "□ 方块键", keycap: "□" },
    { key: "a", label: "× 叉键", keycap: "×" },
    { key: "b", label: "○ 圆圈键", keycap: "○" },
    { key: "y", label: "△ 三角键", keycap: "△" },
    { key: "menu", label: "Options", keycap: "Options" },
    { key: "leftShoulder", label: "L1", keycap: "L1" },
    { key: "rightShoulder", label: "R1", keycap: "R1" },
    { key: "leftTrigger", label: "L2", keycap: "L2" },
    { key: "rightTrigger", label: "R2", keycap: "R2" },
    ...commonDirectionalControls,
    { key: "view", label: "Create", keycap: "Create" },
    { key: "leftStickButton", label: "L3", keycap: "L3" },
    { key: "rightStickButton", label: "R3", keycap: "R3" },
    { key: "touchpadX", label: "触控板横向滑动", keycap: "触控 ↔" },
    { key: "touchpadY", label: "触控板纵向滑动", keycap: "触控 ↕" },
    { key: "touchpadButton", label: "触控板按下", keycap: "触控按下" },
  ],
  defaultActions: [
    { action: "切换听写快捷键", keycap: "□" },
    { action: "发送消息", keycap: "×" },
    { action: "删除上一个字符（Backspace）", keycap: "○" },
    { action: "打开命令菜单", keycap: "△" },
    { action: "上一个任务", keycap: "L1" },
    { action: "下一项任务", keycap: "R1" },
    { action: "降低 / 提高推理强度", keycap: "方向键 ← →" },
    { action: "移动输入光标", keycap: "右摇杆 ↑ ↓ ← →" },
    { action: "触控板滑动", keycap: "移动鼠标光标" },
    { action: "触控板按下", keycap: "鼠标左键单击" },
  ],
};

export function getControllerPresentation(
  controllerType: ControllerType = "xbox",
): ControllerPresentation {
  return controllerType === "playStation5" ? playStation5 : xbox;
}
