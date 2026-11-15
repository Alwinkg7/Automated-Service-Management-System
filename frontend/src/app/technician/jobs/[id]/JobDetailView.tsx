"use client";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { BillSummary } from "@/components/features/bill/BillSummary";
import { InfoList } from "@/components/features/request/InfoList";
import { StatusStepper } from "@/components/features/request/StatusTimeline";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { Icon } from "@/components/ui/Icon";
import { QueryView } from "@/components/ui/QueryView";
import { useAction } from "@/hooks/useAction";
import { technicianApi } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO } from "@/types/enums";
import { BillForm } from "./BillForm";
import { LocationSharingCard } from "./LocationSharingCard";

export function JobDetailView({ id }: { id: number }) {
  const router = useRouter();
  const query = useQuery({
    queryKey: qk.technician.job(id),
    queryFn: () => technicianApi.getJob(id),
  });
  const [confirmReject, setConfirmReject] = useState(false);
  const [confirmCash, setConfirmCash] = useState(false);

  const accept = useAction({
    fn: () => technicianApi.acceptJob(id),
    invalidate: [qk.technician.all],
    success:
      "Job accepted. You're now marked as Busy – share your location so the customer can track you.",
  });
  const reject = useAction({
    fn: () => technicianApi.rejectJob(id),
    invalidate: [qk.technician.all],
    success: "Job declined. It will be reassigned.",
    onSuccess: () => router.push("/technician/jobs"),
  });
  const cash = useAction({
    fn: () => technicianApi.confirmCashCollected(id),
    invalidate: [qk.technician.all],
    success: "Cash payment recorded. Job completed.",
    onSuccess: () => setConfirmCash(false),
  });

  return (
    <QueryView query={query}>
      {(j) => (
        <>
          <PageHeader
            back={{ href: "/technician/jobs", label: "My jobs" }}
            title={
              <span className="flex flex-wrap items-center gap-3">
                {CATEGORY_INFO[j.category].label} #{j.id}
                <StatusBadge status={j.status} />
              </span>
            }
            description={`Scheduled for ${formatDate(j.scheduledDate, true)}`}
            actions={
              j.canAccept ? (
                <>
                  <Button variant="outline" onClick={() => setConfirmReject(true)}>
                    Decline
                  </Button>
                  <Button
                    loading={accept.isPending}
                    onClick={() => accept.mutate()}
                    icon={<Icon name="check" className="size-4" />}
                  >
                    Accept job
                  </Button>
                </>
              ) : null
            }
          />
          <div className="grid gap-6 lg:grid-cols-3">
            <div className="space-y-6 lg:col-span-2">
              <Card>
                <CardBody className="space-y-5">
                  <StatusStepper status={j.status} />
                  <InfoList
                    items={[
                      {
                        label: "Address",
                        value: [j.address, j.pinCode].filter(Boolean).join(" – "),
                      },
                      { label: "Created", value: formatDate(j.createdAt) },
                      { label: "Problem", value: j.description },
                    ]}
                  />
                </CardBody>
              </Card>
              {j.status === "InProgress" ? <LocationSharingCard requestId={j.id} /> : null}
              {j.canBill ? <BillForm jobId={j.id} /> : null}
              {j.status === "Billed" ? (
                <Card>
                  <CardBody className="flex flex-wrap items-center gap-3 text-sm">
                    <Icon name="card" className="text-info" />
                    <span className="min-w-0 flex-1">
                      Bill sent. The job completes when the customer pays online, or when you
                      confirm you collected cash.
                    </span>
                    {j.canComplete ? (
                      <Button variant="outline" onClick={() => setConfirmCash(true)}>
                        Confirm cash collected
                      </Button>
                    ) : null}
                  </CardBody>
                </Card>
              ) : null}
            </div>
            <div className="space-y-6">
              <Card>
                <CardHeader title="Customer" />
                <CardBody className="space-y-2 text-sm">
                  <p className="font-medium">{j.customer.name ?? "—"}</p>
                  {j.customer.phone ? (
                    <a
                      href={`tel:${j.customer.phone}`}
                      className="text-primary flex items-center gap-1.5 hover:underline"
                    >
                      <Icon name="phone" className="size-4" /> {j.customer.phone}
                    </a>
                  ) : null}
                  {j.address ? (
                    <a
                      href={`https://www.openstreetmap.org/search?query=${encodeURIComponent(j.address)}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-primary flex items-center gap-1.5 hover:underline"
                    >
                      <Icon name="pin" className="size-4" /> Open in map
                    </a>
                  ) : null}
                </CardBody>
              </Card>
              {j.bill ? (
                <Card>
                  <CardHeader title="Bill" />
                  <CardBody>
                    <BillSummary bill={j.bill} />
                  </CardBody>
                </Card>
              ) : null}
            </div>
          </div>
          <ConfirmDialog
            open={confirmReject}
            title="Decline this job?"
            description="The job goes back to the queue and will be assigned to another technician."
            confirmLabel="Decline job"
            tone="danger"
            loading={reject.isPending}
            onConfirm={() => reject.mutate()}
            onClose={() => setConfirmReject(false)}
          />
          <ConfirmDialog
            open={confirmCash}
            title="Confirm cash collected?"
            description={`Only confirm if the customer has paid you ${j.bill ? formatMoney(j.bill.amount) : "the bill amount"} in cash. This completes the job.`}
            confirmLabel="Yes, cash received"
            loading={cash.isPending}
            onConfirm={() => cash.mutate()}
            onClose={() => setConfirmCash(false)}
          />
        </>
      )}
    </QueryView>
  );
}
