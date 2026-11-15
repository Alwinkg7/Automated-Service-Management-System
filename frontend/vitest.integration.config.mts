import { defineConfig } from "vitest/config";

// Live API + Next.js integration suite (not part of `npm test`).
export default defineConfig({
  resolve: { tsconfigPaths: true },
  test: {
    environment: "node",
    include: ["src/integration/**/*.int.test.ts"],
    testTimeout: 30_000,
    hookTimeout: 30_000,
  },
});
