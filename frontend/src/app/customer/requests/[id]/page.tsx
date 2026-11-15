import type { Metadata } from "next";
import { numericId } from "@/lib/route-params";
import { RequestDetailView } from "./RequestDetailView";

type Props = { params: Promise<{ id: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return { title: `Request #${(await params).id}` };
}

export default async function RequestDetailPage({ params }: Props) {
  return <RequestDetailView id={await numericId(params)} />;
}
