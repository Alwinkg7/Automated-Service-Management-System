import type { ReactNode } from "react";

/** Label/value pairs for detail pages. */
export function InfoList({ items }: { items: { label: string; value: ReactNode }[] }) {
  return (
    <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
      {items.map((i) => (
        <div key={i.label} className="min-w-0">
          <dt className="text-fg-muted">{i.label}</dt>
          <dd className="mt-0.5 break-words">{i.value ?? "—"}</dd>
        </div>
      ))}
    </dl>
  );
}
