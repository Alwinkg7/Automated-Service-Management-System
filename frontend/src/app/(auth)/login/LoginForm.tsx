"use client";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { Field, FormError, Input } from "@/components/ui/Field";
import { login } from "@/lib/api/auth-client";
import { errorMessage } from "@/lib/api/errors";
import { safeNextPath } from "@/lib/auth/guard";
import { loginSchema, type LoginForm as LoginValues } from "@/lib/validation/auth";

export function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const [formError, setFormError] = useState<string | null>(
    params.get("expired") ? "Your session expired. Please sign in again." : null,
  );
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      const { user } = await login(values.email, values.password);
      router.replace(safeNextPath(params.get("next"), user.role));
      router.refresh();
    } catch (error) {
      setFormError(errorMessage(error));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <FormError message={formError} />
      <Field label="Email" error={errors.email?.message} required>
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
      <Field label="Password" error={errors.password?.message} required>
        {({ id, describedBy, invalid }) => (
          <Input
            id={id}
            type="password"
            autoComplete="current-password"
            aria-describedby={describedBy}
            aria-invalid={invalid}
            {...register("password")}
          />
        )}
      </Field>
      <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
        Sign in
      </Button>
    </form>
  );
}
