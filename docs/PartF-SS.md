# Server-Side Performance

Even without using Fusion's RPC capabilities, you can benefit from Fusion on the server side to cache recurring computations.

## Benchmark Results

Below are results from [Run-Benchmark.cmd from Fusion Samples](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Benchmark):

**Local Services:**

| Test | Result | Speedup |
|------|--------|---------|
| Regular Service | 135.44K calls/s | |
| Fusion Service | 266.58M calls/s | **~1,968x** |

**Remote Services:**

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 100.72K calls/s | |
| HTTP Client → Fusion Service | 431.35K calls/s | **~4.3x** |
| ActualLab.Rpc Client → Fusion Service | 6.92M calls/s | **~69x** |
| Fusion Client → Fusion Service | 226.73M calls/s | **~2,251x** |

## Key Takeaways

- A tiny EF Core-based service exposed via ASP.NET Core serves about **100K** requests per second — mostly because its data set fully fits in RAM.
- The same service with Fusion (`[ComputeMethod]` and `Invalidation.Begin` calls) boosts this to **420K** requests per second when accessed via HTTP — a **4-5x performance boost** with minimal code changes.
- [Similarly to incremental builds](https://alexyakunin.medium.com/the-ungreen-web-why-our-web-apps-are-terribly-inefficient-28791ed48035?source=friends_link&sk=74fb46086ca13ff4fea387d6245cb52b), the more complex your logic is, the more you are expected to gain.

## Learn More

- [Memory Management](./PartF-MM.md) — How Fusion manages computed value lifetimes and memory
- [ComputedOptions](./PartF-CO.md) — Configure caching behavior with `MinCacheDuration` and other options
