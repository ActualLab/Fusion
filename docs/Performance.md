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
| **Speedup** | **2,032x** | **8,126x** |

### Single Reader, No Mutators

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 55.70K calls/s | 1.78K calls/s |
| With Fusion | 19.54M calls/s | 19.66M calls/s |
| **Speedup** | **351x** | **11,045x** |

### Key Observations

- **With Fusion + concurrent readers**: ~315M calls/s regardless of the database,
  because most calls are served from Fusion's in-memory cache.
  This is approximately **2,000x faster** than direct PostgreSQL access
  and **8,000x faster** than the single-reader baseline without Fusion.

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
| Regular Service | 136.91K calls/s | |
| Fusion Service | 263.62M calls/s | **~1,926x** |

### Remote Services

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 99.66K calls/s | |
| HTTP Client → Fusion Service | 420.67K calls/s | **~4.2x** |
| ActualLab.Rpc Client → Fusion Service | 6.10M calls/s | **~61x** |
| Fusion Client → Fusion Service | 223.15M calls/s | **~2,239x** |

## RpcBenchmark.cmd from ActualLab.Fusion.Samples

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks.
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**.
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

### Calls

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Sum | 8.87M calls/s | 1.11M calls/s | 5.34M calls/s |
| GetUser | 7.81M calls/s | 1.09M calls/s | 4.43M calls/s |
| SayHello | 5.58M calls/s | 1.03M calls/s | 2.23M calls/s |

### Streams

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Stream1 | 95.10M items/s | 38.75M items/s | 17.11M items/s |
| Stream100 | 38.90M items/s | 20.63M items/s | 13.61M items/s |
| Stream10K | 320.04K items/s | 636.84K items/s | 387.00K items/s |
