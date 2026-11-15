"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { PageHeader } from "@/components/layout/PageHeader";
import { LineSeriesChart } from "@/components/features/charts";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Field, Input } from "@/components/ui/Field";
import { Modal } from "@/components/ui/Modal";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { technicianApi } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, type ServiceCategory } from "@/types/enums";

function WithdrawModal({ open, onClose, available }: { open: boolean; onClose: () => void; available: number }) {
  const schema = z.object({
    amount: z.number("Enter an amount").positive("Enter an amount greater than zero.").max(available, `Maximum available: ${formatMoney(available)}`),
    upiId: z.string().trim().regex(/^[\w.-]{2,}@[a-zA-Z]{2,}$/, "Enter a valid UPI ID (e.g. name@bank)"),
  });
  type Values = z.infer<typeof schema>;
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Values>({ resolver: zodResolver(schema) });
  const withdraw = useAction({
    fn: (v: Values) => technicianApi.requestWithdrawal(Math.round(v.amount * 100) / 100, v.upiId),
    invalidate: [qk.technician.wallet()],
    success: "Withdrawal requested. Admin will process it within 24 hours.",
    onSuccess: () => {
      reset();
      onClose();
    },
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Withdraw earnings"
      description={`Available: ${formatMoney(available)}`}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="withdraw-form" loading={withdraw.isPending}>
            Request withdrawal
          </Button>
        </>
      }
    >
      <form id="withdraw-form" onSubmit={handleSubmit((v) => withdraw.mutate(v))} noValidate className="space-y-4">
        <Field label="Amount (₹)" error={errors.amount?.message} required>
          {({ id, describedBy, invalid }) => (
            <Input id={id} type="number" step="0.01" min={1} max={available} aria-describedby={describedBy} aria-invalid={invalid} {...register("amount", { valueAsNumber: true })} />
          )}
        </Field>
        <Field label="UPI ID" error={errors.upiId?.message} required>
          {({ id, describedBy, invalid }) => (
            <Input id={id} placeholder="name@bank" aria-describedby={describedBy} aria-invalid={invalid} {...register("upiId")} />
          )}
        </Field>
      </form>
    </Modal>
  );
}

export function WalletView() {
  const query = useQuery({ queryKey: qk.technician.wallet(), queryFn: technicianApi.getWallet });
  const [open, setOpen] = useState(false);

  return (
    <QueryView query={query} loading={<SkeletonStats />} pendingEndpoint="GET /api/technician/wallet">
      {(w) => (
        <>
          <PageHeader
            title="Earnings"
            description={`Net of ${Math.round(w.commissionRate * 100)}% platform commission.`}
            actions={
              <Button onClick={() => setOpen(true)} disabled={w.availableToWithdraw <= 0}>
                Withdraw
              </Button>
            }
          />
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Available to withdraw" value={formatMoney(w.availableToWithdraw)} icon="wallet" hint={w.pendingWithdrawal ? `${formatMoney(w.pendingWithdrawal)} pending` : undefined} />
              <StatCard label="This week" value={formatMoney(w.netEarningsThisWeek)} icon="chart" />
              <StatCard label="This month" value={formatMoney(w.netEarningsThisMonth)} icon="chart" />
              <StatCard label="All time (net)" value={formatMoney(w.netEarningsAllTime)} icon="check" hint={`${w.totalJobsCompleted} jobs · avg ${formatMoney(w.averagePerJob)}`} />
            </StatGrid>

            <Card>
              <CardHeader title="Last 6 months" />
              <CardBody>
                {w.monthly.length ? (
                  <LineSeriesChart
                    data={w.monthly}
                    xKey="label"
                    series={[
                      { key: "gross", label: "Gross" },
                      { key: "net", label: "Net" },
                    ]}
                    format={(v) => `₹${Math.round(v).toLocaleString("en-IN")}`}
                  />
                ) : (
                  <p className="text-sm text-fg-muted">No earnings yet.</p>
                )}
              </CardBody>
            </Card>

            <section>
              <h2 className="mb-3 font-semibold">Payouts</h2>
              {w.payouts.length === 0 ? (
                <EmptyState title="No paid jobs yet" description="Earnings appear here once customers pay their bills." />
              ) : (
                <DataTable
                  caption="Payouts"
                  rows={w.payouts}
                  rowKey={(p) => p.billId}
                  columns={[
                    { header: "Job", cell: (p) => `#${p.requestId}` },
                    { header: "Service", cell: (p) => CATEGORY_INFO[p.category as ServiceCategory]?.label ?? p.category },
                    { header: "Customer", cell: (p) => p.customerName ?? "—", hideOnMobile: true },
                    { header: "Gross", cell: (p) => formatMoney(p.grossAmount), className: "tabular-nums" },
                    { header: "Net", cell: (p) => <span className="font-medium">{formatMoney(p.netAmount)}</span>, className: "tabular-nums" },
                    { header: "Paid", cell: (p) => formatDate(p.paidAt) },
                  ]}
                />
              )}
            </section>

            <section>
              <h2 className="mb-3 font-semibold">Withdrawals</h2>
              {w.withdrawals.length === 0 ? (
                <p className="text-sm text-fg-muted">No withdrawal requests yet.</p>
              ) : (
                <DataTable
                  caption="Withdrawals"
                  rows={w.withdrawals}
                  rowKey={(x) => x.id}
                  columns={[
                    { header: "Requested", cell: (x) => formatDate(x.requestedAt) },
                    { header: "Amount", cell: (x) => formatMoney(x.amount), className: "tabular-nums" },
                    { header: "UPI", cell: (x) => x.upiId, hideOnMobile: true },
                    { header: "Status", cell: (x) => <StatusBadge status={x.status} /> },
                    { header: "Note", cell: (x) => x.adminNote ?? "—", hideOnMobile: true },
                  ]}
                />
              )}
            </section>
          </div>
          <WithdrawModal open={open} onClose={() => setOpen(false)} available={w.availableToWithdraw} />
        </>
      )}
    </QueryView>
  );
}
