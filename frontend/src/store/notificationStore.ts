"use client";
// In-memory notification list for the bell badge (realtime events).
import { create } from "zustand";

export interface AppNotification {
  id: string;
  event: string;
  message: string;
  requestId?: number;
  at: number;
  read: boolean;
}

interface NotificationState {
  items: AppNotification[];
  connected: boolean;
  add: (n: Omit<AppNotification, "id" | "at" | "read">) => void;
  markAllRead: () => void;
  clear: () => void;
  setConnected: (connected: boolean) => void;
}

const MAX_ITEMS = 30;

export const useNotificationStore = create<NotificationState>((set) => ({
  items: [],
  connected: false,
  add: (n) =>
    set((state) => ({
      items: [
        { ...n, id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`, at: Date.now(), read: false },
        ...state.items,
      ].slice(0, MAX_ITEMS),
    })),
  markAllRead: () => set((state) => ({ items: state.items.map((i) => ({ ...i, read: true })) })),
  clear: () => set({ items: [] }),
  setConnected: (connected) => set({ connected }),
}));

export const selectUnreadCount = (state: NotificationState) => state.items.filter((i) => !i.read).length;
