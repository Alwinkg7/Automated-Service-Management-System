import { privateMetadata, RoleLayout } from "@/components/layout/RoleLayout";

export const metadata = privateMetadata;

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return <RoleLayout role="Admin">{children}</RoleLayout>;
}
