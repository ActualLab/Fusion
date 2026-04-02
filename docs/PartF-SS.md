# Server-Side Performance

Even without using Fusion's RPC capabilities, you can benefit from Fusion on the server side to cache recurring computations.

## Benchmark Results

Below are results from [Run-Benchmark.cmd from Fusion Samples](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Benchmark):

**Local Services:**

| Test | Result | Speedup |
|------|--------|---------|
| Regular Service | 112.07K calls/s | |
| Fusion Service | 253.12M calls/s | **~2,259x** |

**Remote Services:**

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 76.03K calls/s | |
| HTTP Client → Fusion Service | 305.22K calls/s | **~4.0x** |
| ActualLab.Rpc Client → Fusion Service | 7.58M calls/s | **~100x** |
| Fusion Client → Fusion Service | 216.00M calls/s | **~2,841x** |

## Key Takeaways

- A tiny EF Core-based service exposed via ASP.NET Core serves about **100K** requests per second — mostly because its data set fully fits in RAM.
- The same service with Fusion (`[ComputeMethod]` and `Invalidation.Begin` calls) boosts this to **420K** requests per second when accessed via HTTP — a **4-5x performance boost** with minimal code changes.
- [Similarly to incremental builds](https://alexyakunin.medium.com/the-ungreen-web-why-our-web-apps-are-terribly-inefficient-28791ed48035?source=friends_link&sk=74fb46086ca13ff4fea387d6245cb52b), the more complex your logic is, the more you are expected to gain.

## Learn More

- [Memory Management](./PartF-MM.md) — How Fusion manages computed value lifetimes and memory
- [ComputedOptions](./PartF-CO.md) — Configure caching behavior with `MinCacheDuration` and other options
