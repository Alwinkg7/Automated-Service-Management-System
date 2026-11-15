// BFF proxy: the browser calls /api/proxy/api/..., we forward to the ASMS API
// with the JWT from the httpOnly cookie. Keeps the token out of JavaScript and
// avoids CORS for normal API traffic.
import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { refreshTokens } from "@/lib/auth/backend";
import { clearAuthCookies, isSameOrigin, setAuthCookies } from "@/lib/auth/cookies";
import { REFRESH_COOKIE, TOKEN_COOKIE } from "@/lib/auth/constants";
import { decodeJwt, isExpired } from "@/lib/auth/jwt";
import { serverApiUrl } from "@/lib/env";

const FORWARD_REQUEST_HEADERS = ["accept", "content-type", "idempotency-key", "x-request-id"];
const FORWARD_RESPONSE_HEADERS = ["content-type", "content-disposition", "retry-after", "x-total-count", "location"];

type Context = { params: Promise<{ path: string[] }> };

async function forward(request: NextRequest, path: string[], body: ArrayBuffer | undefined, token?: string) {
  const target = `${serverApiUrl()}/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`;
  const headers = new Headers();
  for (const name of FORWARD_REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value) headers.set(name, value);
  }
  if (token) headers.set("authorization", `Bearer ${token}`);

  return fetch(target, {
    method: request.method,
    headers,
    body: body && body.byteLength > 0 ? body : undefined,
    cache: "no-store",
    redirect: "manual",
  });
}

async function handle(request: NextRequest, { params }: Context) {
  const { path } = await params;

  // Only the REST API is reachable through the proxy.
  if (path[0] !== "api" || path.includes("..")) {
    return NextResponse.json({ success: false, error: "Not found." }, { status: 404 });
  }
  if (request.method !== "GET" && !isSameOrigin(request)) {
    return NextResponse.json({ success: false, error: "Cross-site request blocked." }, { status: 403 });
  }

  const jar = await cookies();
  const token = jar.get(TOKEN_COOKIE)?.value;
  const refresh = jar.get(REFRESH_COOKIE)?.value;
  const body = request.method === "GET" || request.method === "HEAD" ? undefined : await request.arrayBuffer();

  // Access token already expired (short-lived JWTs): refresh before calling.
  let renewed: Awaited<ReturnType<typeof refreshTokens>> = null;
  let accessToken = token;
  const current = decodeJwt(token);
  if (refresh && (!current || isExpired(current))) {
    renewed = await refreshTokens(refresh);
    if (renewed) accessToken = renewed.token;
  }

  let upstream: Response;
  try {
    upstream = await forward(request, path, body, accessToken);
  } catch {
    return NextResponse.json(
      { success: false, error: "The service is unreachable. Please try again shortly." },
      { status: 502 },
    );
  }

  // Rejected anyway (revoked/rotated): try one silent refresh, then retry.
  if (upstream.status === 401 && refresh && !renewed) {
    renewed = await refreshTokens(refresh);
    if (renewed) upstream = await forward(request, path, body, renewed.token);
  }

  const headers = new Headers();
  for (const name of FORWARD_RESPONSE_HEADERS) {
    const value = upstream.headers.get(name);
    if (value) headers.set(name, value);
  }
  headers.set("cache-control", "no-store");

  const res = new NextResponse(upstream.body, { status: upstream.status, headers });
  if (renewed) setAuthCookies(res.cookies, renewed);
  else if (upstream.status === 401) clearAuthCookies(res.cookies);
  return res;
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const PATCH = handle;
export const DELETE = handle;
