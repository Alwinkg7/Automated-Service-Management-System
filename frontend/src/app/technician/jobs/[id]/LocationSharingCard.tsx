"use client";
import { Button } from "@/components/ui/Button";
import { Card, CardBody } from "@/components/ui/Card";
import { Icon } from "@/components/ui/Icon";
import { useLocationSharing } from "@/hooks/useLocationSharing";

const MESSAGES = {
  off: "Share your live location so the customer can see you on the map.",
  starting: "Getting your location…",
  on: "Sharing your live location with the customer.",
  denied: "Location permission was denied. Enable it in your browser settings to share your location.",
  unsupported: "This device doesn't support location sharing.",
  error: "Couldn't send your location. We'll keep trying.",
} as const;

export function LocationSharingCard({ requestId }: { requestId: number }) {
  const { state, lastSent, start, stop, active } = useLocationSharing(requestId);
  return (
    <Card>
      <CardBody className="flex flex-wrap items-center gap-4">
        <span className={`rounded-full p-2 ${active ? "bg-success-soft text-success" : "bg-surface-muted text-fg-muted"}`}>
          <Icon name="pin" />
        </span>
        <div className="min-w-0 flex-1 text-sm" aria-live="polite">
          <p className="font-medium">Live location</p>
          <p className="text-fg-muted">
            {MESSAGES[state]}
            {state === "on" && lastSent ? ` Last sent ${lastSent.toLocaleTimeString()}.` : ""}
          </p>
        </div>
        {active ? (
          <Button variant="outline" onClick={() => void stop()}>
            Stop sharing
          </Button>
        ) : (
          <Button onClick={start} disabled={state === "unsupported"}>
            Share location
          </Button>
        )}
      </CardBody>
    </Card>
  );
}
