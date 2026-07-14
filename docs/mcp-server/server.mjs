import { readFileSync as readSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import express from "express";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { z } from "zod";
import {
  renderExpandedSearch,
  renderIntro,
  renderSearch,
  renderSection,
} from "../mcp/search.js";
import { createSourceTools } from "./tools/source.mjs";

const serverDir = path.dirname(fileURLToPath(import.meta.url));
const docsDir = path.resolve(serverDir, "..");
const repoRoot = path.resolve(serverDir, "../..");

const distDir = process.env.DIST_DIR ?? path.join(docsDir, ".vitepress", "dist");
const docsIndexPath = process.env.DOCS_INDEX ?? path.join(docsDir, ".generated", "mcp-index.json");
const sourceBaseDir = process.env.SOURCE_DIR ?? repoRoot;
const sourceRoots = (process.env.SOURCE_ROOTS ?? "src,samples,tests").split(",").map(root => root.trim()).filter(Boolean);
const siteUrl = process.env.SITE_URL ?? "https://fusion.actuallab.net";
const port = Number(process.env.PORT ?? 8080);

const docsIndex = existsSync(docsIndexPath)
  ? JSON.parse(readSync(docsIndexPath, "utf8"))
  : { intro: "Documentation index is unavailable.", sections: [] };

const source = createSourceTools(sourceBaseDir, sourceRoots);

const textResult = text => ({ content: [{ type: "text", text }] });

function createMcpServer() {
  const server = new McpServer({ name: "ActualLab.Fusion Documentation", version: "2.0.0" });

  server.registerTool("intro", {
    description: "Get a concise Markdown introduction to ActualLab.Fusion, its mental model, and the available documentation.",
    inputSchema: {},
  }, async () => textResult(renderIntro(docsIndex)));

  server.registerTool("search", {
    description: "Search Fusion documentation and return ranked Markdown links and exact anchors without expanding their content.",
    inputSchema: {
      query: z.string().min(1).describe("Keywords or phrase to find."),
      limit: z.number().int().min(1).max(20).optional().default(10).describe("Number of matches, from 1 through 20."),
    },
  }, async ({ query, limit }) => textResult(renderSearch(docsIndex, query, limit, siteUrl)));

  server.registerTool("get", {
    description: "Get the Markdown for one exact documentation anchor. Sections up to ~4000 characters are returned in full including every sub-heading; larger sections return the immediate text plus links to their sub-headings to fetch individually.",
    inputSchema: {
      anchor: z.string().min(1).describe("An anchor returned by search, such as PartF#invalidation or a full documentation URL."),
    },
  }, async ({ anchor }) => textResult(renderSection(docsIndex, anchor, siteUrl)));

  server.registerTool("search_expanded", {
    description: "Search Fusion documentation and expand each matched Markdown section through the next heading at the same or a higher level.",
    inputSchema: {
      query: z.string().min(1).describe("Keywords or phrase to find."),
      limit: z.number().int().min(1).max(10).optional().default(5).describe("Number of expanded matches, from 1 through 10."),
    },
  }, async ({ query, limit }) => textResult(renderExpandedSearch(docsIndex, query, limit, siteUrl)));

  server.registerTool("source_index", {
    description: "Find Fusion source files by regex over a manifest of file paths and their top-level type names. Use this first to locate the file(s) you need, then source_read or source_search.",
    inputSchema: {
      pattern: z.string().min(1).describe("Case-insensitive regular expression matched against 'path :: TypeNames' lines."),
      limit: z.number().int().min(1).max(200).optional().default(50).describe("Max matching files to list."),
    },
  }, async ({ pattern, limit }) => textResult(await source.sourceIndex({ pattern, limit })));

  server.registerTool("symbol_search", {
    description: "Find Fusion declarations (types, methods, properties, fields — any accessibility) by regex over a 'name  kind  path  startLine  endLine' manifest. Each hit gives an exact region to fetch with source_read.",
    inputSchema: {
      pattern: z.string().min(1).describe("Case-insensitive regex matched against declaration lines (e.g. '^Invalidate' or '\\bComputed\\b')."),
      limit: z.number().int().min(1).max(200).optional().default(50).describe("Max matching declarations to list."),
    },
  }, async ({ pattern, limit }) => textResult(await source.symbolSearch({ pattern, limit })));

  server.registerTool("source_search", {
    description: "Run ripgrep over Fusion .cs/.razor source (src, samples, tests). Output is capped at 64 KB and 1 s; for heavy navigation, clone github.com/ActualLab/Fusion.",
    inputSchema: {
      query: z.string().min(1).describe("Ripgrep regular expression (or literal with fixedStrings)."),
      context: z.number().int().min(0).max(5).optional().default(2).describe("Context lines around each match (0-5)."),
      fixedStrings: z.boolean().optional().default(false).describe("Treat query as a literal string."),
      ignoreCase: z.boolean().optional().default(false).describe("Case-insensitive search."),
    },
  }, async ({ query, context, fixedStrings, ignoreCase }) =>
    textResult(await source.sourceSearch({ query, context, fixedStrings, ignoreCase })));

  server.registerTool("source_read", {
    description: "Read a Fusion source file (or a line range of it). Whole-file reads are capped at 64 KB; pass startLine/endLine for larger files.",
    inputSchema: {
      file: z.string().min(1).describe("Repo-relative path, e.g. src/ActualLab.Core/Result.cs (from source_index/source_search)."),
      startLine: z.number().int().min(1).optional().describe("First line (1-based)."),
      endLine: z.number().int().min(1).optional().describe("Last line (inclusive)."),
    },
  }, async ({ file, startLine, endLine }) => textResult(await source.sourceRead({ file, startLine, endLine })));

  return server;
}

const app = express();
app.use(express.json({ limit: "4mb" }));

app.all("/mcp", async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).json({ jsonrpc: "2.0", error: { code: -32000, message: "Method not allowed." }, id: null });
    return;
  }
  const server = createMcpServer();
  const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined, enableJsonResponse: true });
  res.on("close", () => { transport.close(); server.close(); });
  try {
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  }
  catch (error) {
    if (!res.headersSent)
      res.status(500).json({ jsonrpc: "2.0", error: { code: -32603, message: String(error) }, id: null });
  }
});

app.get("/healthz", (_req, res) => res.type("text/plain").send("ok"));

applyRedirects(app, distDir);
app.use(express.static(distDir, { extensions: ["html"], dotfiles: "ignore" }));
app.use((req, res) => {
  const notFound = path.join(distDir, "404.html");
  if (existsSync(notFound))
    res.status(404).sendFile(notFound);
  else
    res.status(404).type("text/plain").send("Not found");
});

function applyRedirects(application, dir) {
  const file = path.join(dir, "_redirects");
  if (!existsSync(file))
    return;
  const rules = readSync(file, "utf8").split(/\r?\n/)
    .map(line => line.trim())
    .filter(line => line && !line.startsWith("#"))
    .map(line => line.split(/\s+/))
    .filter(parts => parts.length >= 2 && !parts[0].includes("*"))
    .map(([from, to, code]) => ({ from, to, code: Number(code) || 301 }));
  const byPath = new Map(rules.map(rule => [rule.from.replace(/\/$/, ""), rule]));
  application.use((req, res, next) => {
    const rule = byPath.get(req.path.replace(/\/$/, ""));
    if (rule)
      res.redirect(rule.code, rule.to);
    else
      next();
  });
}

app.listen(port, () => {
  console.log(`Fusion docs + MCP server on :${port} (dist=${distDir}, source=${sourceBaseDir} [${sourceRoots.join(", ")}])`);
});
