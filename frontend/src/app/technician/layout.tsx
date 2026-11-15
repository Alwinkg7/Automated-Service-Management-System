import { privateMetadata, RoleLayout } from "@/components/layout/RoleLayout";

export const metadata = privateMetadata;

export default function TechnicianLayout({ children }: { children: React.ReactNode }) {
  return <RoleLayout role="Technician">{children}</RoleLayout>;
}
