import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { RaiseDisputeView } from "./RaiseDisputeView";

export const metadata: Metadata = { title: "Raise a dispute" };

export default function RaiseDisputePage() {
  return (
    <Suspense fallback={<SkeletonList rows={2} />}>
      <RaiseDisputeView />
    </Suspense>
  );
}
