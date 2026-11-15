export const TOKEN_COOKIE = "asms_token";
export const REFRESH_COOKIE = "asms_refresh";

export const ROLES = ["Customer", "Technician", "Admin"] as const;
export type Role = (typeof ROLES)[number];

/** Landing page of each role's area. */
export const ROLE_HOME: Record<Role, string> = {
  Customer: "/customer/dashboard",
  Technician: "/technician/dashboard",
  Admin: "/admin/dashboard",
};

/** URL prefix of each role's area. */
export const ROLE_PREFIX: Record<Role, string> = {
  Customer: "/customer",
  Technician: "/technician",
  Admin: "/admin",
};
