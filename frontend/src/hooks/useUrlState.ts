"use client";
// Filters live in the URL (?status=Pending&q=leak) so they are shareable and
// survive back/forward navigation.
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback } from "react";

export function useUrlState() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const get = useCallback((key: string, fallback = "") => params.get(key) ?? fallback, [params]);

  const set = useCallback(
    (updates: Record<string, string | number | undefined | null>) => {
      const next = new URLSearchParams(params.toString());
      for (const [key, value] of Object.entries(updates)) {
        if (value === undefined || value === null || value === "") next.delete(key);
        else next.set(key, String(value));
      }
      const qs = next.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [params, pathname, router],
  );

  return { get, set };
}
