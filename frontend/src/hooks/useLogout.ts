"use client";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useCallback, useState } from "react";
import { stopConnection } from "@/lib/realtime/connection";
import { useNotificationStore } from "@/store/notificationStore";

/** Clears cookies (server), the realtime connection, notifications and the query cache. */
export function useLogout() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const clear = useNotificationStore((s) => s.clear);
  const [pending, setPending] = useState(false);

  const logout = useCallback(async () => {
    setPending(true);
    try {
      await fetch("/api/auth/logout", { method: "POST" });
    } finally {
      await stopConnection().catch(() => {});
      clear();
      queryClient.clear();
      router.replace("/login");
      router.refresh();
    }
  }, [clear, queryClient, router]);

  return { logout, pending };
}
