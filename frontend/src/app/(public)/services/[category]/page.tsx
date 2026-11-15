import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import { ButtonLink } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { SERVICES, serviceBySlug } from "@/lib/services-content";

// Pre-render every category at build time; unknown slugs 404.
export const dynamicParams = false;

export function generateStaticParams() {
  return SERVICES.map((s) => ({ category: s.slug }));
}

type Props = { params: Promise<{ category: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const service = serviceBySlug((await params).category);
  if (!service) return {};
  return {
    title: service.title,
    description: service.summary,
    alternates: { canonical: `/services/${service.slug}` },
    openGraph: { title: `${service.title} · ASMS`, description: service.summary, url: `/services/${service.slug}` },
  };
}

export default async function ServicePage({ params }: Props) {
  const service = serviceBySlug((await params).category);
  if (!service) notFound();

  // FAQPage JSON-LD mirrors the FAQ text visible on the page.
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: service.faqs.map((f) => ({
      "@type": "Question",
      name: f.q,
      acceptedAnswer: { "@type": "Answer", text: f.a },
    })),
  };

  return (
    <div className="mx-auto max-w-4xl px-4 py-12 sm:px-6">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="flex items-center gap-4">
        <CategoryIcon category={service.category} className="size-12" />
        <h1 className="text-3xl font-semibold tracking-tight">{service.title}</h1>
      </div>
      <p className="mt-4 text-lg text-fg-muted">{service.summary}</p>
      <div className="mt-6 flex flex-wrap gap-3">
        <ButtonLink href={`/customer/requests/new?category=${service.category}`} size="lg">
          Book {service.title.toLowerCase()}
        </ButtonLink>
        <ButtonLink href="/services" variant="outline" size="lg">
          All services
        </ButtonLink>
      </div>

      <section aria-labelledby="covers" className="mt-12">
        <h2 id="covers" className="text-xl font-semibold">
          What&apos;s covered
        </h2>
        <ul className="mt-4 grid gap-2 sm:grid-cols-2">
          {service.covers.map((c) => (
            <li key={c} className="flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-3">
              <Icon name="check" className="text-success" />
              {c}
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="faq" className="mt-12">
        <h2 id="faq" className="text-xl font-semibold">
          Frequently asked questions
        </h2>
        <div className="mt-4 divide-y divide-border rounded-card border border-border bg-surface">
          {service.faqs.map((f) => (
            <details key={f.q} className="group px-5 py-4">
              <summary className="cursor-pointer list-none font-medium marker:hidden">
                <span className="flex items-center justify-between gap-3">
                  {f.q}
                  <Icon name="chevronRight" className="size-4 transition-transform group-open:rotate-90" />
                </span>
              </summary>
              <p className="mt-2 text-sm text-fg-muted">{f.a}</p>
            </details>
          ))}
        </div>
      </section>
    </div>
  );
}
