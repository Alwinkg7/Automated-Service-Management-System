"use client";
import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { JobCard } from "@/components/features/request/JobCard";
import { FilterChips } from "@/components/ui/FilterChips";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useUrlState } from "@/hooks/useUrlState";
import { technicianApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";
import { humanize, type RequestStatus } from "@/types/enums";

const FILTERS: RequestStatus[] = ["Assigned", "InProgress", "Billed", "Completed", "Cancelled"];

export function JobsView() {
  const url = useUrlState();
  const status = url.get("status");
  const query = useQuery({ queryKey: qk.technician.jobs(), queryFn: () => technicianApi.getJobs() });

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    for (const j of query.data ?? []) c[j.status] = (c[j.status] ?? 0) + 1;
    return c;
  }, [query.data]);
  const rows = (query.data ?? []).filter((j) => !status || j.status === status);

  return (
    <>
      <PageHeader title="My jobs" description="Jobs assigned to you. New assignments appear here in real time." />
      <div className="mb-4">
        <FilterChips
          label="Filter by status"
          value={status}
          onChange={(s) => url.set({ status: s })}
          options={[
            { value: "", label: "All", count: query.data?.length },
            ...FILTERS.map((s) => ({ value: s, label: humanize(s), count: counts[s] ?? 0 })),
          ]}
        />
      </div>
      <QueryView
        query={query}
        isEmpty={() => rows.length === 0}
        empty={<EmptyState title="No jobs here" description={status ? "Try another filter." : "Stay online to receive new jobs."} />}
      >
        {() => (
          <ul className="space-y-3">
            {rows.map((j) => (
              <li key={j.id}>
                <JobCard job={j} />
              </li>
            ))}
          </ul>
        )}
      </QueryView>
    </>
  );
}
