interface ShortcutRecorderProps {
  value: string;
  onChange(value: string): void;
}

export function ShortcutRecorder({ value, onChange }: ShortcutRecorderProps) {
  const record = (event: React.KeyboardEvent<HTMLInputElement>) => {
    event.preventDefault();
    if (["Control", "Shift", "Alt", "Meta"].includes(event.key)) return;
    const parts = [
      event.ctrlKey && "Ctrl",
      event.shiftKey && "Shift",
      event.altKey && "Alt",
      event.metaKey && "Win",
      event.key.length === 1 ? event.key.toUpperCase() : event.key,
    ].filter(Boolean);
    onChange(parts.join("+"));
  };

  return (
    <label className="field-stack">
      <span>自定义快捷键</span>
      <input
        aria-label="自定义快捷键"
        value={value}
        placeholder="例如 Ctrl+Alt+K"
        readOnly
        onKeyDown={record}
      />
      <small>按 Windows 常用写法输入；保存后由桌面端执行。</small>
    </label>
  );
}
