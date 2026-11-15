import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { errorMessage } from "@/lib/api/errors";
import { Button } from "./Button";
import { Icon } from "./Icon";

export function EmptyState({
  title,
  description,
  action,
  className,
}: {
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "rounded-card border-border flex flex-col items-center justify-center border border-dashed px-6 py-12 text-center",
        className,
      )}
    >
      <Icon name="inbox" className="text-fg-muted size-10" />
      <h3 className="mt-3 font-semibold">{title}</h3>
      {description ? <p className="text-fg-muted mt-1 max-w-sm text-sm">{description}</p> : null}
      {action ? <div className="mt-4">{action}</div> : null}
    </div>
  );
}

export function ErrorState({
  error,
  onRetry,
  title = "Something went wrong",
  className,
}: {
  error: unknown;
  onRetry?: () => void;
  title?: string;
  className?: string;
}) {
  return (
    <div
      role="alert"
      className={cn(
        "rounded-card border-danger/30 bg-danger-soft/50 flex flex-col items-center border px-6 py-10 text-center",
        className,
      )}
    >
      <Icon name="alert" className="text-danger size-9" />
      <h3 className="mt-3 font-semibold">{title}</h3>
      <p className="text-fg-muted mt-1 max-w-md text-sm">{errorMessage(error)}</p>
      {onRetry ? (
        <Button variant="outline" className="mt-4" onClick={onRetry}>
          Try again
        </Button>
      ) : null}
    </div>
  );
}
