"use client";
// Sidebar + top bar for the signed-in areas. Desktop: fixed sidebar. Mobile: drawer.
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState, type ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import { useSession } from "@/components/providers/SessionProvider";
import { useLogout } from "@/hooks/useLogout";
import { cn } from "@/lib/cn";
import { activeHref, NAV } from "@/lib/navigation";
import { Logo } from "./Logo";
import { NotificationBell } from "./NotificationBell";
import { ThemeToggle } from "./ThemeToggle";

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const user = useSession();
  const pathname = usePathname();
  const items = NAV[user.role];
  const active = activeHref(pathname, items);
  return (
    <nav aria-label="Main" className="space-y-1">
      {items.map((item) => (
        <Link
          key={item.href}
          href={item.href}
          onClick={onNavigate}
          aria-current={active === item.href ? "page" : undefined}
          className={cn(
            "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
            active === item.href
              ? "bg-primary-soft text-primary"
              : "text-fg-muted hover:bg-surface-muted hover:text-fg",
          )}
        >
          <Icon name={item.icon} />
          {item.label}
        </Link>
      ))}
    </nav>
  );
}

function UserBlock() {
  const user = useSession();
  const { logout, pending } = useLogout();
  return (
    <div className="border-border flex items-center gap-3 border-t pt-4">
      <span
        aria-hidden
        className="bg-primary-soft text-primary flex size-9 shrink-0 items-center justify-center rounded-full text-sm font-semibold"
      >
        {(user.name || user.email || "?").charAt(0).toUpperCase()}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{user.name || user.email}</p>
        <p className="text-fg-muted truncate text-xs">{user.role}</p>
      </div>
      <button
        type="button"
        onClick={() => void logout()}
        disabled={pending}
        className="text-fg-muted hover:bg-surface-muted hover:text-fg rounded-lg p-2 disabled:opacity-50"
        aria-label="Sign out"
      >
        <Icon name="logout" />
      </button>
    </div>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const user = useSession();
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[16rem_1fr]">
      <a
        href="#main"
        className="focus:bg-surface sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded focus:px-3 focus:py-2"
      >
        Skip to content
      </a>

      {/* Desktop sidebar */}
      <aside className="border-border bg-surface sticky top-0 hidden h-dvh flex-col gap-6 border-r p-4 lg:flex">
        <Logo />
        <div className="flex-1 overflow-y-auto">
          <NavLinks />
        </div>
        <UserBlock />
      </aside>

      {/* Mobile drawer */}
      {drawerOpen ? (
        <div
          className="fixed inset-0 z-40 lg:hidden"
          role="dialog"
          aria-modal="true"
          aria-label="Menu"
        >
          <button
            type="button"
            className="animate-fade-in absolute inset-0 bg-black/40"
            aria-label="Close menu"
            onClick={() => setDrawerOpen(false)}
          />
          <aside className="animate-slide-up bg-surface relative flex h-full w-72 max-w-[85vw] flex-col gap-6 p-4 shadow-xl">
            <div className="flex items-center justify-between">
              <Logo />
              <button
                type="button"
                onClick={() => setDrawerOpen(false)}
                className="hover:bg-surface-muted rounded-lg p-2"
                aria-label="Close menu"
              >
                <Icon name="close" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto">
              <NavLinks onNavigate={() => setDrawerOpen(false)} />
            </div>
            <UserBlock />
          </aside>
        </div>
      ) : null}

      <div className="flex min-w-0 flex-col">
        <header className="border-border bg-surface/90 sticky top-0 z-30 flex h-14 items-center gap-2 border-b px-4 backdrop-blur sm:px-6">
          <button
            type="button"
            className="hover:bg-surface-muted -ml-2 rounded-lg p-2 lg:hidden"
            onClick={() => setDrawerOpen(true)}
            aria-label="Open menu"
            aria-expanded={drawerOpen}
          >
            <Icon name="menu" />
          </button>
          <div className="lg:hidden">
            <Logo compact />
          </div>
          <div className="ml-auto flex items-center gap-1">
            <ThemeToggle />
            <NotificationBell role={user.role} />
          </div>
        </header>
        <main
          id="main"
          className="animate-fade-in mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6 lg:py-8"
        >
          {children}
        </main>
      </div>
    </div>
  );
}
