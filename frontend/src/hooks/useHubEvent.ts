"use client";
import { useEffect, useRef } from "react";
import { getConnection, type HubEvent } from "@/lib/realtime/connection";

/** Subscribe to a SignalR event for the lifetime of the component. */
export function useHubEvent<T>(event: HubEvent, handler: (payload: T) => void, enabled = true) {
  // Keep the latest handler without re-subscribing on every render.
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  });

  useEffect(() => {
    if (!enabled) return;
    let cancelled = false;
    const listener = (payload: T) => handlerRef.current(payload);
    let unsubscribe: (() => void) | undefined;

    getConnection()
      .then((conn) => {
        if (cancelled) return;
        conn.on(event, listener);
        unsubscribe = () => conn.off(event, listener);
      })
      .catch(() => {
        /* RealtimeProvider reports connection problems */
      });

    return () => {
      cancelled = true;
      unsubscribe?.();
    };
  }, [event, enabled]);
}
