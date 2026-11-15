import { StatusBadge } from "@/components/ui/Badge";
import type { StatusHistoryEntry } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";
import { formatDate } from "@/lib/format";
import { REQUEST_STATUSES, type RequestStatus } from "@/types/enums";

const FLOW: RequestStatus[] = ["Pending", "Assigned", "InProgress", "Billed", "Completed"];

/** Progress stepper for the request lifecycle. */
export function StatusStepper({ status }: { status: RequestStatus }) {
  if (status === "Cancelled") {
    return <p className="text-sm text-fg-muted">This request was cancelled.</p>;
  }
  const current = FLOW.indexOf(status);
  return (
    <ol className="grid grid-cols-5 gap-1" aria-label="Request progress">
      {FLOW.map((step, i) => (
        <li key={step} className="space-y-1.5" aria-current={i === current ? "step" : undefined}>
          <span className={cn("block h-1.5 rounded-full", i <= current ? "bg-primary" : "bg-surface-muted")} />
          <span className={cn("block text-[11px] sm:text-xs", i === current ? "font-medium text-fg" : "text-fg-muted")}>
            {step === "InProgress" ? "In progress" : step}
          </span>
        </li>
      ))}
    </ol>
  );
}

export function StatusTimeline({ entries }: { entries: StatusHistoryEntry[] }) {
  if (!entries.length) return <p className="text-sm text-fg-muted">No history yet.</p>;
  return (
    <ol className="relative space-y-4 border-l border-border pl-5">
      {entries.map((e, i) => (
        <li key={`${e.status}-${i}`} className="relative">
          <span
            aria-hidden
            className={cn(
              "absolute top-1.5 -left-[25px] size-2.5 rounded-full ring-4 ring-surface",
              REQUEST_STATUSES.indexOf(e.status) >= 4 ? "bg-success" : "bg-primary",
            )}
          />
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={e.status} />
            <time className="text-xs text-fg-muted">{formatDate(e.changedAt, true)}</time>
          </div>
          {e.note ? <p className="mt-1 text-sm text-fg-muted">{e.note}</p> : null}
        </li>
      ))}
    </ol>
  );
}
