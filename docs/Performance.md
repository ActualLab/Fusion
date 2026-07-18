# Performance

**Updated:** 2026-07-18<br/>
**ActualLab.Fusion Version:** 14.0.17

This page summarizes results of Fusion performance benchmarks.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **.NET** | 10.0.8 |

Note that Ryzen 9 9950X3D has 32 logical cores due to SMT.

## Fusion Micro-Benchmarks

BenchmarkDotNet measurements of the **single-threaded, per-operation cost** of Fusion's core
compute-method primitives, with an otherwise empty method body (so the numbers reflect Fusion's own
overhead). `Calls/s per core` = `1 / Mean`.

| Operation | Calls/s per core | Mean | Allocated |
|-----------|------------------|------|-----------|
| Cached hit, `long` key<br/>`Service.Get(0L, default)` | 50.68M | 19.73 ns | 32 B |
| Cached hit, `string` key<br/>`Service.Get("key", default)` | 34.90M | 28.65 ns | 32 B |
| Invalidation (activate + 1 call)<br/>`using (Invalidation.Begin())`<br/>`    Service.Get(key, default)` | 18.77M | 53.28 ns | 112 B |
| Recompute + cache (fresh key each call)<br/>`Service.Get(i++, default)` | 2.04M | 490.1 ns | 1007 B |

A cache hit costs ~20 ns and a single `ArgumentList` allocation — this is the ~50M calls/s per core that
Fusion sustains for cached compute-method calls.

## Interception & Proxy Overhead

Fusion builds compute methods and RPC clients on generated proxies. Per-call cost of ActualLab's proxies
vs [Castle DynamicProxy](https://github.com/castleproject/Core) for a simple interceptor (returns the
method's default result), single-threaded:

| Proxy variant | Calls/s per core | Mean | Allocated |
|---------------|------------------|------|-----------|
| No proxy (direct virtual call) | 763.7M | 1.31 ns | – |
| **[ActualLab, simple interceptor](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#proxy-and-interception-benchmarks)** | **386.0M** | **2.59 ns** | **24 B** |
| **[Castle DynamicProxy, simple](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#proxy-and-interception-benchmarks)** | **61.94M** | **16.15 ns** | **128 B** |

ActualLab's interception is **~5-7x faster than Castle DynamicProxy** — and only **~2-3x slower than a
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
| With Fusion | 498.92M calls/s | 533.85M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">3,205x</span> | <span style="color: #22c55e; font-weight: bold;">13,827x</span> |

### Single Reader, No Mutators

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 55.70K calls/s | 1.78K calls/s |
| With Fusion | 26.77M calls/s | 26.74M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">481x</span> | <span style="color: #22c55e; font-weight: bold;">15,022x</span> |

### Key Observations

- **With Fusion + concurrent readers**: ~500M calls/s regardless of the database,
  because most calls are served from Fusion's in-memory cache.
  That's roughly <span style="color: #22c55e; font-weight: bold;">13,800x faster</span> than direct PostgreSQL access
  and <span style="color: #22c55e; font-weight: bold;">3,200x faster</span> than direct SQLite access under the same concurrent load.

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
| Regular Service | 171.05K calls/s | |
| Fusion Service | 344.98M calls/s | <span style="color: #22c55e; font-weight: bold;">~2,017x</span> |

### Remote Services

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 102.82K calls/s | |
| HTTP Client → Fusion Service | 304.87K calls/s | <span style="color: #22c55e; font-weight: bold;">~3.0x</span> |
| ActualLab.Rpc Client → Fusion Service | 7.82M calls/s | <span style="color: #22c55e; font-weight: bold;">~76x</span> |
| Fusion Client → Fusion Service | 230.16M calls/s | <span style="color: #22c55e; font-weight: bold;">~2,239x</span> |

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
| Sum | **9.91M calls/s** | 1.28M calls/s | 4.85M calls/s | <span style="color: #22c55e; font-weight: bold;">2.0..7.7x</span> |
| GetUser | **8.75M calls/s** | 1.24M calls/s | 4.05M calls/s | <span style="color: #22c55e; font-weight: bold;">2.2..7.1x</span> |
| SayHello | **6.04M calls/s** | 1.16M calls/s | 2.16M calls/s | <span style="color: #22c55e; font-weight: bold;">2.8..5.2x</span> |

<ClientOnly>
  <BarChart
    title="RPC Calls (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [9.91, 8.75, 6.04], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [4.85, 4.05, 2.16], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [1.28, 1.24, 1.16], backgroundColor: '#f59e0b' }
    ]"
    :yMax="11"
    yLabel="M calls/s"
  />
</ClientOnly>

### Call Latency Under Peak Throughput

| Framework | Sum (p50 / p95 / p99) | GetUser (p50 / p95 / p99) | SayHello (p50 / p95 / p99) |
|-----------|-----------------------|---------------------------|----------------------------|
| ActualLab.Rpc | **1.8ms** / **2.4ms** / **6.1ms** | **2.0ms** / **2.6ms** / **6.0ms** | **2.9ms** / **3.4ms** / **6.8ms** |
| gRPC | 3.4ms / 4.5ms / 12.1ms | 3.5ms / 4.4ms / 11.0ms | 3.5ms / 4.4ms / 10.5ms |
| SignalR | 5.1ms / 16.4ms / 22.7ms | 6.1ms / 7.8ms / 13.3ms | 11.7ms / 16.2ms / 20.5ms |

### Streams

| Test | ActualLab.Rpc | gRPC | SignalR | Speedup |
|------|---------------|------|---------|---------|
| Stream1 | **99.96M items/s** | 43.78M items/s | 17.97M items/s | <span style="color: #22c55e; font-weight: bold;">2.3..5.6x</span> |
| Stream100 | **44.89M items/s** | 25.87M items/s | 13.90M items/s | <span style="color: #22c55e; font-weight: bold;">1.7..3.2x</span> |
| Stream10K | **807.84K items/s** | 572.76K items/s | 432.00K items/s | <span style="color: #22c55e; font-weight: bold;">1.4..1.9x</span> |

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

### Throughput (items/s × item size)

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Stream1 | **99.96 MB/s** | 43.78 MB/s | 17.97 MB/s |
| Stream100 | **4.49 GB/s** | 2.59 GB/s | 1.39 GB/s |
| Stream10K | **8.27 GB/s** | 5.86 GB/s | 4.42 GB/s |

<ClientOnly>
  <BarChart
    title="RPC Streams Throughput (GB/s)"
    :labels="['Stream100', 'Stream10K']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [4.49, 8.27], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [2.59, 5.86], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [1.39, 4.42], backgroundColor: '#3b82f6' }
    ]"
    :yMax="10"
    yLabel="GB/s"
  />
</ClientOnly>

## Docker-Based RPC Benchmarks

These benchmarks run in Docker containers with CPU limits to measure **4-core server performance**.
The server container is limited to 4 CPUs while client containers have 24 CPUs available,
ensuring the server is the bottleneck. This setup matches [grpc_bench](https://github.com/LesnyRumcajs/grpc_bench), `SayHello` w/ `gRPC` is identical to what `grpc_bench` measures.

### Docker Calls

| Framework | Sum | GetUser | SayHello |
|-----------|-----|---------|----------|
| ActualLab.Rpc | **4.77M calls/s** | **4.38M calls/s** | **2.52M calls/s** |
| SignalR | 2.38M calls/s | 1.94M calls/s | 862.07K calls/s |
| gRPC | 437.85K calls/s | 441.44K calls/s | 399.32K calls/s |
| MagicOnion | 392.59K calls/s | 402.85K calls/s | 362.84K calls/s |
| StreamJsonRpc | 279.62K calls/s | 231.86K calls/s | 99.09K calls/s |
| HTTP | 105.25K calls/s | 103.12K calls/s | 88.18K calls/s |

<ClientOnly>
  <BarChart
    title="Docker RPC Calls - 4 CPU Server (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [4.77, 4.38, 2.52], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [2.38, 1.94, 0.86], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [0.44, 0.44, 0.40], backgroundColor: '#f59e0b' },
      { label: 'MagicOnion', data: [0.39, 0.40, 0.36], backgroundColor: '#a855f7' },
      { label: 'StreamJsonRpc', data: [0.28, 0.23, 0.10], backgroundColor: '#ec4899' },
      { label: 'HTTP', data: [0.11, 0.10, 0.09], backgroundColor: '#6b7280' }
    ]"
    :yMax="5"
    yLabel="M calls/s"
  />
</ClientOnly>

### Docker Call Latency Under Peak Throughput

| Framework | Sum (p50 / p95 / p99) | GetUser (p50 / p95 / p99) | SayHello (p50 / p95 / p99) |
|-----------|-----------------------|---------------------------|----------------------------|
| ActualLab.Rpc | 3.5ms / **8.4ms** / **12.8ms** | 4.1ms / 8.6ms / **13.1ms** | 6.4ms / 9.6ms / 24.3ms |
| SignalR | 8.7ms / 28.1ms / 30.0ms | 9.8ms / 32.2ms / 35.2ms | 20.1ms / 31.2ms / 40.6ms |
| gRPC | **3.3ms** / 32.3ms / 45.4ms | **3.5ms** / **6.8ms** / 32.1ms | **4.2ms** / 9.6ms / 36.2ms |
| MagicOnion | 4.5ms / 8.3ms / 24.1ms | 5.0ms / 10.8ms / 23.1ms | 5.5ms / **8.8ms** / **12.3ms** |
| StreamJsonRpc | 43.4ms / 56.4ms / 59.6ms | 58.9ms / 70.1ms / 72.5ms | 107.3ms / 212.3ms / 222.0ms |
| HTTP | 33.9ms / 51.4ms / 54.9ms | 34.2ms / 44.3ms / 45.9ms | 30.4ms / 44.0ms / 45.5ms |

### Docker Streams

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

| Framework | Stream1 | Stream100 | Stream10K |
|-----------|---------|-----------|-----------|
| ActualLab.Rpc | **35.17M items/s** | **12.97M items/s** | **279.72K items/s** |
| gRPC | 11.79M items/s | 6.19M items/s | 140.40K items/s |
| SignalR | 8.89M items/s | 5.08M items/s | 106.20K items/s |
| StreamJsonRpc | 120.96K items/s | 120.96K items/s | 60.48K items/s |

<ClientOnly>
  <BarChart
    title="Docker RPC Streams - 4 CPU Server (Million items/s)"
    :labels="['Stream1', 'Stream100']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [35.17, 12.97], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [11.79, 6.19], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [8.89, 5.08], backgroundColor: '#3b82f6' },
      { label: 'StreamJsonRpc', data: [0.12, 0.12], backgroundColor: '#ec4899' }
    ]"
    :yMax="36"
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
