// Route protection (Next.js 16 "proxy", formerly middleware).
// UX only: the API enforces auth on every call regardless.
import { NextResponse, type NextRequest } from "next/server";
import { refreshTokens } from "@/lib/auth/backend";
import { setAuthCookies } from "@/lib/auth/cookies";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "@/lib/auth/constants";
import { decideRoute } from "@/lib/auth/guard";
import { decodeJwt, isExpired, type SessionUser } from "@/lib/auth/jwt";
import type { LoginResponse } from "@/lib/api/schemas";

export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  let user: SessionUser | null = decodeJwt(request.cookies.get(TOKEN_COOKIE)?.value);
  let renewed: LoginResponse | null = null;

  // Access tokens are short-lived: renew silently with the refresh cookie.
  const refresh = request.cookies.get(REFRESH_COOKIE)?.value;
  if (refresh && (!user || isExpired(user))) {
    renewed = await refreshTokens(refresh);
    if (renewed) {
      user = decodeJwt(renewed.token);
      // Let server components in this same request see the new token.
      request.cookies.set(TOKEN_COOKIE, renewed.token);
    }
  }

  const decision = decideRoute(pathname, search, user);
  const res =
    decision.action === "redirect"
      ? NextResponse.redirect(new URL(decision.to, request.url))
      : NextResponse.next({ request: { headers: request.headers } });

  if (renewed) setAuthCookies(res.cookies, renewed);
  // Keep logged-in areas out of search engines.
  if (/^\/(customer|technician|admin)(\/|$)/.test(pathname)) {
    res.headers.set("X-Robots-Tag", "noindex, nofollow");
  }
  return res;
}

export const config = {
  matcher: ["/customer/:path*", "/technician/:path*", "/admin/:path*", "/login", "/register"],
};
