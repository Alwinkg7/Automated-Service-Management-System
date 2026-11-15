// Technician endpoints – see docs/API.md "Technician".
import { z } from "zod";
import type { ApiClient } from "../client";
import {
  jobDetailSchema,
  jobSummarySchema,
  technicianDashboardSchema,
  toggleStatusSchema,
  walletSchema,
  withdrawalSchema,
} from "../schemas";
import type { RequestStatus } from "@/types/enums";

export interface CreateBillInput {
  laborCost: number;
  materialCost: number;
  description?: string;
}

export function technicianEndpoints(api: ApiClient) {
  return {
    getDashboard: () => api.get("/api/technician/dashboard", { schema: technicianDashboardSchema }),

    toggleStatus: () =>
      api.post("/api/technician/toggle-status", undefined, { schema: toggleStatusSchema }),

    getJobs: (status?: RequestStatus) =>
      api.get("/api/technician/jobs", { query: { status }, schema: z.array(jobSummarySchema) }),

    getJob: (id: number) => api.get(`/api/technician/jobs/${id}`, { schema: jobDetailSchema }),

    acceptJob: (id: number) => api.post<void>(`/api/technician/jobs/${id}/accept`),

    rejectJob: (id: number) => api.post<void>(`/api/technician/jobs/${id}/reject`),

    /** Technician confirms CASH was collected: records a cash payment, Billed -> Completed. */
    confirmCashCollected: (id: number) =>
      api.post(`/api/technician/jobs/${id}/complete`, undefined, {
        schema: z.object({ paymentId: z.number().nullish() }).nullish(),
      }),

    createBill: (id: number, input: CreateBillInput) =>
      api.post(`/api/technician/jobs/${id}/bill`, input, {
        schema: z.object({ billId: z.number() }),
      }),

    billPdfPath: (billId: number) => `/api/technician/bills/${billId}/pdf`,

    getWallet: () => api.get("/api/technician/wallet", { schema: walletSchema }),

    requestWithdrawal: (amount: number, upiId: string) =>
      api.post(
        "/api/technician/wallet/withdrawals",
        { amount, upiId },
        { schema: z.object({ id: z.number(), withdrawal: withdrawalSchema.nullish() }) },
      ),
  };
}

export type TechnicianEndpoints = ReturnType<typeof technicianEndpoints>;
