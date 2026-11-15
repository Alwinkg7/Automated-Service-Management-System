// The one fetch wrapper every API call goes through.
// - adds JSON headers and (server side) the Bearer token
// - unwraps the `{ success, data }` envelope
// - validates the payload with zod when a schema is given
// - throws a typed ApiError for any failure
import type { z } from "zod";
import { ApiError, toApiError } from "./errors";

export type QueryValue = string | number | boolean | null | undefined;

export interface RequestOptions<T> {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  query?: Record<string, QueryValue>;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  /** Validate (and narrow) the unwrapped payload. */
  schema?: z.ZodType<T>;
  cache?: RequestCache;
  next?: { revalidate?: number | false; tags?: string[] };
}

export interface ApiClientConfig {
  baseUrl: string;
  /** Returns the JWT to send as Bearer (server side). Browser calls go through the BFF and omit it. */
  getToken?: () => string | undefined | Promise<string | undefined>;
  /** Called once for any 401 (e.g. redirect to /login). The error is still thrown. */
  onUnauthorized?: (error: ApiError) => void;
  fetchImpl?: typeof fetch;
}

export interface ApiClient {
  request<T = unknown>(path: string, options?: RequestOptions<T>): Promise<T>;
  get<T = unknown>(path: string, options?: Omit<RequestOptions<T>, "method" | "body">): Promise<T>;
  post<T = unknown>(path: string, body?: unknown, options?: Omit<RequestOptions<T>, "method" | "body">): Promise<T>;
  put<T = unknown>(path: string, body?: unknown, options?: Omit<RequestOptions<T>, "method" | "body">): Promise<T>;
  patch<T = unknown>(path: string, body?: unknown, options?: Omit<RequestOptions<T>, "method" | "body">): Promise<T>;
  delete<T = unknown>(path: string, options?: Omit<RequestOptions<T>, "method" | "body">): Promise<T>;
  /** Fetch a binary file (PDF / CSV). */
  download(path: string, options?: Pick<RequestOptions<never>, "query" | "signal">): Promise<DownloadedFile>;
}

export interface DownloadedFile {
  blob: Blob;
  fileName?: string;
}

export function buildUrl(baseUrl: string, path: string, query?: Record<string, QueryValue>) {
  const url = `${baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  if (!query) return url;
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "") continue;
    params.set(key, String(value));
  }
  const qs = params.toString();
  return qs ? `${url}${url.includes("?") ? "&" : "?"}${qs}` : url;
}

type UnknownRecord = Record<string, unknown>;

function isRecord(value: unknown): value is UnknownRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * `{ success:true, data }` → data.
 * `{ success:true, stats, ... }` (no data key) → the body without `success`.
 * Anything else is returned as-is.
 */
export function unwrapEnvelope(body: unknown): unknown {
  if (!isRecord(body)) return body;
  if ("data" in body && (body.success === true || Object.keys(body).length <= 2)) return body.data;
  if (body.success === true) {
    const { success: _success, ...rest } = body;
    return rest;
  }
  return body;
}

async function readBody(res: Response): Promise<unknown> {
  if (res.status === 204 || res.status === 205) return undefined;
  const text = await res.text();
  if (!text) return undefined;
  const type = res.headers.get("content-type") ?? "";
  if (type.includes("json") || /^[[{]/.test(text.trim())) {
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return text;
    }
  }
  return text;
}

function fileNameFrom(disposition: string | null): string | undefined {
  if (!disposition) return undefined;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star?.[1]) return decodeURIComponent(star[1]);
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain?.[1];
}

export function createApiClient(config: ApiClientConfig): ApiClient {
  const doFetch = config.fetchImpl ?? ((...args: Parameters<typeof fetch>) => fetch(...args));

  async function send(path: string, options: RequestOptions<unknown>, accept: string) {
    const headers: Record<string, string> = { Accept: accept, ...options.headers };
    let body: BodyInit | undefined;
    if (options.body !== undefined) {
      if (options.body instanceof FormData) {
        body = options.body; // browser sets multipart boundary
      } else {
        headers["Content-Type"] = "application/json";
        body = JSON.stringify(options.body);
      }
    }
    const token = config.getToken ? await config.getToken() : undefined;
    if (token) headers.Authorization = `Bearer ${token}`;

    let res: Response;
    try {
      res = await doFetch(buildUrl(config.baseUrl, path, options.query), {
        method: options.method ?? "GET",
        headers,
        body,
        signal: options.signal,
        cache: options.cache,
        ...(options.next ? { next: options.next } : {}),
      } as RequestInit);
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") throw err;
      throw toApiError(0, undefined);
    }

    if (!res.ok) {
      const errBody = await readBody(res);
      const error = toApiError(res.status, errBody, res.headers.get("retry-after"));
      if (res.status === 401) config.onUnauthorized?.(error);
      throw error;
    }
    return res;
  }

  async function request<T>(path: string, options: RequestOptions<T> = {}): Promise<T> {
    const res = await send(path, options as RequestOptions<unknown>, "application/json");
    const raw = await readBody(res);

    // Some endpoints answer 200 with `{ success:false, error }`.
    if (isRecord(raw) && raw.success === false) {
      throw toApiError(400, raw);
    }

    const data = unwrapEnvelope(raw);
    if (!options.schema) return data as T;

    const parsed = options.schema.safeParse(data);
    if (!parsed.success) {
      console.error(`[api] Unexpected response shape for ${path}`, parsed.error.issues);
      throw new ApiError({
        status: 502,
        code: "invalid_response",
        message: "We received an unexpected response from the server.",
      });
    }
    return parsed.data;
  }

  return {
    request,
    get: (path, options) => request(path, { ...options, method: "GET" }),
    post: (path, body, options) => request(path, { ...options, method: "POST", body }),
    put: (path, body, options) => request(path, { ...options, method: "PUT", body }),
    patch: (path, body, options) => request(path, { ...options, method: "PATCH", body }),
    delete: (path, options) => request(path, { ...options, method: "DELETE" }),
    async download(path, options = {}) {
      const res = await send(path, { ...options, method: "GET" }, "*/*");
      return {
        blob: await res.blob(),
        fileName: fileNameFrom(res.headers.get("content-disposition")),
      };
    },
  };
}
