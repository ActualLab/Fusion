---
layout: home

hero:
  name: Fusion Documentation
  text: Welcome!
  tagline: ""
  actions:
    - theme: alt
      text: ▶ Video Tutorial
      link: /README
    - theme: alt
      text: Quick Start
      link: /QuickStart

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

[![Build](https://github.com/ActualLab/Fusion/workflows/Build/badge.svg)](https://github.com/ActualLab/Fusion/actions?query=workflow%3A%22Build%22) [![NuGetVersion](https://img.shields.io/nuget/v/ActualLab.Core)](https://www.nuget.org/packages?q=tags%3A%22actual_lab_fusion%22+Owner%3A%22Actual.chat%22) [![Fusion Place](https://img.shields.io/badge/Fusion%20%40%20Actual%20Chat-BE145B)](https://actual.chat/chat/s-1KCdcYy9z2-uJVPKZsbEo)

<br />
<br />

# FAQ

<br />

<details>
  <summary>What's the best place to ask questions related to Fusion?</summary>
  <p>
    <a href="https://actual.chat/chat/s-1KCdcYy9z2-uJVPKZsbEo">Fusion @ Actual Chat</a> is currently the best place to ask questions and track project updates.
  </p>
</details>

<details>
  <summary>Can I contribute to the project?</summary>
  <p>
    Absolutely. Just create your first <a href="https://github.com/ActualLab/Fusion/pulls">pull request</a> or <a href="https://github.com/ActualLab/Fusion/issues">report a bug</a>. You can also contribute to <a href="https://github.com/ActualLab/Fusion.Samples">Fusion Samples</a>.
  </p>
</details>

<details>
  <summary>Comparison to other libraries?</summary>
  <p>
&nbsp;&ndash;&nbsp;<a href="https://medium.com/@alexyakunin/how-similar-is-stl-fusion-to-signalr-e751c14b70c3?source=friends_link&sk=241d5293494e352f3db338d93c352249">How similar is Fusion to SignalR?</a><br />
&nbsp;&ndash;&nbsp;<a href="https://medium.com/@alexyakunin/how-similar-is-stl-fusion-to-knockout-mobx-fcebd0bef5d5?source=friends_link&sk=a808f7c46c4d5613605f8ada732e790e">How similar is Fusion to Knockout / MobX?</a>
  </p>
</details>

<details>
  <summary>Can I use Fusion with server-side Blazor?</summary>
  <p>Yes, you can use it to implement the same real-time update logic there.
The only difference here is that you don't need API controllers supporting
Fusion publication in this case, i.e. your models might depend right on the
<i>server-side compute services</i> (that's an abstraction you primarily deal with,
that "hides" all the complexities of dealing with <code>Computed&lt;T&gt;</code>
and does it transparently for you).</p>
</details>

<details>
  <summary>Can I use Fusion <i>without</i> Blazor at all?</summary>
  <p>
The answer is yes &ndash; you can use Fusion in all kinds of .NET Core
apps, though I guess the real question is &darr;
  </p>
</details>

<details>
  <summary>Can I use Fusion with some native JavaScript client for it?</summary>
  <div>
    <p>Right now there is no native JavaScript client for Fusion, so if you
want to use Fusion subscriptions / auto-update features in JS,
you still need a counterpart in Blazor that e.g. exports the "live state"
maintained by Fusion to the JavaScript part of the app after every update.</p>
  <p>There is a good chance we (or someone else) will develop a native
  JavaScript client for Fusion in the future.</p>
  </div>
</details>

<details>
  <summary>Are there any benefits of using Fusion on server-side only?</summary>
  <div>
    <p>Yes. Any service backed by Fusion, in fact, gets a cache, that invalidates
right when it should. This makes % of inconsistent reads there is as small
as possible.</p>

  <p>Which is why Fusion is also a very good fit for caching scenarios requiring
nearly real-time invalidation / minimum % of inconsistent reads.</p>
  </div>
</details>

<details>
  <summary>API related questions (TBD)</summary>
  <p>TBD</p>
</details>

[Fusion Samples]: https://github.com/ActualLab/Fusion.Samples
[Fusion Place]: https://actual.chat/chat/s-1KCdcYy9z2-uJVPKZsbEo
[Fusion Feedback Form]: https://forms.gle/TpGkmTZttukhDMRB6
