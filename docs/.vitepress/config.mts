import { defineConfig } from 'vitepress'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  lang: 'en-US',
  title: "ActualLab.Fusion Documentation",
  description: "Fusion is a reactive framework for building scalable, real-time applications. This site hosts Fusion documentation.",
  srcExclude: [
    'mdsource',
    'node-modules',
    'outdated', 
    'performance-test-results', 
    'tables', 
    'to-be-used', 
    'tutorial', 
  ],
  ignoreDeadLinks: false,
  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Documentation', link: '/README' }
    ],
    sidebar: [
      {
        text: 'Documentation',
        items: [
          { text: 'QuickStart', link: '/QuickStart' },
          { text: 'Part 0: NuGet packages', link: '/Part00' },
          { text: 'Part 1: Core Concepts', link: '/Part01' },
          /*
          { text: 'Part 3: State: IState<T> and Its Flavors', link: '/Part03' },
          { text: 'Part 4: Compute Service Clients', link: '/Part04' },
          { text: 'Part 5: Fusion on Server-Side Only', link: '/Part05' },
          { text: 'Part 6: Real-time UI in Blazor Apps', link: '/Part06' },
          { text: 'Part 7: Real-time UI in JS / React Apps', link: '/Part07' },
          { text: 'Part 8: Scaling Fusion Services', link: '/Part08' },
          { text: 'Part 9: CommandR', link: '/Part09' },
          { text: 'Part 10: Multi-Host Invalidation and CQRS with Operations Framework', link: '/Part10' },
          { text: 'Part 11: Authentication in Fusion', link: '/Part11' },
          { text: 'Part 12: ActualLab.Rpc in Fusion 6.1+', link: '/Part12' },
          { text: 'Part 13: Migration to Fusion 6.1+', link: '/Part13' },
          */
        ]
      }
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/ActualLab/Fusion' }
    ]
  }
})
