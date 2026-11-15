"use client";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { JobCard } from "@/components/features/request/JobCard";
import { StatusBadge } from "@/components/ui/Badge";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonList, SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { technicianApi } from "@/lib/api";
import type { TechnicianDashboard } from "@/lib/api/schemas";
import { formatDate, formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { currentConnection } from "@/lib/realtime/connection";
import { CATEGORY_INFO } from "@/types/enums";

export function TechnicianDashboardView() {
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: qk.technician.dashboard(), queryFn: technicianApi.getDashboard });

  const toggle = useAction({
    fn: technicianApi.toggleStatus,
    invalidate: [qk.technician.all],
    success: (r) => `You are now ${r.newStatus}.`,
    onSuccess: (r) => {
      // Optimistic-safe: write the confirmed status into the cache immediately.
      queryClient.setQueryData<TechnicianDashboard>(qk.technician.dashboard(), (old) =>
        old ? { ...old, technician: { ...old.technician, status: r.newStatus } } : old,
      );
      // Join/leave the "new job available" broadcast group.
      void currentConnection()?.invoke("UpdateAvailability", r.newStatus === "Available").catch(() => {});
    },
  });

  return (
    <QueryView
      query={query}
      loading={
        <div className="space-y-6">
          <SkeletonStats />
          <SkeletonList rows={2} />
        </div>
      }
    >
      {(d) => {
        const status = d.technician.status;
        return (
          <>
            <PageHeader
              title={`Hello${d.technician.name ? `, ${d.technician.name.split(" ")[0]}` : ""}`}
              description="Your jobs and availability at a glance."
            />
            <div className="space-y-6">
              <Card>
                <CardBody className="flex flex-wrap items-center justify-between gap-4">
                  <div>
                    <p className="text-sm text-fg-muted">Availability</p>
                    <div className="mt-1 flex items-center gap-2">
                      <StatusBadge status={status} className="text-sm" />
                      <span className="text-sm text-fg-muted">
                        {status === "Busy"
                          ? "You're on a job. You'll be available again once it's completed."
                          : status === "Available"
                            ? "You can receive new jobs."
                            : "You won't receive new jobs."}
                      </span>
                    </div>
                  </div>
                  {status !== "Busy" ? (
                    <Button variant={status === "Available" ? "outline" : "primary"} loading={toggle.isPending} onClick={() => toggle.mutate()}>
                      {status === "Available" ? "Go offline" : "Go online"}
                    </Button>
                  ) : null}
                </CardBody>
              </Card>

              <StatGrid>
                <StatCard label="Rating" value={formatRating(d.technician.rating)} icon="star" />
                <StatCard label="Total jobs" value={d.technician.totalJobs} icon="briefcase" />
                <StatCard label="Completed" value={d.technician.completedJobs} icon="check" />
                <StatCard label="Awaiting payment" value={d.technician.pendingBills} icon="card" />
              </StatGrid>

              <div className="grid gap-6 lg:grid-cols-2">
                <Card>
                  <CardHeader title="Active job" />
                  <CardBody>
                    {d.activeJob ? (
                      <JobCard job={d.activeJob} />
                    ) : (
                      <EmptyState title="No active job" description="New assignments will show up here instantly." />
                    )}
                  </CardBody>
                </Card>
                <Card>
                  <CardHeader
                    title="Recently completed"
                    action={
                      <ButtonLink href="/technician/jobs" variant="ghost" size="sm">
                        All jobs
                      </ButtonLink>
                    }
                  />
                  <CardBody>
                    {d.recentJobs.length === 0 ? (
                      <p className="text-sm text-fg-muted">No completed jobs yet.</p>
                    ) : (
                      <ul className="divide-y divide-border">
                        {d.recentJobs.map((j) => (
                          <li key={j.id} className="flex items-center justify-between gap-3 py-3 text-sm">
                            <Link href={`/technician/jobs/${j.id}`} className="font-medium hover:text-primary">
                              {CATEGORY_INFO[j.category].label} #{j.id}
                              <span className="block text-xs font-normal text-fg-muted">
                                {j.customerName} · {formatDate(j.completedAt)}
                              </span>
                            </Link>
                            <StatusBadge status={j.billPaid ? "Paid" : "Unpaid"} />
                          </li>
                        ))}
                      </ul>
                    )}
                  </CardBody>
                </Card>
              </div>
            </div>
          </>
        );
      }}
    </QueryView>
  );
}
