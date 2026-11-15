import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { TechniciansView } from "./TechniciansView";

export const metadata: Metadata = { title: "Technicians" };

export default function TechniciansPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <TechniciansView />
    </Suspense>
  );
}
