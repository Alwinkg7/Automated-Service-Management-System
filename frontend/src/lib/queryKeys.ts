// TanStack Query keys in one place. Each role's keys share a root, so a
// realtime event can simply invalidate everything for that role.
import type { AdminRequestFilters, TechnicianFilters } from "@/lib/api/endpoints/admin";

export const qk = {
  customer: {
    all: ["customer"] as const,
    dashboard: () => ["customer", "dashboard"] as const,
    requests: () => ["customer", "requests"] as const,
    history: (search: string, category: string) =>
      ["customer", "history", search, category] as const,
    quote: (billId: number) => ["customer", "quote", billId] as const,
    request: (id: number) => ["customer", "request", id] as const,
    categories: () => ["customer", "categories"] as const,
    promotions: () => ["customer", "promotions"] as const,
    disputes: () => ["customer", "disputes"] as const,
    dispute: (id: number) => ["customer", "dispute", id] as const,
  },
  technician: {
    all: ["technician"] as const,
    dashboard: () => ["technician", "dashboard"] as const,
    jobs: (status?: string) => ["technician", "jobs", status ?? ""] as const,
    job: (id: number) => ["technician", "job", id] as const,
    wallet: () => ["technician", "wallet"] as const,
  },
  admin: {
    all: ["admin"] as const,
    dashboard: () => ["admin", "dashboard"] as const,
    analytics: () => ["admin", "analytics"] as const,
    requests: (f?: AdminRequestFilters, page?: number) =>
      ["admin", "requests", f ?? {}, page ?? 0] as const,
    technicians: (f: TechnicianFilters) => ["admin", "technicians", f] as const,
    request: (id: number) => ["admin", "request", id] as const,
    available: (category: string) => ["admin", "available", category] as const,
    users: (role: string) => ["admin", "users", role] as const,
    zones: () => ["admin", "zones"] as const,
    promoCodes: () => ["admin", "promo-codes"] as const,
    disputes: (status: string) => ["admin", "disputes", status] as const,
    dispute: (id: number) => ["admin", "dispute", id] as const,
    withdrawals: () => ["admin", "withdrawals"] as const,
  },
};
