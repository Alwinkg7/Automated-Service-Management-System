"use client";
import { useQuery } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import type { LatLng } from "@/components/features/tracking/TrackingMap";
import { StatusBadge } from "@/components/ui/Badge";
import { Card, CardBody } from "@/components/ui/Card";
import { Icon } from "@/components/ui/Icon";
import { QueryView } from "@/components/ui/QueryView";
import { Skeleton } from "@/components/ui/Skeleton";
import { EmptyState } from "@/components/ui/States";
import { ButtonLink } from "@/components/ui/Button";
import { useHubEvent } from "@/hooks/useHubEvent";
import { customerApi } from "@/lib/api";
import type { LocationPayload } from "@/lib/realtime/connection";
import { qk } from "@/lib/queryKeys";

// Leaflet touches `window`, so it is only loaded in the browser.
const TrackingMap = dynamic(() => import("@/components/features/tracking/TrackingMap"), {
  ssr: false,
  loading: () => <Skeleton className="h-full w-full" />,
});

const MAX_TRAIL = 200;

export function TrackingView({ id }: { id: number }) {
  const query = useQuery({
    queryKey: qk.customer.request(id),
    queryFn: () => customerApi.getRequest(id),
  });
  const [trail, setTrail] = useState<LatLng[]>([]);
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [stopped, setStopped] = useState(false);
  const live = query.data?.status === "InProgress";

  useHubEvent<LocationPayload>(
    "LocationUpdate",
    (p) => {
      if (p.requestId !== id) return;
      setStopped(false);
      setTrail((t) => [...t, { lat: p.lat, lng: p.lng }].slice(-MAX_TRAIL));
      setUpdatedAt(new Date());
    },
    live,
  );
  useHubEvent<{ requestId: number }>(
    "LocationStopped",
    (p) => p.requestId === id && setStopped(true),
    live,
  );

  const position = trail.at(-1) ?? null;

  return (
    <QueryView query={query}>
      {(r) => (
        <>
          <PageHeader
            back={{ href: `/customer/requests/${id}`, label: `Request #${id}` }}
            title="Live tracking"
            description={r.technician?.name ? `Following ${r.technician.name}` : undefined}
            actions={<StatusBadge status={r.status} />}
          />
          {!live ? (
            <EmptyState
              title="Tracking isn't available"
              description="Live tracking is available while your job is in progress."
              action={<ButtonLink href={`/customer/requests/${id}`}>Back to request</ButtonLink>}
            />
          ) : (
            <div className="space-y-4">
              <Card>
                <CardBody className="flex flex-wrap items-center gap-3 text-sm" aria-live="polite">
                  <span
                    className={`size-2.5 rounded-full ${position && !stopped ? "bg-success animate-pulse" : "bg-fg-muted"}`}
                    aria-hidden
                  />
                  {stopped
                    ? "The technician has stopped sharing their location."
                    : position
                      ? `Last update ${updatedAt?.toLocaleTimeString() ?? ""}`
                      : "Waiting for the technician's location…"}
                  {r.technician?.phone ? (
                    <a
                      href={`tel:${r.technician.phone}`}
                      className="text-primary ml-auto inline-flex items-center gap-1.5 hover:underline"
                    >
                      <Icon name="phone" className="size-4" /> Call technician
                    </a>
                  ) : null}
                </CardBody>
              </Card>
              <div className="rounded-card border-border h-[60vh] min-h-80 overflow-hidden border">
                <TrackingMap position={position} trail={trail} />
              </div>
            </div>
          )}
        </>
      )}
    </QueryView>
  );
}
