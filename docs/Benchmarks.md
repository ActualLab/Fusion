# Benchmark Results

**Updated:** 2026-01-17<br/>
**ActualLab.Fusion Version:** 11.4.7

This page summarizes benchmark results from ActualLab.Fusion.Samples repository.

We ran each benchmark 2 times and took the best result for each test.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **.NET Version** | 10.0.1 |

Note that Ryzen 9 9950X3D has 32 logical cores due to SMT.

## Run-Benchmark.cmd

The benchmark measures throughput of a simple repository-style user lookup service that retrieves and updates user records from a database: `UserService.Get(userId)` and `Update(userId, ...)`.

To run the benchmark:
```powershell
dotnet run -c Release --project src/Benchmark/Benchmark.csproj --no-launch-profile
```

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

## Run-RpcBenchmark.cmd

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks. 
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**. 
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

There are two benchmarks in `RpcBenchmark` project: 

RPC calls:
```powershell
dotnet run -c Release --project src/RpcBenchmark/RpcBenchmark.csproj --no-launch-profile -- test -b calls -l rpc,grpc,signalr -f msgpack5c -n 4
```

RPC streaming:
```powershell
dotnet run -c Release --project src/RpcBenchmark/RpcBenchmark.csproj --no-launch-profile -- test -b streams -l rpc,grpc,signalr -f msgpack5c -n 4
```

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
