import { defineConfig } from "vitepress";
import { withMermaid } from "vitepress-plugin-mermaid";

// Grafana theme with Inter font (from modern_mermaid)
const mermaidConfig = {
  theme: 'base' as const,
  themeVariables: {
    darkMode: true,
    background: '#1e1b4b',
    primaryColor: '#1F2428',
    primaryTextColor: '#D8D9DA',
    primaryBorderColor: '#3D434B',
    lineColor: '#5794F2',
    secondaryColor: '#262B31',
    tertiaryColor: '#2C3235',
    fontFamily: '"Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    fontSize: '14px',
  },
  themeCSS: `
    /* Grafana-inspired style with Inter font */
    .node rect, .node circle, .node polygon {
      fill: #1F2428 !important;
      stroke: #3D434B !important;
      stroke-width: 2px !important;
      rx: 3px !important;
      ry: 3px !important;
      filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.3));
    }
    .node .label {
      font-family: "Inter", sans-serif;
      font-weight: 500;
      fill: #D8D9DA !important;
      font-size: 14px;
    }
    .edgePath .path {
      stroke: #5794F2 !important;
      stroke-width: 2px !important;
      stroke-linecap: round;
    }
    .arrowheadPath {
      fill: #5794F2 !important;
      stroke: #5794F2 !important;
    }
    .edgeLabel {
      background-color: #1e1b4b !important;
      color: #D8D9DA !important;
      font-family: "Inter", sans-serif;
      font-size: 13px;
    }
    .cluster rect {
      fill: rgba(87, 148, 242, 0.05) !important;
      stroke: #3D434B !important;
      stroke-width: 2px !important;
      stroke-dasharray: 6 4 !important;
      rx: 3px !important;
    }
    .cluster text {
      fill: #73BF69 !important;
      font-family: "Inter", sans-serif;
      font-weight: 600;
    }
  `,
};

// https://vitepress.dev/reference/site-config
export default withMermaid(defineConfig({
  lang: "en-US",
  title: "ActualLab.Fusion",
  description:
    "Fusion is a reactive framework for building scalable, real-time applications. This site hosts Fusion documentation.",
  head: [
    ["link", { rel: "icon", href: "/favicon.ico" }],
    ["style", {}, `.vp-doc .mermaid p { line-height: normal !important; padding: 2px 0 !important; } .mermaid { background-color: #1e1b4b; background-image: radial-gradient(ellipse at 30% 20%, rgba(59, 130, 246, 0.3) 0%, transparent 40%), radial-gradient(ellipse at 70% 60%, rgba(168, 85, 247, 0.3) 0%, transparent 40%), radial-gradient(ellipse at 50% 80%, rgba(34, 211, 238, 0.2) 0%, transparent 40%); border-radius: 8px; padding: 16px; margin: 16px 0; }`],
    ["script", { async: "", src: "https://www.googletagmanager.com/gtag/js?id=G-PX4G7HX4CM" }],
    ["script", {}, `window.dataLayer = window.dataLayer || [];
function gtag(){dataLayer.push(arguments);}
gtag('js', new Date());
gtag('config', 'G-PX4G7HX4CM');`],
  ],
  srcExclude: [
    "mdsource",
    "node-modules",
    "outdated",
    "tasks",
    "to-be-used",
  ],
  ignoreDeadLinks: false,
  appearance: 'dark',
  sitemap: {
    hostname: 'https://fusion.actuallab.net'
  },
  themeConfig: {
    logo: "/img/Logo128.jpg",
    search: {
      provider: "local",
    },
    sidebar: [
      {
        text: "Documentation",
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
          { text: "7. Unified Serialization", link: "/PartS" },
          { text: "Performance Benchmarks", link: "/Benchmarks" },
          { text: "NuGet Packages", link: "/NuGet-Packages" },
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
          {
            text: "Fusion vs...",
            link: "/ActualLab.Fusion-vs/",
            collapsed: true,
            items: [
              { text: "State Management" },
              { text: "vs Fluxor / Blazor-State", link: "/ActualLab.Fusion-vs/Fluxor" },
              { text: "vs MobX / Knockout", link: "/ActualLab.Fusion-vs/MobX" },
              { text: "vs Redux / Zustand", link: "/ActualLab.Fusion-vs/Redux" },
              { text: "vs Rx.NET", link: "/ActualLab.Fusion-vs/RxNET" },
              { text: "Caching" },
              { text: "vs Redis", link: "/ActualLab.Fusion-vs/Redis" },
              { text: "vs IDistributedCache", link: "/ActualLab.Fusion-vs/IDistributedCache" },
              { text: "vs HybridCache", link: "/ActualLab.Fusion-vs/HybridCache" },
              { text: "Real-Time Communication" },
              { text: "vs SignalR", link: "/ActualLab.Fusion-vs/SignalR" },
              { text: "vs WebSockets", link: "/ActualLab.Fusion-vs/WebSockets" },
              { text: "vs gRPC Streaming", link: "/ActualLab.Fusion-vs/gRPC" },
              { text: "vs Server-Sent Events", link: "/ActualLab.Fusion-vs/SSE" },
              { text: "API & Data Fetching" },
              { text: "vs GraphQL", link: "/ActualLab.Fusion-vs/GraphQL" },
              { text: "vs REST APIs", link: "/ActualLab.Fusion-vs/REST" },
              { text: "Distributed Systems" },
              { text: "vs Orleans", link: "/ActualLab.Fusion-vs/Orleans" },
              { text: "vs Akka.NET", link: "/ActualLab.Fusion-vs/AkkaNET" },
              { text: "Architecture Patterns" },
              { text: "vs CQRS + Event Sourcing", link: "/ActualLab.Fusion-vs/CQRS" },
              { text: "vs MediatR", link: "/ActualLab.Fusion-vs/MediatR" },
              { text: "vs Clean Architecture", link: "/ActualLab.Fusion-vs/CleanArchitecture" },
              { text: "Event-Driven Systems" },
              { text: "vs Message Brokers", link: "/ActualLab.Fusion-vs/MessageBrokers" },
              { text: "Data Access" },
              { text: "vs EF Core Change Tracking", link: "/ActualLab.Fusion-vs/EFCore" },
              { text: "vs Firebase / Firestore", link: "/ActualLab.Fusion-vs/Firebase" },
              { text: "UI Frameworks" },
              { text: "vs React + TanStack Query", link: "/ActualLab.Fusion-vs/TanStackQuery" },
              { text: "vs LiveView / Phoenix", link: "/ActualLab.Fusion-vs/LiveView" },
            ],
          },
          { text: "Story Behind Fusion", link: "/Story" },
        ],
      },
    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/ActualLab/Fusion" },
    ],
    outline: [2, 3],
  },
  mermaid: mermaidConfig,
}));
