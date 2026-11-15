"use client";
// Renders the right state for a TanStack query: skeleton → error → empty → data.
import type { UseQueryResult } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { SkeletonList } from "./Skeleton";
import { ErrorState } from "./States";

interface QueryViewProps<T> {
  query: UseQueryResult<T>;
  children: (data: T) => ReactNode;
  loading?: ReactNode;
  /** Shown when `isEmpty(data)` is true. */
  empty?: ReactNode;
  isEmpty?: (data: T) => boolean;
}

export function QueryView<T>({ query, children, loading, empty, isEmpty }: QueryViewProps<T>) {
  if (query.isPending) return <>{loading ?? <SkeletonList />}</>;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (empty && isEmpty?.(query.data)) return <>{empty}</>;
  return <>{children(query.data)}</>;
}
