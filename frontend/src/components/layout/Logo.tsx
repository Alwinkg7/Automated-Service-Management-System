import Link from "next/link";

export function Logo({ compact = false, href = "/" }: { compact?: boolean; href?: string }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-2 font-semibold tracking-tight"
      aria-label="ASMS home"
    >
      <span
        aria-hidden
        className="bg-primary text-primary-fg flex size-8 items-center justify-center rounded-lg text-sm font-bold"
      >
        A
      </span>
      {compact ? null : <span>ASMS</span>}
    </Link>
  );
}
