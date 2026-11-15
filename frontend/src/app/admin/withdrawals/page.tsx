"use client";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { DataTable } from "@/components/ui/DataTable";
import { Textarea } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import type { Withdrawal } from "@/lib/api/schemas";
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";

export default function WithdrawalsPage() {
  const query = useQuery({
    queryKey: qk.admin.withdrawals(),
    queryFn: () => adminApi.getWithdrawals(),
  });
  const [rejecting, setRejecting] = useState<Withdrawal | null>(null);
  const [note, setNote] = useState("");
  const invalidate = [qk.admin.withdrawals()];
  const approve = useAction({
    fn: (id: number) => adminApi.approveWithdrawal(id),
    invalidate,
    success: "Withdrawal approved.",
  });
  const pay = useAction({
    fn: (id: number) => adminApi.payWithdrawal(id),
    invalidate,
    success: "Marked as paid.",
  });
  const reject = useAction({
    fn: (id: number) => adminApi.rejectWithdrawal(id, note),
    invalidate,
    success: "Withdrawal rejected.",
    onSuccess: () => {
      setRejecting(null);
      setNote("");
    },
  });

  return (
    <>
      <PageHeader
        title="Withdrawals"
        description="Technician payout requests. Pay to the UPI ID shown, then mark as paid."
      />
      <QueryView
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState title="No withdrawal requests" />}
      >
        {(rows) => (
          <DataTable
            caption="Withdrawals"
            rows={rows}
            rowKey={(w) => w.id}
            columns={[
              { header: "Technician", cell: (w) => w.technicianName ?? "—" },
              {
                header: "Amount",
                cell: (w) => <span className="font-medium">{formatMoney(w.amount)}</span>,
                className: "tabular-nums",
              },
              { header: "UPI ID", cell: (w) => w.upiId },
              { header: "Requested", cell: (w) => formatDate(w.requestedAt), hideOnMobile: true },
              { header: "Status", cell: (w) => <StatusBadge status={w.status} /> },
            ]}
            rowAction={(w) => (
              <>
                {w.status === "Pending" ? (
                  <Button
                    size="sm"
                    variant="secondary"
                    loading={approve.isPending && approve.variables === w.id}
                    onClick={() => approve.mutate(w.id)}
                  >
                    Approve
                  </Button>
                ) : null}
                {w.status === "Approved" ? (
                  <Button
                    size="sm"
                    loading={pay.isPending && pay.variables === w.id}
                    onClick={() => pay.mutate(w.id)}
                  >
                    Mark paid
                  </Button>
                ) : null}
                {w.status === "Pending" || w.status === "Approved" ? (
                  <Button size="sm" variant="ghost" onClick={() => setRejecting(w)}>
                    Reject
                  </Button>
                ) : null}
              </>
            )}
          />
        )}
      </QueryView>
      <ConfirmDialog
        open={!!rejecting}
        title="Reject withdrawal?"
        description={
          rejecting ? `${formatMoney(rejecting.amount)} to ${rejecting.upiId}` : undefined
        }
        confirmLabel="Reject"
        tone="danger"
        loading={reject.isPending}
        onConfirm={() => rejecting && note.trim() && reject.mutate(rejecting.id)}
        onClose={() => setRejecting(null)}
      >
        <label className="mt-3 block space-y-1.5 text-sm">
          <span className="font-medium">Reason (shown to the technician)</span>
          <Textarea value={note} onChange={(e) => setNote(e.target.value)} required />
        </label>
      </ConfirmDialog>
    </>
  );
}
