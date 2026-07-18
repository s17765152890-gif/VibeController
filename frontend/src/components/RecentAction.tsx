import { AlertCircle, Mic2 } from "lucide-react";

interface RecentActionProps {
  action: string | null;
}

export function RecentAction({ action }: RecentActionProps) {
  const failed = action?.includes("未发送") || action?.includes("未处于前台") || action?.includes("失败");
  return (
    <div className="last-action" data-failed={failed}>
      <span className="action-icon" aria-hidden="true">{failed ? <AlertCircle size={18}/> : <Mic2 size={18}/>}</span>
      <div>
        <strong>{action ?? "等待手柄输入"}</strong>
        <p>{action ? (failed ? "操作已被安全拦截，请检查 Codex 状态" : "指令已由 VibeController 派发") : "按下任意已映射按键开始"}</p>
      </div>
    </div>
  );
}
