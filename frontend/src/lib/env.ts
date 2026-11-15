// Central place for environment-derived URLs. Only NEXT_PUBLIC_* values reach the browser.

function trimSlash(url: string) {
  return url.replace(/\/+$/, "");
}

/** Public API origin (browser-visible). Used for the SignalR hub. */
export const PUBLIC_API_URL = trimSlash(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5001");

/** SignalR hub URL. */
export const HUB_URL = `${PUBLIC_API_URL}/hubs/service`;

/** Public site URL for canonical links / sitemap. */
export const SITE_URL = trimSlash(process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000");

/** API origin used from the Next.js server (BFF proxy, server components). */
export function serverApiUrl() {
  return trimSlash(process.env.API_URL ?? PUBLIC_API_URL);
}

/** Same-origin prefix the browser uses; the route handler forwards to the API with the JWT. */
export const BROWSER_PROXY_BASE = "/api/proxy";
