import type { Metadata } from "next";
import { AdminDashboardView } from "./AdminDashboardView";

export const metadata: Metadata = { title: "Admin dashboard" };

export default function AdminDashboardPage() {
  return <AdminDashboardView />;
}
