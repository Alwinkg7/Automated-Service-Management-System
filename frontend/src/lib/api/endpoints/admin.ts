import { z } from "zod";
import type { ApiClient } from "../client";
import {
  adminDashboardSchema,
  adminRequestDetailSchema,
  adminRequestsSchema,
  adminUsersSchema,
  availableTechnicianSchema,
  disputeSchema,
  promoCodeSchema,
  withdrawalSchema,
  zoneSchema,
} from "../schemas";
import type { RequestStatus, ServiceCategory, UserRole } from "@/types/enums";

export interface ZoneInput {
  zoneName: string;
  city: string;
  state: string;
  pinCodes: string;
  isActive: boolean;
}

export interface PromoCodeInput {
  code: string;
  description: string;
  discountType: string;
  discountValue: number;
  maxDiscount?: number | null;
  minimumAmount: number;
  maxUses?: number | null;
  expiresAt?: string | null;
  isActive: boolean;
}

export function adminEndpoints(api: ApiClient) {
  return {
    getDashboard: () => api.get("/api/admin/dashboard", { schema: adminDashboardSchema }),

    // Requests
    /** All requests (the API doesn't page yet); search/date/paging happen client-side. */
    getRequests: (status?: RequestStatus) =>
      api.get("/api/admin/requests", { query: { status }, schema: adminRequestsSchema }),

    getRequest: (id: number) =>
      api.get(`/api/admin/requests/${id}`, { schema: adminRequestDetailSchema }),

    getAvailableTechnicians: (category: ServiceCategory) =>
      api.get("/api/admin/technicians/available", {
        query: { category },
        schema: z.array(availableTechnicianSchema),
      }),

    assignTechnician: (requestId: number, technicianProfileId: number) =>
      api.post<void>(`/api/admin/requests/${requestId}/assign`, { technicianProfileId }),

    // Users
    getUsers: (role?: UserRole | "") =>
      api.get("/api/admin/users", { query: { role }, schema: adminUsersSchema }),

    toggleUserActive: (id: string) =>
      api.post(`/api/admin/users/${encodeURIComponent(id)}/toggle-active`, undefined, {
        schema: z.object({ isActive: z.boolean() }).partial(),
      }),

    // ── TODO(api): everything below is proposed – not in docs/API.md yet ──
    // Zones
    getZones: () => api.get("/api/admin/zones", { schema: z.array(zoneSchema) }),
    createZone: (input: ZoneInput) => api.post<void>("/api/admin/zones", input),
    updateZone: (id: number, input: ZoneInput) => api.put<void>(`/api/admin/zones/${id}`, input),
    toggleZone: (id: number) => api.post<void>(`/api/admin/zones/${id}/toggle`),

    // Promo codes
    getPromoCodes: () => api.get("/api/admin/promo-codes", { schema: z.array(promoCodeSchema) }),
    createPromoCode: (input: PromoCodeInput) => api.post<void>("/api/admin/promo-codes", input),
    togglePromoCode: (id: number) => api.post<void>(`/api/admin/promo-codes/${id}/toggle`),

    // Disputes
    getDisputes: (status?: string) =>
      api.get("/api/admin/disputes", { query: { status }, schema: z.array(disputeSchema) }),
    getDispute: (id: number) => api.get(`/api/admin/disputes/${id}`, { schema: disputeSchema }),
    startDisputeReview: (id: number, adminNote: string) =>
      api.post<void>(`/api/admin/disputes/${id}/review`, { adminNote }),
    resolveDispute: (id: number, resolution: string, adminNote: string) =>
      api.post<void>(`/api/admin/disputes/${id}/resolve`, { resolution, adminNote }),
    refundDispute: (id: number, amount: number, adminNote: string) =>
      api.post(
        `/api/admin/disputes/${id}/refund`,
        { amount, adminNote },
        {
          schema: z.object({ razorpayRefundId: z.string().nullish() }).nullish(),
        },
      ),

    // Withdrawals
    getWithdrawals: () => api.get("/api/admin/withdrawals", { schema: z.array(withdrawalSchema) }),
    approveWithdrawal: (id: number) => api.post<void>(`/api/admin/withdrawals/${id}/approve`),
    payWithdrawal: (id: number) => api.post<void>(`/api/admin/withdrawals/${id}/pay`),
    rejectWithdrawal: (id: number, adminNote: string) =>
      api.post<void>(`/api/admin/withdrawals/${id}/reject`, { adminNote }),
  };
}

export type AdminEndpoints = ReturnType<typeof adminEndpoints>;
