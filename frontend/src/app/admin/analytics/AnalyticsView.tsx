"use client";
import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { BarSeriesChart, DonutChart, LineSeriesChart } from "@/components/features/charts";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { FilterChips } from "@/components/ui/FilterChips";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { adminApi } from "@/lib/api";
import { categoryBreakdown, dailySeries, statusBreakdown, topTechnicians } from "@/lib/analytics";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";

const inr = (v: number) => `₹${Math.round(v).toLocaleString("en-IN")}`;

export function AnalyticsView() {
  const [days, setDays] = useState("14");
  const requests = useQuery({
    queryKey: qk.admin.requests(),
    queryFn: () => adminApi.getRequests(),
  });
  const dashboard = useQuery({ queryKey: qk.admin.dashboard(), queryFn: adminApi.getDashboard });

  const rows = useMemo(() => requests.data?.items ?? [], [requests.data]);
  const series = useMemo(() => dailySeries(rows, Number(days)), [rows, days]);
  const statuses = useMemo(() => statusBreakdown(rows), [rows]);
  const categories = useMemo(() => categoryBreakdown(rows), [rows]);
  const top = useMemo(() => topTechnicians(rows), [rows]);

  return (
    <>
      <PageHeader
        title="Analytics"
        description="Computed from all requests. Revenue counts paid bills, grouped by request date."
      />
      <QueryView query={requests} loading={<SkeletonStats count={4} />}>
        {() => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Requests" value={rows.length} icon="list" />
              <StatCard
                label="Completed"
                value={rows.filter((r) => r.status === "Completed").length}
                icon="check"
              />
              <StatCard
                label="Customers"
                value={dashboard.data?.stats.totalCustomers ?? "—"}
                icon="users"
              />
              <StatCard
                label="Revenue"
                value={formatMoney(
                  dashboard.data?.stats.revenue ??
                    rows.reduce((s, r) => s + (r.billPaid ? (r.billAmount ?? 0) : 0), 0),
                )}
                icon="wallet"
              />
            </StatGrid>

            <FilterChips
              label="Time range"
              value={days}
              onChange={setDays}
              options={[
                { value: "7", label: "7 days" },
                { value: "14", label: "14 days" },
                { value: "30", label: "30 days" },
              ]}
            />

            <div className="grid gap-6 lg:grid-cols-2">
              <Card>
                <CardHeader title="New requests per day" />
                <CardBody>
                  <BarSeriesChart
                    data={series}
                    xKey="label"
                    series={[{ key: "requests", label: "Requests" }]}
                  />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="Paid revenue per day" />
                <CardBody>
                  <LineSeriesChart
                    data={series}
                    xKey="label"
                    series={[{ key: "billed", label: "Revenue" }]}
                    format={inr}
                  />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="Requests by status" />
                <CardBody>
                  <DonutChart data={statuses} />
                </CardBody>
              </Card>
              <Card>
                <CardHeader title="Requests by category" />
                <CardBody>
                  <BarSeriesChart
                    data={categories}
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
                rows={top}
                rowKey={(t) => t.name}
                columns={[
                  {
                    header: "Technician",
                    cell: (t) => <span className="font-medium">{t.name}</span>,
                  },
                  { header: "Completed jobs", cell: (t) => t.completed, className: "tabular-nums" },
                  {
                    header: "Paid revenue",
                    cell: (t) => formatMoney(t.revenue),
                    className: "tabular-nums",
                  },
                ]}
              />
            </section>
          </div>
        )}
      </QueryView>
    </>
  );
}
