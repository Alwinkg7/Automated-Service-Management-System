"use client";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { DataTable } from "@/components/ui/DataTable";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { customerApi } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { humanize } from "@/types/enums";

export function DisputesView() {
  const query = useQuery({ queryKey: qk.customer.disputes(), queryFn: customerApi.getDisputes });
  return (
    <>
      <PageHeader title="My disputes" description="Raise a dispute from a completed request. We respond within 48 hours." />
      <QueryView
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState title="No disputes" description="If something went wrong with a completed job, open the request and choose “Raise dispute”." />}
      >
        {(rows) => (
          <DataTable
            caption="My disputes"
            rows={rows}
            rowKey={(d) => d.id}
            columns={[
              {
                header: "Dispute",
                cell: (d) => (
                  <Link href={`/customer/disputes/${d.id}`} className="font-medium text-primary hover:underline">
                    #{d.id}
                  </Link>
                ),
              },
              { header: "Request", cell: (d) => `#${d.requestId}` },
              { header: "Reason", cell: (d) => humanize(d.reason) },
              { header: "Status", cell: (d) => <StatusBadge status={d.status} /> },
              { header: "Raised", cell: (d) => formatDate(d.raisedAt), hideOnMobile: true },
            ]}
          />
        )}
      </QueryView>
    </>
  );
}
