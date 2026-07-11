#!/usr/bin/env node
// Static, build-time SEO gate over .vitepress/dist. Fails (exit 1) when:
//   - a sitemap <loc> ends in .html
//   - a page's <link rel="canonical"> is missing or ends in .html
//   - a page has a same-origin internal <a href> ending in .html
//   - a sitemap URL has no matching legacy .html redirect in _redirects
// Route-level checks (200 / 301 status codes) live in verify-routes.mjs.

import { readdirSync, statSync, existsSync, readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const distDir = join(here, "..", ".vitepress", "dist");
const hostname = "https://fusion.actuallab.net";
const slideRoots = new Set(["slides", "slides-old", "slides-shots"]);

const errors = [];
const fail = (msg) => errors.push(msg);

function walk(dir, base = "") {
  const out = [];
  for (const name of readdirSync(dir)) {
    const abs = join(dir, name);
    const rel = base ? `${base}/${name}` : name;
    if (statSync(abs).isDirectory())
      out.push(...walk(abs, rel));
    else if (name.endsWith(".html"))
      out.push(rel);
  }
  return out;
}

const isSlidePath = (rel) => slideRoots.has(rel.split("/")[0]);

if (!existsSync(distDir)) {
  console.error(`seo-check: dist not found at ${distDir}; run the build first.`);
  process.exit(1);
}

// --- Sitemap ---------------------------------------------------------------
const sitemapPath = join(distDir, "sitemap.xml");
if (!existsSync(sitemapPath))
  fail("sitemap.xml is missing");
const sitemapXml = existsSync(sitemapPath) ? readFileSync(sitemapPath, "utf-8") : "";
const locs = [...sitemapXml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
if (locs.length === 0 && sitemapXml)
  fail("sitemap.xml contains no <loc> entries");
for (const loc of locs)
  if (loc.endsWith(".html"))
    fail(`sitemap <loc> ends in .html: ${loc}`);

// --- _redirects coverage ---------------------------------------------------
const redirectsPath = join(distDir, "_redirects");
if (!existsSync(redirectsPath))
  fail("_redirects is missing (run gen-redirects.mjs)");
const redirectMap = new Map();
if (existsSync(redirectsPath)) {
  for (const line of readFileSync(redirectsPath, "utf-8").split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith("#"))
      continue;
    const [src, dest, code] = t.split(/\s+/);
    redirectMap.set(src, { dest, code });
  }
}

for (const loc of locs) {
  const cleanPath = loc.slice(hostname.length) || "/";
  const legacy = cleanPath === "/"
    ? "/index.html"
    : cleanPath.endsWith("/")
      ? `${cleanPath}index.html`
      : `${cleanPath}.html`;
  const rule = redirectMap.get(legacy);
  if (!rule)
    fail(`no legacy redirect for sitemap URL ${cleanPath} (expected ${legacy})`);
  else if (rule.dest !== cleanPath || !/^30[18]$/.test(rule.code))
    fail(`redirect ${legacy} -> ${rule.dest} (${rule.code}); expected -> ${cleanPath} 301/308`);
}

// --- Per-page canonical + internal links -----------------------------------
const canonicalRe = /<link\b[^>]*\brel=["']canonical["'][^>]*>/i;
const hrefRe = /\bhref=["']([^"']+)["']/gi;

for (const rel of walk(distDir)) {
  if (rel === "404.html" || isSlidePath(rel))
    continue;
  const html = readFileSync(join(distDir, rel), "utf-8");
  const headEnd = html.indexOf("</head>");
  const head = headEnd === -1 ? html : html.slice(0, headEnd);

  const canonTag = head.match(canonicalRe);
  if (!canonTag)
    fail(`${rel}: missing <link rel="canonical">`);
  else {
    const href = canonTag[0].match(/\bhref=["']([^"']+)["']/i)?.[1] ?? "";
    if (href.endsWith(".html"))
      fail(`${rel}: canonical ends in .html: ${href}`);
  }

  for (const m of html.matchAll(hrefRe)) {
    const href = m[1];
    const sameOrigin = href.startsWith("/") && !href.startsWith("//")
      ? href
      : href.startsWith(`${hostname}/`)
        ? href.slice(hostname.length)
        : null;
    if (sameOrigin === null)
      continue;
    const path = sameOrigin.replace(/[?#].*$/, "");
    if (path.endsWith(".html") && !isSlidePath(path.replace(/^\//, "")))
      fail(`${rel}: same-origin internal link ends in .html: ${href}`);
  }
}

if (errors.length) {
  console.error(`seo-check: ${errors.length} problem(s):`);
  for (const e of errors)
    console.error(`  - ${e}`);
  process.exit(1);
}
console.log(`seo-check: OK (${locs.length} sitemap URLs, all canonical + extensionless)`);
