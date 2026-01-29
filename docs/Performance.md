# Performance

This page summarizes results of Fusion performance benchmarks.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **.NET** | 10.0.1 |

Note that Ryzen 9 9950X3D has 32 logical cores due to SMT.

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

## Results

### Multiple Readers + 1 Mutator (all cores)

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 155.68K calls/s | 38.61K calls/s |
| With Fusion | 316.34M calls/s | 313.75M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">2,032x</span> | <span style="color: #22c55e; font-weight: bold;">8,126x</span> |

### Single Reader, No Mutators

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 55.70K calls/s | 1.78K calls/s |
| With Fusion | 19.54M calls/s | 19.66M calls/s |
| **Speedup** | <span style="color: #22c55e; font-weight: bold;">351x</span> | <span style="color: #22c55e; font-weight: bold;">11,045x</span> |

### Key Observations

- **With Fusion + concurrent readers**: ~315M calls/s regardless of the database,
  because most calls are served from Fusion's in-memory cache.
  This is approximately <span style="color: #22c55e; font-weight: bold;">2,000x faster</span> than direct PostgreSQL access
  and <span style="color: #22c55e; font-weight: bold;">8,000x faster</span> than the single-reader baseline without Fusion.

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
| Regular Service | 135.44K calls/s | |
| Fusion Service | 266.58M calls/s | <span style="color: #22c55e; font-weight: bold;">~1,968x</span> |

### Remote Services

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 100.72K calls/s | |
| HTTP Client → Fusion Service | 431.35K calls/s | <span style="color: #22c55e; font-weight: bold;">~4.3x</span> |
| ActualLab.Rpc Client → Fusion Service | 6.92M calls/s | <span style="color: #22c55e; font-weight: bold;">~69x</span> |
| Fusion Client → Fusion Service | 226.73M calls/s | <span style="color: #22c55e; font-weight: bold;">~2,251x</span> |

## RpcBenchmark.cmd from ActualLab.Fusion.Samples

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks.
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**.
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

### Calls

| Test | ActualLab.Rpc | gRPC | SignalR | Speedup |
|------|---------------|------|---------|---------|
| Sum | 9.33M calls/s | 1.11M calls/s | 5.30M calls/s | <span style="color: #22c55e; font-weight: bold;">1.8..8.4x</span> |
| GetUser | 8.37M calls/s | 1.10M calls/s | 4.43M calls/s | <span style="color: #22c55e; font-weight: bold;">1.9..7.6x</span> |
| SayHello | 5.99M calls/s | 1.04M calls/s | 2.25M calls/s | <span style="color: #22c55e; font-weight: bold;">2.7..5.8x</span> |

<ClientOnly>
  <BarChart
    title="RPC Calls (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [9.33, 8.37, 5.99], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [5.30, 4.43, 2.25], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [1.11, 1.10, 1.04], backgroundColor: '#f59e0b' }
    ]"
    :yMax="10"
    yLabel="M calls/s"
  />
</ClientOnly>

### Streams

| Test | ActualLab.Rpc | gRPC | SignalR | Speedup |
|------|---------------|------|---------|---------|
| Stream1 | 101.17M items/s | 39.59M items/s | 17.17M items/s | <span style="color: #22c55e; font-weight: bold;">2.6..5.9x</span> |
| Stream100 | 47.53M items/s | 21.19M items/s | 14.00M items/s | <span style="color: #22c55e; font-weight: bold;">2.2..3.4x</span> |
| Stream10K | 955.44K items/s | 691.20K items/s | 460.80K items/s | <span style="color: #22c55e; font-weight: bold;">1.4..2.1x</span> |

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

### Throughput (items/s × item size)

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Stream1 | 101.17 MB/s | 39.59 MB/s | 17.17 MB/s |
| Stream100 | 4.75 GB/s | 2.12 GB/s | 1.40 GB/s |
| Stream10K | 9.78 GB/s | 7.08 GB/s | 4.72 GB/s |

<ClientOnly>
  <BarChart
    title="RPC Streams Throughput (GB/s)"
    :labels="['Stream100', 'Stream10K']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [4.75, 9.78], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [2.12, 7.08], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [1.40, 4.72], backgroundColor: '#3b82f6' }
    ]"
    :yMax="10"
    yLabel="GB/s"
  />
</ClientOnly>

## Reference: Redis Benchmark

Reference benchmark using `redis-benchmark` tool on the same machine (500K requests, best of 5 runs). Optimal client count (12) was determined via binary search over 1-1000 range.

| Operation | Result |
|-----------|--------|
| PING_INLINE | 231.59K req/s |
| GET | 229.25K req/s |
| SET | 229.67K req/s |
