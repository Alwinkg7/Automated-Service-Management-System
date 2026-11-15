"use client";
// Technician management built on the endpoints the API has today:
// - accounts (activate/deactivate) from /api/admin/users?role=Technician
// - live availability per skill from /api/admin/technicians/available
// A full technicians endpoint (skill/status/zone/earnings, status override) is
// requested in docs/api-requests.md.
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/layout/PageHeader";
import { UserTable } from "@/components/features/admin/UserTable";
import { StatusBadge } from "@/components/ui/Badge";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Select } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { EmptyState } from "@/components/ui/States";
import { useDebounce } from "@/hooks/useDebounce";
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import { formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { CATEGORY_INFO, SERVICE_CATEGORIES, type ServiceCategory } from "@/types/enums";

export function TechniciansView() {
  const url = useUrlState();
  const skill = (url.get("skill") || "Plumbing") as ServiceCategory;
  const search = url.get("q");
  const term = useDebounce(search.trim().toLowerCase());

  const accounts = useQuery({
    queryKey: qk.admin.users("Technician"),
    queryFn: () => adminApi.getUsers("Technician"),
  });
  const available = useQuery({
    queryKey: qk.admin.available(skill),
    queryFn: () => adminApi.getAvailableTechnicians(skill),
  });
  const rows = (accounts.data?.items ?? []).filter(
    (u) => !term || [u.name, u.email, u.phone].some((v) => v?.toLowerCase().includes(term)),
  );

  return (
    <>
      <PageHeader
        title="Technicians"
        description="Manage technician accounts and see who is available by skill."
      />
      <div className="space-y-6">
        <Card>
          <CardHeader
            title="Available now"
            action={
              <Select
                aria-label="Skill"
                value={skill}
                onChange={(e) => url.set({ skill: e.target.value })}
                className="w-48"
              >
                {SERVICE_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {CATEGORY_INFO[c].label}
                  </option>
                ))}
              </Select>
            }
          />
          <CardBody>
            <QueryView
              query={available}
              isEmpty={(d) => d.length === 0}
              empty={
                <EmptyState
                  title="Nobody available"
                  description={`No ${CATEGORY_INFO[skill].label} technicians are available right now.`}
                />
              }
            >
              {(list) => (
                <DataTable
                  caption="Available technicians"
                  rows={list}
                  rowKey={(t) => t.id}
                  columns={[
                    { header: "Name", cell: (t) => <span className="font-medium">{t.name}</span> },
                    { header: "Rating", cell: (t) => formatRating(t.rating) },
                    { header: "Jobs", cell: (t) => t.totalJobs, className: "tabular-nums" },
                    { header: "Phone", cell: (t) => t.phone ?? "—", hideOnMobile: true },
                    { header: "Status", cell: (t) => <StatusBadge status={t.status} /> },
                  ]}
                />
              )}
            </QueryView>
          </CardBody>
        </Card>

        <section className="space-y-3">
          <h2 className="font-semibold">All technician accounts</h2>
          <SearchInput
            value={search}
            onChange={(q) => url.set({ q })}
            placeholder="Search name, email or phone"
          />
          <QueryView
            query={accounts}
            isEmpty={() => rows.length === 0}
            empty={<EmptyState title="No technicians found" />}
          >
            {() => <UserTable users={rows} showRole={false} />}
          </QueryView>
        </section>
      </div>
    </>
  );
}
