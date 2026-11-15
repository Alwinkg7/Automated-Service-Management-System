import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { refreshTokens } from "@/lib/auth/backend";
import { setAuthCookies } from "@/lib/auth/cookies";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "@/lib/auth/constants";
import { decodeJwt, isExpired } from "@/lib/auth/jwt";

// Hands a fresh JWT to the SignalR client only (WebSockets can't go through our
// proxy). Same-origin GET; CORS stops other sites from reading the response.
export async function GET() {
  const jar = await cookies();
  let token = jar.get(TOKEN_COOKIE)?.value;
  const user = decodeJwt(token);

  if (!token || !user || isExpired(user, Date.now(), 60)) {
    const refresh = jar.get(REFRESH_COOKIE)?.value;
    const renewed = refresh ? await refreshTokens(refresh) : null;
    if (!renewed) {
      return NextResponse.json({ success: false, error: "Not signed in." }, { status: 401 });
    }
    setAuthCookies(jar, renewed);
    token = renewed.token;
  }

  return NextResponse.json({ success: true, data: { token } }, { headers: { "Cache-Control": "no-store" } });
}
