import { defineConfig } from "vitepress";

// https://vitepress.dev/reference/site-config
export default defineConfig({
  lang: "en-US",
  title: "ActualLab.Fusion",
  description:
    "Fusion is a reactive framework for building scalable, real-time applications. This site hosts Fusion documentation.",
  head: [
    ["link", { rel: "icon", href: "/favicon.ico" }],
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
              { text: "Configuration Options", link: "/Part02-CO" },
              { text: "Cheat Sheet", link: "/Part02-CS" },
            ],
          },
          {
            text: "3. Real-time UI in Blazor App",
            link: "/Part03",
            collapsed: true,
            items: [
              { text: "Services", link: "/Part03-Services" },
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
              { text: "Events", link: "/Part05-EV" },
              { text: "Transient Operations and Reprocessing", link: "/Part05-TR" },
              { text: "Configuration Options", link: "/Part05-CO" },
              { text: "Providers", link: "/Part05-PR" },
              { text: "Diagrams", link: "/Part05-D" },
              { text: "Cheat Sheet", link: "/Part05-CS" },
            ],
          },
        ],
      },
      {
        text: "Advanced Concepts",
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
          { text: "Story Behind Fusion", link: "/Story" },
        ],
      },
    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/ActualLab/Fusion" },
    ],
    outline: [2, 3],
  },
});
