// Admin endpoints – see docs/API.md "Admin".
import { z } from "zod";
import type { ApiClient } from "../client";
import {
  adminDashboardSchema,
  adminDisputesSchema,
  adminRequestDetailSchema,
  adminRequestsSchema,
  adminTechniciansSchema,
  adminUsersSchema,
  analyticsSchema,
  availableTechnicianSchema,
  disputeSchema,
  promoCodeSchema,
  withdrawalSchema,
  zoneSchema,
} from "../schemas";
import type { RequestStatus, ServiceCategory, TechnicianStatus, UserRole } from "@/types/enums";

export interface AdminRequestFilters {
  search?: string;
  status?: RequestStatus | "";
  /** yyyy-mm-dd, inclusive, on createdAt */
  from?: string;
  to?: string;
}

export interface TechnicianFilters {
  skill?: ServiceCategory | "";
  status?: TechnicianStatus | "";
  search?: string;
  isActive?: "true" | "false" | "";
}

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

    getAnalytics: () => api.get("/api/admin/analytics", { schema: analyticsSchema }),

    runAutoAssign: () =>
      api.post("/api/admin/auto-assign", undefined, {
        schema: z.object({ assigned: z.number(), stillPending: z.number().nullish() }),
      }),

    // ── Requests (server-side filters + paging) ──────────────────
    getRequests: (filters: AdminRequestFilters, page: number, pageSize: number) =>
      api.get("/api/admin/requests", {
        query: { ...filters, page, pageSize },
        schema: adminRequestsSchema,
      }),

    /** CSV with the same filters (UTF-8 BOM, formula injection escaped by the server). */
    exportRequestsCsv: (filters: AdminRequestFilters) =>
      api.download("/api/admin/requests/export", { query: { ...filters } }),

    getRequest: (id: number) =>
      api.get(`/api/admin/requests/${id}`, { schema: adminRequestDetailSchema }),

    cancelRequest: (id: number) => api.post<void>(`/api/admin/requests/${id}/cancel`),

    getAvailableTechnicians: (category: ServiceCategory) =>
      api.get("/api/admin/technicians/available", {
        query: { category },
        schema: z.array(availableTechnicianSchema),
      }),

    assignTechnician: (requestId: number, technicianProfileId: number) =>
      api.post<void>(`/api/admin/requests/${requestId}/assign`, { technicianProfileId }),

    // ── Technicians ──────────────────────────────────────────────
    getTechnicians: (filters: TechnicianFilters) =>
      api.get("/api/admin/technicians", {
        query: { ...filters },
        envelope: "full",
        schema: adminTechniciansSchema,
      }),
    setTechnicianStatus: (id: number, status: TechnicianStatus) =>
      api.post<void>(`/api/admin/technicians/${id}/status`, { status }),
    setTechnicianSkill: (id: number, skill: ServiceCategory) =>
      api.post<void>(`/api/admin/technicians/${id}/skill`, { skill }),
    activateTechnician: (id: number) => api.post<void>(`/api/admin/technicians/${id}/activate`),
    deactivateTechnician: (id: number) => api.post<void>(`/api/admin/technicians/${id}/deactivate`),

    // ── Users ────────────────────────────────────────────────────
    getUsers: (role?: UserRole | "") =>
      api.get("/api/admin/users", { query: { role }, schema: adminUsersSchema }),

    toggleUserActive: (id: string) =>
      api.post(`/api/admin/users/${encodeURIComponent(id)}/toggle-active`, undefined, {
        schema: z.object({ isActive: z.boolean() }).partial(),
      }),

    // ── Zones ────────────────────────────────────────────────────
    getZones: () => api.get("/api/admin/zones", { schema: z.array(zoneSchema) }),
    createZone: (input: ZoneInput) => api.post<void>("/api/admin/zones", input),
    updateZone: (id: number, input: ZoneInput) => api.put<void>(`/api/admin/zones/${id}`, input),
    toggleZone: (id: number) => api.post<void>(`/api/admin/zones/${id}/toggle`),

    // ── Promo codes ──────────────────────────────────────────────
    getPromoCodes: () => api.get("/api/admin/promo-codes", { schema: z.array(promoCodeSchema) }),
    createPromoCode: (input: PromoCodeInput) => api.post<void>("/api/admin/promo-codes", input),
    togglePromoCode: (id: number) => api.post<void>(`/api/admin/promo-codes/${id}/toggle`),
    /** 409 if the code was used – deactivate it instead. */
    deletePromoCode: (id: number) => api.delete<void>(`/api/admin/promo-codes/${id}`),

    // ── Disputes ─────────────────────────────────────────────────
    getDisputes: (status?: string) =>
      api.get("/api/admin/disputes", {
        query: { status },
        envelope: "full",
        schema: adminDisputesSchema,
      }),
    getDispute: (id: number) => api.get(`/api/admin/disputes/${id}`, { schema: disputeSchema }),
    startDisputeReview: (id: number, adminNote: string) =>
      api.post<void>(`/api/admin/disputes/${id}/review`, { adminNote: adminNote || undefined }),
    resolveDispute: (id: number, resolution: string, adminNote: string) =>
      api.post<void>(`/api/admin/disputes/${id}/resolve`, {
        resolution,
        adminNote: adminNote || undefined,
      }),
    refundDispute: (id: number, amount: number, adminNote: string) =>
      api.post(
        `/api/admin/disputes/${id}/refund`,
        { amount, adminNote: adminNote || undefined },
        { schema: z.object({ razorpayRefundId: z.string().nullish() }).nullish() },
      ),

    // ── Withdrawals ──────────────────────────────────────────────
    getWithdrawals: (status?: string) =>
      api.get("/api/admin/withdrawals", { query: { status }, schema: z.array(withdrawalSchema) }),
    approveWithdrawal: (id: number) => api.post<void>(`/api/admin/withdrawals/${id}/approve`),
    payWithdrawal: (id: number) => api.post<void>(`/api/admin/withdrawals/${id}/pay`),
    rejectWithdrawal: (id: number, adminNote: string) =>
      api.post<void>(`/api/admin/withdrawals/${id}/reject`, { adminNote }),
  };
}

export type AdminEndpoints = ReturnType<typeof adminEndpoints>;
