import type { Metadata } from "next";
import { numericId } from "@/lib/route-params";
import { PayView } from "./PayView";

type Props = { params: Promise<{ id: string }> };

export const metadata: Metadata = { title: "Pay bill" };

export default async function PayPage({ params }: Props) {
  return <PayView requestId={await numericId(params)} />;
}
