// Customer endpoints – see docs/API.md "Customer".
import { z } from "zod";
import type { ApiClient } from "../client";
import {
  cashChoiceSchema,
  categoryOptionSchema,
  createRequestResponseSchema,
  customerDashboardSchema,
  customerRequestDetailSchema,
  disputeSchema,
  historySchema,
  paymentOrderSchema,
  paymentResultSchema,
  promoValidationSchema,
  promotionsSchema,
  quoteSchema,
  requestSummarySchema,
} from "../schemas";
import type { RequestStatus, ServiceCategory } from "@/types/enums";

export interface CreateRequestInput {
  category: ServiceCategory;
  description: string;
  /** Local date-time, e.g. 2026-11-20T10:30 */
  scheduledDate: string;
  address: string;
  pinCode?: string;
}

export interface RebookInput {
  scheduledDate: string;
  description?: string;
  address?: string;
}

export interface RazorpayVerifyInput {
  razorpayOrderId: string;
  razorpayPaymentId: string;
  razorpaySignature: string;
}

export interface RaiseDisputeInput {
  requestId: number;
  reason: string;
  description: string;
}

const idempotent = (key: string) => ({ "Idempotency-Key": key });

export function customerEndpoints(api: ApiClient) {
  return {
    getDashboard: () => api.get("/api/customer/dashboard", { schema: customerDashboardSchema }),

    getRequests: (status?: RequestStatus) =>
      api.get("/api/customer/requests", {
        query: { status },
        schema: z.array(requestSummarySchema),
      }),

    getRequest: (id: number) =>
      api.get(`/api/customer/requests/${id}`, { schema: customerRequestDetailSchema }),

    getCategories: () =>
      api.get("/api/customer/categories", { schema: z.array(categoryOptionSchema) }),

    /** A repeated Idempotency-Key returns the original requestId instead of booking twice. */
    createRequest: (input: CreateRequestInput, idempotencyKey: string) =>
      api.post("/api/customer/requests", input, {
        headers: idempotent(idempotencyKey),
        schema: createRequestResponseSchema,
      }),

    rebook: (id: number, input: RebookInput, idempotencyKey: string) =>
      api.post(`/api/customer/requests/${id}/rebook`, input, {
        headers: idempotent(idempotencyKey),
        schema: createRequestResponseSchema,
      }),

    cancelRequest: (id: number) => api.post<void>(`/api/customer/requests/${id}/cancel`),

    rateRequest: (id: number, stars: number, comment?: string) =>
      api.post<void>(`/api/customer/requests/${id}/rate`, { stars, comment }),

    /** Completed + cancelled requests with totals; filters run in SQL. */
    getHistory: (search?: string, category?: string) =>
      api.get("/api/customer/history", { query: { search, category }, schema: historySchema }),

    // ── Bill: discounts and payment ───────────────────────────────
    getQuote: (billId: number) =>
      api.get(`/api/customer/bills/${billId}/quote`, { schema: quoteSchema }),

    previewPromo: (billId: number, code: string) =>
      api.post(`/api/customer/bills/${billId}/promo/preview`, { code }, { schema: quoteSchema }),

    applyPromo: (billId: number, code: string) =>
      api.post(`/api/customer/bills/${billId}/promo`, { code }, { schema: quoteSchema }),

    /** Multiples of 100 points (100 points = ₹10). */
    redeemPoints: (billId: number, points: number) =>
      api.post(`/api/customer/bills/${billId}/points`, { points }, { schema: quoteSchema }),

    /** Records "I'll pay cash"; the technician completes the job when collected. */
    chooseCash: (billId: number) =>
      api.post(`/api/customer/bills/${billId}/cash`, undefined, { schema: cashChoiceSchema }),

    /** Amount (paise) is the server-side discounted payable. */
    createRazorpayOrder: (billId: number, idempotencyKey: string) =>
      api.post(`/api/customer/bills/${billId}/razorpay/order`, undefined, {
        headers: idempotent(idempotencyKey),
        schema: paymentOrderSchema,
      }),

    verifyRazorpayPayment: (billId: number, input: RazorpayVerifyInput) =>
      api.post(`/api/customer/bills/${billId}/razorpay/verify`, input, {
        schema: paymentResultSchema,
      }),

    billPdfPath: (billId: number) => `/api/customer/bills/${billId}/pdf`,

    // ── Rewards ───────────────────────────────────────────────────
    getPromotions: () => api.get("/api/customer/promotions", { schema: promotionsSchema }),

    validatePromo: (code: string, billAmount: number) =>
      api.post(
        "/api/customer/promotions/validate",
        { code, billAmount },
        { schema: promoValidationSchema },
      ),

    // ── Disputes ──────────────────────────────────────────────────
    getDisputes: () => api.get("/api/customer/disputes", { schema: z.array(disputeSchema) }),

    getDispute: (id: number) => api.get(`/api/customer/disputes/${id}`, { schema: disputeSchema }),

    raiseDispute: (input: RaiseDisputeInput) =>
      api.post("/api/customer/disputes", input, { schema: z.object({ id: z.number() }) }),
  };
}

export type CustomerEndpoints = ReturnType<typeof customerEndpoints>;
