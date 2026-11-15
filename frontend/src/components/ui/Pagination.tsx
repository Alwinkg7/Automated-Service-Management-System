"use client";
import { Button } from "./Button";
import { Icon } from "./Icon";

export function Pagination({
  page,
  pageSize,
  totalCount,
  onPageChange,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));
  if (totalPages <= 1) return null;
  return (
    <nav aria-label="Pagination" className="flex items-center justify-between gap-3 pt-2 text-sm">
      <p className="text-fg-muted">
        Page <span className="text-fg font-medium">{page}</span> of {totalPages} · {totalCount}{" "}
        total
      </p>
      <div className="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          aria-label="Previous page"
        >
          <Icon name="chevronLeft" className="size-4" />
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          aria-label="Next page"
        >
          <Icon name="chevronRight" className="size-4" />
        </Button>
      </div>
    </nav>
  );
}
