import Link from "next/link";

export function Logo({ compact = false, href = "/" }: { compact?: boolean; href?: string }) {
  return (
    <Link href={href} className="inline-flex items-center gap-2 font-semibold tracking-tight" aria-label="ASMS home">
      <span aria-hidden className="flex size-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-fg">
        A
      </span>
      {compact ? null : <span>ASMS</span>}
    </Link>
  );
}
