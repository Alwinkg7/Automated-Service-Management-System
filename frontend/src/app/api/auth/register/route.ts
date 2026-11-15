import { NextResponse } from "next/server";
import { z } from "zod";
import { isApiError } from "@/lib/api/errors";
import { loginResponseSchema } from "@/lib/api/schemas";
import { AUTH_PATHS, register } from "@/lib/auth/backend";
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
    // The API registers and signs the user in, returning the same body as /login.
    const body = await register(path, rest);
    const tokens = loginResponseSchema.safeParse(body);
    if (!tokens.success) {
      return NextResponse.json({ success: true, data: { role, requiresLogin: true } }, { status: 201 });
    }
    const res = NextResponse.json({ success: true, data: { role: tokens.data.role } }, { status: 201 });
    setAuthCookies(res.cookies, tokens.data);
    return res;
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
}
