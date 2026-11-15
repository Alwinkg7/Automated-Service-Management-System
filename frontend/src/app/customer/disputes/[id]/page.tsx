"use client";
import { useQuery } from "@tanstack/react-query";
import { use } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { DisputeDetailCard } from "@/components/features/dispute/DisputeDetailCard";
import { QueryView } from "@/components/ui/QueryView";
import { customerApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";

export default function CustomerDisputePage({ params }: { params: Promise<{ id: string }> }) {
  const id = Number(use(params).id);
  const query = useQuery({ queryKey: qk.customer.dispute(id), queryFn: () => customerApi.getDispute(id), enabled: id > 0 });
  return (
    <>
      <PageHeader back={{ href: "/customer/disputes", label: "My disputes" }} title={`Dispute #${id}`} />
      <QueryView query={query} pendingEndpoint="GET /api/customer/disputes/{id}">{(d) => <DisputeDetailCard dispute={d} requestHref={`/customer/requests/${d.requestId}`} />}</QueryView>
    </>
  );
}
