import type { Metadata } from "next";
import Link from "next/link";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import { ButtonLink } from "@/components/ui/Button";
import { Icon, type IconName } from "@/components/ui/Icon";
import { SITE_URL } from "@/lib/env";
import { SERVICES } from "@/lib/services-content";

// Fully static: no request data is read here.
export const dynamic = "force-static";

export const metadata: Metadata = {
  title: { absolute: "ASMS – Book trusted home service technicians" },
  alternates: { canonical: "/" },
  openGraph: {
    title: "ASMS – Book trusted home service technicians",
    description: "Plumbing, electrical, AC repair, cleaning and more. Live tracking and secure online payments.",
    url: "/",
  },
};

const STEPS: { icon: IconName; title: string; text: string }[] = [
  { icon: "plus", title: "Book in a minute", text: "Pick a service, describe the problem and choose a time that suits you." },
  { icon: "users", title: "Matched automatically", text: "We assign a skilled technician who covers your pin code." },
  { icon: "map", title: "Track live", text: "Follow your technician on the map once the job is in progress." },
  { icon: "card", title: "Pay securely", text: "Pay the itemised bill online with Razorpay and download your invoice." },
];

export default function LandingPage() {
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "WebSite",
    name: "ASMS",
    url: SITE_URL,
    description: "Book verified home service technicians, track them live and pay online.",
  };

  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />

      <section className="mx-auto max-w-6xl px-4 pt-16 pb-12 sm:px-6 sm:pt-24">
        <div className="max-w-2xl animate-slide-up">
          <p className="mb-3 inline-flex rounded-full bg-primary-soft px-3 py-1 text-xs font-medium text-primary">
            Home services, handled end to end
          </p>
          <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">
            Trusted technicians at your door, <span className="text-primary">tracked live.</span>
          </h1>
          <p className="mt-4 text-lg text-fg-muted">
            Book plumbers, electricians, AC technicians and more. Get matched automatically, follow the visit in real time, and pay
            securely online.
          </p>
          <div className="mt-8 flex flex-wrap gap-3">
            <ButtonLink href="/register" size="lg">
              Book a service
            </ButtonLink>
            <ButtonLink href="/services" size="lg" variant="outline">
              Browse services
            </ButtonLink>
          </div>
        </div>
      </section>

      <section aria-labelledby="services-heading" className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
        <h2 id="services-heading" className="text-2xl font-semibold tracking-tight">
          Popular services
        </h2>
        <ul className="mt-6 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {SERVICES.map((s) => (
            <li key={s.slug}>
              <Link
                href={`/services/${s.slug}`}
                className="flex h-full gap-3 rounded-card border border-border bg-surface p-4 transition-colors hover:border-primary/50"
              >
                <CategoryIcon category={s.category} />
                <span>
                  <span className="block font-medium">{s.title}</span>
                  <span className="mt-0.5 block text-sm text-fg-muted">{s.covers.slice(0, 2).join(", ")}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section id="how-it-works" aria-labelledby="how-heading" className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
        <h2 id="how-heading" className="text-2xl font-semibold tracking-tight">
          How it works
        </h2>
        <ol className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {STEPS.map((step, i) => (
            <li key={step.title} className="rounded-card border border-border bg-surface p-5">
              <span className="flex items-center gap-2 text-sm text-fg-muted">
                <Icon name={step.icon} className="text-primary" />
                Step {i + 1}
              </span>
              <h3 className="mt-3 font-semibold">{step.title}</h3>
              <p className="mt-1 text-sm text-fg-muted">{step.text}</p>
            </li>
          ))}
        </ol>
      </section>

      <section className="mx-auto max-w-6xl px-4 pt-6 pb-20 sm:px-6">
        <div className="flex flex-col items-start gap-4 rounded-card bg-primary p-8 text-primary-fg sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-xl font-semibold">Are you a skilled technician?</h2>
            <p className="mt-1 opacity-90">Join ASMS, get jobs in your area and track your earnings in one place.</p>
          </div>
          <ButtonLink href="/register?role=Technician" variant="outline" className="border-transparent">
            Join as a technician
          </ButtonLink>
        </div>
      </section>
    </>
  );
}
