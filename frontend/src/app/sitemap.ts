import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/env";
import { SERVICES } from "@/lib/services-content";

export default function sitemap(): MetadataRoute.Sitemap {
  const pages = ["", "/services", "/login", "/register"].map((path) => ({
    url: `${SITE_URL}${path}`,
    changeFrequency: "monthly" as const,
    priority: path === "" ? 1 : 0.6,
  }));
  const services = SERVICES.map((s) => ({
    url: `${SITE_URL}/services/${s.slug}`,
    changeFrequency: "monthly" as const,
    priority: 0.8,
  }));
  return [...pages, ...services];
}
