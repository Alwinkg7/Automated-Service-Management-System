import type { Metadata } from "next";
import { CustomerDashboardView } from "./CustomerDashboardView";

export const metadata: Metadata = { title: "Dashboard" };

export default function CustomerDashboardPage() {
  return <CustomerDashboardView />;
}
