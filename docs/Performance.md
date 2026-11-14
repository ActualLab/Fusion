# Performance

**Updated:** 2026-11-14<br/>
**ActualLab.Fusion Version:** 15.1.38

This page summarizes results of Fusion performance benchmarks.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **.NET** | 11.0.0 |

Note that Ryzen 9 9950X3D has 32 logical cores due to SMT.

## Fusion Micro-Benchmarks

BenchmarkDotNet measurements of the **single-threaded, per-operation cost** of Fusion's core
compute-method primitives, with an otherwise empty method body (so the numbers reflect Fusion's own
overhead). `Calls/s per core` = `1 / Mean`.

| Operation | Calls/s per core | Mean | Allocated |
|-----------|------------------|------|-----------|
| Cached hit, `long` key<br/>`Service.Get(0L, default)` | 47.08M | 21.24 ns | 32 B |
| Cached hit, `string` key<br/>`Service.Get("key", default)` | 34.35M | 29.11 ns | 32 B |
| Invalidation (activate + 1 call)<br/>`using (Invalidation.Begin())`<br/>`    Service.Get(key, default)` | 19.74M | 50.65 ns | 112 B |
| Recompute + cache (fresh key each call)<br/>`Service.Get(i++, default)` | 2.02M | 495.3 ns | 1021 B |

A cache hit costs ~21 ns and a single `ArgumentList` allocation — this is the ~47M calls/s per core that
Fusion sustains for cached compute-method calls.

## Interception & Proxy Overhead

Fusion builds compute methods and RPC clients on generated proxies. Per-call cost of ActualLab's proxies
vs [Castle DynamicProxy](https://github.com/castleproject/Core) for a simple interceptor (returns the
method's default result), single-threaded:

| Proxy variant | Calls/s per core | Mean | Allocated |
|---------------|------------------|------|-----------|
| No proxy (direct virtual call) | 869.1M | 1.15 ns | – |
| [ActualLab, simple interceptor](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#proxy-and-interception-benchmarks) | 404.0M | 2.48 ns | 24 B |
| [Castle DynamicProxy, simple](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#proxy-and-interception-benchmarks) | 63.30M | 15.80 ns | 128 B |

ActualLab's interception is **~4-6x faster than Castle DynamicProxy** — and only **~2-4x slower than a
plain virtual call** — while allocating far less. That's why Fusion can afford to wrap every compute
method and RPC call in a proxy. See the full breakdown (sync/async, pass-through, no-handler variants) in
the source report linked at the bottom of this page.

## Run-PerformanceTest.cmd from Fusion Test suite

The benchmark measures throughput of a simple repository-style user lookup service (`UserService.Get(userId)`) that retrieves user records from a database. The test compares two scenarios:

1. **With Fusion**: `UserService.Get` is a `[ComputeMethod]`, so its results are
   cached, and thus a majority of database calls are avoided (unless they happen right after a mutation).

2. **Without Fusion**: `UserService.Get` is a regular method, so every call to it executes a simple SQL query.

### Test Scenarios

- **Multiple readers, 1 mutator**: Simulates a realistic high-intensity workload with ~640 concurrent reader tasks (20 per CPU core) performing lookups, while a single mutator task periodically updates random user records. This tests how well Fusion handles cache invalidation under concurrent load.

- **Single reader, no mutators**: A single task performs sequential lookups with
  no concurrent mutations. This measures the peak lookup throughput per CPU core.

The test uses a pool of 1,000 pre-populated user records. Each run performs
multiple iterations, and the best result from 3 runs is reported.

### Multiple Readers + 1 Mutator (all cores)

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 155.68K calls/s | 38.61K calls/s |
| With Fusion | 487.46M calls/s | 535.40M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">3,131x</span> | <span style="color: #22c55e; font-weight: bold;">13,867x</span> |

### Single Reader, No Mutators

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 55.70K calls/s | 1.78K calls/s |
| With Fusion | 23.74M calls/s | 26.97M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">426x</span> | <span style="color: #22c55e; font-weight: bold;">15,152x</span> |

### Key Observations

- **With Fusion + concurrent readers**: ~500M calls/s regardless of the database,
  because most calls are served from Fusion's in-memory cache.
  That's roughly <span style="color: #22c55e; font-weight: bold;">13,900x faster</span> than direct PostgreSQL access
  and <span style="color: #22c55e; font-weight: bold;">3,100x faster</span> than direct SQLite access under the same concurrent load.

- **Without Fusion**: Performance is entirely database-bound.
  SQLite (in-process) outperforms PostgreSQL (network round-trip) significantly,
  especially for single-threaded access.

- **Concurrent access amplifies the difference**: With many readers, Fusion's
  lock-free cache scales linearly with CPU cores, while database access becomes
  the bottleneck.

## Benchmark.cmd from ActualLab.Fusion.Samples

The benchmark measures throughput of a simple repository-style user lookup service that retrieves and updates user records from a database: `UserService.Get(userId)` and `Update(userId, ...)`.

### Local Services

| Test | Result | Speedup |
|------|--------|---------|
| Regular Service | 161.23K calls/s | |
| Fusion Service | 330.53M calls/s | <span style="color: #22c55e; font-weight: bold;">~2,050x</span> |

### Remote Services

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 103.88K calls/s | |
| HTTP Client → Fusion Service | 365.18K calls/s | <span style="color: #22c55e; font-weight: bold;">~3.5x</span> |
| ActualLab.Rpc Client → Fusion Service | 7.81M calls/s | <span style="color: #22c55e; font-weight: bold;">~75x</span> |
| Fusion Client → Fusion Service | 226.05M calls/s | <span style="color: #22c55e; font-weight: bold;">~2,176x</span> |

Wondering what each of these scenarios means (HTTP vs ActualLab.Rpc vs Fusion client)? See
[What each scenario means](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#what-each-scenario-means)
in the source report.

## RpcBenchmark.cmd from ActualLab.Fusion.Samples

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks.
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**.
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

### Calls

| Test | ActualLab.Rpc | gRPC | SignalR | Speedup |
|------|---------------|------|---------|---------|
| Sum | **9.97M calls/s** | 842.95K calls/s | 4.84M calls/s | <span style="color: #22c55e; font-weight: bold;">2.1..11.8x</span> |
| GetUser | **8.91M calls/s** | 848.30K calls/s | 4.28M calls/s | <span style="color: #22c55e; font-weight: bold;">2.1..10.5x</span> |
| SayHello | **5.95M calls/s** | 808.67K calls/s | 2.23M calls/s | <span style="color: #22c55e; font-weight: bold;">2.7..7.4x</span> |

_gRPC call throughput is lower on .NET 11.0.0 than it was on .NET 10.0.8 (1.28M / 1.24M / 1.16M calls/s
with the same benchmark), even with the
[pipe scheduler fix](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#net-11-pipe-scheduler-fix)
this benchmark applies on .NET 11._

<ClientOnly>
  <BarChart
    title="RPC Calls (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [9.97, 8.91, 5.95], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [4.84, 4.28, 2.23], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [0.84, 0.85, 0.81], backgroundColor: '#f59e0b' }
    ]"
    :yMax="11"
    yLabel="M calls/s"
  />
</ClientOnly>

### Streams

| Test | ActualLab.Rpc | gRPC | SignalR | Speedup |
|------|---------------|------|---------|---------|
| Stream1 | **104.98M items/s** | 39.51M items/s | 22.36M items/s | <span style="color: #22c55e; font-weight: bold;">2.7..4.7x</span> |
| Stream100 | **45.89M items/s** | 19.94M items/s | 14.78M items/s | <span style="color: #22c55e; font-weight: bold;">2.3..3.1x</span> |
| Stream10K | **808.92K items/s** | 648.00K items/s | 402.48K items/s | <span style="color: #22c55e; font-weight: bold;">1.2..2.0x</span> |

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

### Throughput (items/s × item size)

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Stream1 | **104.98 MB/s** | 39.51 MB/s | 22.36 MB/s |
| Stream100 | **4.59 GB/s** | 1.99 GB/s | 1.48 GB/s |
| Stream10K | **8.28 GB/s** | 6.64 GB/s | 4.12 GB/s |

<ClientOnly>
  <BarChart
    title="RPC Streams Throughput (GB/s)"
    :labels="['Stream100', 'Stream10K']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [4.59, 8.28], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [1.99, 6.64], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [1.48, 4.12], backgroundColor: '#3b82f6' }
    ]"
    :yMax="10"
    yLabel="GB/s"
  />
</ClientOnly>

## Docker-Based RPC Benchmarks

These benchmarks run in Docker containers with CPU limits to measure **4-core server performance**.
The server container is limited to 4 CPUs while client containers have 24 CPUs available,
ensuring the server is the bottleneck. This setup matches [grpc_bench](https://github.com/LesnyRumcajs/grpc_bench), `SayHello` w/ `gRPC` is identical to what `grpc_bench` measures.

> **External cross-check.** grpc_bench's own
> [.NET gRPC result on an i9-13900KF @ 5.5 GHz](https://github.com/LesnyRumcajs/grpc_bench/discussions/441)
> (4-CPU server — a desktop CPU with per-core throughput close to the 9950X3D) is **~402K req/s**,
> close to our `SayHello` gRPC figure of **441.37K** below. Same library (grpc-dotnet),
> same 4-CPU setup — which confirms our gRPC numbers are measured fairly. gRPC-dotnet is the *fastest*
> framework in that grpc_bench run, and ActualLab.Rpc's `SayHello` (**2.46M**) is still **~6.1× faster**
> than it.

### Docker Calls

| Framework | Sum | GetUser | SayHello |
|-----------|-----|---------|----------|
| ActualLab.Rpc | **5.25M calls/s** | **4.13M calls/s** | **2.46M calls/s** |
| SignalR | 2.42M calls/s | 2.02M calls/s | 931.18K calls/s |
| gRPC | 483.52K calls/s | 467.10K calls/s | 441.37K calls/s |
| MagicOnion | 351.54K calls/s | 342.24K calls/s | 329.68K calls/s |
| StreamJsonRpc | 332.66K calls/s | 263.40K calls/s | 114.41K calls/s |
| HTTP | 93.08K calls/s | 93.87K calls/s | 92.56K calls/s |

<ClientOnly>
  <BarChart
    title="Docker RPC Calls - 4 CPU Server (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [5.25, 4.13, 2.46], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [2.42, 2.02, 0.93], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [0.48, 0.47, 0.44], backgroundColor: '#f59e0b' },
      { label: 'MagicOnion', data: [0.35, 0.34, 0.33], backgroundColor: '#a855f7' },
      { label: 'StreamJsonRpc', data: [0.33, 0.26, 0.11], backgroundColor: '#ec4899' },
      { label: 'HTTP', data: [0.09, 0.09, 0.09], backgroundColor: '#6b7280' }
    ]"
    :yMax="6"
    yLabel="M calls/s"
  />
</ClientOnly>

### Docker Streams

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

| Framework | Stream1 | Stream100 | Stream10K |
|-----------|---------|-----------|-----------|
| ActualLab.Rpc | **37.77M items/s** | **13.33M items/s** | **285.12K items/s** |
| gRPC | 13.42M items/s | 7.27M items/s | 149.04K items/s |
| SignalR | 9.71M items/s | 5.50M items/s | 155.52K items/s |
| StreamJsonRpc | 127.44K items/s | 127.44K items/s | 63.72K items/s |

<ClientOnly>
  <BarChart
    title="Docker RPC Streams - 4 CPU Server (Million items/s)"
    :labels="['Stream1', 'Stream100']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [37.77, 13.33], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [13.42, 7.27], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [9.71, 5.50], backgroundColor: '#3b82f6' },
      { label: 'StreamJsonRpc', data: [0.13, 0.13], backgroundColor: '#ec4899' }
    ]"
    :yMax="40"
    yLabel="M items/s"
  />
</ClientOnly>

## Reference: Redis Benchmark

Reference benchmark using `redis-benchmark` tool on the same machine (500K requests, best of 5 runs). Optimal client count (12) was determined via binary search over 1-1000 range.

| Operation | Result |
|-----------|--------|
| PING_INLINE | 231.59K req/s |
| GET | 229.25K req/s |
| SET | 229.67K req/s |

---

**More details:** these numbers come from the benchmark report in ActualLab.Fusion.Samples, which also
lists the exact commands, additional frameworks, and the full interception/microbenchmark breakdown:
[**Benchmarks.md**](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md).
