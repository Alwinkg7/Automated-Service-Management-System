// Route protection (Next.js 16 "proxy", formerly middleware).
// Only UX: the API enforces auth on every call regardless.
import { NextResponse, type NextRequest } from "next/server";
import { TOKEN_COOKIE } from "@/lib/auth/constants";
import { decideRoute } from "@/lib/auth/guard";
import { decodeJwt } from "@/lib/auth/jwt";

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const user = decodeJwt(request.cookies.get(TOKEN_COOKIE)?.value);
  const decision = decideRoute(pathname, search, user);

  if (decision.action === "redirect") {
    return NextResponse.redirect(new URL(decision.to, request.url));
  }

  const res = NextResponse.next();
  // Keep logged-in areas out of search engines.
  if (/^\/(customer|technician|admin)(\/|$)/.test(pathname)) {
    res.headers.set("X-Robots-Tag", "noindex, nofollow");
  }
  return res;
}

export const config = {
  matcher: ["/customer/:path*", "/technician/:path*", "/admin/:path*", "/login", "/register"],
};
