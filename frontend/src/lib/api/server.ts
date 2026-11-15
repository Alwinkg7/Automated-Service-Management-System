import "server-only";
// Server-side API client (Server Components / Route Handlers).
// Reads the JWT from the httpOnly cookie and talks to the API directly.
import { cookies } from "next/headers";
import { serverApiUrl } from "@/lib/env";
import { TOKEN_COOKIE } from "@/lib/auth/constants";
import { createApiClient } from "./client";

export const serverApi = createApiClient({
  baseUrl: serverApiUrl(),
  getToken: async () => (await cookies()).get(TOKEN_COOKIE)?.value,
});

/** Unauthenticated server client (public data). */
export const publicServerApi = createApiClient({ baseUrl: serverApiUrl() });
