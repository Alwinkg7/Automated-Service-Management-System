"use client";
// Current user for Client Components. Filled from the role layout (server),
// so there is no extra request and no token in JavaScript.
import { createContext, useContext, type ReactNode } from "react";
import type { Role } from "@/lib/auth/constants";

export interface ClientUser {
  id: string;
  name: string;
  email: string;
  role: Role;
}

const SessionContext = createContext<ClientUser | null>(null);

export function SessionProvider({ user, children }: { user: ClientUser; children: ReactNode }) {
  return <SessionContext value={user}>{children}</SessionContext>;
}

export function useSession(): ClientUser {
  const user = useContext(SessionContext);
  if (!user) throw new Error("useSession must be used inside a role layout");
  return user;
}
