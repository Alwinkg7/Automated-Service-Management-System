// Decode (NOT verify) a JWT payload. The API verifies signatures; we only read
// claims for routing and UX. Works in Node, the proxy runtime and the browser.
import { ROLES, type Role } from "./constants";

export interface SessionUser {
  id: string;
  email: string;
  name: string;
  role: Role;
  /** Expiry as unix seconds (0 = unknown). */
  exp: number;
}

const ROLE_CLAIMS = ["role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
const NAME_CLAIMS = ["fullName", "name", "unique_name"];
const ID_CLAIMS = [
  "sub",
  "nameid",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
];
const EMAIL_CLAIMS = ["email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];

function base64UrlDecode(input: string): string {
  const base64 = input.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, "=");
  const binary = atob(padded);
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function pick(payload: Record<string, unknown>, keys: string[]): string | undefined {
  for (const key of keys) {
    const value = payload[key];
    if (typeof value === "string" && value) return value;
    // A user can carry several roles; take the first string.
    if (Array.isArray(value)) {
      const first = value.find((v): v is string => typeof v === "string");
      if (first) return first;
    }
  }
  return undefined;
}

export function decodeJwt(token: string | undefined | null): SessionUser | null {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length !== 3 || !parts[1]) return null;
  try {
    const payload = JSON.parse(base64UrlDecode(parts[1])) as Record<string, unknown>;
    const role = pick(payload, ROLE_CLAIMS);
    const id = pick(payload, ID_CLAIMS);
    const exp = typeof payload.exp === "number" ? payload.exp : 0;
    if (!role || !id || !(ROLES as readonly string[]).includes(role)) return null;
    return {
      id,
      role: role as Role,
      email: pick(payload, EMAIL_CLAIMS) ?? "",
      name: pick(payload, NAME_CLAIMS) ?? "",
      exp,
    };
  } catch {
    return null;
  }
}

/** True when the token expires within `skewSeconds` (tokens without exp never expire here). */
export function isExpired(user: Pick<SessionUser, "exp">, nowMs = Date.now(), skewSeconds = 30) {
  if (!user.exp) return false;
  return user.exp * 1000 <= nowMs + skewSeconds * 1000;
}
