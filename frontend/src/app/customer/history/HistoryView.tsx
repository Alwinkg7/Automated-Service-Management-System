"use client";
// Completed + cancelled requests with search, category filter and re-booking
// (mirrors Customer/Requests/History in the MVC app).
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { ButtonLink } from "@/components/ui/Button";
import { DataTable } from "@/components/ui/DataTable";
import { Select } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { useDebounce } from "@/hooks/useDebounce";
import { useUrlState } from "@/hooks/useUrlState";
import { customerApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";
import { formatDate, formatMoney } from "@/lib/format";
import { CATEGORY_INFO, SERVICE_CATEGORIES } from "@/types/enums";

export function HistoryView() {
  const url = useUrlState();
  const category = url.get("category");
  const search = url.get("q");
  const term = useDebounce(search.trim().toLowerCase());
  const query = useQuery({
    queryKey: qk.customer.requests(),
    queryFn: () => customerApi.getRequests(),
  });

  const past = useMemo(
    () => (query.data ?? []).filter((r) => r.status === "Completed" || r.status === "Cancelled"),
    [query.data],
  );
  const rows = useMemo(
    () =>
      past.filter(
        (r) =>
          (!category || r.category === category) &&
          (!term || [r.description, r.technicianName].some((v) => v?.toLowerCase().includes(term))),
      ),
    [past, category, term],
  );
  const completed = past.filter((r) => r.status === "Completed");
  const spent = completed.reduce((sum, r) => sum + (r.billPaid ? (r.billAmount ?? 0) : 0), 0);

  return (
    <>
      <PageHeader title="Service history" description="Your completed and cancelled requests." />
      <QueryView query={query}>
        {() => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Past jobs" value={past.length} icon="history" />
              <StatCard label="Completed" value={completed.length} icon="check" />
              <StatCard label="Cancelled" value={past.length - completed.length} icon="close" />
              <StatCard label="Total spent" value={formatMoney(spent)} icon="wallet" />
            </StatGrid>
            <div className="flex flex-col gap-3 sm:flex-row">
              <SearchInput
                className="flex-1"
                value={search}
                onChange={(q) => url.set({ q })}
                placeholder="Search description or technician"
              />
              <Select
                aria-label="Category"
                className="sm:w-56"
                value={category}
                onChange={(e) => url.set({ category: e.target.value })}
              >
                <option value="">All categories</option>
                {SERVICE_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {CATEGORY_INFO[c].label}
                  </option>
                ))}
              </Select>
            </div>
            {rows.length === 0 ? (
              <EmptyState
                title="Nothing here yet"
                description="Finished and cancelled requests will appear here."
              />
            ) : (
              <DataTable
                caption="Service history"
                rows={rows}
                rowKey={(r) => r.id}
                columns={[
                  {
                    header: "Request",
                    cell: (r) => (
                      <Link
                        href={`/customer/requests/${r.id}`}
                        className="text-primary font-medium hover:underline"
                      >
                        {CATEGORY_INFO[r.category].label} #{r.id}
                      </Link>
                    ),
                  },
                  { header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                  { header: "Date", cell: (r) => formatDate(r.scheduledDate) },
                  {
                    header: "Technician",
                    cell: (r) => r.technicianName ?? "—",
                    hideOnMobile: true,
                  },
                  {
                    header: "Amount",
                    cell: (r) => formatMoney(r.billAmount),
                    className: "tabular-nums",
                  },
                ]}
                rowAction={(r) => (
                  <ButtonLink
                    href={`/customer/requests/new?rebook=${r.id}`}
                    variant="outline"
                    size="sm"
                  >
                    Re-book
                  </ButtonLink>
                )}
              />
            )}
          </div>
        )}
      </QueryView>
    </>
  );
}
