// Zod schemas for API responses. Types are inferred from them so the compiler
// and the runtime agree. Fields the backend may omit are `.nullish()`.
//
// Source of truth: ServiceApp.Api/Controllers/*ApiController.cs (+ docs/API.md).
import { z } from "zod";
import {
  DISPUTE_STATUSES,
  REQUEST_STATUSES,
  SERVICE_CATEGORIES,
  TECHNICIAN_STATUSES,
  USER_ROLES,
} from "@/types/enums";

const str = z.string();
const optStr = z.string().nullish();
const money = z.number();
const optMoney = z.number().nullish();
const requestStatus = z.enum(REQUEST_STATUSES);
const category = z.enum(SERVICE_CATEGORIES);
const techStatus = z.enum(TECHNICIAN_STATUSES);
/** Ids are ints for most entities; Identity user ids are GUID strings. */
const id = z.number().int();

// ── Pagination ───────────────────────────────────────────────────
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Accepts `{ items, page, pageSize, totalCount }` or a bare array and normalizes to Paged. */
export function pagedOrArray<T extends z.ZodType>(item: T) {
  const paged = z.object({
    items: z.array(item),
    page: z.number().int(),
    pageSize: z.number().int(),
    totalCount: z.number().int(),
  });
  return z.union([paged, z.array(item)]).transform((value): Paged<z.infer<T>> => {
    if (Array.isArray(value)) {
      return { items: value, page: 1, pageSize: value.length || 1, totalCount: value.length };
    }
    return value as Paged<z.infer<T>>;
  });
}

// ── Auth ─────────────────────────────────────────────────────────
export const loginResponseSchema = z.object({
  token: str,
  expiresAt: optStr,
  refreshToken: optStr,
  refreshTokenExpiresAt: optStr,
  role: z.enum(USER_ROLES),
  userId: str,
  name: optStr,
  email: optStr,
});
export type LoginResponse = z.infer<typeof loginResponseSchema>;

// ── Shared pieces ────────────────────────────────────────────────
export const categoryOptionSchema = z.object({
  value: category,
  label: str,
  description: optStr,
  icon: optStr,
});
export type CategoryOption = z.infer<typeof categoryOptionSchema>;

export const statusHistorySchema = z.object({
  status: requestStatus,
  changedAt: str,
  note: optStr,
});
export type StatusHistoryEntry = z.infer<typeof statusHistorySchema>;

export const billItemSchema = z.object({
  description: str,
  quantity: z.number(),
  unitPrice: money,
  amount: optMoney,
});

export const billSchema = z.object({
  id: id.nullish(),
  amount: money,
  laborCost: optMoney,
  materialCost: optMoney,
  description: optStr,
  isPaid: z.boolean(),
  createdAt: optStr,
  paidAt: optStr,
  items: z.array(billItemSchema).nullish(),
});
export type Bill = z.infer<typeof billSchema>;

// ── Customer ─────────────────────────────────────────────────────
export const requestSummarySchema = z.object({
  id,
  category,
  status: requestStatus,
  description: optStr,
  address: optStr,
  scheduledDate: str,
  createdAt: str,
  technicianName: optStr,
  hasBill: z.boolean(),
  billPaid: z.boolean(),
  billAmount: optMoney,
  rating: z.number().nullish(),
});
export type RequestSummary = z.infer<typeof requestSummarySchema>;

export const customerDashboardSchema = z.object({
  stats: z.object({
    total: z.number(),
    pending: z.number(),
    inProgress: z.number(),
    completed: z.number(),
  }),
  recentRequests: z.array(requestSummarySchema),
});
export type CustomerDashboard = z.infer<typeof customerDashboardSchema>;

export const customerRequestDetailSchema = z.object({
  id,
  category,
  status: requestStatus,
  description: str,
  address: str,
  pinCode: optStr,
  scheduledDate: str,
  createdAt: str,
  canCancel: z.boolean(),
  rating: z.number().nullish(),
  feedback: optStr,
  technician: z
    .object({
      name: optStr,
      phone: optStr,
      email: optStr,
      rating: z.number().nullish(),
    })
    .nullish(),
  bill: billSchema.nullish(),
  statusHistory: z.array(statusHistorySchema).default([]),
});
export type CustomerRequestDetail = z.infer<typeof customerRequestDetailSchema>;

export const createRequestResponseSchema = z.object({ requestId: id });

export const paymentOrderSchema = z.object({
  keyId: str,
  orderId: str,
  /** Amount in paise, as Razorpay expects. */
  amount: z.number(),
  currency: str.default("INR"),
  billId: id.nullish(),
  discount: optMoney,
  customerName: optStr,
  customerEmail: optStr,
  customerPhone: optStr,
});
export type PaymentOrder = z.infer<typeof paymentOrderSchema>;

export const paymentResultSchema = z.object({
  paymentId: id.nullish(),
});

export const promotionsSchema = z.object({
  pointsBalance: z.number(),
  pointsValue: optMoney,
  referralCode: optStr,
  history: z
    .array(
      z.object({
        points: z.number(),
        transactionType: str,
        note: optStr,
        requestId: id.nullish(),
        transactedAt: str,
        expiresAt: optStr,
      }),
    )
    .default([]),
});
export type Promotions = z.infer<typeof promotionsSchema>;

export const promoValidationSchema = z.object({
  discount: z.number(),
  message: optStr,
});
export type PromoValidation = z.infer<typeof promoValidationSchema>;

export const disputeSchema = z.object({
  id,
  requestId: id,
  reason: str,
  description: str,
  status: z.enum(DISPUTE_STATUSES),
  resolution: optStr,
  adminNote: optStr,
  refundAmount: optMoney,
  refundIssued: z.boolean().nullish(),
  razorpayRefundId: optStr,
  raisedAt: str,
  resolvedAt: optStr,
  slaDeadline: optStr,
  isSlaBreached: z.boolean().nullish(),
  raisedByName: optStr,
  category: category.nullish(),
  billAmount: optMoney,
});
export type Dispute = z.infer<typeof disputeSchema>;

// ── Technician ───────────────────────────────────────────────────
export const technicianDashboardSchema = z.object({
  technician: z.object({
    name: optStr,
    status: techStatus,
    rating: z.number(),
    totalJobs: z.number(),
    completedJobs: z.number(),
    pendingBills: z.number(),
  }),
  activeJob: z
    .object({
      id,
      category,
      status: requestStatus,
      customerName: optStr,
      address: optStr,
      scheduledDate: str,
    })
    .nullish(),
  recentJobs: z
    .array(
      z.object({
        id,
        category,
        customerName: optStr,
        completedAt: optStr,
        billPaid: z.boolean(),
      }),
    )
    .default([]),
});
export type TechnicianDashboard = z.infer<typeof technicianDashboardSchema>;

export const jobSummarySchema = z.object({
  id,
  category,
  status: requestStatus,
  description: optStr,
  address: optStr,
  scheduledDate: str,
  customerName: optStr,
  customerPhone: optStr,
  hasBill: z.boolean(),
  billPaid: z.boolean(),
});
export type JobSummary = z.infer<typeof jobSummarySchema>;

export const jobDetailSchema = z.object({
  id,
  category,
  status: requestStatus,
  description: str,
  address: str,
  pinCode: optStr,
  scheduledDate: str,
  createdAt: str,
  canAccept: z.boolean(),
  canBill: z.boolean(),
  canComplete: z.boolean(),
  customer: z.object({ name: optStr, phone: optStr, email: optStr }),
  bill: billSchema.nullish(),
});
export type JobDetail = z.infer<typeof jobDetailSchema>;

export const toggleStatusSchema = z.object({ newStatus: techStatus });

export const withdrawalSchema = z.object({
  id,
  amount: z.number(),
  status: str,
  upiId: str,
  adminNote: optStr,
  requestedAt: str,
  processedAt: optStr,
  technicianName: optStr,
});
export type Withdrawal = z.infer<typeof withdrawalSchema>;

export const walletSchema = z.object({
  grossEarningsAllTime: z.number(),
  commissionAllTime: z.number(),
  netEarningsAllTime: z.number(),
  netEarningsThisMonth: z.number(),
  netEarningsThisWeek: z.number(),
  availableToWithdraw: z.number(),
  pendingWithdrawal: z.number(),
  totalJobsCompleted: z.number(),
  averagePerJob: z.number(),
  commissionRate: z.number(),
  monthly: z.array(z.object({ label: str, gross: z.number(), net: z.number() })).default([]),
  payouts: z
    .array(
      z.object({
        requestId: id,
        billId: id,
        customerName: optStr,
        category: str,
        grossAmount: z.number(),
        commission: z.number(),
        netAmount: z.number(),
        paymentMethod: optStr,
        paidAt: str,
      }),
    )
    .default([]),
  withdrawals: z.array(withdrawalSchema).default([]),
});
export type Wallet = z.infer<typeof walletSchema>;


// ── Admin ────────────────────────────────────────────────────────
export const adminDashboardSchema = z.object({
  stats: z.object({
    totalRequests: z.number(),
    pending: z.number(),
    inProgress: z.number(),
    completed: z.number(),
    totalCustomers: z.number(),
    totalTechnicians: z.number(),
    revenue: z.number(),
  }),
  pendingRequests: z
    .array(
      z.object({
        id,
        category,
        status: requestStatus,
        customerName: optStr,
        scheduledDate: str,
        createdAt: str,
        address: optStr,
      }),
    )
    .default([]),
});
export type AdminDashboard = z.infer<typeof adminDashboardSchema>;

export const adminRequestRowSchema = z.object({
  id,
  category,
  status: requestStatus,
  customerName: optStr,
  technicianName: optStr,
  scheduledDate: str,
  createdAt: str,
  hasBill: z.boolean(),
  billPaid: z.boolean(),
  billAmount: optMoney,
  isUnassigned: z.boolean().nullish(),
  address: optStr,
  pinCode: optStr,
});
export type AdminRequestRow = z.infer<typeof adminRequestRowSchema>;
export const adminRequestsSchema = pagedOrArray(adminRequestRowSchema);

export const adminRequestDetailSchema = z.object({
  id,
  category,
  status: requestStatus,
  description: str,
  address: str,
  pinCode: optStr,
  scheduledDate: str,
  createdAt: str,
  canAssign: z.boolean(),
  canCancel: z.boolean().nullish(),
  customer: z.object({ name: optStr, email: optStr, phone: optStr }),
  technician: z
    .object({
      id,
      name: optStr,
      phone: optStr,
      rating: z.number().nullish(),
      status: techStatus.nullish(),
    })
    .nullish(),
  bill: billSchema.partial({ isPaid: true }).nullish(),
  statusHistory: z.array(statusHistorySchema).nullish(),
});
export type AdminRequestDetail = z.infer<typeof adminRequestDetailSchema>;

export const availableTechnicianSchema = z.object({
  id,
  name: optStr,
  phone: optStr,
  rating: z.number(),
  totalJobs: z.number(),
  skills: optStr,
  status: techStatus,
});
export type AvailableTechnician = z.infer<typeof availableTechnicianSchema>;

export const adminUserSchema = z.object({
  id: str,
  name: optStr,
  email: optStr,
  phone: optStr,
  role: z.enum(USER_ROLES),
  isActive: z.boolean(),
  createdAt: str,
});
export type AdminUser = z.infer<typeof adminUserSchema>;
export const adminUsersSchema = pagedOrArray(adminUserSchema);

export const adminTechnicianSchema = z.object({
  id,
  userId: optStr,
  name: optStr,
  email: optStr,
  phone: optStr,
  skill: category,
  status: techStatus,
  rating: z.number(),
  totalJobsCompleted: z.number(),
  isActive: z.boolean().nullish(),
  zoneName: optStr,
  totalEarned: optMoney,
  activeJobs: z.number().nullish(),
});
export type AdminTechnician = z.infer<typeof adminTechnicianSchema>;
export const adminTechniciansSchema = pagedOrArray(adminTechnicianSchema);

export const zoneSchema = z.object({
  id,
  zoneName: str,
  city: str,
  state: str,
  isActive: z.boolean(),
  /** Comma-separated in the entity; the API may send an array. */
  pinCodes: z.union([z.string(), z.array(z.string())]).transform((v) =>
    Array.isArray(v)
      ? v
      : v
          .split(",")
          .map((p) => p.trim())
          .filter(Boolean),
  ),
  technicianCount: z.number().nullish(),
});
export type Zone = z.infer<typeof zoneSchema>;

export const promoCodeSchema = z.object({
  id,
  code: str,
  description: optStr,
  discountType: str,
  discountValue: z.number(),
  maxDiscount: optMoney,
  minimumAmount: z.number(),
  maxUses: z.number().nullish(),
  usedCount: z.number(),
  isActive: z.boolean(),
  expiresAt: optStr,
  createdAt: optStr,
});
export type PromoCode = z.infer<typeof promoCodeSchema>;

export const analyticsSchema = z.object({
  totals: z.object({
    totalRequests: z.number(),
    completedRequests: z.number(),
    pendingRequests: z.number(),
    totalCustomers: z.number(),
    totalTechnicians: z.number(),
    totalRevenue: z.number(),
  }),
  statusBreakdown: z.record(z.string(), z.number()),
  revenueByDay: z.array(z.object({ date: str, amount: z.number() })),
  requestsByDay: z.array(z.object({ date: str, count: z.number() })),
  categories: z.array(z.object({ category: str, count: z.number() })),
  topTechnicians: z
    .array(
      z.object({
        name: optStr,
        skill: str,
        totalJobsCompleted: z.number(),
        rating: z.number(),
        status: str,
        totalEarned: z.number(),
      }),
    )
    .default([]),
  recentActivity: z
    .array(
      z.object({
        requestId: id,
        status: str,
        note: optStr,
        changedAt: str,
        category: optStr,
      }),
    )
    .default([]),
});
export type Analytics = z.infer<typeof analyticsSchema>;
