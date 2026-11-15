"use client";
// Completed + cancelled requests with search, category filter, totals and
// re-booking. Filtering and totals come from GET /api/customer/history.
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
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
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, SERVICE_CATEGORIES } from "@/types/enums";

export function HistoryView() {
  const url = useUrlState();
  const category = url.get("category");
  const search = url.get("q");
  const term = useDebounce(search.trim());
  const query = useQuery({
    queryKey: qk.customer.history(term, category),
    queryFn: () => customerApi.getHistory(term || undefined, category || undefined),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader title="Service history" description="Your completed and cancelled requests." />
      <div className="mb-6 flex flex-col gap-3 sm:flex-row">
        <SearchInput
          className="flex-1"
          value={search}
          onChange={(q) => url.set({ q })}
          placeholder="Search description, address or technician"
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
      <QueryView query={query}>
        {(h) => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Past jobs" value={h.totalJobs} icon="history" />
              <StatCard label="Completed" value={h.completedJobs} icon="check" />
              <StatCard label="Total spent" value={formatMoney(h.totalSpent)} icon="wallet" />
              <StatCard
                label="Average rating given"
                value={h.averageRating ? h.averageRating.toFixed(1) : "—"}
                icon="star"
              />
            </StatGrid>
            {h.items.length === 0 ? (
              <EmptyState
                title="Nothing here yet"
                description="Finished and cancelled requests will appear here."
              />
            ) : (
              <DataTable
                caption="Service history"
                rows={h.items}
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
