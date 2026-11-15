import { describe, expect, it } from "vitest";
import { toCsv } from "./csv";
import { formatDate, parseApiDate } from "./format";

describe("parseApiDate", () => {
  it("parses the API's preformatted dates", () => {
    expect(parseApiDate("05 Nov 2026")).toEqual(new Date(2026, 10, 5));
    expect(parseApiDate("05 Nov 2026, 02:30 PM")).toEqual(new Date(2026, 10, 5, 14, 30));
    expect(parseApiDate("05 Nov 2026, 12:05 AM")).toEqual(new Date(2026, 10, 5, 0, 5));
  });
  it("parses ISO strings and rejects junk", () => {
    expect(parseApiDate("2026-11-05T10:00:00Z")?.toISOString()).toBe("2026-11-05T10:00:00.000Z");
    expect(parseApiDate("not a date")).toBeNull();
    expect(parseApiDate(undefined)).toBeNull();
  });
});

describe("formatDate", () => {
  it("leaves preformatted strings alone", () => {
    expect(formatDate("05 Nov 2026")).toBe("05 Nov 2026");
    expect(formatDate(null)).toBe("—");
  });
});

describe("toCsv", () => {
  it("escapes and guards against formula injection", () => {
    const csv = toCsv(
      [{ name: 'Ann "A", Jr', note: "=HYPERLINK(1)", n: 5 }],
      [
        { header: "Name", value: (r) => r.name },
        { header: "Note", value: (r) => r.note },
        { header: "N", value: (r) => r.n },
      ],
    );
    expect(csv).toBe('﻿Name,Note,N\r\n"Ann ""A"", Jr",\'=HYPERLINK(1),5');
  });
});
