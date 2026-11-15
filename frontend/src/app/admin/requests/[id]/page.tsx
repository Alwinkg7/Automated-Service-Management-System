import type { Metadata } from "next";
import { numericId } from "@/lib/route-params";
import { AdminRequestDetailView } from "./AdminRequestDetailView";

type Props = { params: Promise<{ id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return { title: `Request #${(await params).id}` };
}

export default async function AdminRequestDetailPage({ params }: Props) {
  return <AdminRequestDetailView id={await numericId(params)} />;
}
