"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import type { Role } from "@/lib/auth/constants";
import { cn } from "@/lib/cn";
import { selectUnreadCount, useNotificationStore } from "@/store/notificationStore";

function linkFor(role: Role, requestId?: number) {
  if (!requestId) return undefined;
  if (role === "Customer") return `/customer/requests/${requestId}`;
  if (role === "Technician") return `/technician/jobs/${requestId}`;
  return `/admin/requests/${requestId}`;
}

export function NotificationBell({ role }: { role: Role }) {
  const [open, setOpen] = useState(false);
  const items = useNotificationStore((s) => s.items);
  const connected = useNotificationStore((s) => s.connected);
  const unread = useNotificationStore(selectUnreadCount);
  const markAllRead = useNotificationStore((s) => s.markAllRead);
  const ref = useRef<HTMLDivElement>(null);

  // Close on outside click / Escape.
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => {
          setOpen((o) => !o);
          if (!open) markAllRead();
        }}
        className="text-fg-muted hover:bg-surface-muted hover:text-fg relative rounded-lg p-2"
        aria-label={unread ? `Notifications, ${unread} unread` : "Notifications"}
        aria-expanded={open}
        aria-haspopup="true"
      >
        <Icon name="bell" />
        {unread > 0 ? (
          <span className="bg-danger absolute -top-0.5 -right-0.5 flex min-w-5 items-center justify-center rounded-full px-1 text-[10px] font-semibold text-white">
            {unread > 9 ? "9+" : unread}
          </span>
        ) : null}
      </button>
      {open ? (
        <div className="animate-fade-in rounded-card border-border bg-surface absolute right-0 z-40 mt-2 w-80 max-w-[calc(100vw-2rem)] border shadow-lg">
          <div className="border-border flex items-center justify-between border-b px-4 py-2.5">
            <p className="text-sm font-semibold">Notifications</p>
            <span className="text-fg-muted flex items-center gap-1.5 text-xs">
              <span
                className={cn("size-2 rounded-full", connected ? "bg-success" : "bg-fg-muted")}
                aria-hidden
              />
              {connected ? "Live" : "Offline"}
            </span>
          </div>
          {items.length === 0 ? (
            <p className="text-fg-muted px-4 py-6 text-center text-sm">
              You&apos;re all caught up.
            </p>
          ) : (
            <ul className="divide-border max-h-80 divide-y overflow-y-auto">
              {items.map((n) => {
                const href = linkFor(role, n.requestId);
                const body = (
                  <>
                    <p className="text-sm">{n.message}</p>
                    <p className="text-fg-muted mt-0.5 text-xs">
                      {new Date(n.at).toLocaleTimeString()}
                    </p>
                  </>
                );
                return (
                  <li key={n.id}>
                    {href ? (
                      <Link
                        href={href}
                        onClick={() => setOpen(false)}
                        className="hover:bg-surface-muted block px-4 py-3"
                      >
                        {body}
                      </Link>
                    ) : (
                      <div className="px-4 py-3">{body}</div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      ) : null}
    </div>
  );
}
