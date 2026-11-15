// Display helpers. The API sometimes sends preformatted dates ("12 Nov 2026"),
// sometimes ISO strings; both render sensibly.

const inr = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 2 });

export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return inr.format(value);
}

const ISO_LIKE = /^\d{4}-\d{2}-\d{2}/;

export function formatDate(value: string | null | undefined, withTime = false): string {
  if (!value) return "—";
  if (!ISO_LIKE.test(value)) return value; // already formatted by the API
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    ...(withTime ? { hour: "2-digit", minute: "2-digit" } : {}),
  });
}

/** Best-effort parse for sorting/filtering by date (ISO or "dd MMM yyyy"). */
export function parseDate(value: string | null | undefined): number | null {
  if (!value) return null;
  const t = Date.parse(value);
  return Number.isNaN(t) ? null : t;
}

export function formatRating(value: number | null | undefined): string {
  if (!value) return "No ratings";
  return `${value.toFixed(1)} / 5`;
}

/** Value for <input type="datetime-local"> N hours from now (local time). */
export function localDateTimeInput(hoursFromNow: number): string {
  const d = new Date(Date.now() + hoursFromNow * 3600_000);
  d.setSeconds(0, 0);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Trigger a browser download for a Blob. */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
