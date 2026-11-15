import type { Metadata } from "next";
import { numericId } from "@/lib/route-params";
import { JobDetailView } from "./JobDetailView";

type Props = { params: Promise<{ id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return { title: `Job #${(await params).id}` };
}

export default async function JobDetailPage({ params }: Props) {
  return <JobDetailView id={await numericId(params)} />;
}
