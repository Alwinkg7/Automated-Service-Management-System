"use client";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

export function SearchInput({
  value,
  onChange,
  placeholder = "Search…",
  label = "Search",
  className,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  label?: string;
  className?: string;
}) {
  return (
    <label className={cn("relative block", className)}>
      <span className="sr-only">{label}</span>
      <Icon name="search" className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-fg-muted" />
      <input
        type="search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="h-10 w-full rounded-lg border border-border bg-surface pr-3 pl-9 text-sm placeholder:text-fg-muted/70 focus:border-primary focus:outline-2 focus:outline-ring/40"
      />
    </label>
  );
}
