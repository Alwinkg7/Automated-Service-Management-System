import type { Metadata } from "next";
import { WalletView } from "./WalletView";

export const metadata: Metadata = { title: "Earnings" };

export default function WalletPage() {
  return <WalletView />;
}
