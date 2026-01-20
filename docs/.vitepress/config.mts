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
  themeConfig: {
    logo: "/img/Logo128.jpg",
    search: {
      provider: "local",
    },
    sidebar: [
      {
        text: "Documentation",
        items: [
          { text: "NuGet Packages", link: "/NuGet-Packages" },
          {
            text: "1. Compute Services, Computed<T>, and States",
            link: "/Part01",
            collapsed: true,
            items: [
              { text: "Computed<T>", link: "/Part01-C" },
              { text: "ComputedOptions", link: "/Part01-CO" },
              { text: "States", link: "/Part01-ST" },
              { text: "Server-Only Use Case", link: "/Part01-SS" },
              { text: "Diagrams", link: "/Part01-D" },
              { text: "Cheat Sheet", link: "/Part01-CS" },
            ],
          },
          {
            text: "2. ActualLab.Rpc and Distributed Compute Services",
            link: "/Part02",
            collapsed: true,
            items: [
              { text: "Key Concepts", link: "/Part02-CC" },
              { text: "RpcNoWait", link: "/Part02-RpcNoWait" },
              { text: "RpcStream", link: "/Part02-RpcStream" },
              { text: "Server-to-Client Calls", link: "/Part02-ReverseRpc" },
              { text: "Call Routing", link: "/Part02-CallRouting" },
              { text: "System Calls", link: "/Part02-SystemCalls" },
              { text: "Configuration Options", link: "/Part02-CO" },
              { text: "Diagrams", link: "/Part02-D" },
              { text: "Cheat Sheet", link: "/Part02-CS" },
            ],
          },
          {
            text: "3. Real-time UI in Blazor App",
            link: "/Part03",
            collapsed: true,
            items: [
              { text: "Services", link: "/Part03-Services" },
              { text: "UICommander and UIActionTracker", link: "/Part03-UICommander" },
              { text: "Authentication", link: "/Part03-Auth" },
              { text: "Parameter Comparison", link: "/Part03-Parameters" },
              { text: "Diagrams", link: "/Part03-D" },
              { text: "Cheat Sheet", link: "/Part03-CS" },
            ],
          },
          {
            text: "4. CommandR",
            link: "/Part04",
            collapsed: true,
            items: [
              { text: "Command Interfaces", link: "/Part04-CI" },
              { text: "Built-in Handlers", link: "/Part04-BH" },
              { text: "MediatR Comparison", link: "/Part04-MC" },
              { text: "Diagrams", link: "/Part04-D" },
              { text: "Cheat Sheet", link: "/Part04-CS" },
            ],
          },
          {
            text: "5. Operations Framework",
            link: "/Part05",
            collapsed: true,
            items: [
              { text: "Transient Operations", link: "/Part05-TR" },
              { text: "Reprocessing", link: "/Part05-RP" },
              { text: "Events", link: "/Part05-EV" },
              { text: "Log Watchers", link: "/Part05-PR" },
              { text: "Configuration Options", link: "/Part05-CO" },
              { text: "Diagrams", link: "/Part05-D" },
              { text: "Cheat Sheet", link: "/Part05-CS" },
            ],
          },
          { text: "Videos and Slides", link: "/Videos-and-Slides" },
        ],
      },
      {
        text: "Advanced Topics",
        items: [
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
        ],
      },
      {
        text: "Extras",
        items: [
          { text: "Learn Fusion via HelloCart Sample", link: "/HelloCart" },
          { text: "Posts and Other Content", link: "/Posts" },
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
