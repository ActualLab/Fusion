# Benchmark Results

**Updated:** 2026-01-27<br/>
**ActualLab.Fusion Version:** 12.0.9

This page summarizes benchmark results from ActualLab.Fusion.Samples repository.

We ran each benchmark 3 times and took the best result for each test.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **.NET Version** | 10.0.1 |

Note that Ryzen 9 9950X3D has 32 logical cores due to SMT.

## Reference Redis Benchmark

Reference benchmark using `redis-benchmark` tool on the same machine (500K requests, best of 5 runs). Optimal client count (12) was determined via binary search over 1-1000 range.

| Operation | Result |
|-----------|--------|
| PING_INLINE | 231.59K req/s |
| GET | 229.25K req/s |
| SET | 229.67K req/s |

## Run-Benchmark.cmd

The benchmark measures throughput of a simple repository-style user lookup service that retrieves and updates user records from a database: `UserService.Get(userId)` and `Update(userId, ...)`.

To run the benchmark:
```powershell
dotnet run -c Release --project src/Benchmark/Benchmark.csproj --no-launch-profile
```

### Local Services

| Test | Result | Speedup |
|------|--------|---------|
| Regular Service | 136.40K calls/s | |
| Fusion Service | 264.71M calls/s | **~1,941x** |

### Remote Services

| Test | Result | Speedup |
|------|--------|---------|
| HTTP Client → Regular Service | 100.33K calls/s | |
| HTTP Client → Fusion Service | 423.41K calls/s | **~4.2x** |
| ActualLab.Rpc Client → Fusion Service | 5.85M calls/s | **~58x** |
| Fusion Client → Fusion Service | 222.06M calls/s | **~2,214x** |

## Run-RpcBenchmark.cmd

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks. 
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**. 
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

There are two benchmarks in `RpcBenchmark` project: 

RPC calls:
```powershell
dotnet run -c Release --project src/RpcBenchmark/RpcBenchmark.csproj --no-launch-profile -- test -b calls -l rpc,grpc,signalr -f msgpack6c -n 4
```

RPC streaming:
```powershell
dotnet run -c Release --project src/RpcBenchmark/RpcBenchmark.csproj --no-launch-profile -- test -b streams -l rpc,grpc,signalr -f msgpack6c -n 4
```

### Calls

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Sum | 7.34M calls/s | 1.11M calls/s | 5.35M calls/s |
| GetUser | 6.65M calls/s | 1.10M calls/s | 4.41M calls/s |
| SayHello | 4.98M calls/s | 1.02M calls/s | 2.24M calls/s |

### Streams

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.

| Test | ActualLab.Rpc | gRPC | SignalR |
|------|---------------|------|---------|
| Stream1 | 95.39M items/s | 38.25M items/s | 17.15M items/s |
| Stream100 | 46.46M items/s | 20.77M items/s | 13.78M items/s |
| Stream10K | 941.40K items/s | 691.20K items/s | 460.80K items/s |
