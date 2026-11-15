// Pure route-protection rules, shared by proxy.ts and unit tests.
// The API still enforces authorization; this only decides where to send the user.
import { ROLE_HOME, ROLE_PREFIX, ROLES, type Role } from "./constants";
import { isExpired, type SessionUser } from "./jwt";

export type GuardDecision = { action: "allow" } | { action: "redirect"; to: string };

const AUTH_PAGES = ["/login", "/register"];

export function requiredRoleFor(pathname: string): Role | null {
  for (const role of ROLES) {
    const prefix = ROLE_PREFIX[role];
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return role;
  }
  return null;
}

export function decideRoute(
  pathname: string,
  search: string,
  user: SessionUser | null,
  nowMs = Date.now(),
): GuardDecision {
  const session = user && !isExpired(user, nowMs) ? user : null;
  const required = requiredRoleFor(pathname);

  if (required) {
    if (!session) {
      const next = encodeURIComponent(pathname + search);
      return { action: "redirect", to: `/login?next=${next}` };
    }
    if (session.role !== required) return { action: "redirect", to: "/403" };
    // "/customer" -> "/customer/dashboard"
    if (pathname === ROLE_PREFIX[required]) return { action: "redirect", to: ROLE_HOME[required] };
    return { action: "allow" };
  }

  // Signed-in users don't need the login/register pages.
  if (session && AUTH_PAGES.includes(pathname)) {
    return { action: "redirect", to: ROLE_HOME[session.role] };
  }
  return { action: "allow" };
}

/** Only allow same-site relative redirects after login (prevents open redirects). */
export function safeNextPath(next: string | null | undefined, role: Role): string {
  if (next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\")) {
    const required = requiredRoleFor(next.split("?")[0] ?? next);
    if (!required || required === role) return next;
  }
  return ROLE_HOME[role];
}
