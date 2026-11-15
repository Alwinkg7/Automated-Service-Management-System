import Link from "next/link";
import { StatusBadge } from "@/components/ui/Badge";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import type { RequestSummary } from "@/lib/api/schemas";
import { formatDate, formatMoney } from "@/lib/format";
import { CATEGORY_INFO } from "@/types/enums";

/** One request in a customer list. Whole card is a link to the detail page. */
export function RequestCard({ request }: { request: RequestSummary }) {
  return (
    <Link
      href={`/customer/requests/${request.id}`}
      className="flex gap-4 rounded-card border border-border bg-surface p-4 transition-colors hover:border-primary/40"
    >
      <CategoryIcon category={request.category} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="font-medium">
            {CATEGORY_INFO[request.category].label} <span className="text-fg-muted">#{request.id}</span>
          </p>
          <StatusBadge status={request.status} />
          {request.hasBill && !request.billPaid ? <StatusBadge status="Unpaid" /> : null}
        </div>
        {request.description ? <p className="mt-1 line-clamp-1 text-sm text-fg-muted">{request.description}</p> : null}
        <p className="mt-1 text-xs text-fg-muted">
          Scheduled {formatDate(request.scheduledDate)}
          {request.technicianName ? ` · ${request.technicianName}` : ""}
        </p>
      </div>
      {request.billAmount ? (
        <p className="hidden self-center text-sm font-medium tabular-nums sm:block">{formatMoney(request.billAmount)}</p>
      ) : null}
    </Link>
  );
}
