// Mirrors ServiceApp.Core/Enums/AppEnums.cs (serialized as strings) plus string
// "enums" used on entities (Dispute.Status, TechnicianWithdrawal.Status, PromoCode.DiscountType).

export const REQUEST_STATUSES = [
  "Pending",
  "Assigned",
  "InProgress",
  "Billed",
  "Completed",
  "Cancelled",
] as const;
export type RequestStatus = (typeof REQUEST_STATUSES)[number];

export const SERVICE_CATEGORIES = [
  "Plumbing",
  "Electrical",
  "Carpentry",
  "Cleaning",
  "AC_Repair",
  "Appliance_Repair",
  "Painting",
  "General",
] as const;
export type ServiceCategory = (typeof SERVICE_CATEGORIES)[number];

export const TECHNICIAN_STATUSES = ["Available", "Busy", "Offline"] as const;
export type TechnicianStatus = (typeof TECHNICIAN_STATUSES)[number];

export const USER_ROLES = ["Customer", "Technician", "Admin"] as const;
export type UserRole = (typeof USER_ROLES)[number];

export const DISPUTE_STATUSES = ["Open", "UnderReview", "Resolved"] as const;
export type DisputeStatus = (typeof DISPUTE_STATUSES)[number];

export const DISPUTE_REASONS = [
  "ServiceQuality",
  "LateArrival",
  "WrongCharge",
  "Unprofessional",
  "JobIncomplete",
  "Other",
] as const;
export type DisputeReason = (typeof DISPUTE_REASONS)[number];

export const DISPUTE_RESOLUTIONS = ["Refunded", "Rejected", "ClosedNoAction"] as const;
export type DisputeResolution = (typeof DISPUTE_RESOLUTIONS)[number];

export const WITHDRAWAL_STATUSES = ["Pending", "Approved", "Paid", "Rejected"] as const;
export type WithdrawalStatus = (typeof WITHDRAWAL_STATUSES)[number];

export const PROMO_TYPES = ["Flat", "Percentage", "FirstOnly"] as const;
export type PromoType = (typeof PROMO_TYPES)[number];

/** "AC_Repair" -> "AC Repair", "InProgress" -> "In Progress" */
export function humanize(value: string): string {
  return value
    .replace(/_/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .trim();
}

export const CATEGORY_INFO: Record<
  ServiceCategory,
  { label: string; description: string; slug: string }
> = {
  Plumbing: { label: "Plumbing", description: "Pipes, leaks, taps & drainage", slug: "plumbing" },
  Electrical: {
    label: "Electrical",
    description: "Wiring, switches & power issues",
    slug: "electrical",
  },
  Carpentry: { label: "Carpentry", description: "Furniture, doors & woodwork", slug: "carpentry" },
  Cleaning: { label: "Cleaning", description: "Deep clean, pest control", slug: "cleaning" },
  AC_Repair: { label: "AC Repair", description: "AC servicing & installation", slug: "ac-repair" },
  Appliance_Repair: {
    label: "Appliance Repair",
    description: "Washing machine, fridge & more",

    slug: "appliance-repair",
  },
  Painting: { label: "Painting", description: "Interior & exterior painting", slug: "painting" },
  General: { label: "General", description: "General home services", slug: "general" },
};

export function categoryFromSlug(slug: string): ServiceCategory | undefined {
  return SERVICE_CATEGORIES.find((c) => CATEGORY_INFO[c].slug === slug);
}
