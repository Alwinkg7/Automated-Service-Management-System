"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { Field, Input, Textarea } from "@/components/ui/Field";
import { useAction } from "@/hooks/useAction";
import { technicianApi } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";

// Mirrors CreateBillDto: both costs 0–10,000,000, at least one > 0.
const billSchema = z
  .object({
    laborCost: z.number("Enter an amount").min(0).max(10_000_000),
    materialCost: z.number("Enter an amount").min(0).max(10_000_000),
    description: z.string().max(500).optional(),
  })
  .refine((v) => v.laborCost > 0 || v.materialCost > 0, {
    message: "Enter at least one cost (labour or materials).",
    path: ["laborCost"],
  });
type BillValues = z.infer<typeof billSchema>;

export function BillForm({ jobId }: { jobId: number }) {
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<BillValues>({ resolver: zodResolver(billSchema), defaultValues: { laborCost: 0, materialCost: 0, description: "" } });
  const [labor, material] = useWatch({ control, name: ["laborCost", "materialCost"] });

  const create = useAction({
    fn: (v: BillValues) => technicianApi.createBill(jobId, v),
    invalidate: [qk.technician.all],
    success: "Bill created and sent to the customer.",
  });

  return (
    <Card>
      <CardHeader title="Create bill" description="The customer pays this amount online. Double-check before sending." />
      <CardBody>
        <form onSubmit={handleSubmit((v) => create.mutate(v))} noValidate className="grid gap-4 sm:grid-cols-2">
          <Field label="Labour (₹)" error={errors.laborCost?.message}>
            {({ id, describedBy, invalid }) => (
              <Input id={id} type="number" min={0} step="0.01" aria-describedby={describedBy} aria-invalid={invalid} {...register("laborCost", { valueAsNumber: true })} />
            )}
          </Field>
          <Field label="Materials & parts (₹)" error={errors.materialCost?.message}>
            {({ id, describedBy, invalid }) => (
              <Input id={id} type="number" min={0} step="0.01" aria-describedby={describedBy} aria-invalid={invalid} {...register("materialCost", { valueAsNumber: true })} />
            )}
          </Field>
          <Field label="Materials description" error={errors.description?.message} className="sm:col-span-2">
            {({ id, describedBy, invalid }) => (
              <Textarea id={id} rows={2} placeholder="e.g. 2m copper wire, 1 switch" aria-describedby={describedBy} aria-invalid={invalid} {...register("description")} />
            )}
          </Field>
          <div className="flex items-center justify-between gap-3 sm:col-span-2">
            <p className="text-sm">
              Total: <span className="font-semibold tabular-nums">{formatMoney((Number(labor) || 0) + (Number(material) || 0))}</span>
            </p>
            <Button type="submit" loading={create.isPending}>
              Send bill
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}
