"use client";
// Connects to the SignalR hub for signed-in users, turns events into toasts +
// bell notifications, and invalidates the role's cached queries so lists refresh.
import { HubConnectionState } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, type ReactNode } from "react";
import { toast } from "sonner";
import type { Role } from "@/lib/auth/constants";
import { qk } from "@/lib/queryKeys";
import { getConnection, HUB_EVENTS, type HubPayload } from "@/lib/realtime/connection";
import { useNotificationStore } from "@/store/notificationStore";

// Location pings are too frequent for toasts; the tracking map handles them.
const SILENT_EVENTS = new Set(["LocationUpdate"]);

const ROLE_ROOT: Record<Role, readonly string[]> = {
  Customer: qk.customer.all,
  Technician: qk.technician.all,
  Admin: qk.admin.all,
};

export function RealtimeProvider({ role, children }: { role: Role; children: ReactNode }) {
  const queryClient = useQueryClient();
  const add = useNotificationStore((s) => s.add);
  const setConnected = useNotificationStore((s) => s.setConnected);

  useEffect(() => {
    let disposed = false;
    const cleanups: Array<() => void> = [];

    getConnection()
      .then((conn) => {
        if (disposed) return;
        setConnected(conn.state === HubConnectionState.Connected);
        conn.onreconnecting(() => setConnected(false));
        conn.onreconnected(() => {
          setConnected(true);
          // We may have missed events while offline.
          void queryClient.invalidateQueries({ queryKey: ROLE_ROOT[role] });
        });
        conn.onclose(() => setConnected(false));

        for (const event of HUB_EVENTS) {
          const handler = (payload: HubPayload) => {
            void queryClient.invalidateQueries({ queryKey: ROLE_ROOT[role] });
            if (SILENT_EVENTS.has(event)) return;
            const message =
              typeof payload?.message === "string" && payload.message
                ? payload.message
                : `Update on request #${payload?.requestId ?? ""}`;
            add({ event, message, requestId: payload?.requestId });
            toast(message);
          };
          conn.on(event, handler);
          cleanups.push(() => conn.off(event, handler));
        }
      })
      .catch(() => {
        // Realtime is an enhancement; pages still work (and refetch on focus).
        setConnected(false);
      });

    return () => {
      disposed = true;
      cleanups.forEach((fn) => fn());
    };
  }, [role, queryClient, add, setConnected]);

  return children;
}
