import type { Metadata } from "next";
import { ButtonLink } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";

export const metadata: Metadata = { title: "Access denied", robots: { index: false } };

export default function ForbiddenPage() {
  return (
    <main id="main" className="flex min-h-dvh flex-col items-center justify-center px-4 text-center">
      <Icon name="shield" className="size-12 text-warning" />
      <h1 className="mt-4 text-2xl font-semibold">You don&apos;t have access to this page</h1>
      <p className="mt-2 max-w-md text-fg-muted">This area belongs to a different account type. Sign in with the right account or go back home.</p>
      <div className="mt-6 flex gap-3">
        <ButtonLink href="/">Go home</ButtonLink>
        <ButtonLink href="/login" variant="outline">
          Switch account
        </ButtonLink>
      </div>
    </main>
  );
}
