"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { PageHeader } from "@/components/layout/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { DataTable } from "@/components/ui/DataTable";
import { Field, Input, Textarea } from "@/components/ui/Field";
import { Modal } from "@/components/ui/Modal";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import type { Zone } from "@/lib/api/schemas";
import { qk } from "@/lib/queryKeys";

const zoneSchema = z.object({
  zoneName: z.string().trim().min(2, "Zone name is required").max(100),
  city: z.string().trim().min(2, "City is required").max(100),
  state: z.string().trim().min(2, "State is required").max(100),
  pinCodes: z
    .string()
    .trim()
    .min(6, "Add at least one pin code")
    .refine(
      (v) => v.split(",").every((p) => /^\d{6}$/.test(p.trim())),
      "Use comma-separated 6-digit pin codes",
    ),
  isActive: z.boolean(),
});
type ZoneValues = z.infer<typeof zoneSchema>;

function ZoneForm({ zone, onDone }: { zone: Zone | null; onDone: () => void }) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ZoneValues>({
    resolver: zodResolver(zoneSchema),
    defaultValues: zone
      ? {
          zoneName: zone.zoneName,
          city: zone.city,
          state: zone.state,
          pinCodes: zone.pinCodes.join(", "),
          isActive: zone.isActive,
        }
      : { zoneName: "", city: "", state: "", pinCodes: "", isActive: true },
  });
  const save = useAction({
    fn: (v: ZoneValues) => {
      const body = {
        ...v,
        pinCodes: v.pinCodes
          .split(",")
          .map((p) => p.trim())
          .join(","),
      };
      return zone ? adminApi.updateZone(zone.id, body) : adminApi.createZone(body);
    },
    invalidate: [qk.admin.zones()],
    success: zone ? "Zone updated." : "Zone created.",
    onSuccess: onDone,
  });

  return (
    <form
      id="zone-form"
      onSubmit={handleSubmit((v) => save.mutate(v))}
      noValidate
      className="grid gap-4 sm:grid-cols-2"
    >
      <Field label="Zone name" error={errors.zoneName?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("zoneName")}
          />
        )}
      </Field>
      <Field label="City" error={errors.city?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("city")}
          />
        )}
      </Field>
      <Field label="State" error={errors.state?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("state")}
          />
        )}
      </Field>
      <label className="flex items-center gap-2 self-end pb-2 text-sm">
        <input type="checkbox" className="accent-primary size-4" {...register("isActive")} /> Active
      </label>
      <Field
        label="Pin codes"
        error={errors.pinCodes?.message}
        hint="Comma-separated, e.g. 680001, 680002"
        required
        className="sm:col-span-2"
      >
        {({ id, describedBy, invalid }) => (
          <Textarea
            id={id}
            rows={3}
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("pinCodes")}
          />
        )}
      </Field>
      <div className="flex justify-end gap-2 sm:col-span-2">
        <Button variant="outline" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" loading={save.isPending}>
          {zone ? "Save changes" : "Create zone"}
        </Button>
      </div>
    </form>
  );
}

export default function ZonesPage() {
  const query = useQuery({ queryKey: qk.admin.zones(), queryFn: adminApi.getZones });
  const [editing, setEditing] = useState<Zone | null | "new">(null);
  const toggle = useAction({
    fn: (id: number) => adminApi.toggleZone(id),
    invalidate: [qk.admin.zones()],
    success: "Zone updated.",
  });

  return (
    <>
      <PageHeader
        title="Service zones"
        description="Technicians only receive jobs from pin codes in their zone."
        actions={<Button onClick={() => setEditing("new")}>New zone</Button>}
      />
      <QueryView
        query={query}
        isEmpty={(d) => d.length === 0}
        empty={
          <EmptyState
            title="No zones yet"
            description="Create a zone to start geo-matching technicians."
          />
        }
      >
        {(zones) => (
          <DataTable
            caption="Zones"
            rows={zones}
            rowKey={(z) => z.id}
            columns={[
              { header: "Zone", cell: (z) => <span className="font-medium">{z.zoneName}</span> },
              { header: "City / State", cell: (z) => `${z.city}, ${z.state}` },
              {
                header: "Pin codes",
                cell: (z) => <span className="line-clamp-2 text-xs">{z.pinCodes.join(", ")}</span>,
                hideOnMobile: true,
              },
              { header: "Technicians", cell: (z) => z.technicianCount ?? "—", hideOnMobile: true },
              {
                header: "Status",
                cell: (z) => <StatusBadge status={z.isActive ? "Active" : "Inactive"} />,
              },
            ]}
            rowAction={(z) => (
              <>
                <Button size="sm" variant="outline" onClick={() => setEditing(z)}>
                  Edit
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  loading={toggle.isPending && toggle.variables === z.id}
                  onClick={() => toggle.mutate(z.id)}
                >
                  {z.isActive ? "Disable" : "Enable"}
                </Button>
              </>
            )}
          />
        )}
      </QueryView>
      <Modal
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing === "new" ? "New zone" : "Edit zone"}
        size="lg"
      >
        {editing !== null ? (
          <ZoneForm zone={editing === "new" ? null : editing} onDone={() => setEditing(null)} />
        ) : null}
      </Modal>
    </>
  );
}
