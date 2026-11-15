import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { Card, CardBody } from "@/components/ui/Card";
import { LoginForm } from "./LoginForm";

export const metadata: Metadata = {
  title: "Sign in",
  description: "Sign in to manage your service requests, jobs or the ASMS platform.",
  alternates: { canonical: "/login" },
};

export default function LoginPage() {
  return (
    <Card>
      <CardBody className="space-y-6 sm:p-8">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Welcome back</h1>
          <p className="mt-1 text-sm text-fg-muted">Sign in to continue.</p>
        </div>
        {/* useSearchParams needs a Suspense boundary */}
        <Suspense>
          <LoginForm />
        </Suspense>
        <p className="text-center text-sm text-fg-muted">
          New here?{" "}
          <Link href="/register" className="font-medium text-primary hover:underline">
            Create an account
          </Link>
        </p>
      </CardBody>
    </Card>
  );
}
