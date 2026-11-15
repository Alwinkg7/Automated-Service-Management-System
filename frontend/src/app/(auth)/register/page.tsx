import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { Card, CardBody } from "@/components/ui/Card";
import { RegisterForm } from "./RegisterForm";

export const metadata: Metadata = {
  title: "Create an account",
  description: "Create a customer account to book services, or join ASMS as a technician.",
  alternates: { canonical: "/register" },
};

export default function RegisterPage() {
  return (
    <Card>
      <CardBody className="space-y-6 sm:p-8">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Create your account</h1>
          <p className="mt-1 text-sm text-fg-muted">Book services as a customer, or join as a technician.</p>
        </div>
        <Suspense>
          <RegisterForm />
        </Suspense>
        <p className="text-center text-sm text-fg-muted">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-primary hover:underline">
            Sign in
          </Link>
        </p>
      </CardBody>
    </Card>
  );
}
