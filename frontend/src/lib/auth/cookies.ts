// Cookie helpers shared by the auth route handlers, the BFF proxy and proxy.ts.
import type { LoginResponse } from "@/lib/api/schemas";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "./constants";
import { decodeJwt } from "./jwt";

const secure = process.env.NODE_ENV === "production";
const SEVEN_DAYS = 60 * 60 * 24 * 7;

interface CookieOptions {
  httpOnly: boolean;
  secure: boolean;
  sameSite: "lax" | "strict";
  path: string;
  maxAge: number;
}

/** Anything with a `set` like NextResponse.cookies or `await cookies()`. */
export interface CookieWriter {
  set(name: string, value: string, options: CookieOptions): unknown;
}

function secondsUntil(iso: string | null | undefined): number | undefined {
  if (!iso) return undefined;
  const ms = Date.parse(iso);
  if (Number.isNaN(ms)) return undefined;
  return Math.max(60, Math.floor((ms - Date.now()) / 1000));
}

export function setAuthCookies(target: CookieWriter, tokens: Pick<LoginResponse, "token" | "refreshToken" | "refreshTokenExpiresAt">) {
  const user = decodeJwt(tokens.token);
  const accessMaxAge = user?.exp ? Math.max(60, user.exp - Math.floor(Date.now() / 1000)) : SEVEN_DAYS;
  target.set(TOKEN_COOKIE, tokens.token, { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: accessMaxAge });
  if (tokens.refreshToken) {
    // httpOnly + SameSite=Strict: never readable by JS, never sent cross-site.
    target.set(REFRESH_COOKIE, tokens.refreshToken, {
      httpOnly: true,
      secure,
      sameSite: "strict",
      path: "/",
      maxAge: secondsUntil(tokens.refreshTokenExpiresAt) ?? SEVEN_DAYS * 4,
    });
  }
}

export function clearAuthCookies(target: CookieWriter) {
  target.set(TOKEN_COOKIE, "", { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: 0 });
  target.set(REFRESH_COOKIE, "", { httpOnly: true, secure, sameSite: "strict", path: "/", maxAge: 0 });
}

/** Reject cross-site mutating requests (defence in depth on top of SameSite cookies). */
export function isSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (!origin) return true; // older browsers omit Origin on same-origin requests
  try {
    const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host");
    return new URL(origin).host === (host ?? new URL(request.url).host);
  } catch {
    return false;
  }
}
