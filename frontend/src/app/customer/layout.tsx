import { privateMetadata, RoleLayout } from "@/components/layout/RoleLayout";

export const metadata = privateMetadata;

export default function CustomerLayout({ children }: { children: React.ReactNode }) {
  return <RoleLayout role="Customer">{children}</RoleLayout>;
}
