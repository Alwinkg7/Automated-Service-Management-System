"use client";
// One shared SignalR connection per browser tab.
// The JWT is fetched from our /api/auth/token route (which refreshes it when
// needed) and sent as access_token, which the hub reads for WebSockets.
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { HUB_URL } from "@/lib/env";

export const HUB_EVENTS = [
  "JobAssigned",
  "NewJobAvailable",
  "JobAccepted",
  "BillCreated",
  "JobCompleted",
  "NewRequest",
  "StatusChanged",
  "LocationUpdate",
  "LocationStopped",
] as const;
export type HubEvent = (typeof HUB_EVENTS)[number];

/** Common payload shape: every event carries the request id; most carry a message. */
export interface HubPayload {
  requestId?: number;
  message?: string;
  timestamp?: string;
  [key: string]: unknown;
}

export interface LocationPayload {
  requestId: number;
  lat: number;
  lng: number;
  timestamp?: string;
}

let connection: HubConnection | null = null;
let starting: Promise<HubConnection> | null = null;

async function fetchToken(): Promise<string> {
  const res = await fetch("/api/auth/token", { cache: "no-store" });
  if (!res.ok) throw new Error("Not signed in");
  const body = (await res.json()) as { data?: { token?: string } };
  return body.data?.token ?? "";
}

function build(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: fetchToken })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(process.env.NODE_ENV === "production" ? LogLevel.None : LogLevel.Warning)
    .build();
}

/** Start (or reuse) the shared connection. */
export function getConnection(): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) return Promise.resolve(connection);
  if (starting) return starting;
  connection ??= build();
  const conn = connection;
  starting = (conn.state === HubConnectionState.Disconnected ? conn.start() : Promise.resolve())
    .then(() => conn)
    .finally(() => {
      starting = null;
    });
  return starting;
}

/** Stop and forget the connection (on logout). */
export async function stopConnection() {
  const conn = connection;
  connection = null;
  if (conn && conn.state !== HubConnectionState.Disconnected) await conn.stop();
}

/** The connection object if it was created (may not be connected yet). */
export function currentConnection() {
  return connection;
}
