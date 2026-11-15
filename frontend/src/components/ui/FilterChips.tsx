"use client";
import { cn } from "@/lib/cn";

export interface ChipOption {
  value: string;
  label: string;
  count?: number;
}

/** A row of toggle buttons for single-choice filters (status, role...). */
export function FilterChips({
  options,
  value,
  onChange,
  label,
}: {
  options: ChipOption[];
  value: string;
  onChange: (value: string) => void;
  label: string;
}) {
  return (
    <div role="group" aria-label={label} className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
      {options.map((o) => {
        const active = o.value === value;
        return (
          <button
            key={o.value || "all"}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(o.value)}
            className={cn(
              "inline-flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm transition-colors",
              active
                ? "border-primary bg-primary text-primary-fg"
                : "border-border bg-surface text-fg hover:bg-surface-muted",
            )}
          >
            {o.label}
            {o.count !== undefined ? (
              <span className={cn("rounded-full px-1.5 text-xs tabular-nums", active ? "bg-white/20" : "bg-surface-muted")}>
                {o.count}
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
