import { InfoList } from "@/components/features/request/InfoList";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import type { Dispute } from "@/lib/api/schemas";
import { formatDate, formatMoney } from "@/lib/format";
import { humanize } from "@/types/enums";

export function DisputeDetailCard({ dispute, requestHref }: { dispute: Dispute; requestHref: string }) {
  return (
    <Card>
      <CardHeader
        title={`Dispute #${dispute.id}`}
        action={
          <div className="flex gap-2">
            {dispute.isSlaBreached ? <Badge tone="danger">SLA breached</Badge> : null}
            <StatusBadge status={dispute.status} />
          </div>
        }
      />
      <CardBody className="space-y-4">
        <InfoList
          items={[
            { label: "Request", value: <a href={requestHref} className="text-primary hover:underline">#{dispute.requestId}</a> },
            { label: "Reason", value: humanize(dispute.reason) },
            { label: "Raised", value: formatDate(dispute.raisedAt, true) },
            { label: "Respond by", value: formatDate(dispute.slaDeadline, true) },
            ...(dispute.raisedByName ? [{ label: "Raised by", value: dispute.raisedByName }] : []),
            ...(dispute.resolution ? [{ label: "Resolution", value: humanize(dispute.resolution) }] : []),
            ...(dispute.refundIssued ? [{ label: "Refund", value: `${formatMoney(dispute.refundAmount)} (${dispute.razorpayRefundId ?? "processing"})` }] : []),
            ...(dispute.resolvedAt ? [{ label: "Resolved", value: formatDate(dispute.resolvedAt, true) }] : []),
          ]}
        />
        <div>
          <p className="text-sm text-fg-muted">Description</p>
          <p className="mt-1 text-sm whitespace-pre-wrap">{dispute.description}</p>
        </div>
        {dispute.adminNote ? (
          <div className="rounded-lg bg-surface-muted p-3 text-sm">
            <p className="font-medium">Note from support</p>
            <p className="mt-1 whitespace-pre-wrap text-fg-muted">{dispute.adminNote}</p>
          </div>
        ) : null}
      </CardBody>
    </Card>
  );
}
