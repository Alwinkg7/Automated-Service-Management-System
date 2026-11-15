import type { IconName } from "@/components/ui/Icon";
import type { Role } from "@/lib/auth/constants";

export interface NavItem {
  href: string;
  label: string;
  icon: IconName;
}

export const NAV: Record<Role, NavItem[]> = {
  Customer: [
    { href: "/customer/dashboard", label: "Dashboard", icon: "home" },
    { href: "/customer/requests/new", label: "Book a service", icon: "plus" },
    { href: "/customer/requests", label: "My requests", icon: "list" },
    { href: "/customer/history", label: "History", icon: "history" },
    { href: "/customer/promotions", label: "Rewards", icon: "gift" },
    { href: "/customer/disputes", label: "Disputes", icon: "flag" },
  ],
  Technician: [
    { href: "/technician/dashboard", label: "Dashboard", icon: "home" },
    { href: "/technician/jobs", label: "My jobs", icon: "briefcase" },
    { href: "/technician/wallet", label: "Earnings", icon: "wallet" },
  ],
  Admin: [
    { href: "/admin/dashboard", label: "Dashboard", icon: "home" },
    { href: "/admin/analytics", label: "Analytics", icon: "chart" },
    { href: "/admin/requests", label: "Requests", icon: "list" },
    { href: "/admin/technicians", label: "Technicians", icon: "wrench" },
    { href: "/admin/users", label: "Users", icon: "users" },
    { href: "/admin/zones", label: "Zones", icon: "zone" },
    { href: "/admin/promo-codes", label: "Promo codes", icon: "tag" },
    { href: "/admin/disputes", label: "Disputes", icon: "flag" },
    { href: "/admin/withdrawals", label: "Withdrawals", icon: "wallet" },
  ],
};

/** Longest matching nav href wins, so "/customer/requests/new" doesn't also light up "My requests". */
export function activeHref(pathname: string, items: NavItem[]): string | undefined {
  return items
    .filter((i) => pathname === i.href || pathname.startsWith(`${i.href}/`))
    .sort((a, b) => b.href.length - a.href.length)[0]?.href;
}
