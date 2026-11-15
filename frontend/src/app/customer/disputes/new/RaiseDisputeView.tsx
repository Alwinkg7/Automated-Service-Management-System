"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Card, CardBody } from "@/components/ui/Card";
import { Field, FormError, Select, Textarea } from "@/components/ui/Field";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { customerApi } from "@/lib/api";
import { errorMessage } from "@/lib/api/errors";
import { qk } from "@/lib/queryKeys";
import { disputeFormSchema, type DisputeForm } from "@/lib/validation/request";
import { CATEGORY_INFO, DISPUTE_REASONS, humanize } from "@/types/enums";

export function RaiseDisputeView() {
  const requestId = Number(useSearchParams().get("requestId")) || 0;
  const router = useRouter();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);
  const request = useQuery({
    queryKey: qk.customer.request(requestId),
    queryFn: () => customerApi.getRequest(requestId),
    enabled: requestId > 0,
  });
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<DisputeForm>({ resolver: zodResolver(disputeFormSchema), defaultValues: { reason: "", description: "" } });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const { id } = await customerApi.raiseDispute({ requestId, ...values });
      await queryClient.invalidateQueries({ queryKey: qk.customer.disputes() });
      toast.success(`Dispute #${id} raised. We'll review it within 48 hours.`);
      router.push(`/customer/disputes/${id}`);
    } catch (e) {
      setFormError(errorMessage(e));
    }
  });

  if (!requestId) {
    return <EmptyState title="Choose a request first" description="Open a completed request and choose “Raise dispute”." action={<ButtonLink href="/customer/history">Go to history</ButtonLink>} />;
  }

  return (
    <>
      <PageHeader back={{ href: `/customer/requests/${requestId}`, label: `Request #${requestId}` }} title="Raise a dispute" />
      <QueryView query={request}>
        {(r) =>
          r.status !== "Completed" ? (
            <EmptyState title="Not available" description="Disputes can only be raised for completed requests." />
          ) : (
            <Card className="max-w-2xl">
              <CardBody>
                <form onSubmit={onSubmit} noValidate className="space-y-4">
                  <p className="text-sm text-fg-muted">
                    {CATEGORY_INFO[r.category].label} #{r.id}
                    {r.technician?.name ? ` · ${r.technician.name}` : ""}
                  </p>
                  <FormError message={formError} />
                  <Field label="Reason" error={errors.reason?.message} required>
                    {({ id, describedBy, invalid }) => (
                      <Select id={id} aria-describedby={describedBy} aria-invalid={invalid} {...register("reason")}>
                        <option value="" disabled>
                          Select a reason
                        </option>
                        {DISPUTE_REASONS.map((reason) => (
                          <option key={reason} value={reason}>
                            {humanize(reason)}
                          </option>
                        ))}
                      </Select>
                    )}
                  </Field>
                  <Field label="What happened?" error={errors.description?.message} hint="At least 20 characters." required>
                    {({ id, describedBy, invalid }) => (
                      <Textarea id={id} rows={5} aria-describedby={describedBy} aria-invalid={invalid} {...register("description")} />
                    )}
                  </Field>
                  <div className="flex justify-end">
                    <Button type="submit" loading={isSubmitting}>
                      Submit dispute
                    </Button>
                  </div>
                </form>
              </CardBody>
            </Card>
          )
        }
      </QueryView>
    </>
  );
}
