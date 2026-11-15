import type { Metadata } from "next";
import { Suspense } from "react";
import { SkeletonList } from "@/components/ui/Skeleton";
import { UsersView } from "./UsersView";

export const metadata: Metadata = { title: "Users" };

export default function UsersPage() {
  return (
    <Suspense fallback={<SkeletonList />}>
      <UsersView />
    </Suspense>
  );
}
