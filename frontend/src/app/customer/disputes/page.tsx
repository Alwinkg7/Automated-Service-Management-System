import type { Metadata } from "next";
import { DisputesView } from "./DisputesView";

export const metadata: Metadata = { title: "My disputes" };

export default function DisputesPage() {
  return <DisputesView />;
}
