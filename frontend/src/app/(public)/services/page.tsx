import type { Metadata } from "next";
import Link from "next/link";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import { SERVICES } from "@/lib/services-content";

export const dynamic = "force-static";

export const metadata: Metadata = {
  title: "All home services",
  description:
    "Plumbing, electrical, carpentry, cleaning, AC repair, appliance repair, painting and general home services.",
  alternates: { canonical: "/services" },
};

export default function ServicesPage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
      <h1 className="text-3xl font-semibold tracking-tight">Home services</h1>
      <p className="text-fg-muted mt-2 max-w-2xl">
        Choose a service to see what&apos;s covered and how booking works.
      </p>
      <ul className="mt-8 grid gap-4 sm:grid-cols-2">
        {SERVICES.map((s) => (
          <li key={s.slug}>
            <Link
              href={`/services/${s.slug}`}
              className="rounded-card border-border bg-surface hover:border-primary/50 flex h-full gap-4 border p-5 transition-colors"
            >
              <CategoryIcon category={s.category} />
              <span>
                <span className="block text-lg font-medium">{s.title}</span>
                <span className="text-fg-muted mt-1 block text-sm">{s.summary}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
