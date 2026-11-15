import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { AdminRequestsView } from "./AdminRequestsView";

export const metadata: Metadata = { title: "All requests" };

export default function AdminRequestsPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <AdminRequestsView />
    </Suspense>
  );
}
