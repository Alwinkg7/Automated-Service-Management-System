"use client";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { BillSummary } from "@/components/features/bill/BillSummary";
import { InfoList } from "@/components/features/request/InfoList";
import { StatusStepper, StatusTimeline } from "@/components/features/request/StatusTimeline";
import { StatusBadge } from "@/components/ui/Badge";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { Textarea } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { Modal } from "@/components/ui/Modal";
import { QueryView } from "@/components/ui/QueryView";
import { StarRating } from "@/components/ui/StarRating";
import { useAction } from "@/hooks/useAction";
import { useDownload } from "@/hooks/useDownload";
import { customerApi } from "@/lib/api";
import { formatDate, formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO } from "@/types/enums";

export function RequestDetailView({ id }: { id: number }) {
  // Realtime events invalidate the customer cache, so this refreshes live.
  const query = useQuery({
    queryKey: qk.customer.request(id),
    queryFn: () => customerApi.getRequest(id),
  });
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [rateOpen, setRateOpen] = useState(false);
  const [stars, setStars] = useState(0);
  const [comment, setComment] = useState("");
  const { download, pending: downloading } = useDownload();

  const cancel = useAction({
    fn: () => customerApi.cancelRequest(id),
    invalidate: [qk.customer.all],
    success: "Request cancelled.",
    onSuccess: () => setConfirmCancel(false),
  });
  const rate = useAction({
    fn: () => customerApi.rateRequest(id, stars, comment || undefined),
    invalidate: [qk.customer.all],
    success: "Thanks for your feedback!",
    onSuccess: () => setRateOpen(false),
  });

  return (
    <QueryView query={query}>
      {(r) => {
        const canRate = r.status === "Completed" && !r.rating;
        const canRebook = r.canRebook ?? (r.status === "Completed" || r.status === "Cancelled");
        return (
          <>
            <PageHeader
              back={{ href: "/customer/requests", label: "My requests" }}
              title={
                <span className="flex flex-wrap items-center gap-3">
                  {CATEGORY_INFO[r.category].label} #{r.id}
                  <StatusBadge status={r.status} />
                </span>
              }
              description={`Created ${formatDate(r.createdAt, true)}`}
              actions={
                <>
                  {r.status === "InProgress" ? (
                    <ButtonLink
                      href={`/customer/requests/${r.id}/track`}
                      icon={<Icon name="map" className="size-4" />}
                    >
                      Track technician
                    </ButtonLink>
                  ) : null}
                  {r.status === "Completed" ? (
                    <ButtonLink
                      href={`/customer/disputes/new?requestId=${r.id}`}
                      variant="outline"
                      icon={<Icon name="flag" className="size-4" />}
                    >
                      Raise dispute
                    </ButtonLink>
                  ) : null}
                  {canRebook ? (
                    <ButtonLink href={`/customer/requests/new?rebook=${r.id}`} variant="outline">
                      Re-book
                    </ButtonLink>
                  ) : null}
                  {r.canCancel ? (
                    <Button variant="danger" onClick={() => setConfirmCancel(true)}>
                      Cancel request
                    </Button>
                  ) : null}
                </>
              }
            />

            <div className="grid gap-6 lg:grid-cols-3">
              <div className="space-y-6 lg:col-span-2">
                <Card>
                  <CardBody className="space-y-5">
                    <StatusStepper status={r.status} />
                    <InfoList
                      items={[
                        { label: "Scheduled for", value: formatDate(r.scheduledDate, true) },
                        {
                          label: "Address",
                          value: [r.address, r.pinCode].filter(Boolean).join(" – "),
                        },
                        { label: "Description", value: r.description },
                        {
                          label: "Your rating",
                          value: r.rating ? (
                            <StarRating value={r.rating} />
                          ) : canRate ? (
                            "Not rated yet"
                          ) : (
                            "—"
                          ),
                        },
                      ]}
                    />
                    {canRate ? (
                      <Button
                        variant="secondary"
                        onClick={() => setRateOpen(true)}
                        icon={<Icon name="star" className="size-4" />}
                      >
                        Rate this service
                      </Button>
                    ) : null}
                  </CardBody>
                </Card>

                <Card>
                  <CardHeader title="Timeline" />
                  <CardBody>
                    <StatusTimeline entries={r.statusHistory} />
                  </CardBody>
                </Card>
              </div>

              <div className="space-y-6">
                <Card>
                  <CardHeader title="Technician" />
                  <CardBody>
                    {r.technician ? (
                      <div className="space-y-2 text-sm">
                        <p className="font-medium">{r.technician.name}</p>
                        <p className="text-fg-muted">{formatRating(r.technician.rating)}</p>
                        {r.technician.phone ? (
                          <a
                            href={`tel:${r.technician.phone}`}
                            className="text-primary inline-flex items-center gap-1.5 hover:underline"
                          >
                            <Icon name="phone" className="size-4" />
                            {r.technician.phone}
                          </a>
                        ) : null}
                      </div>
                    ) : (
                      <p className="text-fg-muted text-sm">
                        We&apos;re finding a technician for you. You&apos;ll be notified as soon as
                        one is assigned.
                      </p>
                    )}
                  </CardBody>
                </Card>

                {r.bill ? (
                  <Card>
                    <CardHeader title="Bill" />
                    <CardBody className="space-y-4">
                      <BillSummary bill={r.bill} />
                      <div className="flex flex-col gap-2">
                        {!r.bill.isPaid && r.bill.id ? (
                          <ButtonLink
                            href={`/customer/requests/${r.id}/pay`}
                            size="lg"
                            icon={<Icon name="card" className="size-4" />}
                          >
                            Pay now
                          </ButtonLink>
                        ) : null}
                        {r.bill.id ? (
                          <Button
                            variant="outline"
                            loading={downloading}
                            icon={<Icon name="download" className="size-4" />}
                            onClick={() =>
                              void download(
                                customerApi.billPdfPath(r.bill!.id!),
                                `bill-${r.bill!.id}.pdf`,
                              )
                            }
                          >
                            Download invoice (PDF)
                          </Button>
                        ) : null}
                      </div>
                    </CardBody>
                  </Card>
                ) : null}
              </div>
            </div>

            <ConfirmDialog
              open={confirmCancel}
              title="Cancel this request?"
              description="The request will be cancelled and no technician will be sent."
              confirmLabel="Yes, cancel"
              tone="danger"
              loading={cancel.isPending}
              onConfirm={() => cancel.mutate()}
              onClose={() => setConfirmCancel(false)}
            />

            <Modal
              open={rateOpen}
              onClose={() => setRateOpen(false)}
              title="Rate this service"
              footer={
                <>
                  <Button variant="outline" onClick={() => setRateOpen(false)}>
                    Close
                  </Button>
                  <Button
                    disabled={stars === 0}
                    loading={rate.isPending}
                    onClick={() => rate.mutate()}
                  >
                    Submit rating
                  </Button>
                </>
              }
            >
              <div className="space-y-4">
                <StarRating value={stars} onChange={setStars} label="Your rating" />
                <label className="block space-y-1.5 text-sm">
                  <span className="font-medium">Comment (optional)</span>
                  <Textarea
                    value={comment}
                    maxLength={1000}
                    onChange={(e) => setComment(e.target.value)}
                  />
                </label>
              </div>
            </Modal>
          </>
        );
      }}
    </QueryView>
  );
}
