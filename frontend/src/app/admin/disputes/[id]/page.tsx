"use client";
import { useQuery } from "@tanstack/react-query";
import { use, useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { DisputeDetailCard } from "@/components/features/dispute/DisputeDetailCard";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { Field, Input, Select, Textarea } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { DISPUTE_RESOLUTIONS, humanize } from "@/types/enums";

export default function AdminDisputePage({ params }: { params: Promise<{ id: string }> }) {
  const id = Number(use(params).id);
  const query = useQuery({
    queryKey: qk.admin.dispute(id),
    queryFn: () => adminApi.getDispute(id),
    enabled: id > 0,
  });
  const [note, setNote] = useState("");
  const [resolution, setResolution] = useState<string>("Rejected");
  const [refund, setRefund] = useState("");
  const invalidate = [qk.admin.dispute(id), qk.admin.disputes("")];

  const review = useAction({
    fn: () => adminApi.startDisputeReview(id, note),
    invalidate,
    success: "Dispute is now under review.",
  });
  const resolve = useAction({
    fn: () => adminApi.resolveDispute(id, resolution, note),
    invalidate,
    success: "Dispute resolved.",
  });
  const issueRefund = useAction({
    fn: () => adminApi.refundDispute(id, Number(refund), note),
    invalidate,
    success: (r) => `Refund issued${r?.razorpayRefundId ? ` (ref ${r.razorpayRefundId})` : ""}.`,
  });

  return (
    <>
      <PageHeader back={{ href: "/admin/disputes", label: "Disputes" }} title={`Dispute #${id}`} />
      <QueryView query={query}>
        {(d) => (
          <div className="grid gap-6 lg:grid-cols-3">
            <div className="lg:col-span-2">
              <DisputeDetailCard dispute={d} requestHref={`/admin/requests/${d.requestId}`} />
            </div>
            {d.status !== "Resolved" ? (
              <Card>
                <CardHeader title="Take action" />
                <CardBody className="space-y-4">
                  <Field label="Admin note" hint="Visible to the customer.">
                    {({ id: fid }) => (
                      <Textarea
                        id={fid}
                        rows={3}
                        value={note}
                        onChange={(e) => setNote(e.target.value)}
                      />
                    )}
                  </Field>
                  {d.status === "Open" ? (
                    <Button
                      variant="secondary"
                      className="w-full"
                      loading={review.isPending}
                      onClick={() => review.mutate()}
                    >
                      Start review
                    </Button>
                  ) : null}
                  <div className="border-border space-y-2 border-t pt-4">
                    <Field
                      label="Refund amount (₹)"
                      hint={d.billAmount ? `Bill total ${formatMoney(d.billAmount)}` : undefined}
                    >
                      {({ id: fid }) => (
                        <Input
                          id={fid}
                          type="number"
                          min={1}
                          max={d.billAmount ?? undefined}
                          step="0.01"
                          value={refund}
                          onChange={(e) => setRefund(e.target.value)}
                        />
                      )}
                    </Field>
                    <Button
                      variant="danger"
                      className="w-full"
                      disabled={
                        !(Number(refund) > 0) || (!!d.billAmount && Number(refund) > d.billAmount)
                      }
                      loading={issueRefund.isPending}
                      onClick={() => issueRefund.mutate()}
                    >
                      Issue refund via Razorpay
                    </Button>
                  </div>
                  <div className="border-border space-y-2 border-t pt-4">
                    <Field label="Resolution">
                      {({ id: fid }) => (
                        <Select
                          id={fid}
                          value={resolution}
                          onChange={(e) => setResolution(e.target.value)}
                        >
                          {DISPUTE_RESOLUTIONS.filter((r) => r !== "Refunded").map((r) => (
                            <option key={r} value={r}>
                              {humanize(r)}
                            </option>
                          ))}
                        </Select>
                      )}
                    </Field>
                    <Button
                      variant="outline"
                      className="w-full"
                      loading={resolve.isPending}
                      onClick={() => resolve.mutate()}
                    >
                      Resolve without refund
                    </Button>
                  </div>
                </CardBody>
              </Card>
            ) : null}
          </div>
        )}
      </QueryView>
    </>
  );
}
