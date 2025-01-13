---
layout: home

hero:
  name: Fusion Tutorial
  text: Welcome to Fusion Tutorial!
  tagline: ''
  actions:
    - theme: brand
      text: Tutorial
      link: /README
    - theme: alt
      text: QuickStart
      link: /QuickStart

features:
  - title: Real-Time State Synchronization
    details: 
  - title: Distributed Caching and Dependency Tracking
    details: Fusion tracks data dependencies and performs real-time cache invalidation to ensure only necessary values are recomputed.
  - title: Persistent Client-Side Caches
    details: Fusion-based clients can operate even when offline, providing a seamless experience.
  - title: Extremely Efficient RPC
    details: Fusion's RPC client eliminates unnecessary network round trips by using cached results that aren't marked as stale. The stale-while-revalidate strategy allows Fusion-based clients to rely on speculative execution to pack hundreds of calls into a single transmission frame. As a result, all the data needed for a given UI view is often retrieved via a single network round trip.
  - title: UI State Management
    details: The UI is just one of the application states Fusion manages, removing the need for specialized libraries like Recoil.
  - title: Unified Codebase for All Clients
    details: Fusion allows you to maintain a single codebase for all of your clients, including Blazor Server, Blazor WebAssembly, and Blazor Hybrid/MAUI.
---

