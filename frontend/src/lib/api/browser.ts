"use client";
// Browser-side API client. Calls go to our own /api/proxy route handler,
// which attaches the JWT from the httpOnly cookie, so the token never touches JS.
import { BROWSER_PROXY_BASE } from "@/lib/env";
import { createApiClient } from "./client";

let redirecting = false;

export const browserApi = createApiClient({
  baseUrl: BROWSER_PROXY_BASE,
  onUnauthorized: () => {
    if (typeof window === "undefined" || redirecting) return;
    redirecting = true;
    const next = encodeURIComponent(window.location.pathname + window.location.search);
    // Clear cookies server-side, then go to login.
    void fetch("/api/auth/logout", { method: "POST" }).finally(() => {
      window.location.assign(`/login?next=${next}&expired=1`);
    });
  },
});
