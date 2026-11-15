import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { JobsView } from "./JobsView";

export const metadata: Metadata = { title: "My jobs" };

export default function JobsPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <JobsView />
    </Suspense>
  );
}
