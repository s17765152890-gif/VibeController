import type { ReactNode } from "react";

type Tone = "neutral" | "success" | "warning" | "danger" | "info";

interface StatusPillProps {
  children: ReactNode;
  tone?: Tone;
}

export function StatusPill({ children, tone = "neutral" }: StatusPillProps) {
  return (
    <span className="status-pill" data-tone={tone}>
      <span className="status-dot" aria-hidden="true" />
      {children}
    </span>
  );
}
