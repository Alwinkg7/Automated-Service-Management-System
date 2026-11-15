"use client";
// Lazy chart components: recharts is split into its own chunk.
import dynamic from "next/dynamic";
import { createElement } from "react";
import { Skeleton } from "@/components/ui/Skeleton";

const loading = () => createElement(Skeleton, { className: "h-[260px] w-full" });

export const LineSeriesChart = dynamic(() => import("./Charts").then((m) => m.LineSeriesChart), {
  ssr: false,
  loading,
});
export const BarSeriesChart = dynamic(() => import("./Charts").then((m) => m.BarSeriesChart), {
  ssr: false,
  loading,
});
export const DonutChart = dynamic(() => import("./Charts").then((m) => m.DonutChart), {
  ssr: false,
  loading,
});
