import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { TOKEN_COOKIE } from "@/lib/auth/constants";
import { decodeJwt, isExpired } from "@/lib/auth/jwt";

// Hands the JWT to the SignalR client only (WebSockets can't use our proxy).
// Same-origin GET; CORS stops other sites from reading the response.
export async function GET() {
  const token = (await cookies()).get(TOKEN_COOKIE)?.value;
  const user = decodeJwt(token);
  if (!token || !user || isExpired(user)) {
    return NextResponse.json({ success: false, error: "Not signed in." }, { status: 401 });
  }
  return NextResponse.json(
    { success: true, data: { token } },
    { headers: { "Cache-Control": "no-store" } },
  );
}
