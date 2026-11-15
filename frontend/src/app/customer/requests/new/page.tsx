import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { NewRequestView } from "./NewRequestView";

export const metadata: Metadata = { title: "Book a service" };

export default function NewRequestPage() {
  return (
    <Suspense fallback={<SkeletonList rows={3} />}>
      <NewRequestView />
    </Suspense>
  );
}
