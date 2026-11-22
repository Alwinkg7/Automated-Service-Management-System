import type { NextConfig } from "next";

// Origins the browser talks to directly (SignalR hub, Razorpay checkout).
// Regular API calls go through our own /api/proxy route, so they are same-origin.
const apiOrigin = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5001";
const isDev = process.env.NODE_ENV !== "production";

function toWs(origin: string) {
  return origin.replace(/^http/, "ws");
}

const csp = [
  "default-src 'self'",
  // Next.js needs inline scripts for hydration; dev also needs eval for HMR.
  `script-src 'self' 'unsafe-inline' ${isDev ? "'unsafe-eval'" : ""} https://checkout.razorpay.com`,
  "style-src 'self' 'unsafe-inline'",
  // OpenStreetMap tiles for the live tracking map.
  "img-src 'self' data: blob: https://*.tile.openstreetmap.org https://unpkg.com",
  "font-src 'self' data:",
  `connect-src 'self' ${apiOrigin} ${toWs(apiOrigin)} https://*.razorpay.com https://lumberjack.razorpay.com ${isDev ? "ws://localhost:3000" : ""}`,
  "frame-src https://api.razorpay.com https://checkout.razorpay.com",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
]
  .join("; ")
  .replace(/\s+/g, " ");

const nextConfig: NextConfig = {
  // Cache Components is off on purpose: authenticated pages read the session
  // cookie on every request, and the classic dynamic model keeps that simple.
  poweredByHeader: false,
  // Self-contained server bundle (.next/standalone) for Azure App Service / Docker.
  output: "standalone",
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: csp },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=(self), payment=(self)",
          },
        ],
      },
    ];
  },
};

export default nextConfig;
