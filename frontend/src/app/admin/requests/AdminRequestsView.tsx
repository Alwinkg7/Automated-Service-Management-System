"use client";
// All requests with search, status + date filters, paging and CSV export.
// The API returns the full list, so filtering happens here (mirrors the MVC
// Admin/Requests/Index). Server-side paging is requested in docs/api-requests.md.
import { useQuery } from "@tanstack/react-query";
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
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import type { AdminRequestRow } from "@/lib/api/schemas";
import { toCsv } from "@/lib/csv";
import { dayKey, formatMoney, parseApiDate, saveBlob } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, humanize, REQUEST_STATUSES } from "@/types/enums";

const PAGE_SIZE = 20;

function matches(r: AdminRequestRow, term: string) {
  if (!term) return true;
  if (String(r.id) === term.replace("#", "")) return true;
  return [r.customerName, r.technicianName, r.address, r.pinCode, CATEGORY_INFO[r.category].label].some((v) =>
    v?.toLowerCase().includes(term),
  );
}

export function AdminRequestsView() {
  const url = useUrlState();
  const status = url.get("status");
  const search = url.get("q");
  const from = url.get("from");
  const to = url.get("to");
  const page = Math.max(1, Number(url.get("page", "1")) || 1);
  const term = useDebounce(search.trim().toLowerCase());

  const query = useQuery({ queryKey: qk.admin.requests(), queryFn: () => adminApi.getRequests() });
  const all = useMemo(() => query.data?.items ?? [], [query.data]);

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    for (const r of all) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  }, [all]);

  const filtered = useMemo(
    () =>
      all.filter((r) => {
        if (status && r.status !== status) return false;
        if (!matches(r, term)) return false;
        if (from || to) {
          const created = parseApiDate(r.createdAt);
          if (!created) return false;
          const key = dayKey(created);
          if (from && key < from) return false;
          if (to && key > to) return false;
        }
        return true;
      }),
    [all, status, term, from, to],
  );
  const pageRows = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  function exportCsv() {
    const csv = toCsv(filtered, [
      { header: "Id", value: (r) => r.id },
      { header: "Category", value: (r) => CATEGORY_INFO[r.category].label },
      { header: "Status", value: (r) => r.status },
      { header: "Customer", value: (r) => r.customerName },
      { header: "Technician", value: (r) => r.technicianName },
      { header: "Scheduled", value: (r) => r.scheduledDate },
      { header: "Created", value: (r) => r.createdAt },
      { header: "Bill amount", value: (r) => r.billAmount },
      { header: "Bill paid", value: (r) => (r.hasBill ? (r.billPaid ? "Yes" : "No") : "") },
    ]);
    saveBlob(new Blob([csv], { type: "text/csv;charset=utf-8" }), `asms-requests-${dayKey(new Date())}.csv`);
  }

  return (
    <>
      <PageHeader
        title="All requests"
        description="Search, filter and export every service request."
        actions={
          <Button variant="outline" onClick={exportCsv} disabled={!filtered.length} icon={<Icon name="download" className="size-4" />}>
            Export CSV ({filtered.length})
          </Button>
        }
      />
      <div className="mb-4 space-y-3">
        <div className="grid gap-3 md:grid-cols-[1fr_auto_auto]">
          <SearchInput value={search} onChange={(q) => url.set({ q, page: undefined })} placeholder="Customer, technician, address, pin code or #id" />
          <label className="flex items-center gap-2 text-sm">
            <span className="text-fg-muted">From</span>
            <Input type="date" value={from} max={to || undefined} onChange={(e) => url.set({ from: e.target.value, page: undefined })} className="w-auto" />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <span className="text-fg-muted">To</span>
            <Input type="date" value={to} min={from || undefined} onChange={(e) => url.set({ to: e.target.value, page: undefined })} className="w-auto" />
          </label>
        </div>
        <FilterChips
          label="Filter by status"
          value={status}
          onChange={(s) => url.set({ status: s, page: undefined })}
          options={[
            { value: "", label: "All", count: all.length },
            ...REQUEST_STATUSES.map((s) => ({ value: s, label: humanize(s), count: counts[s] ?? 0 })),
          ]}
        />
      </div>
      <QueryView
        query={query}
        isEmpty={() => filtered.length === 0}
        empty={<EmptyState title="No matching requests" description="Adjust the search or filters." />}
      >
        {() => (
          <div className="space-y-3">
            <DataTable
              caption="Requests"
              rows={pageRows}
              rowKey={(r) => r.id}
              columns={[
                {
                  header: "Request",
                  cell: (r) => (
                    <Link href={`/admin/requests/${r.id}`} className="font-medium text-primary hover:underline">
                      {CATEGORY_INFO[r.category].label} #{r.id}
                    </Link>
                  ),
                },
                { header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                { header: "Customer", cell: (r) => r.customerName ?? "—" },
                { header: "Technician", cell: (r) => r.technicianName ?? <span className="text-fg-muted">Unassigned</span>, hideOnMobile: true },
                { header: "Scheduled", cell: (r) => r.scheduledDate, hideOnMobile: true },
                {
                  header: "Bill",
                  cell: (r) => (r.hasBill ? `${formatMoney(r.billAmount)}${r.billPaid ? "" : " (unpaid)"}` : "—"),
                  className: "tabular-nums",
                },
              ]}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={filtered.length} onPageChange={(p) => url.set({ page: p })} />
          </div>
        )}
      </QueryView>
    </>
  );
}
