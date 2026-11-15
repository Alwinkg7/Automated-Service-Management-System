import { StatusBadge } from "@/components/ui/Badge";
import type { Bill } from "@/lib/api/schemas";
import { formatDate, formatMoney } from "@/lib/format";

/** Read-only bill breakdown shared by customer, technician and admin views. */
export function BillSummary({ bill }: { bill: Partial<Bill> & { amount: number } }) {
  return (
    <div className="space-y-3 text-sm">
      <div className="flex items-center justify-between">
        <span className="text-fg-muted">Status</span>
        <StatusBadge status={bill.isPaid ? "Paid" : "Unpaid"} />
      </div>
      {bill.items?.length ? (
        <ul className="divide-border border-border divide-y rounded-lg border">
          {bill.items.map((item, i) => (
            <li key={i} className="flex justify-between gap-3 px-3 py-2">
              <span>
                {item.description} <span className="text-fg-muted">× {item.quantity}</span>
              </span>
              <span className="tabular-nums">
                {formatMoney(item.amount ?? item.quantity * item.unitPrice)}
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <dl className="space-y-1.5">
          {bill.laborCost !== undefined && bill.laborCost !== null ? (
            <div className="flex justify-between">
              <dt className="text-fg-muted">Labour</dt>
              <dd className="tabular-nums">{formatMoney(bill.laborCost)}</dd>
            </div>
          ) : null}
          {bill.materialCost !== undefined && bill.materialCost !== null ? (
            <div className="flex justify-between">
              <dt className="text-fg-muted">Materials</dt>
              <dd className="tabular-nums">{formatMoney(bill.materialCost)}</dd>
            </div>
          ) : null}
        </dl>
      )}
      {bill.description ? <p className="text-fg-muted">{bill.description}</p> : null}
      <div className="border-border flex justify-between border-t pt-3 text-base font-semibold">
        <span>Total</span>
        <span className="tabular-nums">{formatMoney(bill.amount)}</span>
      </div>
      {bill.createdAt ? (
        <p className="text-fg-muted text-xs">Billed on {formatDate(bill.createdAt)}</p>
      ) : null}
    </div>
  );
}
