"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { DataTable } from "@/components/ui/DataTable";
import { Field, Input, Select } from "@/components/ui/Field";
import { Modal } from "@/components/ui/Modal";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import type { PromoCode } from "@/lib/api/schemas";
import { formatDate, formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { PROMO_TYPES } from "@/types/enums";

const optionalNumber = z.preprocess((v) => (v === "" || v === null || Number.isNaN(v) ? undefined : v), z.number().positive().optional());

const promoSchema = z
  .object({
    code: z.string().trim().min(3, "At least 3 characters").max(20).regex(/^[A-Za-z0-9]+$/, "Letters and numbers only"),
    description: z.string().trim().min(3, "Add a short description").max(200),
    discountType: z.enum(PROMO_TYPES),
    discountValue: z.number("Enter a value").positive("Must be greater than 0"),
    maxDiscount: optionalNumber,
    minimumAmount: z.number("Enter a value").min(0),
    maxUses: optionalNumber,
    expiresAt: z.string().optional(),
    isActive: z.boolean(),
  })
  .refine((v) => v.discountType !== "Percentage" || v.discountValue <= 100, {
    message: "A percentage can't exceed 100",
    path: ["discountValue"],
  });
type PromoValues = z.input<typeof promoSchema>;
type PromoOutput = z.output<typeof promoSchema>;

function describeDiscount(p: PromoCode) {
  if (p.discountType === "Percentage") return `${p.discountValue}%${p.maxDiscount ? ` (max ${formatMoney(p.maxDiscount)})` : ""}`;
  return `${formatMoney(p.discountValue)}${p.discountType === "FirstOnly" ? " · first booking" : ""}`;
}

function PromoForm({ onDone }: { onDone: () => void }) {
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<PromoValues, unknown, PromoOutput>({
    resolver: zodResolver(promoSchema),
    defaultValues: { discountType: "Flat", minimumAmount: 0, isActive: true, code: "", description: "" },
  });
  const type = useWatch({ control, name: "discountType" });
  const create = useAction({
    fn: (v: PromoOutput) =>
      adminApi.createPromoCode({
        ...v,
        code: v.code.toUpperCase(),
        maxDiscount: v.maxDiscount ?? null,
        maxUses: v.maxUses ?? null,
        expiresAt: v.expiresAt || null,
      }),
    invalidate: [qk.admin.promoCodes()],
    success: (_, v) => `Promo code ${v.code.toUpperCase()} created.`,
    onSuccess: onDone,
  });

  return (
    <form onSubmit={handleSubmit((v) => create.mutate(v))} noValidate className="grid gap-4 sm:grid-cols-2">
      <Field label="Code" error={errors.code?.message} required>
        {({ id, describedBy, invalid }) => <Input id={id} className="uppercase" aria-describedby={describedBy} aria-invalid={invalid} {...register("code")} />}
      </Field>
      <Field label="Type" error={errors.discountType?.message} required>
        {({ id }) => (
          <Select id={id} {...register("discountType")}>
            <option value="Flat">Flat amount</option>
            <option value="Percentage">Percentage</option>
            <option value="FirstOnly">Flat, first booking only</option>
          </Select>
        )}
      </Field>
      <Field label="Description" error={errors.description?.message} required className="sm:col-span-2">
        {({ id, describedBy, invalid }) => <Input id={id} aria-describedby={describedBy} aria-invalid={invalid} {...register("description")} />}
      </Field>
      <Field label={type === "Percentage" ? "Discount (%)" : "Discount (₹)"} error={errors.discountValue?.message} required>
        {({ id, describedBy, invalid }) => <Input id={id} type="number" step="0.01" aria-describedby={describedBy} aria-invalid={invalid} {...register("discountValue", { valueAsNumber: true })} />}
      </Field>
      {type === "Percentage" ? (
        <Field label="Max discount (₹)" error={errors.maxDiscount?.message}>
          {({ id }) => <Input id={id} type="number" step="0.01" {...register("maxDiscount", { valueAsNumber: true })} />}
        </Field>
      ) : null}
      <Field label="Minimum bill (₹)" error={errors.minimumAmount?.message} required>
        {({ id }) => <Input id={id} type="number" step="0.01" {...register("minimumAmount", { valueAsNumber: true })} />}
      </Field>
      <Field label="Max uses" error={errors.maxUses?.message} hint="Leave empty for unlimited">
        {({ id }) => <Input id={id} type="number" {...register("maxUses", { valueAsNumber: true })} />}
      </Field>
      <Field label="Expires on" hint="Optional">
        {({ id }) => <Input id={id} type="date" {...register("expiresAt")} />}
      </Field>
      <label className="flex items-center gap-2 self-end pb-2 text-sm">
        <input type="checkbox" className="size-4 accent-primary" {...register("isActive")} /> Active
      </label>
      <div className="flex justify-end gap-2 sm:col-span-2">
        <Button variant="outline" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" loading={create.isPending}>
          Create code
        </Button>
      </div>
    </form>
  );
}

export default function PromoCodesPage() {
  const query = useQuery({ queryKey: qk.admin.promoCodes(), queryFn: adminApi.getPromoCodes });
  const [open, setOpen] = useState(false);
  const toggle = useAction({ fn: (id: number) => adminApi.togglePromoCode(id), invalidate: [qk.admin.promoCodes()], success: "Promo code updated." });

  return (
    <>
      <PageHeader title="Promo codes" description="Discount codes customers can apply at payment." actions={<Button onClick={() => setOpen(true)}>New code</Button>} />
      <QueryView
        query={query}
        pendingEndpoint="GET /api/admin/promo-codes"
        isEmpty={(d) => d.length === 0}
        empty={<EmptyState title="No promo codes" description="Create your first discount code." />}
      >
        {(codes) => (
          <DataTable
            caption="Promo codes"
            rows={codes}
            rowKey={(p) => p.id}
            columns={[
              { header: "Code", cell: (p) => <Badge tone="primary">{p.code}</Badge> },
              { header: "Discount", cell: (p) => describeDiscount(p) },
              { header: "Min bill", cell: (p) => formatMoney(p.minimumAmount), hideOnMobile: true },
              { header: "Used", cell: (p) => `${p.usedCount}${p.maxUses ? ` / ${p.maxUses}` : ""}`, className: "tabular-nums" },
              { header: "Expires", cell: (p) => (p.expiresAt ? formatDate(p.expiresAt) : "Never"), hideOnMobile: true },
              { header: "Status", cell: (p) => <StatusBadge status={p.isActive ? "Active" : "Inactive"} /> },
            ]}
            rowAction={(p) => (
              <Button size="sm" variant="outline" loading={toggle.isPending && toggle.variables === p.id} onClick={() => toggle.mutate(p.id)}>
                {p.isActive ? "Deactivate" : "Activate"}
              </Button>
            )}
          />
        )}
      </QueryView>
      <Modal open={open} onClose={() => setOpen(false)} title="New promo code" size="lg">
        {open ? <PromoForm onDone={() => setOpen(false)} /> : null}
      </Modal>
    </>
  );
}
