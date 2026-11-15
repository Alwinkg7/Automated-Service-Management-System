import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { revokeRefreshToken } from "@/lib/auth/backend";
import { clearAuthCookies } from "@/lib/auth/cookies";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "@/lib/auth/constants";

export async function POST() {
  const jar = await cookies();
  const refresh = jar.get(REFRESH_COOKIE)?.value;
  if (refresh) await revokeRefreshToken(refresh, jar.get(TOKEN_COOKIE)?.value);

  const res = NextResponse.json({ success: true });
  clearAuthCookies(res);
  return res;
}
