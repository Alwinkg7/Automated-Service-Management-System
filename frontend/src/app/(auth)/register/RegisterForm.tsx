"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch, type FieldErrors } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Field, FormError, Input, Select } from "@/components/ui/Field";
import { FilterChips } from "@/components/ui/FilterChips";
import { registerAccount } from "@/lib/api/auth-client";
import { errorMessage, isApiError } from "@/lib/api/errors";
import { ROLE_HOME } from "@/lib/auth/constants";
import { registerSchema, type RegisterForm as RegisterValues } from "@/lib/validation/auth";
import { CATEGORY_INFO, SERVICE_CATEGORIES } from "@/types/enums";

type Role = RegisterValues["role"];

export function RegisterForm() {
  const router = useRouter();
  const params = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);
  const initialRole: Role = params.get("role") === "Technician" ? "Technician" : "Customer";

  const {
    register,
    handleSubmit,
    control,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { role: initialRole } as RegisterValues,
  });
  const role = useWatch({ control, name: "role" });
  // Field errors differ per role; widen for display.
  const fieldErrors = errors as FieldErrors<{
    fullName: string;
    email: string;
    phone: string;
    password: string;
    confirmPassword: string;
    skill: string;
    referralCode: string;
  }>;

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const result = await registerAccount(values);
      if (result.requiresLogin) {
        router.replace("/login");
        return;
      }
      router.replace(ROLE_HOME[result.role]);
      router.refresh();
    } catch (error) {
      if (isApiError(error)) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (field && messages[0])
            setError(field as keyof RegisterValues, { message: messages[0] });
        }
      }
      setFormError(errorMessage(error));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <FilterChips
        label="Account type"
        value={role}
        onChange={(v) => setValue("role", v as Role, { shouldValidate: false })}
        options={[
          { value: "Customer", label: "I need a service" },
          { value: "Technician", label: "I'm a technician" },
        ]}
      />
      <FormError message={formError} />

      <Field label="Full name" error={fieldErrors.fullName?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            autoComplete="name"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("fullName")}
          />
        )}
      </Field>
      <Field label="Email" error={fieldErrors.email?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            type="email"
            autoComplete="email"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("email")}
          />
        )}
      </Field>
      <Field label="Phone number" error={fieldErrors.phone?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            type="tel"
            autoComplete="tel"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("phone")}
          />
        )}
      </Field>

      {role === "Technician" ? (
        <Field label="Your skill / trade" error={fieldErrors.skill?.message} required>
          {({ id, describedBy, invalid }) => (
            <Select
              id={id}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              defaultValue=""
              {...register("skill")}
            >
              <option value="" disabled>
                Select a skill
              </option>
              {SERVICE_CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {CATEGORY_INFO[c].label}
                </option>
              ))}
            </Select>
          )}
        </Field>
      ) : (
        <Field label="Referral code (optional)" error={fieldErrors.referralCode?.message}>
          {({ id, describedBy, invalid }) => (
            <Input
              id={id}
              aria-describedby={describedBy}
              aria-invalid={invalid}
              {...register("referralCode")}
            />
          )}
        </Field>
      )}

      <Field
        label="Password"
        error={fieldErrors.password?.message}
        hint="At least 8 characters."
        required
      >
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            type="password"
            autoComplete="new-password"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("password")}
          />
        )}
      </Field>
      <Field label="Confirm password" error={fieldErrors.confirmPassword?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            type="password"
            autoComplete="new-password"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("confirmPassword")}
          />
        )}
      </Field>

      <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
        Create account
      </Button>
    </form>
  );
}
