"use client";
// All requests: search, status + date filters and paging run in SQL on the API;
// CSV export uses the same filters on the server.
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { DataTable } from "@/components/ui/DataTable";
import { Input } from "@/components/ui/Field";
import { FilterChips } from "@/components/ui/FilterChips";
import { Icon } from "@/components/ui/Icon";
import { Pagination } from "@/components/ui/Pagination";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { EmptyState } from "@/components/ui/States";
import { useDebounce } from "@/hooks/useDebounce";
import { useDownload } from "@/hooks/useDownload";
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import type { AdminRequestFilters } from "@/lib/api/endpoints/admin";
import { dayKey, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, humanize, REQUEST_STATUSES, type RequestStatus } from "@/types/enums";

const PAGE_SIZE = 20;

export function AdminRequestsView() {
  const url = useUrlState();
  const status = url.get("status") as RequestStatus | "";
  const search = url.get("q");
  const from = url.get("from");
  const to = url.get("to");
  const page = Math.max(1, Number(url.get("page", "1")) || 1);
  const debounced = useDebounce(search.trim());
  const { download, pending: exporting } = useDownload();

  const filters = useMemo<AdminRequestFilters>(
    () => ({
      status,
      search: debounced || undefined,
      from: from || undefined,
      to: to || undefined,
    }),
    [status, debounced, from, to],
  );
  const query = useQuery({
    queryKey: qk.admin.requests(filters, page),
    queryFn: () => adminApi.getRequests(filters, page, PAGE_SIZE),
    placeholderData: keepPreviousData, // keep the table while the next page loads
  });

  return (
    <>
      <PageHeader
        title="All requests"
        description="Search, filter and export every service request."
        actions={
          <Button
            variant="outline"
            loading={exporting}
            onClick={() =>
              void download(
                "/api/admin/requests/export",
                `asms-requests-${dayKey(new Date())}.csv`,
                { ...filters },
              )
            }
            icon={<Icon name="download" className="size-4" />}
          >
            Export CSV
          </Button>
        }
      />
      <div className="mb-4 space-y-3">
        <div className="grid gap-3 md:grid-cols-[1fr_auto_auto]">
          <SearchInput
            value={search}
            onChange={(q) => url.set({ q, page: undefined })}
            placeholder="Customer, technician, address, pin code or #id"
          />
          <label className="flex items-center gap-2 text-sm">
            <span className="text-fg-muted">From</span>
            <Input
              type="date"
              value={from}
              max={to || undefined}
              onChange={(e) => url.set({ from: e.target.value, page: undefined })}
              className="w-auto"
            />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <span className="text-fg-muted">To</span>
            <Input
              type="date"
              value={to}
              min={from || undefined}
              onChange={(e) => url.set({ to: e.target.value, page: undefined })}
              className="w-auto"
            />
          </label>
        </div>
        <FilterChips
          label="Filter by status"
          value={status}
          onChange={(s) => url.set({ status: s, page: undefined })}
          options={[
            { value: "", label: "All" },
            ...REQUEST_STATUSES.map((s) => ({ value: s, label: humanize(s) })),
          ]}
        />
      </div>
      <QueryView
        query={query}
        isEmpty={(d) => d.items.length === 0}
        empty={
          <EmptyState title="No matching requests" description="Adjust the search or filters." />
        }
      >
        {(d) => (
          <div className="space-y-3">
            <DataTable
              caption="Requests"
              rows={d.items}
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
                { header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                { header: "Customer", cell: (r) => r.customerName ?? "—" },
                {
                  header: "Technician",
                  cell: (r) =>
                    r.technicianName ?? <span className="text-fg-muted">Unassigned</span>,
                  hideOnMobile: true,
                },
                { header: "Scheduled", cell: (r) => r.scheduledDate, hideOnMobile: true },
                {
                  header: "Bill",
                  cell: (r) =>
                    r.hasBill
                      ? `${formatMoney(r.billAmount)}${r.billPaid ? "" : " (unpaid)"}`
                      : "—",
                  className: "tabular-nums",
                },
              ]}
            />
            <Pagination
              page={d.page}
              pageSize={d.pageSize}
              totalCount={d.totalCount}
              onPageChange={(p) => url.set({ page: p })}
            />
          </div>
        )}
      </QueryView>
    </>
  );
}
