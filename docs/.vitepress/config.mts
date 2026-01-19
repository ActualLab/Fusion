import { defineConfig } from "vitepress";

// https://vitepress.dev/reference/site-config
export default defineConfig({
  lang: "en-US",
  title: "ActualLab.Fusion",
  description:
    "Fusion is a reactive framework for building scalable, real-time applications. This site hosts Fusion documentation.",
  head: [["link", { rel: "icon", href: "/favicon.ico" }]],
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
          {
            text: "1. Core Concepts",
            link: "/Part01",
            collapsed: true,
            items: [
              { text: "Computed<T>", link: "/Part01-C" },
              { text: "ComputedOptions", link: "/Part01-CO" },
              { text: "States", link: "/Part01-ST" },
              { text: "Diagrams", link: "/Part01-D" },
              { text: "Cheat Sheet", link: "/Part01-CS" },
            ],
          },
          { text: "2. Local Compute Services on Server-Side", link: "/Part02" },
          {
            text: "3. ActualLab.Rpc and Distributed Compute Services",
            link: "/Part03",
            collapsed: true,
            items: [
              { text: "Core Concepts", link: "/Part03-CC" },
              { text: "Configuration Options", link: "/Part03-CO" },
              { text: "Cheat Sheet", link: "/Part03-CS" },
            ],
          },
          {
            text: "4. Real-time UI in Blazor App",
            link: "/Part04",
            collapsed: true,
            items: [
              { text: "Cheat Sheet", link: "/Part04-CS" },
            ],
          },
          {
            text: "5. CommandR",
            link: "/Part05",
            collapsed: true,
            items: [
              { text: "Cheat Sheet", link: "/Part05-CS" },
            ],
          },
          {
            text: "6. Multi-Host Invalidation with Operations Framework",
            link: "/Part06",
            collapsed: true,
            items: [
              { text: "Cheat Sheet", link: "/Part06-CS" },
            ],
          },
          {
            text: "7. Authentication in Fusion",
            link: "/Part07",
            collapsed: true,
            items: [
              { text: "Cheat Sheet", link: "/Part07-CS" },
            ],
          },
          { text: "NuGet Packages", link: "/NuGet-Packages" },
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
