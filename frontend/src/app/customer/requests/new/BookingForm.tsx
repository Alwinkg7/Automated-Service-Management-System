"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import { Button } from "@/components/ui/Button";
import { Card, CardBody } from "@/components/ui/Card";
import { Field, FormError, Input, Textarea } from "@/components/ui/Field";
import { customerApi } from "@/lib/api";
import { errorMessage, isApiError } from "@/lib/api/errors";
import { cn } from "@/lib/cn";
import { localDateTimeInput } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { createRequestSchema, type CreateRequestForm } from "@/lib/validation/request";
import { CATEGORY_INFO, SERVICE_CATEGORIES } from "@/types/enums";

function newKey() {
  return typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function BookingForm({
  defaults,
  previousTechnician,
  rebookOf,
}: {
  defaults: Partial<CreateRequestForm>;
  previousTechnician?: string;
  /** Re-book this past request (keeps its category; new time/description/address). */
  rebookOf?: number;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);
  // One key per booking attempt: a double click / retry reuses it, so the API
  // can recognise the duplicate and not create a second request.
  const [idempotencyKey, setIdempotencyKey] = useState(newKey);

  const {
    register,
    handleSubmit,
    control,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CreateRequestForm>({
    resolver: zodResolver(createRequestSchema),
    defaultValues: {
      description: "",
      address: "",
      pinCode: "",
      scheduledDate: localDateTimeInput(3),
      ...defaults,
    },
  });
  const selected = useWatch({ control, name: "category" });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const { requestId } = rebookOf
        ? await customerApi.rebook(
            rebookOf,
            {
              scheduledDate: values.scheduledDate,
              description: values.description,
              address: values.address,
            },
            idempotencyKey,
          )
        : await customerApi.createRequest(
            { ...values, pinCode: values.pinCode || undefined },
            idempotencyKey,
          );
      setIdempotencyKey(newKey());
      await queryClient.invalidateQueries({ queryKey: qk.customer.all });
      toast.success(`Request #${requestId} submitted. We'll assign a technician shortly.`);
      router.push(`/customer/requests/${requestId}`);
    } catch (error) {
      if (isApiError(error)) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (field in values && messages[0])
            setError(field as keyof CreateRequestForm, { message: messages[0] });
        }
      }
      setFormError(errorMessage(error));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-6">
      <FormError message={formError} />
      {previousTechnician ? (
        <p className="bg-info-soft text-info rounded-lg px-3 py-2 text-sm">
          Last time this job was handled by {previousTechnician}.
        </p>
      ) : null}

      <fieldset disabled={!!rebookOf} className="disabled:opacity-80">
        <legend className="mb-3 text-sm font-medium">
          Service <span className="text-danger">*</span>
          {rebookOf ? (
            <span className="text-fg-muted font-normal"> (same as the original request)</span>
          ) : null}
        </legend>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {SERVICE_CATEGORIES.map((c) => (
            <label
              key={c}
              className={cn(
                "rounded-card bg-surface has-focus-visible:outline-ring flex cursor-pointer flex-col items-start gap-2 border p-3 transition-colors has-focus-visible:outline-2",
                selected === c
                  ? "border-primary ring-primary ring-1"
                  : "border-border hover:border-primary/40",
              )}
            >
              <input
                type="radio"
                value={c}
                className="sr-only"
                checked={selected === c}
                onChange={() => setValue("category", c, { shouldValidate: true })}
              />
              <CategoryIcon category={c} className="size-9" />
              <span className="text-sm font-medium">{CATEGORY_INFO[c].label}</span>
              <span className="text-fg-muted text-xs">{CATEGORY_INFO[c].description}</span>
            </label>
          ))}
        </div>
        {errors.category ? (
          <p role="alert" className="text-danger mt-2 text-xs font-medium">
            {errors.category.message}
          </p>
        ) : null}
      </fieldset>

      <Card>
        <CardBody className="grid gap-4 sm:grid-cols-2">
          <Field
            label="What's the problem?"
            error={errors.description?.message}
            required
            className="sm:col-span-2"
          >
            {({ id, describedBy, invalid }) => (
              <Textarea
                id={id}
                rows={4}
                placeholder="e.g. Kitchen tap is leaking from the base"
                aria-describedby={describedBy}
                aria-invalid={invalid}
                {...register("description")}
              />
            )}
          </Field>
          <Field
            label="Preferred date & time"
            error={errors.scheduledDate?.message}
            hint="At least 1 hour from now."
            required
          >
            {({ id, describedBy, invalid }) => (
              <Input
                id={id}
                type="datetime-local"
                min={localDateTimeInput(1)}
                aria-describedby={describedBy}
                aria-invalid={invalid}
                {...register("scheduledDate")}
              />
            )}
          </Field>
          <Field
            label="Pin code"
            error={errors.pinCode?.message}
            hint="Helps us find a technician near you."
          >
            {({ id, describedBy, invalid }) => (
              <Input
                id={id}
                inputMode="numeric"
                maxLength={6}
                autoComplete="postal-code"
                aria-describedby={describedBy}
                aria-invalid={invalid}
                {...register("pinCode")}
              />
            )}
          </Field>
          <Field label="Address" error={errors.address?.message} required className="sm:col-span-2">
            {({ id, describedBy, invalid }) => (
              <Textarea
                id={id}
                rows={2}
                autoComplete="street-address"
                aria-describedby={describedBy}
                aria-invalid={invalid}
                {...register("address")}
              />
            )}
          </Field>
        </CardBody>
      </Card>

      <div className="flex justify-end">
        <Button type="submit" size="lg" loading={isSubmitting}>
          Confirm booking
        </Button>
      </div>
    </form>
  );
}
