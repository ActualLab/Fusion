#!/usr/bin/env node
// Notifies IndexNow (Bing, Yandex, and other participating engines; Google does
// not use IndexNow) of every URL in the freshly built sitemap. Runs after the
// site is deployed, so the engines recrawl changed pages within minutes instead
// of waiting for the next sitemap poll.
//
// Ownership is proven by hosting KEY at KEY_LOCATION (docs/public/<KEY>.txt,
// served at the site root by VitePress). The key is public by design — it is
// not a secret, so it lives in the repo, not in CI secrets.
//
// The step runs seconds after Cloudflare publishes, so the key file may not yet
// be live at the edge an engine's validator happens to hit. If a validator
// fetches it too early it caches a "key invalid" verdict and then forbids every
// later submission (HTTP 403 UserForbiddedToAccessSite). To avoid seeding that
// cache we first confirm the key file is actually live (waitForKey) and only
// then submit. We also post to Yandex directly in addition to the generic
// aggregator, so one engine's outage or cached rejection can't silence the rest.

import { readFileSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HOST = "fusion.actuallab.net";
const KEY = "8e99a640359326f5f935d4006436c7cc";
const KEY_LOCATION = `https://${HOST}/${KEY}.txt`;

// Both fan out to all IndexNow participants, but on independent backends: the
// aggregator shares Bing's backend, so a Bing-side cached rejection also breaks
// it — Yandex's own endpoint stays a working path regardless.
const ENDPOINTS = [
  "https://api.indexnow.org/indexnow",
  "https://yandex.com/indexnow",
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Poll KEY_LOCATION until it returns 200 with exactly KEY as its body, so no
// engine validates against a not-yet-propagated file. Best-effort: on timeout
// we skip submission rather than risk poisoning an engine's validation cache.
async function waitForKey(attempts = 6, delayMs = 5000) {
  for (let i = 1; i <= attempts; i++) {
    try {
      const res = await fetch(KEY_LOCATION, { cache: "no-store" });
      const body = res.ok ? (await res.text()).trim() : "";
      if (res.ok && body === KEY) {
        if (i > 1)
          console.log(`submit-indexnow: key file live after ${i} checks`);
        return true;
      }
    } catch {
      // Network hiccup during propagation — fall through to the retry.
    }
    if (i < attempts)
      await sleep(delayMs);
  }
  return false;
}

async function submit(endpoint, urlList) {
  const res = await fetch(endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=utf-8" },
    body: JSON.stringify({ host: HOST, key: KEY, keyLocation: KEY_LOCATION, urlList }),
  });
  const body = await res.text().catch(() => "");
  // 200 (accepted) or 202 (accepted, key validation pending) are the success
  // codes; anything else is logged but never fails the deploy — submission is a
  // best-effort nudge on top of the sitemap, which remains the source of truth.
  if (res.status === 200 || res.status === 202)
    console.log(`submit-indexnow: ${endpoint} OK (${res.status}), submitted ${urlList.length} URLs`);
  else
    console.warn(`submit-indexnow: ${endpoint} returned ${res.status} ${res.statusText} — ${body.slice(0, 200)}`);
}

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

if (!(await waitForKey())) {
  console.warn(`submit-indexnow: key file at ${KEY_LOCATION} not confirmed live — skipping submission`);
  process.exit(0);
}

for (const endpoint of ENDPOINTS) {
  try {
    await submit(endpoint, urlList);
  } catch (err) {
    console.warn(`submit-indexnow: ${endpoint} failed — ${err?.message ?? err}`);
  }
}
