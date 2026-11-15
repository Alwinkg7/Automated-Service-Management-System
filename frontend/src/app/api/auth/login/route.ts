import { NextResponse } from "next/server";
import { z } from "zod";
import { isApiError } from "@/lib/api/errors";
import { loginWithPassword } from "@/lib/auth/backend";
import { isSameOrigin, setAuthCookies } from "@/lib/auth/cookies";
import { decodeJwt } from "@/lib/auth/jwt";

const bodySchema = z.object({
  email: z.email(),
  password: z.string().min(1),
});

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json(
      { success: false, error: "Cross-site request blocked." },
      { status: 403 },
    );
  }

  const parsed = bodySchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json(
      { success: false, error: "Email and password are required." },
      { status: 400 },
    );
  }

  try {
    const result = await loginWithPassword(parsed.data.email, parsed.data.password);
    const user = decodeJwt(result.token);
    const res = NextResponse.json({
      success: true,
      data: {
        user: {
          id: result.userId,
          role: result.role,
          name: result.name ?? user?.name ?? "",
          email: result.email ?? user?.email ?? parsed.data.email,
        },
      },
    });
    setAuthCookies(res.cookies, result);
    return res;
  } catch (error) {
    const status = isApiError(error) ? error.status : 502;
    const message = isApiError(error) ? error.message : "Login failed. Please try again.";
    const headers: Record<string, string> = {};
    if (isApiError(error) && error.retryAfterSeconds)
      headers["Retry-After"] = String(error.retryAfterSeconds);
    return NextResponse.json({ success: false, error: message }, { status, headers });
  }
}
