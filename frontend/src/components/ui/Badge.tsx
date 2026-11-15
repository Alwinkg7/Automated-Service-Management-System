import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { humanize } from "@/types/enums";

export type Tone = "neutral" | "primary" | "success" | "warning" | "danger" | "info";

const tones: Record<Tone, string> = {
  neutral: "bg-surface-muted text-fg-muted",
  primary: "bg-primary-soft text-primary",
  success: "bg-success-soft text-success",
  warning: "bg-warning-soft text-warning",
  danger: "bg-danger-soft text-danger",
  info: "bg-info-soft text-info",
};

export function Badge({ tone = "neutral", children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={cn("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap", tones[tone], className)}>
      {children}
    </span>
  );
}

// One mapping for every status-like value in the app, so colors stay consistent.
const STATUS_TONES: Record<string, Tone> = {
  Pending: "warning",
  Assigned: "info",
  InProgress: "primary",
  Billed: "info",
  Completed: "success",
  Cancelled: "neutral",
  Available: "success",
  Busy: "warning",
  Offline: "neutral",
  Open: "warning",
  UnderReview: "info",
  Resolved: "success",
  Approved: "info",
  Paid: "success",
  Unpaid: "warning",
  Rejected: "danger",
  Refunded: "info",
  Active: "success",
  Inactive: "neutral",
};

export function StatusBadge({ status, className }: { status: string; className?: string }) {
  return (
    <Badge tone={STATUS_TONES[status] ?? "neutral"} className={className}>
      {humanize(status)}
    </Badge>
  );
}
