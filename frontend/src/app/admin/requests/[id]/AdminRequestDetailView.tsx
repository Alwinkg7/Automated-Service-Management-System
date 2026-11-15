"use client";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { BillSummary } from "@/components/features/bill/BillSummary";
import { InfoList } from "@/components/features/request/InfoList";
import { StatusStepper, StatusTimeline } from "@/components/features/request/StatusTimeline";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import type { AdminRequestDetail } from "@/lib/api/schemas";
import { cn } from "@/lib/cn";
import { formatDate, formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO } from "@/types/enums";

function AssignPanel({ request }: { request: AdminRequestDetail }) {
  const [selected, setSelected] = useState<number | null>(null);
  const techs = useQuery({
    queryKey: qk.admin.available(request.category),
    queryFn: () => adminApi.getAvailableTechnicians(request.category),
    staleTime: 10_000,
  });
  const assign = useAction({
    fn: (techId: number) => adminApi.assignTechnician(request.id, techId),
    invalidate: [qk.admin.all],
    success: "Technician assigned and notified.",
  });

  return (
    <Card>
      <CardHeader
        title="Assign a technician"
        description={`Available ${CATEGORY_INFO[request.category].label} technicians`}
      />
      <CardBody>
        <QueryView
          query={techs}
          isEmpty={(d) => d.length === 0}
          empty={
            <EmptyState
              title="No one available"
              description="No active technicians with this skill are available right now."
            />
          }
        >
          {(list) => (
            <div className="space-y-4">
              <ul role="radiogroup" aria-label="Available technicians" className="space-y-2">
                {list.map((t) => (
                  <li key={t.id}>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selected === t.id}
                      onClick={() => setSelected(t.id)}
                      className={cn(
                        "flex w-full items-center justify-between gap-3 rounded-lg border px-3 py-2.5 text-left text-sm transition-colors",
                        selected === t.id
                          ? "border-primary bg-primary-soft"
                          : "border-border hover:bg-surface-muted",
                      )}
                    >
                      <span>
                        <span className="block font-medium">{t.name}</span>
                        <span className="text-fg-muted text-xs">
                          {formatRating(t.rating)} · {t.totalJobs} jobs
                          {t.phone ? ` · ${t.phone}` : ""}
                        </span>
                      </span>
                      <StatusBadge status={t.status} />
                    </button>
                  </li>
                ))}
              </ul>
              <Button
                className="w-full"
                disabled={selected === null}
                loading={assign.isPending}
                onClick={() => selected !== null && assign.mutate(selected)}
              >
                Assign technician
              </Button>
            </div>
          )}
        </QueryView>
      </CardBody>
    </Card>
  );
}

export function AdminRequestDetailView({ id }: { id: number }) {
  const query = useQuery({
    queryKey: qk.admin.request(id),
    queryFn: () => adminApi.getRequest(id),
  });
  return (
    <QueryView query={query}>
      {(r) => (
        <>
          <PageHeader
            back={{ href: "/admin/requests", label: "All requests" }}
            title={
              <span className="flex flex-wrap items-center gap-3">
                {CATEGORY_INFO[r.category].label} #{r.id}
                <StatusBadge status={r.status} />
              </span>
            }
            description={`Created ${formatDate(r.createdAt)}`}
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
                    ]}
                  />
                </CardBody>
              </Card>
              {r.statusHistory?.length ? (
                <Card>
                  <CardHeader title="Timeline" />
                  <CardBody>
                    <StatusTimeline entries={r.statusHistory} />
                  </CardBody>
                </Card>
              ) : null}
            </div>
            <div className="space-y-6">
              {r.canAssign ? <AssignPanel request={r} /> : null}
              <Card>
                <CardHeader title="Customer" />
                <CardBody className="space-y-1 text-sm">
                  <p className="font-medium">{r.customer.name ?? "—"}</p>
                  {r.customer.email ? <p className="text-fg-muted">{r.customer.email}</p> : null}
                  {r.customer.phone ? <p className="text-fg-muted">{r.customer.phone}</p> : null}
                </CardBody>
              </Card>
              {r.technician ? (
                <Card>
                  <CardHeader
                    title="Technician"
                    action={
                      r.technician.status ? <StatusBadge status={r.technician.status} /> : null
                    }
                  />
                  <CardBody className="space-y-1 text-sm">
                    <p className="font-medium">{r.technician.name}</p>
                    <p className="text-fg-muted">{formatRating(r.technician.rating)}</p>
                    {r.technician.phone ? (
                      <p className="text-fg-muted">{r.technician.phone}</p>
                    ) : null}
                  </CardBody>
                </Card>
              ) : null}
              {r.bill ? (
                <Card>
                  <CardHeader title="Bill" />
                  <CardBody>
                    <BillSummary bill={r.bill} />
                  </CardBody>
                </Card>
              ) : null}
            </div>
          </div>
        </>
      )}
    </QueryView>
  );
}
