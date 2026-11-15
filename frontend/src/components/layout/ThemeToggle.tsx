"use client";
import { useTheme } from "next-themes";
import { useSyncExternalStore } from "react";
import { Icon } from "@/components/ui/Icon";

// True only after hydration – avoids a server/client icon mismatch.
const subscribe = () => () => {};
function useMounted() {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const mounted = useMounted();
  const isDark = mounted && resolvedTheme === "dark";
  return (
    <button
      type="button"
      onClick={() => setTheme(isDark ? "light" : "dark")}
      className="text-fg-muted hover:bg-surface-muted hover:text-fg rounded-lg p-2"
      aria-label={isDark ? "Switch to light mode" : "Switch to dark mode"}
    >
      <Icon name={isDark ? "sun" : "moon"} />
    </button>
  );
}
