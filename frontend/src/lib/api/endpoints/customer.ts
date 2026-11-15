import { z } from "zod";
import type { ApiClient } from "../client";
import {
  categoryOptionSchema,
  createRequestResponseSchema,
  customerDashboardSchema,
  customerRequestDetailSchema,
  disputeSchema,
  paymentOrderSchema,
  paymentResultSchema,
  promoValidationSchema,
  promotionsSchema,
  requestSummarySchema,
} from "../schemas";
import type { RequestStatus, ServiceCategory } from "@/types/enums";

export interface CreateRequestInput {
  category: ServiceCategory;
  description: string;
  /** ISO local date-time, e.g. 2026-11-20T10:30 */
  scheduledDate: string;
  address: string;
  pinCode?: string;
}

export interface RazorpayVerifyInput {
  razorpayOrderId: string;
  razorpayPaymentId: string;
  razorpaySignature: string;
}

export interface PaymentOptionsInput {
  promoCode?: string;
  redeemPoints?: boolean;
}

export interface RaiseDisputeInput {
  requestId: number;
  reason: string;
  description: string;
}

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

    /** The idempotency key makes a double-submit return the same request instead of booking twice. */
    createRequest: (input: CreateRequestInput, idempotencyKey: string) =>
      api.post("/api/customer/requests", input, {
        headers: { "Idempotency-Key": idempotencyKey },
        schema: createRequestResponseSchema,
      }),

    cancelRequest: (id: number) => api.post<void>(`/api/customer/requests/${id}/cancel`),

    rateRequest: (id: number, stars: number, comment?: string) =>
      api.post<void>(`/api/customer/requests/${id}/rate`, { stars, comment }),

    /** Amount always comes from the server-side bill; options are sent for promo/points support. */
    createRazorpayOrder: (billId: number, options: PaymentOptionsInput, idempotencyKey: string) =>
      api.post(`/api/customer/bills/${billId}/razorpay/order`, options, {
        headers: { "Idempotency-Key": idempotencyKey },
        schema: paymentOrderSchema,
      }),

    verifyRazorpayPayment: (billId: number, input: RazorpayVerifyInput) =>
      api.post(`/api/customer/bills/${billId}/razorpay/verify`, input, {
        schema: paymentResultSchema,
      }),

    billPdfPath: (billId: number) => `/api/customer/bills/${billId}/pdf`,

    // TODO(api): promotions + disputes are proposed – not in docs/API.md yet
    getPromotions: () => api.get("/api/customer/promotions", { schema: promotionsSchema }),

    validatePromo: (code: string, billAmount: number) =>
      api.post(
        "/api/customer/promotions/validate",
        { code, billAmount },
        { schema: promoValidationSchema },
      ),

    getDisputes: () => api.get("/api/customer/disputes", { schema: z.array(disputeSchema) }),

    getDispute: (id: number) => api.get(`/api/customer/disputes/${id}`, { schema: disputeSchema }),

    raiseDispute: (input: RaiseDisputeInput) =>
      api.post("/api/customer/disputes", input, { schema: z.object({ id: z.number() }) }),
  };
}

export type CustomerEndpoints = ReturnType<typeof customerEndpoints>;
