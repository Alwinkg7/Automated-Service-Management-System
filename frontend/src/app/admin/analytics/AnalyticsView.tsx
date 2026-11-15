"use client";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { BarSeriesChart, DonutChart, LineSeriesChart } from "@/components/features/charts";
import { StatusBadge } from "@/components/ui/Badge";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { adminApi } from "@/lib/api";
import { formatDate, formatMoney, formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, humanize, type ServiceCategory } from "@/types/enums";

const inr = (v: number) => `₹${Math.round(v).toLocaleString("en-IN")}`;
const categoryLabel = (c: string) => CATEGORY_INFO[c as ServiceCategory]?.label ?? humanize(c);

export function AnalyticsView() {
  const query = useQuery({ queryKey: qk.admin.analytics(), queryFn: adminApi.getAnalytics });

  return (
    <>
      <PageHeader
        title="Analytics"
        description="Platform performance. Revenue is grouped by the date bills were paid."
      />
      <QueryView query={query} loading={<SkeletonStats count={4} />}>
        {(a) => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Requests" value={a.totals.totalRequests} icon="list" />
              <StatCard
                label="Completed"
                value={a.totals.completedRequests}
                icon="check"
                hint={
                  a.totals.completionRate !== null && a.totals.completionRate !== undefined
                    ? `${a.totals.completionRate}% completion rate` // API sends a percentage
                    : undefined
                }
              />
              <StatCard
                label="Customers / technicians"
                value={`${a.totals.totalCustomers} / ${a.totals.totalTechnicians}`}
                icon="users"
              />
              <StatCard label="Revenue" value={formatMoney(a.totals.totalRevenue)} icon="wallet" />
            </StatGrid>

            <div className="grid gap-6 lg:grid-cols-2">
              <Card>
                <CardHeader title="Paid revenue per day" />
                <CardBody>
                  <LineSeriesChart
                    data={a.revenueByDay}
                    xKey="label"
                    series={[{ key: "amount", label: "Revenue" }]}
                    format={inr}
                  />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="New requests per day" />
                <CardBody>
                  <BarSeriesChart
                    data={a.requestsByDay}
                    xKey="label"
                    series={[{ key: "count", label: "Requests" }]}
                  />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="Requests by status" />
                <CardBody>
                  <DonutChart
                    data={Object.entries(a.statusBreakdown).map(([name, value]) => ({
                      name: humanize(name),
                      value,
                    }))}
                  />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="Requests by category" />
                <CardBody>
                  <BarSeriesChart
                    data={a.categories.map((c) => ({ ...c, category: categoryLabel(c.category) }))}
                    xKey="category"
                    series={[{ key: "count", label: "Requests" }]}
                    horizontal
                  />
                </CardBody>
              </Card>
            </div>

            <section>
              <h2 className="mb-3 font-semibold">Top technicians</h2>
              <DataTable
                caption="Top technicians"
                rows={a.topTechnicians}
                rowKey={(t) => t.id}
                columns={[
                  {
                    header: "Technician",
                    cell: (t) => <span className="font-medium">{t.name}</span>,
                  },
                  { header: "Skill", cell: (t) => CATEGORY_INFO[t.skill].label },
                  { header: "Jobs", cell: (t) => t.totalJobsCompleted, className: "tabular-nums" },
                  { header: "Rating", cell: (t) => formatRating(t.rating), hideOnMobile: true },
                  {
                    header: "Earned",
                    cell: (t) => formatMoney(t.totalEarned),
                    className: "tabular-nums",
                  },
                  { header: "Status", cell: (t) => <StatusBadge status={t.status} /> },
                ]}
              />
            </section>

            <section>
              <h2 className="mb-3 font-semibold">Recent activity</h2>
              <DataTable
                caption="Recent activity"
                rows={a.recentActivity}
                rowKey={(r) => `${r.requestId}-${r.changedAt}-${r.status}`}
                columns={[
                  {
                    header: "Request",
                    cell: (r) => (
                      <Link
                        href={`/admin/requests/${r.requestId}`}
                        className="text-primary hover:underline"
                      >
                        #{r.requestId}
                      </Link>
                    ),
                  },
                  {
                    header: "Category",
                    cell: (r) => (r.category ? categoryLabel(r.category) : "—"),
                  },
                  { header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                  { header: "Note", cell: (r) => r.note ?? "—", hideOnMobile: true },
                  { header: "When", cell: (r) => formatDate(r.changedAt, true) },
                ]}
              />
            </section>
          </div>
        )}
      </QueryView>
    </>
  );
}
