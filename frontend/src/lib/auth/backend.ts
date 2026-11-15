// Server-side calls (route handlers and proxy.ts; no secrets here) to the API's /api/auth/* endpoints, used by our route handlers.
import { serverApiUrl } from "@/lib/env";
import { createApiClient } from "@/lib/api/client";
import { loginResponseSchema, type LoginResponse } from "@/lib/api/schemas";

const authApi = createApiClient({ baseUrl: serverApiUrl() });

export const AUTH_PATHS = {
  login: "/api/auth/login",
  registerCustomer: "/api/auth/register/customer",
  registerTechnician: "/api/auth/register/technician",
  refresh: "/api/auth/refresh",
  logout: "/api/auth/logout",
} as const;

export function loginWithPassword(email: string, password: string): Promise<LoginResponse> {
  return authApi.post(
    AUTH_PATHS.login,
    { email, password },
    { schema: loginResponseSchema, cache: "no-store" },
  );
}

export function register(path: string, body: unknown): Promise<unknown> {
  return authApi.post(path, body, { cache: "no-store" });
}

/** Exchange a refresh token for a new access token. Returns null when refresh isn't possible. */
export async function refreshTokens(refreshToken: string): Promise<LoginResponse | null> {
  try {
    return await authApi.post(
      AUTH_PATHS.refresh,
      { refreshToken },
      { schema: loginResponseSchema, cache: "no-store" },
    );
  } catch {
    return null;
  }
}

/** Best-effort server-side revoke; logout must succeed locally regardless. */
export async function revokeRefreshToken(refreshToken: string, accessToken?: string) {
  try {
    const client = createApiClient({ baseUrl: serverApiUrl(), getToken: () => accessToken });
    await client.post(AUTH_PATHS.logout, { refreshToken }, { cache: "no-store" });
  } catch {
    /* ignore */
  }
}
