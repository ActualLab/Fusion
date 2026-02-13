import * as esbuild from "esbuild";
import path from "path";
import { createRequire } from "module";

const isWatch = process.argv.includes("--watch");
const isProd = process.env.NODE_ENV === "production";

// Force all react imports to resolve to the single copy in our node_modules.
// Without this, file:-linked packages (fusion-react) can resolve their own
// copy of react, causing the "Invalid hook call" error at runtime.
const require = createRequire(import.meta.url);
const reactPath = path.dirname(require.resolve("react/package.json"));
const reactDomPath = path.dirname(require.resolve("react-dom/package.json"));

/** @type {import("esbuild").BuildOptions} */
const buildOptions = {
  entryPoints: ["src/index.tsx"],
  bundle: true,
  format: "iife",
  outfile: "../Host/wwwroot/js/todo-react.js",
  minify: isProd,
  sourcemap: !isProd,
  target: "es2022",
  logLevel: "info",
  alias: {
    "react": reactPath,
    "react-dom": reactDomPath,
  },
};

if (isWatch) {
  const ctx = await esbuild.context(buildOptions);
  await ctx.watch();
  console.log("Watching for changes...");
} else {
  await esbuild.build(buildOptions);
}
