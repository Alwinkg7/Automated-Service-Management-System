// Tiny CSV writer. Escapes quotes/commas/newlines and neutralises spreadsheet
// formula injection (cells starting with = + - @ are prefixed with ').

export type CsvValue = string | number | boolean | null | undefined;

/** UTF-8 byte order mark (written as a code point so formatters keep it visible). */
export const BOM = String.fromCharCode(0xfeff);

function cell(value: CsvValue): string {
  if (value === null || value === undefined) return "";
  let text = String(value);
  if (typeof value === "string" && /^[=+\-@\t\r]/.test(text)) text = `'${text}`;
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

export function toCsv<T>(
  rows: T[],
  columns: { header: string; value: (row: T) => CsvValue }[],
): string {
  const lines = [columns.map((c) => cell(c.header)).join(",")];
  for (const row of rows) lines.push(columns.map((c) => cell(c.value(row))).join(","));
  // CRLF + BOM so Excel opens UTF-8 (₹, names) correctly.
  return BOM + lines.join("\r\n");
}
