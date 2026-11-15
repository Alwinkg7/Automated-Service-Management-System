import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { CustomerRequestsView } from "./CustomerRequestsView";

export const metadata: Metadata = { title: "My requests" };

export default function CustomerRequestsPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <CustomerRequestsView />
    </Suspense>
  );
}
