import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, type HeadConfig } from "vitepress";

const hostname = "https://fusion.actuallab.net";
const siteDescription =
  "ActualLab.Fusion is an end-to-end reactivity framework for .NET and TypeScript — " +
  "real-time UI updates, distributed caching, the fastest RPC, MIT-licensed.";

// Pulls the first real paragraph out of a Markdown source file, trimmed to a
// meta-description-sized snippet — so every page gets a unique description
// instead of all 100+ pages sharing the site-wide one.
function deriveDescription(srcDir: string, relativePath: string): string {
  let raw: string;
  try {
    raw = fs.readFileSync(path.join(srcDir, relativePath), "utf-8");
  } catch {
    return "";
  }
  raw = raw.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, "");
  const paragraph: string[] = [];
  for (const line of raw.split(/\r?\n/)) {
    const t = line.trim();
    if (paragraph.length === 0) {
      if (t === "" || /^(?:#|<|```|:::|\||[-*]\s|\d+\.\s)/.test(t))
        continue;
    } else if (t === "" || /^(?:#|```|:::)/.test(t))
      break;
    paragraph.push(t.replace(/^>\s?/, ""));
  }
  const entities: Record<string, string> = {
    amp: "&", lt: "<", gt: ">", quot: "\"", "#39": "'",
    nbsp: " ", ndash: "–", mdash: "—", hellip: "…",
  };
  let text = paragraph.join(" ")
    .replace(/!\[[^\]]*]\([^)]*\)/g, "")
    .replace(/\[([^\]]+)]\([^)]*\)/g, "$1")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/[*_]{1,3}([^*_]+)[*_]{1,3}/g, "$1")
    .replace(/<[^>]+>/g, "")
    .replace(/\\([<>&])/g, "$1")
    .replace(/&(amp|lt|gt|quot|#39|nbsp|ndash|mdash|hellip);/g, (_m, e) => entities[e] ?? "")
    .replace(/\s+/g, " ")
    .trim();
  if (text.length > 160)
    text = text.slice(0, 157).replace(/\s+\S*$/, "") + "…";
  return text;
}

// https://vitepress.dev/reference/site-config
export default defineConfig({
  lang: "en-US",
  title: "ActualLab.Fusion",
  description: siteDescription,
  head: [
    ["meta", { name: "msvalidate.01", content: "1CE54B5BA968A223C22D8083ACFF0F67" }],
    ["link", { rel: "icon", href: "/favicon.ico" }],
["script", { async: "", src: "https://www.googletagmanager.com/gtag/js?id=G-PX4G7HX4CM" }],
    ["script", {}, `window.dataLayer = window.dataLayer || [];
function gtag(){dataLayer.push(arguments);}
gtag('js', new Date());
gtag('config', 'G-PX4G7HX4CM');`],
  ],
  srcExclude: [
    "AGENTS.md",
    "ExternalLinks0.md",
    "Tasks.md",
    "api-index.md",
    "mdsource",
    "node-modules",
    "outdated",
    "plans",
    "public/**",
    "slides",
    "tasks",
    "to-be-used",
  ],
  ignoreDeadLinks: false,
  appearance: 'dark',
  // Extensionless URLs: /PartF instead of /PartF.html. The host (Cloudflare
  // Pages) serves PartF.html for /PartF and 301s /PartF.html -> /PartF; see
  // scripts/gen-redirects.mjs for the generated legacy redirects.
  cleanUrls: true,
  // Enables real Git-based <lastmod> in sitemap.xml (getLastmod reads the
  // file's last commit date); also adds a "Last updated" line in the footer.
  lastUpdated: true,
  sitemap: {
    hostname,
  },
  transformPageData(pageData, { siteConfig }) {
    if (!pageData.description) {
      const derived = deriveDescription(siteConfig.srcDir, pageData.relativePath);
      if (derived.length >= 40)
        pageData.description = derived;
    }
  },
  transformHead({ pageData, title, description }) {
    // Self-referencing canonical link on the extensionless URL, matching
    // cleanUrls + the sitemap. index.md -> /, Foo.md -> /Foo, a/b.md -> /a/b.
    const pagePath = pageData.relativePath
      .replace(/(^|\/)index\.md$/, "$1")
      .replace(/\.md$/, "");
    const url = `${hostname}/${pagePath}`;
    const desc = description || siteDescription;
    const image = `${hostname}/og-image.jpg`;
    const head: HeadConfig[] = [
      ["link", { rel: "canonical", href: url }],
      ["meta", { property: "og:type", content: "website" }],
      ["meta", { property: "og:site_name", content: "ActualLab.Fusion" }],
      ["meta", { property: "og:title", content: title }],
      ["meta", { property: "og:description", content: desc }],
      ["meta", { property: "og:url", content: url }],
      ["meta", { property: "og:image", content: image }],
      ["meta", { property: "og:image:width", content: "1200" }],
      ["meta", { property: "og:image:height", content: "630" }],
      ["meta", { property: "og:locale", content: "en_US" }],
      ["meta", { name: "twitter:card", content: "summary_large_image" }],
      ["meta", { name: "twitter:title", content: title }],
      ["meta", { name: "twitter:description", content: desc }],
      ["meta", { name: "twitter:image", content: image }],
    ];
    if (pageData.relativePath === "index.md")
      head.push(["script", { type: "application/ld+json" }, JSON.stringify({
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        name: "ActualLab.Fusion",
        applicationCategory: "DeveloperApplication",
        operatingSystem: "Windows, macOS, Linux",
        description: desc,
        url: `${hostname}/`,
        image,
        license: "https://opensource.org/licenses/MIT",
        isAccessibleForFree: true,
        offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
        author: {
          "@type": "Organization",
          name: "ActualLab",
          url: "https://github.com/ActualLab",
        },
        sameAs: [
          "https://github.com/ActualLab/Fusion",
          "https://www.nuget.org/packages/ActualLab.Core",
        ],
      })]);
    return head;
  },
  vite: {
    resolve: {
      // Replace VitePress's VPHomeContent with a copy that drops the
      // window-width-derived --vp-offset inline style (see the component for
      // why); removes a benign SSR hydration style mismatch on the home page.
      alias: [
        {
          find: /^.*\/VPHomeContent\.vue$/,
          replacement: fileURLToPath(new URL("./theme/CustomHomeContent.vue", import.meta.url)),
        },
      ],
    },
    plugins: [
      {
        // In dev, VitePress's SPA fallback intercepts /slides/<deck>/ before
        // Vite can serve public/slides/<deck>/index.html. Rewrite the URL so
        // the static file is found. Production builds don't need this — the
        // built public/ files take precedence over SPA routes.
        name: "slides-index-rewrite",
        configureServer(server) {
          server.middlewares.use((req, res, next) => {
            if (req.url) {
              // /<root>/<deck> -> /<root>/<deck>/ (redirect for the canonical
              // form so relative URLs resolve correctly).
              const bare = req.url.match(/^(\/(?:slides|slides-old)\/[^/?#]+)(\?[^#]*)?(#.*)?$/);
              if (bare) {
                res.statusCode = 301;
                res.setHeader("Location", bare[1] + "/" + (bare[2] ?? "") + (bare[3] ?? ""));
                res.end();
                return;
              }
              // /<root>/<deck>/<route> -> /<root>/<deck>/index.html when the
              // last segment has no file extension. Assets such as
              // /<root>/<deck>/assets/foo.js fall through to static serving.
              const m = req.url.match(/^(\/(?:slides|slides-old)\/[^/?#]+\/)([^?#]*)(.*)$/);
              if (m) {
                const last = m[2].split("/").pop() ?? "";
                if (last === "" || !last.includes(".")) {
                  req.url = m[1] + "index.html" + m[3];
                }
              }
            }
            next();
          });
        },
      },
    ],
  },
  themeConfig: {
    logo: "/img/Logo128.jpg",
    search: {
      provider: "local",
    },
    nav: [
      { text: "Samples", link: "https://github.com/ActualLab/Fusion.Samples" },
      { text: "GitHub", link: "https://github.com/ActualLab/Fusion" },
      { text: "Chat", link: "https://voxt.ai/chat/s-1KCdcYy9z2-uJVPKZsbEo" },
    ],
    sidebar: [
      {
        items: [
          { text: "Videos and Slides", link: "/Videos-and-Slides" },
          {
            text: "1. Compute Services, Computed<T>, and States",
            link: "/PartF",
            collapsed: true,
            items: [
              { text: "Computed<T>", link: "/PartF-C" },
              { text: "ComputedOptions", link: "/PartF-CO" },
              { text: "States", link: "/PartF-ST" },
              { text: "Memory Management", link: "/PartF-MM" },
              { text: "Server-Side Performance", link: "/PartF-SS" },
              { text: "Diagrams", link: "/PartF-D" },
              { text: "Cheat Sheet", link: "/PartF-CS" },
            ],
          },
          {
            text: "2. ActualLab.Rpc and Distributed Compute Services",
            link: "/PartR",
            collapsed: true,
            items: [
              { text: "Key Concepts", link: "/PartR-CC" },
              { text: "RpcNoWait", link: "/PartR-RpcNoWait" },
              { text: "RpcStream", link: "/PartR-RpcStream" },
              { text: "Server-to-Client Calls", link: "/PartR-ReverseRpc" },
              { text: "Call Routing", link: "/PartR-CallRouting" },
              { text: "Serialization Formats", link: "/PartR-Serialization" },
              { text: "System Calls", link: "/PartR-SystemCalls" },
              { text: "HTTP/2 Transport", link: "/PartR-HttpTransport" },
              { text: "Configuration Options", link: "/PartR-CO" },
              { text: "Diagrams", link: "/PartR-D" },
              { text: "Cheat Sheet", link: "/PartR-CS" },
            ],
          },
          {
            text: "3. CommandR: CQRS and Beyond",
            link: "/PartC",
            collapsed: true,
            items: [
              { text: "Command Interfaces", link: "/PartC-CI" },
              { text: "Built-in Handlers", link: "/PartC-BH" },
              { text: "MediatR Comparison", link: "/PartC-MC" },
              { text: "Diagrams", link: "/PartC-D" },
              { text: "Cheat Sheet", link: "/PartC-CS" },
            ],
          },
          {
            text: "4. Real-time UI in Blazor App",
            link: "/PartB",
            collapsed: true,
            items: [
              { text: "Services", link: "/PartB-Services" },
              { text: "UICommander and UIActionTracker", link: "/PartB-UICommander" },
              { text: "Authentication", link: "/PartB-Auth" },
              { text: "Parameter Comparison", link: "/PartB-Parameters" },
              { text: "Diagrams", link: "/PartB-D" },
              { text: "Cheat Sheet", link: "/PartB-CS" },
            ],
          },
          {
            text: "5. Entity Framework Extensions",
            link: "/PartEF",
          },
          {
            text: "6. Operations Framework",
            link: "/PartO",
            collapsed: true,
            items: [
              { text: "Transient Operations", link: "/PartO-TR" },
              { text: "Reprocessing", link: "/PartO-RP" },
              { text: "Events", link: "/PartO-EV" },
              { text: "Log Watchers", link: "/PartO-PR" },
              { text: "Serialization", link: "/PartO-Serialization" },
              { text: "Configuration Options", link: "/PartO-CO" },
              { text: "Diagrams", link: "/PartO-D" },
              { text: "Cheat Sheet", link: "/PartO-CS" },
            ],
          },
          {
            text: "7. ActualLab.Core",
            link: "/PartCore",
            collapsed: true,
            items: [
              { text: "Result and Option", link: "/PartCore-Result" },
              { text: "Time (Moment)", link: "/PartCore-Time" },
              { text: "Transiency Resolvers", link: "/PartCore-Transiency" },
              { text: "(Mutable)PropertyBag", link: "/PartCore-PropertyBag" },
              { text: "AsyncLock and AsyncLockSet", link: "/PartCore-AsyncLock" },
              { text: "AsyncChain", link: "/PartCore-AsyncChain" },
              { text: "WorkerBase", link: "/PartCore-Worker" },
              { text: "Unified Serialization", link: "/PartS" },
            ],
          },
          {
            text: "8. TypeScript Port",
            link: "/PartTS",
            collapsed: true,
            items: [
              { text: "@actuallab/core", link: "/PartTS-Core" },
              { text: "@actuallab/fusion", link: "/PartTS-Fusion" },
              { text: "@actuallab/rpc", link: "/PartTS-Rpc" },
              { text: "@actuallab/fusion-rpc", link: "/PartTS-FusionRpc" },
              { text: "@actuallab/fusion-react", link: "/PartTS-React" },
            ],
          },
          { text: "Performance Benchmarks", link: "/Performance" },
          { text: "Changelog", link: "/CHANGELOG" },
        ],
      },
      {
        text: "Advanced Topics",
        items: [
          {
            text: "Cache-Aware API Design",
            link: "/PartAC",
            collapsed: true,
            items: [
              { text: "Pseudo-Dependencies", link: "/PartAC-PM" },
              { text: "Observing Changes", link: "/PartAC-OC" },
              { text: "Persistent Cache", link: "/PartAC-PC" },
            ],
          },
          {
            text: "Authentication in Fusion",
            link: "/PartAA",
            collapsed: true,
            items: [
              { text: "Interfaces & Commands", link: "/PartAA-Interfaces" },
              { text: "Database Services", link: "/PartAA-DB" },
              { text: "Server Components", link: "/PartAA-Server" },
              { text: "Standalone Authentication", link: "/PartAA-X" },
              { text: "Diagrams", link: "/PartAA-D" },
              { text: "Cheat Sheet", link: "/PartAA-CS" },
            ],
          },
          {
            text: "Interceptors and Proxies",
            link: "/PartAP",
            collapsed: true,
            items: [
              { text: "ArgumentList API", link: "/PartAP-AL" },
              { text: "Proxy Generation", link: "/PartAP-PG" },
              { text: "Built-in Interceptors", link: "/PartAP-BI" },
              { text: "Diagrams", link: "/PartAP-D" },
              { text: "Cheat Sheet", link: "/PartAP-CS" },
            ],
          },
          { text: "Native AOT Support", link: "/PartAOT" },
        ],
      },
      {
        text: "Extras",
        items: [
          { text: "Glossary", link: "/glossary" },
          { text: "API Index", link: "/api-index-full" },
          { text: "NuGet Packages", link: "/NuGet-Packages" },
          { text: "MCP Server", link: "/MCP" },
          {
            text: "Fusion vs...",
            link: "/ActualLab.Fusion-vs/",
            collapsed: true,
            items: [
              { text: "State Management:" },
              { text: "· Fluxor / Blazor-State", link: "/ActualLab.Fusion-vs/Fluxor" },
              { text: "· MobX / Knockout", link: "/ActualLab.Fusion-vs/MobX" },
              { text: "· Redux / Zustand", link: "/ActualLab.Fusion-vs/Redux" },
              { text: "· Rx.NET", link: "/ActualLab.Fusion-vs/RxNET" },
              { text: "Caching:" },
              { text: "· Redis", link: "/ActualLab.Fusion-vs/Redis" },
              { text: "· IDistributedCache", link: "/ActualLab.Fusion-vs/IDistributedCache" },
              { text: "· HybridCache", link: "/ActualLab.Fusion-vs/HybridCache" },
              { text: "Real-Time Communication:" },
              { text: "· SignalR", link: "/ActualLab.Fusion-vs/SignalR" },
              { text: "· WebSockets", link: "/ActualLab.Fusion-vs/WebSockets" },
              { text: "· gRPC Streaming", link: "/ActualLab.Fusion-vs/gRPC" },
              { text: "· Server-Sent Events", link: "/ActualLab.Fusion-vs/SSE" },
              { text: "API & Data Fetching:" },
              { text: "· GraphQL", link: "/ActualLab.Fusion-vs/GraphQL" },
              { text: "· REST APIs", link: "/ActualLab.Fusion-vs/REST" },
              { text: "Distributed Systems:" },
              { text: "· Orleans", link: "/ActualLab.Fusion-vs/Orleans" },
              { text: "· Akka.NET", link: "/ActualLab.Fusion-vs/AkkaNET" },
              { text: "Architecture Patterns:" },
              { text: "· CQRS + Event Sourcing", link: "/ActualLab.Fusion-vs/CQRS" },
              { text: "· MediatR", link: "/ActualLab.Fusion-vs/MediatR" },
              { text: "· Clean Architecture", link: "/ActualLab.Fusion-vs/CleanArchitecture" },
              { text: "Event-Driven Systems:" },
              { text: "· Message Brokers", link: "/ActualLab.Fusion-vs/MessageBrokers" },
              { text: "Data Access:" },
              { text: "· EF Core Change Tracking", link: "/ActualLab.Fusion-vs/EFCore" },
              { text: "· Firebase / Firestore", link: "/ActualLab.Fusion-vs/Firebase" },
              { text: "UI Frameworks:" },
              { text: "· React + TanStack Query", link: "/ActualLab.Fusion-vs/TanStackQuery" },
              { text: "· LiveView / Phoenix", link: "/ActualLab.Fusion-vs/LiveView" },
            ],
          },
          { text: "Story Behind Fusion", link: "/Story" },
        ],
      },
    ],
    outline: [2, 3],
  },
});
