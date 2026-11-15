import { NextResponse } from "next/server";
import { z } from "zod";
import { isApiError } from "@/lib/api/errors";
import { AUTH_PATHS, loginWithPassword, register } from "@/lib/auth/backend";
import { isSameOrigin, setAuthCookies } from "@/lib/auth/cookies";
import { registerSchema } from "@/lib/validation/auth";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) {
    return NextResponse.json({ success: false, error: "Cross-site request blocked." }, { status: 403 });
  }

  const parsed = registerSchema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json(
      { success: false, error: "Please check the form.", errors: z.flattenError(parsed.error).fieldErrors },
      { status: 400 },
    );
  }

  const { role, confirmPassword: _confirm, ...rest } = parsed.data;
  const path = role === "Technician" ? AUTH_PATHS.registerTechnician : AUTH_PATHS.registerCustomer;

  try {
    await register(path, rest);
  } catch (error) {
    const status = isApiError(error) ? error.status : 502;
    return NextResponse.json(
      {
        success: false,
        error: isApiError(error) ? error.message : "Registration failed.",
        errors: isApiError(error) ? error.fieldErrors : undefined,
      },
      { status },
    );
  }

  // Sign the new user in straight away.
  try {
    const login = await loginWithPassword(rest.email, rest.password);
    const res = NextResponse.json({ success: true, data: { role: login.role } }, { status: 201 });
    setAuthCookies(res, login.token, login.refreshToken);
    return res;
  } catch {
    // Account exists but auto-login failed (e.g. needs approval) – let them sign in manually.
    return NextResponse.json({ success: true, data: { role, requiresLogin: true } }, { status: 201 });
  }
}
