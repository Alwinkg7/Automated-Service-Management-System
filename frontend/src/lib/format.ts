// Display helpers. The API sometimes sends preformatted dates ("12 Nov 2026"),
// sometimes ISO strings; both render sensibly.

const inr = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 2,
});

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

const MONTHS = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
const API_DATE = /^(\d{1,2}) ([A-Za-z]{3}) (\d{4})(?:, (\d{1,2}):(\d{2}) ([AP]M))?$/;

/**
 * Parse ISO strings and the API's legacy display format
 * ("05 Nov 2026" / "05 Nov 2026, 02:30 PM", local time). Null when unparseable.
 */
export function parseApiDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const m = API_DATE.exec(value.trim());
  if (m) {
    const month = MONTHS.indexOf(m[2]!.toLowerCase());
    if (month < 0) return null;
    let hours = m[4] ? Number(m[4]) % 12 : 0;
    if (m[6] === "PM") hours += 12;
    return new Date(Number(m[3]), month, Number(m[1]), hours, m[5] ? Number(m[5]) : 0);
  }
  if (!ISO_LIKE.test(value)) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** yyyy-mm-dd in local time (for <input type="date"> and day buckets). */
export function dayKey(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
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
