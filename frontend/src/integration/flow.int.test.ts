// End-to-end contract check: real ASMS API behind the real Next.js BFF.
// Uses the same typed endpoint functions + zod schemas as the app, so any
// drift between docs/API.md and the frontend fails here.
//
// Prerequisites: API on :5001, `next start` on :3000, and an admin account
// (INT_ADMIN_EMAIL / INT_ADMIN_PASSWORD). Run: npm run test:integration
import { beforeAll, describe, expect, it } from "vitest";
import { createApiClient, type ApiClient } from "@/lib/api/client";
import { adminEndpoints } from "@/lib/api/endpoints/admin";
import { customerEndpoints } from "@/lib/api/endpoints/customer";
import { technicianEndpoints } from "@/lib/api/endpoints/technician";
import { isApiError } from "@/lib/api/errors";
import { localDateTimeInput } from "@/lib/format";

const WEB = process.env.INT_WEB_URL ?? "http://localhost:3000";
const ADMIN_EMAIL = process.env.INT_ADMIN_EMAIL ?? "";
const ADMIN_PASSWORD = process.env.INT_ADMIN_PASSWORD ?? "";
const stamp = Date.now().toString(36);
const PASSWORD = "Integr8tion!x";

/** Minimal cookie jar: remembers Set-Cookie from our BFF and replays it. */
class Session {
  cookies = new Map<string, string>();
  fetch: typeof fetch = async (input, init) => {
    const headers = new Headers(init?.headers);
    if (this.cookies.size) {
      headers.set("cookie", [...this.cookies].map(([k, v]) => `${k}=${v}`).join("; "));
    }
    const res = await fetch(input, { ...init, headers });
    for (const raw of res.headers.getSetCookie()) {
      const [pair] = raw.split(";");
      const [name, ...rest] = (pair ?? "").split("=");
      const value = rest.join("=");
      if (!name) continue;
      if (value && !/max-age=0/i.test(raw)) this.cookies.set(name.trim(), value);
      else this.cookies.delete(name.trim());
    }
    return res;
  };
  api(): ApiClient {
    return createApiClient({ baseUrl: `${WEB}/api/proxy`, fetchImpl: this.fetch });
  }
  auth(): ApiClient {
    return createApiClient({ baseUrl: WEB, fetchImpl: this.fetch });
  }
}

async function expectStatus(promise: Promise<unknown>, status: number | number[]) {
  const statuses = Array.isArray(status) ? status : [status];
  const error = await promise.then(
    () => null,
    (e: unknown) => e,
  );
  expect(isApiError(error), `expected ApiError ${statuses.join("/")}`).toBe(true);
  if (isApiError(error)) expect(statuses).toContain(error.status);
}

const customer = new Session();
const tech = new Session();
const admin = new Session();
const c = customerEndpoints(customer.api());
const t = technicianEndpoints(tech.api());
const a = adminEndpoints(admin.api());

let requestId = 0;
let billId = 0;
let disputeId = 0;
let withdrawalId = 0;

describe("ASMS through the Next.js BFF", () => {
  beforeAll(() => {
    if (!ADMIN_EMAIL || !ADMIN_PASSWORD)
      throw new Error("Set INT_ADMIN_EMAIL / INT_ADMIN_PASSWORD");
  });

  it("registers a customer and a technician and logs in the admin (httpOnly cookies)", async () => {
    const reg = (s: Session, body: Record<string, unknown>) =>
      s.auth().post<{ role: string }>("/api/auth/register", body);

    await expect(
      reg(customer, {
        role: "Customer",
        fullName: "Int Customer",
        email: `int-cust-${stamp}@asms.test`,
        phone: "9876543210",
        password: PASSWORD,
        confirmPassword: PASSWORD,
      }),
    ).resolves.toMatchObject({ role: "Customer" });
    await expect(
      reg(tech, {
        role: "Technician",
        fullName: "Int Technician",
        email: `int-tech-${stamp}@asms.test`,
        phone: "9876543211",
        password: PASSWORD,
        confirmPassword: PASSWORD,
        skill: "Plumbing",
      }),
    ).resolves.toMatchObject({ role: "Technician" });
    await admin.auth().post("/api/auth/login", { email: ADMIN_EMAIL, password: ADMIN_PASSWORD });

    for (const s of [customer, tech, admin]) {
      expect(s.cookies.has("asms_token")).toBe(true);
      expect(s.cookies.has("asms_refresh")).toBe(true);
    }
  });

  it("rejects bad credentials with 401 and protects pages", async () => {
    await expectStatus(
      new Session()
        .auth()
        .post("/api/auth/login", { email: ADMIN_EMAIL, password: "wrong-password" }),
      401,
    );
    const page = await fetch(`${WEB}/admin/dashboard`, { redirect: "manual" });
    expect(page.status).toBe(307);
    const wrongRole = await customer.fetch(`${WEB}/admin/dashboard`, { redirect: "manual" });
    expect(wrongRole.headers.get("location")).toContain("/403");
    const ok = await customer.fetch(`${WEB}/customer/dashboard`, { redirect: "manual" });
    expect(ok.status).toBe(200);
  });

  it("silently refreshes an expired/missing access token via the refresh cookie", async () => {
    customer.cookies.delete("asms_token");
    const dash = await c.getDashboard();
    expect(dash.stats.total).toBe(0);
    expect(customer.cookies.has("asms_token")).toBe(true);
  });

  it("books a request; a repeated Idempotency-Key does not double-book", async () => {
    const categories = await c.getCategories();
    expect(categories.map((x) => x.value)).toContain("Plumbing");
    const input = {
      category: "Plumbing" as const,
      description: "Integration test: kitchen tap leaking",
      scheduledDate: localDateTimeInput(5),
      address: "12 Test Street, Kochi",
      pinCode: "682001",
    };
    const key = crypto.randomUUID();
    const first = await c.createRequest(input, key);
    const replay = await c.createRequest(input, key);
    expect(replay.requestId).toBe(first.requestId);
    requestId = first.requestId;
    expect(await c.getRequests()).toHaveLength(1);
    const detail = await c.getRequest(requestId);
    expect(detail.status).toBe("Pending");
    expect(detail.canCancel).toBe(true);
  });

  it("admin sees it (paged, filtered, CSV) and assigns the technician", async () => {
    const paged = await a.getRequests({ search: String(requestId) }, 1, 20);
    expect(paged.items.some((r) => r.id === requestId)).toBe(true);
    expect(paged.totalCount).toBeGreaterThan(0);
    const csv = await a.exportRequestsCsv({ status: "Pending" });
    expect(await csv.blob.text()).toContain("Plumbing");
    const detail = await a.getRequest(requestId);
    expect(detail.canAssign).toBe(true);

    const techs = await a.getTechnicians({ search: `int-tech-${stamp}` });
    const me = techs.data[0];
    expect(me?.skill).toBe("Plumbing");
    expect(techs.counts.total).toBeGreaterThan(0);
    await a.assignTechnician(requestId, me!.id);
    expect((await a.getRequest(requestId)).status).toBe("Assigned");
  });

  it("technician accepts, bills, and can download the bill PDF", async () => {
    const jobs = await t.getJobs();
    expect(jobs.some((j) => j.id === requestId)).toBe(true);
    await t.acceptJob(requestId);
    const job = await t.getJob(requestId);
    expect(job.status).toBe("InProgress");
    expect(job.canBill).toBe(true);
    const { billId: id } = await t.createBill(requestId, {
      laborCost: 500,
      materialCost: 200,
      description: "Washer",
    });
    billId = id;
    const billed = await t.getJob(requestId);
    expect(billed.status).toBe("Billed");
    expect(billed.bill?.id).toBe(billId);
    const pdf = await tech.api().download(t.billPdfPath(billId));
    expect(pdf.blob.type).toContain("pdf");
  });

  it("customer quote, promo errors, Razorpay order (fake creds), cash choice, PDF", async () => {
    const quote = await c.getQuote(billId);
    expect(quote).toMatchObject({ billId, subtotal: 700, discount: 0, payable: 700 });
    await expectStatus(c.previewPromo(billId, "NOSUCHCODE"), 400);
    await expectStatus(c.redeemPoints(billId, 100), 422); // new customer has no points
    // Fake Razorpay credentials: the API must fail cleanly (502), never charge.
    await expectStatus(c.createRazorpayOrder(billId, crypto.randomUUID()), [502, 503, 504]);
    const cash = await c.chooseCash(billId);
    expect(cash.preferredPaymentMethod).toBe("Cash");
    const detail = await c.getRequest(requestId);
    expect(detail.bill?.preferredPaymentMethod).toBe("Cash");
    const pdf = await customer.api().download(c.billPdfPath(billId));
    expect(pdf.blob.size).toBeGreaterThan(500);
  });

  it("admin promo code lifecycle and customer applies it", async () => {
    const code = `INT${stamp}`.toUpperCase().slice(0, 20);
    await a.createPromoCode({
      code,
      description: "Integration flat 50",
      discountType: "Flat",
      discountValue: 50,
      minimumAmount: 100,
      isActive: true,
    });
    const codes = await a.getPromoCodes();
    const created = codes.find((p) => p.code === code);
    expect(created?.usedCount).toBe(0);

    const preview = await c.previewPromo(billId, code);
    expect(preview.discount).toBe(50);
    const applied = await c.applyPromo(billId, code);
    expect(applied.payable).toBe(650);
    await expectStatus(c.applyPromo(billId, code), 409);

    // A used code can be toggled but not deleted.
    await a.togglePromoCode(created!.id);
    await expectStatus(a.deletePromoCode(created!.id), 409);
  });

  it("technician confirms cash → completed; customer history + dispute", async () => {
    await t.confirmCashCollected(requestId);
    expect((await c.getRequest(requestId)).status).toBe("Completed");
    const history = await c.getHistory();
    expect(history.completedJobs).toBe(1);
    expect(history.items[0]?.id).toBe(requestId);

    await expectStatus(
      c.raiseDispute({ requestId, reason: "ServiceQuality", description: "too short" }),
      400,
    );
    const { id } = await c.raiseDispute({
      requestId,
      reason: "JobIncomplete",
      description: "Integration test: the tap still drips after the visit.",
    });
    disputeId = id;
    await expectStatus(
      c.raiseDispute({
        requestId,
        reason: "Other",
        description: "Integration test: second dispute for same job.",
      }),
      409,
    );
    expect((await c.getDisputes()).map((d) => d.id)).toContain(disputeId);
    expect((await c.getDispute(disputeId)).status).toBe("Open");
  });

  it("admin reviews and resolves the dispute (cash refund is refused with 422)", async () => {
    const list = await a.getDisputes("Open");
    expect(list.data.some((d) => d.id === disputeId)).toBe(true);
    expect(list.counts.Open).toBeGreaterThan(0);
    await a.startDisputeReview(disputeId, "Looking into it");
    await expectStatus(a.refundDispute(disputeId, 100, "cash job"), 422);
    await a.resolveDispute(disputeId, "ClosedNoAction", "Technician will revisit");
    expect((await c.getDispute(disputeId)).status).toBe("Resolved");
  });

  it("technician wallet + withdrawal; admin approves and pays it", async () => {
    const wallet = await t.getWallet();
    expect(wallet.totalJobsCompleted).toBe(1);
    expect(wallet.availableToWithdraw).toBeGreaterThan(100);
    await expectStatus(t.requestWithdrawal(wallet.availableToWithdraw + 1, "int@upi"), 422);
    const created = await t.requestWithdrawal(100, "int@upi");
    withdrawalId = created.id;
    const pending = await a.getWithdrawals("Pending");
    expect(pending.some((w) => w.id === withdrawalId)).toBe(true);
    await a.approveWithdrawal(withdrawalId);
    await a.payWithdrawal(withdrawalId);
    const after = await t.getWallet();
    expect(after.withdrawals.find((w) => w.id === withdrawalId)?.status).toBe("Paid");
  });

  it("admin dashboards, analytics, users, zones, auto-assign load and validate", async () => {
    const dash = await a.getDashboard();
    expect(dash.stats.totalRequests).toBeGreaterThan(0);
    const analytics = await a.getAnalytics();
    expect(analytics.totals.totalRequests).toBeGreaterThan(0);
    const users = await a.getUsers("Customer");
    expect(users.items.some((u) => u.email === `int-cust-${stamp}@asms.test`)).toBe(true);
    const zones = await a.getZones();
    expect(Array.isArray(zones)).toBe(true);
    await expectStatus(
      a.createZone({ zoneName: "", city: "", state: "", pinCodes: "12", isActive: true }),
      400,
    );
    const run = await a.runAutoAssign();
    expect(run.assigned).toBeGreaterThanOrEqual(0);
    // Technician account management round-trip.
    const me = (await a.getTechnicians({ search: `int-tech-${stamp}` })).data[0]!;
    await a.deactivateTechnician(me.id);
    await a.activateTechnician(me.id);
  });

  it("logout revokes the refresh token and clears cookies", async () => {
    const refresh = customer.cookies.get("asms_refresh");
    await customer.auth().post("/api/auth/logout");
    expect(customer.cookies.has("asms_token")).toBe(false);
    await expectStatus(c.getDashboard(), 401);
    // The revoked refresh token can no longer mint a session.
    const replay = new Session();
    replay.cookies.set("asms_refresh", refresh ?? "");
    await expectStatus(customerEndpoints(replay.api()).getDashboard(), 401);
  });
});
