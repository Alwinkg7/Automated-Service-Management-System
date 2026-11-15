import Link from "next/link";
import { CategoryIcon } from "@/components/features/CategoryIcon";
import { StatusBadge } from "@/components/ui/Badge";
import { formatDate } from "@/lib/format";
import { CATEGORY_INFO, type RequestStatus, type ServiceCategory } from "@/types/enums";

interface JobLike {
  id: number;
  category: ServiceCategory;
  status: RequestStatus;
  customerName?: string | null;
  address?: string | null;
  scheduledDate: string;
  description?: string | null;
}

export function JobCard({ job }: { job: JobLike }) {
  return (
    <Link
      href={`/technician/jobs/${job.id}`}
      className="flex gap-4 rounded-card border border-border bg-surface p-4 transition-colors hover:border-primary/40"
    >
      <CategoryIcon category={job.category} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="font-medium">
            {CATEGORY_INFO[job.category].label} <span className="text-fg-muted">#{job.id}</span>
          </p>
          <StatusBadge status={job.status} />
        </div>
        <p className="mt-1 text-sm text-fg-muted">
          {job.customerName ?? "Customer"} · {formatDate(job.scheduledDate, true)}
        </p>
        {job.address ? <p className="mt-0.5 line-clamp-1 text-xs text-fg-muted">{job.address}</p> : null}
      </div>
    </Link>
  );
}
