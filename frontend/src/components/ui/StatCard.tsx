import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon, type IconName } from "./Icon";

export function StatCard({
  label,
  value,
  icon,
  hint,
  className,
}: {
  label: string;
  value: ReactNode;
  icon?: IconName;
  hint?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("rounded-card border-border bg-surface border p-4", className)}>
      <div className="flex items-center justify-between gap-2">
        <p className="text-fg-muted text-sm">{label}</p>
        {icon ? (
          <span className="bg-primary-soft text-primary rounded-lg p-1.5">
            <Icon name={icon} className="size-4" />
          </span>
        ) : null}
      </div>
      <p className="mt-2 text-2xl font-semibold tabular-nums">{value}</p>
      {hint ? <p className="text-fg-muted mt-1 text-xs">{hint}</p> : null}
    </div>
  );
}

export function StatGrid({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">{children}</div>;
}
