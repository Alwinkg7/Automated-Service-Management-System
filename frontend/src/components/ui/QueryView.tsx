"use client";
// Renders the right state for a TanStack query: skeleton → error → empty → data.
import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { isApiError } from "@/lib/api/errors";
import { SkeletonList } from "./Skeleton";
import { EmptyState, ErrorState } from "./States";

interface QueryViewProps<T> {
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
  loading?: ReactNode;
  /** Shown when `isEmpty(data)` is true. */
  empty?: ReactNode;
  isEmpty?: (data: T) => boolean;
  /**
   * For screens whose endpoint the API doesn't provide yet: a 404/405/501 shows
   * a friendly "not available yet" state naming the endpoint instead of an error.
   */
  pendingEndpoint?: string;
}

export function QueryView<T>({ query, children, loading, empty, isEmpty, pendingEndpoint }: QueryViewProps<T>) {
  if (query.isPending) return <>{loading ?? <SkeletonList />}</>;
  if (query.isError && pendingEndpoint && isApiError(query.error) && [404, 405, 501].includes(query.error.status)) {
    return (
      <EmptyState
        title="Not available yet"
        description={
          <>
            This screen is ready but the API doesn&apos;t provide <code className="text-xs">{pendingEndpoint}</code> yet.
          </>
        }
      />
    );
  }
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (empty && isEmpty?.(query.data)) return <>{empty}</>;
  return <>{children(query.data)}</>;
}
