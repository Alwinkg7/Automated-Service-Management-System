import { SkeletonList, SkeletonStats } from "@/components/ui/Skeleton";
import { Skeleton } from "@/components/ui/Skeleton";

export default function Loading() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-8 w-56" />
      <SkeletonStats />
      <SkeletonList rows={4} />
    </div>
  );
}
