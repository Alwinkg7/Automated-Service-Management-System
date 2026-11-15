import type { ReactNode } from "react";
import type { Metadata } from "next";
import { requireRole } from "@/lib/auth/server";
import type { Role } from "@/lib/auth/constants";
import { RealtimeProvider } from "@/components/providers/RealtimeProvider";
import { SessionProvider } from "@/components/providers/SessionProvider";
import { AppShell } from "./AppShell";

/** Signed-in areas are never indexed. */
export const privateMetadata: Metadata = { robots: { index: false, follow: false } };

/** Server component used by /customer, /technician and /admin layouts. */
export async function RoleLayout({ role, children }: { role: Role; children: ReactNode }) {
  const user = await requireRole(role);
  return (
    <SessionProvider user={{ id: user.id, name: user.name, email: user.email, role: user.role }}>
      <RealtimeProvider role={role}>
        <AppShell>{children}</AppShell>
      </RealtimeProvider>
    </SessionProvider>
  );
}
