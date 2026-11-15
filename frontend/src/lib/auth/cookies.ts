import "server-only";
// Cookie helpers for the auth route handlers.
import type { NextResponse } from "next/server";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "./constants";
import { decodeJwt } from "./jwt";

const secure = process.env.NODE_ENV === "production";
const SEVEN_DAYS = 60 * 60 * 24 * 7;

export function setAuthCookies(res: NextResponse, token: string, refreshToken?: string | null) {
  const user = decodeJwt(token);
  const maxAge = user?.exp ? Math.max(60, user.exp - Math.floor(Date.now() / 1000)) : SEVEN_DAYS;
  res.cookies.set(TOKEN_COOKIE, token, {
    httpOnly: true,
    secure,
    sameSite: "lax",
    path: "/",
    maxAge,
  });
  if (refreshToken) {
    // Only our /api/* route handlers ever need the refresh token.
    res.cookies.set(REFRESH_COOKIE, refreshToken, {
      httpOnly: true,
      secure,
      sameSite: "strict",
      path: "/api/",
      maxAge: SEVEN_DAYS * 4,
    });
  }
}

export function clearAuthCookies(res: NextResponse) {
  res.cookies.set(TOKEN_COOKIE, "", { httpOnly: true, secure, sameSite: "lax", path: "/", maxAge: 0 });
  res.cookies.set(REFRESH_COOKIE, "", {
    httpOnly: true,
    secure,
    sameSite: "strict",
    path: "/api/",
    maxAge: 0,
  });
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
