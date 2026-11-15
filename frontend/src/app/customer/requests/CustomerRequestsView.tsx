"use client";
import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { RequestCard } from "@/components/features/request/RequestCard";
import { ButtonLink } from "@/components/ui/Button";
import { FilterChips } from "@/components/ui/FilterChips";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { EmptyState } from "@/components/ui/States";
import { useDebounce } from "@/hooks/useDebounce";
import { useUrlState } from "@/hooks/useUrlState";
import { customerApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, humanize, REQUEST_STATUSES } from "@/types/enums";

export function CustomerRequestsView() {
  const url = useUrlState();
  const status = url.get("status");
  const search = url.get("q");
  const debounced = useDebounce(search.trim().toLowerCase());

  // One fetch for all requests; filtering is instant and counts stay accurate.
  const query = useQuery({ queryKey: qk.customer.requests(), queryFn: () => customerApi.getRequests() });

  const counts = useMemo(() => {
    const c: Record<string, number> = {};
    for (const r of query.data ?? []) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  }, [query.data]);

  const filtered = useMemo(
    () =>
      (query.data ?? []).filter((r) => {
        if (status && r.status !== status) return false;
        if (!debounced) return true;
        return [r.description, r.technicianName, CATEGORY_INFO[r.category].label, String(r.id)]
          .filter(Boolean)
          .some((v) => v!.toLowerCase().includes(debounced));
      }),
    [query.data, status, debounced],
  );

  return (
    <>
      <PageHeader
        title="My requests"
        description="Track every booking from request to payment."
        actions={<ButtonLink href="/customer/requests/new">Book a service</ButtonLink>}
      />
      <div className="mb-4 space-y-3">
        <SearchInput value={search} onChange={(q) => url.set({ q })} placeholder="Search by description, technician or #id" />
        <FilterChips
          label="Filter by status"
          value={status}
          onChange={(s) => url.set({ status: s })}
          options={[
            { value: "", label: "All", count: query.data?.length },
            ...REQUEST_STATUSES.map((s) => ({ value: s, label: humanize(s), count: counts[s] ?? 0 })),
          ]}
        />
      </div>
      <QueryView
        query={query}
        isEmpty={() => filtered.length === 0}
        empty={
          query.data?.length ? (
            <EmptyState title="No matching requests" description="Try a different filter or search term." />
          ) : (
            <EmptyState
              title="No requests yet"
              description="Book a service to get started."
              action={<ButtonLink href="/customer/requests/new">Book a service</ButtonLink>}
            />
          )
        }
      >
        {() => (
          <ul className="space-y-3">
            {filtered.map((r) => (
              <li key={r.id}>
                <RequestCard request={r} />
              </li>
            ))}
          </ul>
        )}
      </QueryView>
    </>
  );
}
