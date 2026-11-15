"use client";
// Technician management: filter by skill/status/active, override status,
// change skill, activate/deactivate (mirrors MVC Admin/Technicians).
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { DataTable } from "@/components/ui/DataTable";
import { Field, Select } from "@/components/ui/Field";
import { FilterChips } from "@/components/ui/FilterChips";
import { Modal } from "@/components/ui/Modal";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { useDebounce } from "@/hooks/useDebounce";
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import type { TechnicianFilters } from "@/lib/api/endpoints/admin";
import type { AdminTechnician } from "@/lib/api/schemas";
import { formatMoney, formatRating } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import {
  CATEGORY_INFO,
  SERVICE_CATEGORIES,
  TECHNICIAN_STATUSES,
  type ServiceCategory,
  type TechnicianStatus,
} from "@/types/enums";

function EditTechnicianModal({ tech, onClose }: { tech: AdminTechnician; onClose: () => void }) {
  const [status, setStatus] = useState<TechnicianStatus>(tech.status);
  const [skill, setSkill] = useState<ServiceCategory>(tech.skill);
  const invalidate = [qk.admin.all];
  const saveStatus = useAction({
    fn: () => adminApi.setTechnicianStatus(tech.id, status),
    invalidate,
    success: `Status set to ${status}.`,
  });
  const saveSkill = useAction({
    fn: () => adminApi.setTechnicianSkill(tech.id, skill),
    invalidate,
    success: `Skill changed to ${CATEGORY_INFO[skill].label}.`,
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={tech.name ?? "Technician"}
      description={tech.email ?? undefined}
    >
      <div className="space-y-5">
        <div className="flex items-end gap-2">
          <Field label="Availability override" className="flex-1">
            {({ id }) => (
              <Select
                id={id}
                value={status}
                onChange={(e) => setStatus(e.target.value as TechnicianStatus)}
              >
                {TECHNICIAN_STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <Button
            variant="outline"
            disabled={status === tech.status}
            loading={saveStatus.isPending}
            onClick={() => saveStatus.mutate()}
          >
            Save
          </Button>
        </div>
        <div className="flex items-end gap-2">
          <Field
            label="Skill"
            className="flex-1"
            hint="Can't change while the technician has an active job."
          >
            {({ id, describedBy }) => (
              <Select
                id={id}
                aria-describedby={describedBy}
                value={skill}
                onChange={(e) => setSkill(e.target.value as ServiceCategory)}
              >
                {SERVICE_CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {CATEGORY_INFO[c].label}
                  </option>
                ))}
              </Select>
            )}
          </Field>
          <Button
            variant="outline"
            disabled={skill === tech.skill}
            loading={saveSkill.isPending}
            onClick={() => saveSkill.mutate()}
          >
            Save
          </Button>
        </div>
      </div>
    </Modal>
  );
}

export function TechniciansView() {
  const url = useUrlState();
  const skill = url.get("skill") as ServiceCategory | "";
  const status = url.get("status") as TechnicianStatus | "";
  const active = url.get("active") as "true" | "false" | "";
  const search = url.get("q");
  const term = useDebounce(search.trim());
  const [editing, setEditing] = useState<AdminTechnician | null>(null);
  const [toggling, setToggling] = useState<AdminTechnician | null>(null);

  const filters = useMemo<TechnicianFilters>(
    () => ({ skill, status, isActive: active, search: term || undefined }),
    [skill, status, active, term],
  );
  const query = useQuery({
    queryKey: qk.admin.technicians(filters),
    queryFn: () => adminApi.getTechnicians(filters),
    placeholderData: keepPreviousData,
  });
  const toggleActive = useAction({
    fn: (t: AdminTechnician) =>
      t.isActive ? adminApi.deactivateTechnician(t.id) : adminApi.activateTechnician(t.id),
    invalidate: [qk.admin.all],
    success: (_, t) => (t.isActive ? "Technician deactivated." : "Technician activated."),
    onSuccess: () => setToggling(null),
  });
  const counts = query.data?.counts;

  return (
    <>
      <PageHeader title="Technicians" description="Availability, skills and account status." />
      <div className="space-y-4">
        <StatGrid>
          <StatCard label="Total" value={counts?.total ?? "—"} icon="users" />
          <StatCard label="Available" value={counts?.available ?? "—"} icon="check" />
          <StatCard label="Busy" value={counts?.busy ?? "—"} icon="wrench" />
          <StatCard label="Offline" value={counts?.offline ?? "—"} icon="moon" />
        </StatGrid>
        <div className="grid gap-3 md:grid-cols-[1fr_12rem_12rem]">
          <SearchInput
            value={search}
            onChange={(q) => url.set({ q })}
            placeholder="Search name, email or phone"
          />
          <Select
            aria-label="Skill"
            value={skill}
            onChange={(e) => url.set({ skill: e.target.value })}
          >
            <option value="">All skills</option>
            {SERVICE_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {CATEGORY_INFO[c].label}
              </option>
            ))}
          </Select>
          <Select
            aria-label="Account"
            value={active}
            onChange={(e) => url.set({ active: e.target.value })}
          >
            <option value="">Active and inactive</option>
            <option value="true">Active only</option>
            <option value="false">Inactive only</option>
          </Select>
        </div>
        <FilterChips
          label="Filter by availability"
          value={status}
          onChange={(s) => url.set({ status: s })}
          options={[
            { value: "", label: "All" },
            ...TECHNICIAN_STATUSES.map((s) => ({ value: s, label: s })),
          ]}
        />
        <QueryView
          query={query}
          isEmpty={(d) => d.data.length === 0}
          empty={<EmptyState title="No technicians found" description="Try different filters." />}
        >
          {({ data }) => (
            <DataTable
              caption="Technicians"
              rows={data}
              rowKey={(t) => t.id}
              columns={[
                {
                  header: "Technician",
                  cell: (t) => (
                    <span>
                      <span className="block font-medium">{t.name}</span>
                      <span className="text-fg-muted text-xs">{t.phone ?? t.email}</span>
                    </span>
                  ),
                },
                { header: "Skill", cell: (t) => CATEGORY_INFO[t.skill].label },
                {
                  header: "Status",
                  cell: (t) => (
                    <span className="inline-flex gap-1.5">
                      <StatusBadge status={t.status} />
                      {!t.isActive ? <Badge tone="danger">Inactive</Badge> : null}
                    </span>
                  ),
                },
                { header: "Rating", cell: (t) => formatRating(t.rating), hideOnMobile: true },
                { header: "Jobs", cell: (t) => t.totalJobsCompleted, className: "tabular-nums" },
                { header: "Zone", cell: (t) => t.zoneName ?? "—", hideOnMobile: true },
                {
                  header: "Earned",
                  cell: (t) => formatMoney(t.totalEarned),
                  className: "tabular-nums",
                  hideOnMobile: true,
                },
              ]}
              rowAction={(t) => (
                <>
                  <Button size="sm" variant="outline" onClick={() => setEditing(t)}>
                    Edit
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => setToggling(t)}>
                    {t.isActive ? "Deactivate" : "Activate"}
                  </Button>
                </>
              )}
            />
          )}
        </QueryView>
      </div>
      {editing ? <EditTechnicianModal tech={editing} onClose={() => setEditing(null)} /> : null}
      <ConfirmDialog
        open={!!toggling}
        title={toggling?.isActive ? `Deactivate ${toggling?.name}?` : `Activate ${toggling?.name}?`}
        description={
          toggling?.isActive
            ? "They stop receiving jobs and are signed out within about a minute."
            : "They can sign in and receive jobs again."
        }
        confirmLabel={toggling?.isActive ? "Deactivate" : "Activate"}
        tone={toggling?.isActive ? "danger" : "primary"}
        loading={toggleActive.isPending}
        onConfirm={() => toggling && toggleActive.mutate(toggling)}
        onClose={() => setToggling(null)}
      />
    </>
  );
}
