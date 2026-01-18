import { defineConfig } from "vitepress";

// https://vitepress.dev/reference/site-config
export default defineConfig({
  lang: "en-US",
  title: "ActualLab.Fusion",
  description:
    "Fusion is a reactive framework for building scalable, real-time applications. This site hosts Fusion documentation.",
  head: [["link", { rel: "icon", href: "/img/fusion-docs-icon.png" }]],
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
          { text: "1. Core Concepts", link: "/Part01" },
          { text: "2. Local Compute Services on Server-Side", link: "/Part02" },
          { text: "3. Distributed Compute Services", link: "/Part03" },
          { text: "4. Real-time UI in Blazor App", link: "/Part04" },
          { text: "5. CommandR", link: "/Part05" },
          { text: "6. Multi-Host Invalidation with Operations Framework", link: "/Part06" },
          { text: "7. Authentication in Fusion", link: "/Part07" },
        ],
      },
      {
        text: "Extras",
        items: [
          { text: "Learn Fusion via HelloCart Sample", link: "/HelloCart" },
          { text: "Cheat Sheet", link: "/Cheat-Sheet" },
          { text: "NuGet Packages", link: "/NuGet-Packages" },
        ],
      },
    ],
    socialLinks: [
      { icon: "github", link: "https://github.com/ActualLab/Fusion" },
    ],
    outline: [2, 3],
  },
});
