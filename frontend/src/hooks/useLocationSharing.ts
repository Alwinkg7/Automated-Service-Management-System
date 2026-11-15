"use client";
// Shares the technician's GPS position for an in-progress job over SignalR.
// The hub resolves the customer server-side and checks job ownership.
import { useCallback, useEffect, useRef, useState } from "react";
import { getConnection } from "@/lib/realtime/connection";

const MIN_INTERVAL_MS = 10_000; // don't flood the hub; the MVC app used ~30s

export type SharingState = "off" | "starting" | "on" | "denied" | "unsupported" | "error";

export function useLocationSharing(requestId: number) {
  const [state, setState] = useState<SharingState>("off");
  const [lastSent, setLastSent] = useState<Date | null>(null);
  const watchId = useRef<number | null>(null);
  const lastSentAt = useRef(0);

  const stop = useCallback(async () => {
    if (watchId.current !== null) navigator.geolocation.clearWatch(watchId.current);
    watchId.current = null;
    setState("off");
    try {
      const conn = await getConnection();
      // Second argument (customerId) is ignored by the hub; kept for signature compatibility.
      await conn.invoke("StopSharing", requestId, "");
    } catch {
      /* best effort */
    }
  }, [requestId]);

  const start = useCallback(() => {
    if (typeof navigator === "undefined" || !("geolocation" in navigator)) {
      setState("unsupported");
      return;
    }
    setState("starting");
    watchId.current = navigator.geolocation.watchPosition(
      async (pos) => {
        const now = Date.now();
        if (now - lastSentAt.current < MIN_INTERVAL_MS) return;
        lastSentAt.current = now;
        try {
          const conn = await getConnection();
          await conn.invoke("ShareLocation", requestId, pos.coords.latitude, pos.coords.longitude, "");
          setState("on");
          setLastSent(new Date());
        } catch {
          setState("error");
        }
      },
      (err) => setState(err.code === err.PERMISSION_DENIED ? "denied" : "error"),
      { enableHighAccuracy: true, maximumAge: 5_000, timeout: 20_000 },
    );
  }, [requestId]);

  // Stop the GPS watch when leaving the page.
  useEffect(
    () => () => {
      if (watchId.current !== null) navigator.geolocation.clearWatch(watchId.current);
    },
    [],
  );

  return { state, lastSent, start, stop, active: state === "on" || state === "starting" };
}
