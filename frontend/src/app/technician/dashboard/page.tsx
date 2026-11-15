import type { Metadata } from "next";
import { TechnicianDashboardView } from "./TechnicianDashboardView";

export const metadata: Metadata = { title: "Dashboard" };

export default function TechnicianDashboardPage() {
  return <TechnicianDashboardView />;
}
