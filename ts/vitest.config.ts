import { defineConfig } from "vitest/config";
import path from "path";

export default defineConfig({
  resolve: {
    alias: {
      "@actuallab/core": path.resolve(__dirname, "packages/core/src/index.ts"),
      "@actuallab/rpc": path.resolve(__dirname, "packages/rpc/src/index.ts"),
      "@actuallab/fusion": path.resolve(__dirname, "packages/fusion/src/index.ts"),
    },
  },
  test: {
    include: ["packages/*/tests/**/*.test.ts"],
  },
});
