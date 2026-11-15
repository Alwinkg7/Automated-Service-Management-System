"use client";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Field, Input } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { customerApi } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";

export function PromotionsView() {
  const query = useQuery({
    queryKey: qk.customer.promotions(),
    queryFn: customerApi.getPromotions,
  });
  const [code, setCode] = useState("");
  const [amount, setAmount] = useState("500");
  const [result, setResult] = useState<string | null>(null);

  const check = useAction({
    fn: () => customerApi.validatePromo(code.trim(), Number(amount) || 0),
    onSuccess: (r) =>
      setResult(
        r.message ??
          `Valid! You save ${formatMoney(r.discount)} on a ${formatMoney(Number(amount))} bill.`,
      ),
    onError: () => setResult(null),
  });

  async function copyReferral(value: string) {
    try {
      await navigator.clipboard.writeText(value);
      toast.success("Referral code copied.");
    } catch {
      toast.error("Couldn't copy – select the code and copy it manually.");
    }
  }

  return (
    <>
      <PageHeader title="Rewards" description="Loyalty points, referrals and promo codes." />
      <QueryView query={query} loading={<SkeletonStats count={3} />}>
        {(p) => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard
                label="Points balance"
                value={p.pointsBalance}
                icon="gift"
                hint="Earn 1 point per ₹10 spent"
              />
              <StatCard
                label="Worth"
                value={formatMoney(p.pointsValue ?? Math.floor(p.pointsBalance / 100) * 10)}
                icon="wallet"
                hint="100 points = ₹10"
              />
              <div className="rounded-card border-border bg-surface col-span-2 border p-4">
                <p className="text-fg-muted text-sm">Your referral code</p>
                <div className="mt-2 flex items-center gap-3">
                  <code className="bg-surface-muted rounded-lg px-3 py-1.5 text-lg font-semibold tracking-wider">
                    {p.referralCode ?? "—"}
                  </code>
                  {p.referralCode ? (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void copyReferral(p.referralCode!)}
                    >
                      Copy
                    </Button>
                  ) : null}
                </div>
                <p className="text-fg-muted mt-2 text-xs">
                  Friends who sign up with your code earn you both bonus points.
                </p>
              </div>
            </StatGrid>

            <Card>
              <CardHeader
                title="Check a promo code"
                description="Promo codes are applied when you pay a bill."
              />
              <CardBody>
                <form
                  className="grid gap-3 sm:grid-cols-[1fr_10rem_auto] sm:items-end"
                  onSubmit={(e) => {
                    e.preventDefault();
                    if (code.trim()) check.mutate();
                  }}
                >
                  <Field label="Promo code">
                    {({ id }) => (
                      <Input id={id} value={code} onChange={(e) => setCode(e.target.value)} />
                    )}
                  </Field>
                  <Field label="Bill amount (₹)">
                    {({ id }) => (
                      <Input
                        id={id}
                        type="number"
                        min={0}
                        value={amount}
                        onChange={(e) => setAmount(e.target.value)}
                      />
                    )}
                  </Field>
                  <Button type="submit" loading={check.isPending} disabled={!code.trim()}>
                    Check
                  </Button>
                </form>
                {result ? (
                  <p role="status" className="text-success mt-3 text-sm">
                    {result}
                  </p>
                ) : null}
              </CardBody>
            </Card>

            <section>
              <h2 className="mb-3 font-semibold">Points history</h2>
              {p.history.length === 0 ? (
                <EmptyState
                  title="No points yet"
                  description="You'll earn points every time you pay for a completed service."
                />
              ) : (
                <DataTable
                  caption="Points history"
                  rows={p.history}
                  rowKey={(h) => `${h.transactedAt}-${h.points}-${h.requestId ?? ""}`}
                  columns={[
                    { header: "Date", cell: (h) => formatDate(h.transactedAt) },
                    {
                      header: "Type",
                      cell: (h) => (
                        <Badge tone={h.points >= 0 ? "success" : "warning"}>
                          {h.transactionType}
                        </Badge>
                      ),
                    },
                    {
                      header: "Points",
                      cell: (h) => (
                        <span className="font-medium tabular-nums">
                          {h.points > 0 ? `+${h.points}` : h.points}
                        </span>
                      ),
                    },
                    {
                      header: "Note",
                      cell: (h) => h.note ?? (h.requestId ? `Request #${h.requestId}` : "—"),
                      hideOnMobile: true,
                    },
                  ]}
                />
              )}
            </section>
          </div>
        )}
      </QueryView>
    </>
  );
}
