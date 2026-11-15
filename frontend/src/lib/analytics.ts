// Analytics derived from GET /api/admin/requests (the API has no analytics
// endpoint yet). Pure functions so they are easy to test and reason about.
import type { AdminRequestRow } from "@/lib/api/schemas";
import { dayKey, parseApiDate } from "@/lib/format";
import { CATEGORY_INFO, REQUEST_STATUSES, humanize } from "@/types/enums";

export interface DayPoint {
  [key: string]: string | number;
  label: string;
  requests: number;
  billed: number;
}

/** Requests created and bill value per day for the last `days` days (by request creation date). */
export function dailySeries(rows: AdminRequestRow[], days: number, now = new Date()): DayPoint[] {
  const buckets = new Map<string, DayPoint>();
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - i);
    buckets.set(dayKey(d), {
      label: d.toLocaleDateString("en-IN", { day: "2-digit", month: "short" }),
      requests: 0,
      billed: 0,
    });
  }
  for (const r of rows) {
    const created = parseApiDate(r.createdAt);
    const bucket = created ? buckets.get(dayKey(created)) : undefined;
    if (!bucket) continue;
    bucket.requests += 1;
    if (r.billPaid && r.billAmount) bucket.billed += r.billAmount;
  }
  return [...buckets.values()];
}

export function statusBreakdown(rows: AdminRequestRow[]) {
  return REQUEST_STATUSES.map((s) => ({ name: humanize(s), value: rows.filter((r) => r.status === s).length }));
}

export function categoryBreakdown(rows: AdminRequestRow[]) {
  const counts = new Map<string, number>();
  for (const r of rows) counts.set(r.category, (counts.get(r.category) ?? 0) + 1);
  return [...counts.entries()]
    .map(([category, count]) => ({ category: CATEGORY_INFO[category as keyof typeof CATEGORY_INFO]?.label ?? category, count }))
    .sort((a, b) => b.count - a.count);
}

export function topTechnicians(rows: AdminRequestRow[], limit = 5) {
  const stats = new Map<string, { name: string; completed: number; revenue: number }>();
  for (const r of rows) {
    if (!r.technicianName) continue;
    const s = stats.get(r.technicianName) ?? { name: r.technicianName, completed: 0, revenue: 0 };
    if (r.status === "Completed") s.completed += 1;
    if (r.billPaid && r.billAmount) s.revenue += r.billAmount;
    stats.set(r.technicianName, s);
  }
  return [...stats.values()].sort((a, b) => b.completed - a.completed || b.revenue - a.revenue).slice(0, limit);
}
