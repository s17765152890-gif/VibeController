export const actionOptions = [
  ["dictation", "切换听写快捷键"],
  ["send", "发送消息"],
  ["cancel", "Esc（取消 / 返回）"],
  ["commandPalette", "打开命令菜单"],
  ["previousChat", "上一个任务"],
  ["nextChat", "下一项任务"],
  ["previousRecentThread", "上一个最近查看的任务"],
  ["nextRecentThread", "下一个最近查看的任务"],
  ["previousTab", "上一个标签页"],
  ["nextTab", "下一个标签页"],
  ["increaseReasoning", "提高推理强度"],
  ["decreaseReasoning", "降低推理强度"],
  ["activateCodex", "激活 Codex 窗口"],
  ["keyboardShortcut", "键盘快捷键"],
  ["mouseMove", "鼠标：移动"],
  ["mouseLeftClick", "鼠标：左键单击"],
  ["mouseRightClick", "鼠标：右键单击"],
  ["mouseScrollUp", "鼠标：滚轮向上"],
  ["mouseScrollDown", "鼠标：滚轮向下"],
  ["none", "不执行操作"],
] as const;

interface ActionPickerProps {
  value: string;
  onChange(value: string): void;
}

export function ActionPicker({ value, onChange }: ActionPickerProps) {
  return (
    <label className="field-stack">
      <span>绑定操作</span>
      <select aria-label="绑定操作" value={value} onChange={(event) => onChange(event.target.value)}>
        {actionOptions.map(([optionValue, label]) => (
          <option key={optionValue} value={optionValue}>{label}</option>
        ))}
      </select>
    </label>
  );
}
