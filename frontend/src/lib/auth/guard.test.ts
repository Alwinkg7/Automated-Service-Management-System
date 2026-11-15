import { describe, expect, it } from "vitest";
import { decideRoute, safeNextPath } from "./guard";
import { decodeJwt, type SessionUser } from "./jwt";

const now = Date.UTC(2026, 10, 15);
const future = Math.floor(now / 1000) + 3600;

function user(role: SessionUser["role"], exp = future): SessionUser {
  return { id: "u1", email: "a@b.c", name: "A", role, exp };
}

function fakeJwt(payload: Record<string, unknown>) {
  const enc = (o: unknown) => Buffer.from(JSON.stringify(o)).toString("base64url");
  return `${enc({ alg: "HS256" })}.${enc(payload)}.sig`;
}

describe("decideRoute", () => {
  it("lets anyone see public pages", () => {
    expect(decideRoute("/", "", null, now)).toEqual({ action: "allow" });
    expect(decideRoute("/services/plumbing", "", null, now)).toEqual({ action: "allow" });
  });

  it("sends signed-out users to login with a next param", () => {
    expect(decideRoute("/customer/requests", "?status=Pending", null, now)).toEqual({
      action: "redirect",
      to: `/login?next=${encodeURIComponent("/customer/requests?status=Pending")}`,
    });
  });

  it("treats an expired token as signed out", () => {
    const expired = user("Customer", Math.floor(now / 1000) - 10);
    expect(decideRoute("/customer/dashboard", "", expired, now)).toMatchObject({ action: "redirect" });
    expect(decideRoute("/login", "", expired, now)).toEqual({ action: "allow" });
  });

  it("blocks the wrong role with /403", () => {
    expect(decideRoute("/admin/users", "", user("Customer"), now)).toEqual({ action: "redirect", to: "/403" });
    expect(decideRoute("/technician/jobs", "", user("Admin"), now)).toEqual({ action: "redirect", to: "/403" });
  });

  it("allows the matching role", () => {
    expect(decideRoute("/technician/jobs/4", "", user("Technician"), now)).toEqual({ action: "allow" });
  });

  it("does not confuse similar prefixes", () => {
    expect(decideRoute("/administrator", "", null, now)).toEqual({ action: "allow" });
  });

  it("redirects area roots and auth pages to the dashboard", () => {
    expect(decideRoute("/admin", "", user("Admin"), now)).toEqual({ action: "redirect", to: "/admin/dashboard" });
    expect(decideRoute("/login", "", user("Customer"), now)).toEqual({
      action: "redirect",
      to: "/customer/dashboard",
    });
  });
});

describe("safeNextPath", () => {
  it("rejects open redirects and other roles' areas", () => {
    expect(safeNextPath("https://evil.example", "Customer")).toBe("/customer/dashboard");
    expect(safeNextPath("//evil.example", "Customer")).toBe("/customer/dashboard");
    expect(safeNextPath("/admin/users", "Customer")).toBe("/customer/dashboard");
    expect(safeNextPath("/customer/requests/3", "Customer")).toBe("/customer/requests/3");
  });
});

describe("decodeJwt", () => {
  it("reads ASP.NET claim names", () => {
    const token = fakeJwt({
      sub: "abc",
      email: "t@x.io",
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Technician",
      fullName: "Tara",
      exp: future,
    });
    expect(decodeJwt(token)).toEqual({ id: "abc", email: "t@x.io", role: "Technician", name: "Tara", exp: future });
  });

  it("returns null for garbage or unknown roles", () => {
    expect(decodeJwt("not.a.jwt")).toBeNull();
    expect(decodeJwt(fakeJwt({ sub: "x", role: "Hacker" }))).toBeNull();
    expect(decodeJwt(undefined)).toBeNull();
  });
});
