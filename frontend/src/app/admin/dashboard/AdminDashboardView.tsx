"use client";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { ButtonLink } from "@/components/ui/Button";
import { Card, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonList, SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { adminApi } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO } from "@/types/enums";

export function AdminDashboardView() {
  // New requests / status changes arrive over SignalR and invalidate this query.
  const query = useQuery({ queryKey: qk.admin.dashboard(), queryFn: adminApi.getDashboard });
  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Platform overview. Updates live as requests come in."
        actions={
          <ButtonLink href="/admin/analytics" variant="outline">
            View analytics
          </ButtonLink>
        }
      />
      <QueryView
        query={query}
        loading={
          <div className="space-y-6">
            <SkeletonStats count={8} />
            <SkeletonList rows={3} />
          </div>
        }
      >
        {(d) => (
          <div className="space-y-6">
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <StatCard label="Total requests" value={d.stats.totalRequests} icon="list" />
              <StatCard label="Pending" value={d.stats.pending} icon="history" />
              <StatCard label="In progress" value={d.stats.inProgress} icon="wrench" />
              <StatCard label="Completed" value={d.stats.completed} icon="check" />
              <StatCard label="Customers" value={d.stats.totalCustomers} icon="users" />
              <StatCard label="Technicians" value={d.stats.totalTechnicians} icon="briefcase" />
              <StatCard
                label="Revenue (paid bills)"
                value={formatMoney(d.stats.revenue)}
                icon="wallet"
                className="col-span-2"
              />
            </div>
            <Card>
              <CardHeader
                title="Waiting for assignment"
                description="Oldest scheduled first. Auto-assignment also runs in the background."
                action={
                  <ButtonLink href="/admin/requests?status=Pending" variant="ghost" size="sm">
                    All pending
                  </ButtonLink>
                }
              />
              {d.pendingRequests.length === 0 ? (
                <div className="p-4">
                  <EmptyState
                    title="Nothing pending"
                    description="Every request has a technician."
                  />
                </div>
              ) : (
                <div className="p-2 sm:p-4">
                  <DataTable
                    caption="Pending requests"
                    rows={d.pendingRequests}
                    rowKey={(r) => r.id}
                    columns={[
                      {
                        header: "Request",
                        cell: (r) => (
                          <Link
                            href={`/admin/requests/${r.id}`}
                            className="text-primary font-medium hover:underline"
                          >
                            {CATEGORY_INFO[r.category].label} #{r.id}
                          </Link>
                        ),
                      },
                      { header: "Customer", cell: (r) => r.customerName ?? "—" },
                      { header: "Scheduled", cell: (r) => r.scheduledDate },
                      {
                        header: "Address",
                        cell: (r) => <span className="line-clamp-1">{r.address}</span>,
                        hideOnMobile: true,
                      },
                    ]}
                    rowAction={(r) => (
                      <ButtonLink href={`/admin/requests/${r.id}`} size="sm" variant="secondary">
                        Assign
                      </ButtonLink>
                    )}
                  />
                </div>
              )}
            </Card>
          </div>
        )}
      </QueryView>
    </>
  );
}
