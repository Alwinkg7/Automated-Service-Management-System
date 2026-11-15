import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { HistoryView } from "./HistoryView";

export const metadata: Metadata = { title: "Service history" };

export default function HistoryPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <HistoryView />
    </Suspense>
  );
}
