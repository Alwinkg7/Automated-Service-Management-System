import type { Metadata } from "next";
import { numericId } from "@/lib/route-params";
import { TrackingView } from "./TrackingView";

type Props = { params: Promise<{ id: string }> };

export const metadata: Metadata = { title: "Live tracking" };

export default async function TrackingPage({ params }: Props) {
  return <TrackingView id={await numericId(params)} />;
}
