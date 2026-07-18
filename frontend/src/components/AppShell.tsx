import type { ReactNode } from "react";
import { Gamepad2, LayoutDashboard, SlidersHorizontal, Settings2 } from "lucide-react";

export type AppPage = "dashboard" | "mapping" | "settings";

interface AppShellProps {
  children: ReactNode;
  page: AppPage;
  onNavigate(page: AppPage): void;
}

export function AppShell({ children, page, onNavigate }: AppShellProps) {
  return (
    <div className="app-shell">
      <div className="app-frame">
        <header className="topbar">
          <div className="brand">
            <span className="brand-mark" aria-hidden="true"><Gamepad2 size={18} /></span>
            <span className="brand-copy">
              <strong>VibeController</strong>
              <span>Controllers for Codex</span>
            </span>
          </div>
          <nav className="main-nav" aria-label="主导航">
            <button type="button" data-active={page === "dashboard"} onClick={() => onNavigate("dashboard")}><LayoutDashboard size={15}/>状态</button>
            <button type="button" data-active={page === "mapping"} onClick={() => onNavigate("mapping")}><SlidersHorizontal size={15}/>映射</button>
            <button type="button" data-active={page === "settings"} onClick={() => onNavigate("settings")}><Settings2 size={15}/>设置</button>
          </nav>
          <div className="topbar-actions">
            <button className="quiet-button compact-only" type="button" onClick={() => onNavigate("settings")}>
              <Settings2 size={15} /> 设置
            </button>
          </div>
        </header>
        {children}
      </div>
    </div>
  );
}
