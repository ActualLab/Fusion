---
layout: home

hero:
  name: Fusion Documentation
  text: Welcome to Fusion Documentation!
  tagline: ''
  actions:
    - theme: brand
      text: Home
      link: /
    - theme: alt
      text: QuickStart
      link: /QuickStart
    - theme: alt
      text: Cheat Sheet
      link: /CheatSheet

features:
  - title: Real-Time State Synchronization
    details: Fusion provides automatic real-time state synchronization across all clients, ensuring your application stays in sync without manual intervention. Changes are propagated instantly to all connected clients.
  - title: Distributed Caching and Dependency Tracking
    details: Fusion's intelligent dependency tracking system automatically detects and manages data relationships, performing precise cache invalidation to optimize performance and reduce unnecessary computations.
  - title: Persistent Client-Side Caches
    details: Built-in offline support with persistent client-side caching enables your application to work seamlessly even without an internet connection, with automatic synchronization when connectivity is restored.
  - title: Extremely Efficient RPC
    details: Fusion's RPC system revolutionizes client-server communication by eliminating redundant network calls. It uses a sophisticated stale-while-revalidate strategy and speculative execution to batch hundreds of calls into single transmission frames, dramatically reducing network overhead.
  - title: UI State Management
    details: Fusion seamlessly integrates UI state management into its core architecture, eliminating the need for separate state management libraries. This unified approach simplifies development and reduces complexity.
  - title: Unified Codebase for All Clients
    details: Write once, run anywhere. Fusion enables you to maintain a single codebase that works across all Blazor platforms - Server, WebAssembly, and Hybrid/MAUI - while automatically optimizing for each platform's unique characteristics.
---
