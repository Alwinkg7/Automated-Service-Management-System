import type { Metadata } from "next";
import { PromotionsView } from "./PromotionsView";

export const metadata: Metadata = { title: "Rewards" };

export default function PromotionsPage() {
  return <PromotionsView />;
}
