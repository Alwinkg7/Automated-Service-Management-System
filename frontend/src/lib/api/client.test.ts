import { describe, expect, it, vi } from "vitest";
import { z } from "zod";
import { buildUrl, createApiClient, unwrapEnvelope } from "./client";
import { ApiError } from "./errors";

function jsonResponse(body: unknown, init: ResponseInit = {}) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json", ...(init.headers ?? {}) },
    ...init,
  });
}

function clientWith(response: Response | (() => Promise<Response>), extra = {}) {
  const fetchImpl = vi.fn(async () => (typeof response === "function" ? response() : response.clone()));
  const client = createApiClient({ baseUrl: "http://api.test", fetchImpl, ...extra });
  return { client, fetchImpl };
}

describe("buildUrl", () => {
  it("skips empty query values", () => {
    expect(buildUrl("http://x", "/a", { s: "", n: 0, b: undefined, c: "Pending" })).toBe("http://x/a?n=0&c=Pending");
  });
});

describe("unwrapEnvelope", () => {
  it("returns data from the success envelope", () => {
    expect(unwrapEnvelope({ success: true, data: [1, 2] })).toEqual([1, 2]);
  });
  it("strips success when there is no data key", () => {
    expect(unwrapEnvelope({ success: true, stats: { a: 1 } })).toEqual({ stats: { a: 1 } });
  });
});

describe("createApiClient", () => {
  it("unwraps and validates with a schema", async () => {
    const { client } = clientWith(jsonResponse({ success: true, data: { id: 5, name: "x", extra: true } }));
    const result = await client.get("/api/thing", { schema: z.object({ id: z.number(), name: z.string() }) });
    expect(result).toEqual({ id: 5, name: "x" });
  });

  it("sends JSON body, bearer token and custom headers", async () => {
    const { client, fetchImpl } = clientWith(jsonResponse({ success: true }), { getToken: () => "tok" });
    await client.post("/api/customer/requests", { a: 1 }, { headers: { "Idempotency-Key": "k1" } });
    const [url, init] = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(url).toBe("http://api.test/api/customer/requests");
    expect(init.method).toBe("POST");
    expect(init.body).toBe('{"a":1}');
    expect(headers.Authorization).toBe("Bearer tok");
    expect(headers["Content-Type"]).toBe("application/json");
    expect(headers["Idempotency-Key"]).toBe("k1");
  });

  it("throws ApiError with the legacy envelope message", async () => {
    const { client } = clientWith(jsonResponse({ success: false, error: "Invalid category." }, { status: 400 }));
    await expect(client.get("/x")).rejects.toMatchObject({ status: 400, message: "Invalid category." });
  });

  it("parses ProblemDetails with field errors", async () => {
    const { client } = clientWith(
      jsonResponse(
        {
          type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
          title: "One or more validation errors occurred.",
          status: 400,
          detail: "Validation failed",
          traceId: "00-abc",
          errors: { PreferredDateTime: ["Pick a time at least 1 hour from now."] },
        },
        { status: 400, headers: { "content-type": "application/problem+json" } },
      ),
    );
    const error = (await client.get("/x").catch((e: unknown) => e)) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.message).toBe("Validation failed");
    expect(error.traceId).toBe("00-abc");
    expect(error.fieldErrors.preferredDateTime).toEqual(["Pick a time at least 1 hour from now."]);
  });

  it("treats a 200 with success:false as an error", async () => {
    const { client } = clientWith(jsonResponse({ success: false, error: "Already paid." }));
    await expect(client.post("/x")).rejects.toMatchObject({ message: "Already paid." });
  });

  it("never exposes raw server text for 5xx", async () => {
    const { client } = clientWith(jsonResponse({ success: false, error: "SqlException: boom" }, { status: 500 }));
    const error = (await client.get("/x").catch((e: unknown) => e)) as ApiError;
    expect(error.isServerError).toBe(true);
    expect(error.message).not.toContain("SqlException");
  });

  it("reads Retry-After on 429", async () => {
    const { client } = clientWith(
      jsonResponse({ title: "Too many" }, { status: 429, headers: { "retry-after": "12" } }),
    );
    const error = (await client.get("/x").catch((e: unknown) => e)) as ApiError;
    expect(error.retryAfterSeconds).toBe(12);
    expect(error.message).toContain("12s");
  });

  it("calls onUnauthorized for 401", async () => {
    const onUnauthorized = vi.fn();
    const { client } = clientWith(jsonResponse({ success: false, error: "nope" }, { status: 401 }), {
      onUnauthorized,
    });
    await expect(client.get("/x")).rejects.toMatchObject({ status: 401 });
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it("maps network failures to status 0", async () => {
    const { client } = clientWith(() => Promise.reject(new TypeError("fetch failed")));
    await expect(client.get("/x")).rejects.toMatchObject({ status: 0 });
  });

  it("rejects responses that don't match the schema", async () => {
    const { client } = clientWith(jsonResponse({ success: true, data: { id: "not-a-number" } }));
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    await expect(client.get("/x", { schema: z.object({ id: z.number() }) })).rejects.toMatchObject({
      code: "invalid_response",
    });
    spy.mockRestore();
  });

  it("returns undefined for 204 No Content", async () => {
    const { client } = clientWith(new Response(null, { status: 204 }));
    await expect(client.post("/x")).resolves.toBeUndefined();
  });
});
