#!/usr/bin/env node
// Route-level SEO verification. Serves .vitepress/dist through a minimal
// emulator of Cloudflare Pages' clean-URL + _redirects behavior, then asserts
// (exit 1 on any failure) for every sitemap URL:
//   - the clean URL returns 200 with a self-referencing canonical
//   - the legacy .html URL returns 301/308 to the clean URL
//   - following the legacy URL terminates at a 200 with no redirect loop
// The emulator keeps this runnable in CI without deploying to Cloudflare.

import { createServer, get as httpGet } from "node:http";
import { statSync, existsSync, readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const distDir = join(here, "..", ".vitepress", "dist");
const hostname = "https://fusion.actuallab.net";

if (!existsSync(distDir)) {
  console.error(`verify-routes: dist not found at ${distDir}; run the build first.`);
  process.exit(1);
}

// --- _redirects rules ------------------------------------------------------
const rules = [];
const redirectsPath = join(distDir, "_redirects");
if (existsSync(redirectsPath)) {
  for (const line of readFileSync(redirectsPath, "utf-8").split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith("#"))
      continue;
    const [src, dest, code] = t.split(/\s+/);
    rules.push({ src, dest, code: Number(code), splat: src.endsWith("/*") });
  }
}

function matchRule(path) {
  for (const rule of rules) {
    if (rule.splat) {
      const prefix = rule.src.slice(0, -1);
      if (path.startsWith(prefix))
        return { rule, splat: path.slice(prefix.length) };
    } else if (path === rule.src)
      return { rule, splat: "" };
  }
  return null;
}

function staticFile(path) {
  const candidates = path === "/"
    ? ["index.html"]
    : path.endsWith("/")
      ? [path.slice(1), `${path.slice(1)}index.html`]
      : [path.slice(1), `${path.slice(1)}.html`];
  for (const c of candidates) {
    const abs = join(distDir, c);
    if (existsSync(abs) && statSync(abs).isFile())
      return abs;
  }
  return null;
}

const server = createServer((req, res) => {
  const path = decodeURIComponent(req.url.replace(/[?#].*$/, ""));
  const match = matchRule(path);
  if (match) {
    const { rule, splat } = match;
    if (rule.code === 200) {
      const original = staticFile(path);
      const target = original ?? join(distDir, rule.dest.replace(/^\//, ""));
      if (existsSync(target) && statSync(target).isFile()) {
        res.writeHead(200, { "content-type": "text/html" });
        res.end(readFileSync(target));
        return;
      }
    } else {
      res.writeHead(rule.code, { location: rule.dest.replace(":splat", splat) });
      res.end();
      return;
    }
  }
  const file = staticFile(path);
  if (file) {
    res.writeHead(200, { "content-type": "text/html" });
    res.end(readFileSync(file));
    return;
  }
  res.writeHead(404);
  res.end("Not found");
});

function get(base, path) {
  return new Promise((resolve, reject) => {
    const request = httpGet(new URL(path, base), (res) => {
      const chunks = [];
      res.on("data", (c) => chunks.push(c));
      res.on("end", () => resolve({
        status: res.statusCode,
        location: res.headers.location,
        body: Buffer.concat(chunks).toString("utf-8"),
      }));
    });
    request.on("error", reject);
  });
}

const errors = [];
const fail = (msg) => errors.push(msg);

function canonicalOf(body) {
  const tag = body.match(/<link\b[^>]*\brel=["']canonical["'][^>]*>/i);
  return tag ? tag[0].match(/\bhref=["']([^"']+)["']/i)?.[1] ?? null : null;
}

function normalizePath(location) {
  if (!location)
    return null;
  return location.startsWith("http") ? new URL(location).pathname : location;
}

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const base = `http://127.0.0.1:${server.address().port}`;

try {
  const sitemapXml = readFileSync(join(distDir, "sitemap.xml"), "utf-8");
  const locs = [...sitemapXml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);

  for (const loc of locs) {
    const cleanPath = loc.slice(hostname.length) || "/";
    const legacy = cleanPath === "/"
      ? "/index.html"
      : cleanPath.endsWith("/")
        ? `${cleanPath}index.html`
        : `${cleanPath}.html`;

    const clean = await get(base, cleanPath);
    if (clean.status !== 200)
      fail(`${cleanPath} returned ${clean.status}, expected 200`);
    else {
      const canon = canonicalOf(clean.body);
      if (canon !== `${hostname}${cleanPath}`)
        fail(`${cleanPath} canonical is ${canon}, expected ${hostname}${cleanPath}`);
    }

    const legacyRes = await get(base, legacy);
    if (![301, 308].includes(legacyRes.status))
      fail(`${legacy} returned ${legacyRes.status}, expected 301/308`);
    else if (normalizePath(legacyRes.location) !== cleanPath)
      fail(`${legacy} redirects to ${legacyRes.location}, expected ${cleanPath}`);

    // Follow the chain from the legacy URL and confirm it ends at 200.
    let hop = legacy;
    let ok = false;
    for (let i = 0; i < 10; i++) {
      const r = await get(base, hop);
      if ([301, 302, 307, 308].includes(r.status)) {
        hop = normalizePath(r.location);
        continue;
      }
      ok = r.status === 200;
      break;
    }
    if (!ok)
      fail(`${legacy} did not terminate at 200 within 10 hops (possible loop)`);
  }

  if (errors.length) {
    console.error(`verify-routes: ${errors.length} problem(s):`);
    for (const e of errors)
      console.error(`  - ${e}`);
    process.exitCode = 1;
  } else
    console.log(`verify-routes: OK (${locs.length} clean URLs 200 + canonical, ` +
      `legacy .html 301 -> clean, no loops)`);
} finally {
  server.close();
}
