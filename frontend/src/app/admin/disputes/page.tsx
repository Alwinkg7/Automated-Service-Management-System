"use client";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Suspense } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { DataTable } from "@/components/ui/DataTable";
import { FilterChips } from "@/components/ui/FilterChips";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { DISPUTE_STATUSES, humanize } from "@/types/enums";

function DisputesList() {
  const url = useUrlState();
  const status = url.get("status");
  const query = useQuery({ queryKey: qk.admin.disputes(status), queryFn: () => adminApi.getDisputes(status || undefined) });
  return (
    <>
      <div className="mb-4">
        <FilterChips
          label="Filter by status"
          value={status}
          onChange={(s) => url.set({ status: s })}
          options={[{ value: "", label: "All" }, ...DISPUTE_STATUSES.map((s) => ({ value: s, label: humanize(s) }))]}
        />
      </div>
      <QueryView
        query={query}
        pendingEndpoint="GET /api/admin/disputes"
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState title="No disputes" description="Nothing needs your attention." />}
      >
        {(rows) => (
          <DataTable
            caption="Disputes"
            rows={rows}
            rowKey={(d) => d.id}
            columns={[
              {
                header: "Dispute",
                cell: (d) => (
                  <Link href={`/admin/disputes/${d.id}`} className="font-medium text-primary hover:underline">
                    #{d.id}
                  </Link>
                ),
              },
              { header: "Request", cell: (d) => <Link href={`/admin/requests/${d.requestId}`} className="hover:underline">#{d.requestId}</Link> },
              { header: "Reason", cell: (d) => humanize(d.reason) },
              { header: "Raised by", cell: (d) => d.raisedByName ?? "—", hideOnMobile: true },
              { header: "Raised", cell: (d) => formatDate(d.raisedAt), hideOnMobile: true },
              {
                header: "Status",
                cell: (d) => (
                  <span className="inline-flex gap-1.5">
                    <StatusBadge status={d.status} />
                    {d.isSlaBreached ? <Badge tone="danger">SLA</Badge> : null}
                  </span>
                ),
              },
            ]}
          />
        )}
      </QueryView>
    </>
  );
}

export default function AdminDisputesPage() {
  return (
    <>
      <PageHeader title="Disputes" description="Resolve within 48 hours. Refunds go back through Razorpay." />
      <Suspense>
        <DisputesList />
      </Suspense>
    </>
  );
}
