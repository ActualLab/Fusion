import { defineConfig } from "tsup";

export default defineConfig({
  format: ["esm", "cjs"],
  dts: true,
  clean: true,
  sourcemap: true,
});
