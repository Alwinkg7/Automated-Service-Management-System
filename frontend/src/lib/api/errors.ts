// Typed API error that understands both the legacy envelope `{ success:false, error }`
// and RFC 7807 ProblemDetails `{ type, title, status, detail, traceId, errors }`.

export type FieldErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: FieldErrors;
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;
  readonly code?: string;

  constructor(init: {
    status: number;
    message: string;
    fieldErrors?: FieldErrors;
    traceId?: string;
    retryAfterSeconds?: number;
    code?: string;
  }) {
    super(init.message);
    this.name = "ApiError";
    this.status = init.status;
    this.fieldErrors = init.fieldErrors ?? {};
    this.traceId = init.traceId;
    this.retryAfterSeconds = init.retryAfterSeconds;
    this.code = init.code;
  }

  get isUnauthorized() {
    return this.status === 401;
  }
  get isForbidden() {
    return this.status === 403;
  }
  get isNotFound() {
    return this.status === 404;
  }
  get isConflict() {
    return this.status === 409;
  }
  get isValidation() {
    return this.status === 400 || this.status === 422;
  }
  get isServerError() {
    return this.status >= 500;
  }
}

export function isApiError(value: unknown): value is ApiError {
  return value instanceof ApiError;
}

/** Messages we show when the server did not give a usable one (or must not be shown). */
export function defaultMessageForStatus(status: number, retryAfterSeconds?: number): string {
  switch (status) {
    case 0:
      return "Could not reach the server. Check your connection and try again.";
    case 400:
    case 422:
      return "Please check the highlighted fields and try again.";
    case 401:
      return "Your session has expired. Please sign in again.";
    case 403:
      return "You don't have access to this.";
    case 404:
      return "We couldn't find what you were looking for.";
    case 409:
      return "This was changed by someone else. Refresh and try again.";
    case 415:
      return "Something went wrong sending your request.";
    case 429:
      return retryAfterSeconds
        ? `Too many attempts. Try again in ${retryAfterSeconds}s.`
        : "Too many attempts. Please wait a moment and try again.";
    default:
      return status >= 500
        ? "Something went wrong on our side. Please try again in a moment."
        : "Something went wrong. Please try again.";
  }
}

type UnknownRecord = Record<string, unknown>;

function isRecord(value: unknown): value is UnknownRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function firstString(...values: unknown[]): string | undefined {
  for (const v of values) {
    if (typeof v === "string" && v.trim().length > 0) return v;
  }
  return undefined;
}

/** ASP.NET returns "PreferredDateTime"; our forms use "preferredDateTime". */
function camelKey(key: string) {
  const last = key.split(".").pop() ?? key;
  return last.charAt(0).toLowerCase() + last.slice(1);
}

function parseFieldErrors(errors: unknown): FieldErrors {
  const out: FieldErrors = {};
  if (isRecord(errors)) {
    for (const [key, val] of Object.entries(errors)) {
      const messages = Array.isArray(val)
        ? val.filter((m): m is string => typeof m === "string")
        : typeof val === "string"
          ? [val]
          : [];
      if (messages.length) out[camelKey(key)] = messages;
    }
  } else if (Array.isArray(errors)) {
    // Identity-style: [{ code, description }]
    const messages = errors
      .map((e) => (isRecord(e) ? firstString(e.description, e.message) : undefined))
      .filter((m): m is string => !!m);
    if (messages.length) out[""] = messages;
  }
  return out;
}

export function parseRetryAfter(header: string | null): number | undefined {
  if (!header) return undefined;
  const seconds = Number(header);
  if (Number.isFinite(seconds)) return Math.max(0, Math.round(seconds));
  const date = Date.parse(header);
  if (Number.isNaN(date)) return undefined;
  return Math.max(0, Math.round((date - Date.now()) / 1000));
}

/** Build an ApiError from a failed response body (already JSON-parsed, or a raw string). */
export function toApiError(status: number, body: unknown, retryAfterHeader: string | null = null) {
  const retryAfterSeconds = status === 429 ? parseRetryAfter(retryAfterHeader) : undefined;
  let serverMessage: string | undefined;
  let fieldErrors: FieldErrors = {};
  let traceId: string | undefined;
  let code: string | undefined;

  if (isRecord(body)) {
    fieldErrors = parseFieldErrors(body.errors);
    // ProblemDetails puts the human text in `detail`; the envelope uses `error`.
    serverMessage = firstString(
      body.detail,
      body.error,
      isRecord(body.error) ? body.error.message : undefined,
      body.message,
      body.title,
    );
    traceId = firstString(body.traceId);
    code = firstString(body.code, body.type);
  }

  // Never surface raw server text for 5xx – it may leak internals.
  const message =
    status >= 500 || status === 0 || status === 415 || !serverMessage
      ? defaultMessageForStatus(status, retryAfterSeconds)
      : status === 429
        ? defaultMessageForStatus(status, retryAfterSeconds)
        : serverMessage;

  if (status === 415 && typeof console !== "undefined") {
    console.error("[api] 415 Unsupported Media Type – check the request Content-Type.");
  }

  return new ApiError({ status, message, fieldErrors, traceId, retryAfterSeconds, code });
}

/** Human message for anything thrown (ApiError, network error, unknown). */
export function errorMessage(error: unknown): string {
  if (isApiError(error)) return error.message;
  if (error instanceof Error && error.name === "AbortError") return "Request was cancelled.";
  return defaultMessageForStatus(0);
}
