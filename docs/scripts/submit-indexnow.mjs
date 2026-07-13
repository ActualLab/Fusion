#!/usr/bin/env node
// Notifies IndexNow (Bing, Yandex, and other participating engines; Google does
// not use IndexNow) of every URL in the freshly built sitemap. Runs after the
// site is deployed, so the engines recrawl changed pages within minutes instead
// of waiting for the next sitemap poll.
//
// Ownership is proven by hosting KEY at KEY_LOCATION (docs/public/<KEY>.txt,
// served at the site root by VitePress). The key is public by design — it is
// not a secret, so it lives in the repo, not in CI secrets.

import { readFileSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HOST = "fusion.actuallab.net";
const KEY = "8e99a640359326f5f935d4006436c7cc";
const KEY_LOCATION = `https://${HOST}/${KEY}.txt`;
const ENDPOINT = "https://api.indexnow.org/indexnow";

const here = dirname(fileURLToPath(import.meta.url));
const sitemapPath = join(here, "..", ".vitepress", "dist", "sitemap.xml");

if (!existsSync(sitemapPath)) {
  console.error(`submit-indexnow: sitemap.xml is missing at ${sitemapPath}`);
  process.exit(1);
}

const sitemapXml = readFileSync(sitemapPath, "utf-8");
const urlList = [...sitemapXml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
if (urlList.length === 0) {
  console.error("submit-indexnow: sitemap.xml contains no <loc> entries");
  process.exit(1);
}

const res = await fetch(ENDPOINT, {
  method: "POST",
  headers: { "Content-Type": "application/json; charset=utf-8" },
  body: JSON.stringify({ host: HOST, key: KEY, keyLocation: KEY_LOCATION, urlList }),
});

// IndexNow returns 200 (accepted) or 202 (accepted, key validation pending).
// Anything else is logged but does not fail the deploy — submission is a
// best-effort nudge on top of the sitemap, which remains the source of truth.
const body = await res.text().catch(() => "");
if (res.status === 200 || res.status === 202)
  console.log(`submit-indexnow: OK (${res.status}), submitted ${urlList.length} URLs`);
else
  console.warn(`submit-indexnow: engine returned ${res.status} ${res.statusText} — ${body.slice(0, 200)}`);
