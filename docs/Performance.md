# Performance

**Updated:** 2026-11-16<br/>
**ActualLab.Fusion Version:** 15.1.38

This page summarizes results of Fusion performance benchmarks.

## Test Environment

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D 16-Core Processor |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11 |
| **Docker** | Docker Desktop, WSL2 backend (32 CPUs, 25 GB). The server container runs with `--cpus 4 --memory 8g` |
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
| **Speedup** | <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">↑3,131×</span> | <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">↑13,867×</span> |

### Single Reader, No Mutators

| Test | SQLite | PostgreSQL |
|------|--------|------------|
| Without Fusion | 55.70K calls/s | 1.78K calls/s |
| With Fusion | 23.74M calls/s | 26.97M calls/s |
| **Speedup** | <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">↑426×</span> | <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">↑15,152×</span> |

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

| Test | Result |
|------|--------|
| Regular Service | 161.23K calls/s |
| Fusion Service | 330.53M calls/s |

### Remote Services

| Test | Result |
|------|--------|
| HTTP Client → Regular Service | 103.88K calls/s |
| HTTP Client → Fusion Service | 365.18K calls/s |
| ActualLab.Rpc Client → Fusion Service | 7.81M calls/s |
| Fusion Client → Fusion Service | 226.05M calls/s |

Wondering what each of these scenarios means (HTTP vs ActualLab.Rpc vs Fusion client)? See
[What each scenario means](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#what-each-scenario-means)
in the source report.

## RpcBenchmark.cmd from ActualLab.Fusion.Samples

This benchmark compares **ActualLab.Rpc** with **gRPC**, **SignalR**, and other RPC frameworks.
The tables below include only **ActualLab.Rpc**, **gRPC**, and **SignalR**.
Other options, such as **StreamJsonRpc** and **RESTful API**, are way slower, so we omit them.

A colored number after a result shows how many times slower it is than the fastest result in the same test:
<span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span> is the fastest,
<span style="background: rgb(163,116,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span> is half as fast,
<span style="background: rgb(137,39,39); color: #fff; padding: 2px 6px; border-radius: 3px;">↓10×</span> is ten times slower.
The color goes from green (1x) through red (3x) to black (50x or more) on a log scale.

### Calls

| Test | ActualLab.Rpc | gRPC | SignalR |
|---|---|---|---|
| Sum | <span style="white-space: nowrap;">**9.97M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">842.95K calls/s <span style="background: rgb(122,35,35); color: #fff; padding: 2px 6px; border-radius: 3px;">↓12×</span></span> | <span style="white-space: nowrap;">4.84M calls/s <span style="background: rgb(169,112,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> |
| GetUser | <span style="white-space: nowrap;">**8.91M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">848.30K calls/s <span style="background: rgb(133,38,38); color: #fff; padding: 2px 6px; border-radius: 3px;">↓11×</span></span> | <span style="white-space: nowrap;">4.28M calls/s <span style="background: rgb(171,111,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> |
| SayHello | <span style="white-space: nowrap;">**5.95M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">808.67K calls/s <span style="background: rgb(163,46,46); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.4×</span></span> | <span style="white-space: nowrap;">2.23M calls/s <span style="background: rgb(217,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> |

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

| Test | ActualLab.Rpc | gRPC | SignalR |
|---|---|---|---|
| Stream1 | <span style="white-space: nowrap;">**104.98M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">39.51M items/s <span style="background: rgb(216,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> | <span style="white-space: nowrap;">22.36M items/s <span style="background: rgb(201,57,57); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.7×</span></span> |
| Stream100 | <span style="white-space: nowrap;">**45.89M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">19.94M items/s <span style="background: rgb(190,99,74); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.3×</span></span> | <span style="white-space: nowrap;">14.78M items/s <span style="background: rgb(236,67,67); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.1×</span></span> |
| Stream10K | <span style="white-space: nowrap;">**808.92K items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">648.00K items/s <span style="background: rgb(75,171,89); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.2×</span></span> | <span style="white-space: nowrap;">402.48K items/s <span style="background: rgb(164,115,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span></span> |

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
The server container is limited to 4 CPUs while the client container has 24 CPUs available,
ensuring the server is the bottleneck. This setup matches [grpc_bench](https://github.com/LesnyRumcajs/grpc_bench), `SayHello` w/ `gRPC` is identical to what `grpc_bench` measures.

These figures were measured on 2026-11-16 **without TLS**, as grpc_bench does (`RpcBenchmark` runs
without TLS by default now, `-tls` turns it on), and with fixed worker and concurrency parameters
instead of the parameter search. With TLS, calls are 3-10% slower for ActualLab.Rpc and SignalR and
17-20% slower for gRPC and MagicOnion.

> **Comparison with other gRPC servers.** We ran 26 servers from grpc_bench on the same machine in
> the same 4-CPU setup, on current versions of their libraries and with the load generator tuned
> until each server is saturated. The fastest of them, `rust_ntex`, reaches **1.06M** `SayHello`
> calls/s, and grpc_bench's own grpc-dotnet server reaches **670K**, which agrees with the **567K**
> our gRPC test measures below with a less favorable client. ActualLab.Rpc's `SayHello` (**2.54M**)
> is **2.4x** the fastest of these gRPC servers. See
> [The Fastest .NET RPC Libraries vs the Fastest gRPC Libraries](./posts/261116-grpc-bench.md)

### Docker Calls

| Framework | Sum | GetUser | SayHello |
|---|---|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">**5.62M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**4.36M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**2.54M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | <span style="white-space: nowrap;">2.71M calls/s <span style="background: rgb(170,111,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> | <span style="white-space: nowrap;">2.24M calls/s <span style="background: rgb(158,119,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">999.19K calls/s <span style="background: rgb(208,87,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.5×</span></span> |
| gRPC | <span style="white-space: nowrap;">625.64K calls/s <span style="background: rgb(146,41,41); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.0×</span></span> | <span style="white-space: nowrap;">602.70K calls/s <span style="background: rgb(164,47,47); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.2×</span></span> | <span style="white-space: nowrap;">567.12K calls/s <span style="background: rgb(205,58,58); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.5×</span></span> |
| MagicOnion | <span style="white-space: nowrap;">582.60K calls/s <span style="background: rgb(140,40,40); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.6×</span></span> | <span style="white-space: nowrap;">563.39K calls/s <span style="background: rgb(158,45,45); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.7×</span></span> | <span style="white-space: nowrap;">518.14K calls/s <span style="background: rgb(197,56,56); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.9×</span></span> |
| StreamJsonRpc | <span style="white-space: nowrap;">396.57K calls/s <span style="background: rgb(107,30,30); color: #fff; padding: 2px 6px; border-radius: 3px;">↓14×</span></span> | <span style="white-space: nowrap;">306.24K calls/s <span style="background: rgb(107,30,30); color: #fff; padding: 2px 6px; border-radius: 3px;">↓14×</span></span> | <span style="white-space: nowrap;">134.15K calls/s <span style="background: rgb(82,23,23); color: #fff; padding: 2px 6px; border-radius: 3px;">↓19×</span></span> |
| HTTP | <span style="white-space: nowrap;">146.06K calls/s <span style="background: rgb(22,6,6); color: #fff; padding: 2px 6px; border-radius: 3px;">↓38×</span></span> | <span style="white-space: nowrap;">147.05K calls/s <span style="background: rgb(44,13,13); color: #fff; padding: 2px 6px; border-radius: 3px;">↓30×</span></span> | <span style="white-space: nowrap;">130.46K calls/s <span style="background: rgb(80,23,23); color: #fff; padding: 2px 6px; border-radius: 3px;">↓19×</span></span> |

<ClientOnly>
  <BarChart
    title="Docker RPC Calls - 4 CPU Server (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [5.62, 4.36, 2.54], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [2.71, 2.24, 1.00], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [0.63, 0.60, 0.57], backgroundColor: '#f59e0b' },
      { label: 'MagicOnion', data: [0.58, 0.56, 0.52], backgroundColor: '#a855f7' },
      { label: 'StreamJsonRpc', data: [0.40, 0.31, 0.13], backgroundColor: '#ec4899' },
      { label: 'HTTP', data: [0.15, 0.15, 0.13], backgroundColor: '#6b7280' }
    ]"
    :yMax="6"
    yLabel="M calls/s"
  />
</ClientOnly>

### Docker Streams

Test names indicate item size: Stream1 = 1-byte items, Stream100 = 100-byte items, Stream10K = 10KB items.
Stream tests count the items received inside the measurement window.

| Framework | Stream1 | Stream100 | Stream10K |
|---|---|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">**40.19M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**18.64M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**492.15K items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| gRPC | <span style="white-space: nowrap;">15.40M items/s <span style="background: rgb(213,84,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> | <span style="white-space: nowrap;">9.42M items/s <span style="background: rgb(161,117,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span></span> | <span style="white-space: nowrap;">328.92K items/s <span style="background: rgb(109,150,84); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.5×</span></span> |
| SignalR | <span style="white-space: nowrap;">12.01M items/s <span style="background: rgb(230,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.3×</span></span> | <span style="white-space: nowrap;">6.97M items/s <span style="background: rgb(218,81,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> | <span style="white-space: nowrap;">239.28K items/s <span style="background: rgb(169,112,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> |
| StreamJsonRpc | <span style="white-space: nowrap;">162.62K items/s <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓247×</span></span> | <span style="white-space: nowrap;">163.20K items/s <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓114×</span></span> | <span style="white-space: nowrap;">94.63K items/s <span style="background: rgb(192,55,55); color: #fff; padding: 2px 6px; border-radius: 3px;">↓5.2×</span></span> |

<ClientOnly>
  <BarChart
    title="Docker RPC Streams - 4 CPU Server (Million items/s)"
    :labels="['Stream1', 'Stream100']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [40.19, 18.64], backgroundColor: '#22c55e' },
      { label: 'gRPC', data: [15.40, 9.42], backgroundColor: '#f59e0b' },
      { label: 'SignalR', data: [12.01, 6.97], backgroundColor: '#3b82f6' },
      { label: 'StreamJsonRpc', data: [0.16, 0.16], backgroundColor: '#ec4899' }
    ]"
    :yMax="45"
    yLabel="M items/s"
  />
</ClientOnly>

## Real-Network RPC Benchmarks

The Docker benchmarks above run the client and the server on one machine. These run over a real
**5 Gbit/s LAN** between two machines, without TLS, and were measured on 2026-11-17. The server is
limited to **2 CPUs**, and the client is a separate PC with 32 logical CPUs, so the server is the bottleneck.

| | Client | Server |
|-|--------|--------|
| **Machine** | AMD Ryzen AI MAX+ PRO 395 (16 cores / 32 logical CPUs), 96 GB RAM | VM (16 vCPUs, 26 GB RAM) on Proxmox VE 9.0.3, on an AMD Ryzen 7 8745HS host (8 cores / 16 logical CPUs), 30 GB RAM |
| **OS** | Windows 11 Pro | Ubuntu 24.04.5 (kernel 6.8.0-149) in the VM, kernel 6.14.8-2-pve on the host |
| **Network** | Realtek USB 5GbE adapter | Realtek RTL8157 USB 5GbE adapter, Linux bridge, virtio NIC with 8 queues |
| **Runtime** | .NET 11.0.0, native process | .NET 11.0.0, Docker 29.10.0, container with `--cpus 2 --memory 8g` |

`iperf3` measures 4.74 Gbit/s of TCP throughput on this link. The server runs in a container, inside a
VM, on a Proxmox host, so the network path is longer than in the Docker tests. These figures are not
comparable with the Docker ones: the server has 2 CPUs instead of 4, its CPU is slower, and the
client is a different machine. The full setup, the commands, and the traffic and CPU measurements are in
[Real Network RPC Benchmarks](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#real-network-rpc-benchmarks).

### Network Calls

| Framework | Sum | GetUser | SayHello |
|---|---|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">**2.22M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**1.71M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**1.07M calls/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | <span style="white-space: nowrap;">1.06M calls/s <span style="background: rgb(172,110,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> | <span style="white-space: nowrap;">764.92K calls/s <span style="background: rgb(184,103,75); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.2×</span></span> | <span style="white-space: nowrap;">416.16K calls/s <span style="background: rgb(210,86,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| gRPC | <span style="white-space: nowrap;">364.40K calls/s <span style="background: rgb(179,51,51); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.1×</span></span> | <span style="white-space: nowrap;">355.75K calls/s <span style="background: rgb(199,57,57); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.8×</span></span> | <span style="white-space: nowrap;">313.92K calls/s <span style="background: rgb(228,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| HTTP | <span style="white-space: nowrap;">98.42K calls/s <span style="background: rgb(68,19,19); color: #fff; padding: 2px 6px; border-radius: 3px;">↓23×</span></span> | <span style="white-space: nowrap;">103.78K calls/s <span style="background: rgb(94,27,27); color: #fff; padding: 2px 6px; border-radius: 3px;">↓16×</span></span> | <span style="white-space: nowrap;">81.52K calls/s <span style="background: rgb(114,32,32); color: #fff; padding: 2px 6px; border-radius: 3px;">↓13×</span></span> |

Every call test is limited by the server's 2 CPUs (187-196% usage), and none of them uses more than a
quarter of the link. ActualLab.Rpc is 2.1-2.6x faster than SignalR, 3.4-6.1x faster than gRPC and
12-23x faster than HTTP.

<ClientOnly>
  <BarChart
    title="Network RPC Calls - 2 CPU Server (Million/s)"
    :labels="['Sum', 'GetUser', 'SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [2.22, 1.71, 1.07], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [1.06, 0.76, 0.42], backgroundColor: '#3b82f6' },
      { label: 'gRPC', data: [0.36, 0.36, 0.31], backgroundColor: '#f59e0b' },
      { label: 'HTTP', data: [0.10, 0.10, 0.08], backgroundColor: '#6b7280' }
    ]"
    :yMax="2.5"
    yLabel="M calls/s"
  />
</ClientOnly>

`SayHello` latency (p50 / p95 / p99) at these loads: ActualLab.Rpc 14.3 / 25.4 / 45.3 ms, SignalR
32.6 / 250.5 / 260.1 ms, gRPC 3.6 / 5.1 / 23.6 ms, HTTP 15.1 / 244.6 / 246.9 ms. Latency grows with the
number of calls in flight, so compare it together with the worker count: ActualLab.Rpc runs with
about 15x more concurrent calls than gRPC here.

### Network Streams

Stream10K wasn't run, because it saturates the 5 Gbit/s NICs at any server speed.

| Framework | Stream1 | Stream100 | Stream100 traffic (client rx) |
|---|---|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">**18.51M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**5.45M items/s** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | 4.91 Gbit/s |
| gRPC | <span style="white-space: nowrap;">7.79M items/s <span style="background: rgb(195,95,74); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.4×</span></span> | <span style="white-space: nowrap;">4.54M items/s <span style="background: rgb(68,176,90); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.2×</span></span> | 4.37 Gbit/s |
| SignalR | <span style="white-space: nowrap;">4.98M items/s <span style="background: rgb(221,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.7×</span></span> | <span style="white-space: nowrap;">2.63M items/s <span style="background: rgb(170,111,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> | 4.53 Gbit/s |

- **Stream1** is limited by the server's CPU for all three frameworks.
- **Stream100:** ActualLab.Rpc reaches 92% of the link's 5.93M items/s ceiling (4.74 Gbit/s with no
  framing at all) and has CPU to spare. gRPC runs out of CPU at 4.54M items/s. SignalR's default JSON
  protocol encodes the 100 bytes as base64, so it needs about twice as many bytes per item.

### Compared with gRPC Servers in Other Languages

The 7 servers from [grpc_bench](https://github.com/LesnyRumcajs/grpc_bench) that are the fastest in
their languages in the [4-CPU tests](./posts/261116-grpc-bench.md) ran in the same LAN setup with the
same 2-CPU limit. A Rust client (`tonic_client`) on the client PC loaded them. `SayHello`:

| Server | Client | Calls/s |
|---|---|---|
| ActualLab.Rpc | RpcBenchmark | <span style="white-space: nowrap;">1.07M <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | RpcBenchmark | <span style="white-space: nowrap;">416.2K <span style="background: rgb(210,86,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| rust_ntex (Rust) | tonic_client | <span style="white-space: nowrap;">400.7K <span style="background: rgb(217,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> |
| java_vertx_grpc (Java) | tonic_client | <span style="white-space: nowrap;">318.9K <span style="background: rgb(229,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| gRPC (grpc-dotnet) | RpcBenchmark | <span style="white-space: nowrap;">313.9K <span style="background: rgb(228,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| dotnet_grpc (grpc-dotnet) | tonic_client | <span style="white-space: nowrap;">284.8K <span style="background: rgb(220,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| go_vtgrpc (Go) | tonic_client | <span style="white-space: nowrap;">179.3K <span style="background: rgb(181,51,51); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.0×</span></span> |
| scala_akka (Scala) | tonic_client | <span style="white-space: nowrap;">153.9K <span style="background: rgb(168,48,48); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.0×</span></span> |
| cpp_grpc_mt (C++) | tonic_client | <span style="white-space: nowrap;">150.9K <span style="background: rgb(166,47,47); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.1×</span></span> |
| swift_grpc (Swift) | tonic_client | <span style="white-space: nowrap;">92.3K <span style="background: rgb(124,35,35); color: #fff; padding: 2px 6px; border-radius: 3px;">↓12×</span></span> |
| HTTP | RpcBenchmark | <span style="white-space: nowrap;">89.3K <span style="background: rgb(121,35,35); color: #fff; padding: 2px 6px; border-radius: 3px;">↓12×</span></span> |

Each `RpcBenchmark` row is the better of its two parameter sets for `SayHello`. The two groups are not
measured identically: `RpcBenchmark` figures are the best of six 5-second tries, and grpc_bench figures
are single 15-second runs, which favors the `RpcBenchmark` rows. The details are in
[Benchmarks-gRPC.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks-gRPC.md#real-network).

## Reference: Redis Benchmark

Redis is one of the fastest network servers you can put next to ActualLab.Rpc, so we use it as a
reference point. We ran `redis-benchmark` (Redis 8.10.3, `GET` only) on the same machine and in the same
setup as the Docker RPC benchmarks above: the server container is limited to 4 CPUs, and the clients run in
separate containers with their own cores.

The numbers below are the peak throughput we could get from Redis under that limit, with the server's
CPU saturated.

| Redis configuration | Pipelining | GET throughput | Per core | Server CPU |
|---------------------|------------|----------------|----------|------------|
| Single thread (default) | none | 77.33K req/s | 77K | 100% (1 core) |
| Single thread (default) | 16 commands | 1.30M req/s | 1.30M | 98% (1 core) |
| 6 I/O threads | none | 212.91K req/s | 53K | 405% (4 cores) |
| 6 I/O threads | 16 commands | **2.48M req/s** | 621K | 416% (4 cores) |

For comparison, ActualLab.Rpc under the same 4-core limit (see Docker Calls above):

| Test | Throughput | Per core |
|------|------------|----------|
| GetUser | 4.13M calls/s | 1.03M |
| Sum | 5.25M calls/s | 1.31M |

**This is not an apples-to-apples comparison, and it favors Redis.** With pipelining,
`redis-benchmark` builds batches of 16 commands by hand and sends them back-to-back on every
connection, so the server reads 16 commands and writes 16 replies per network operation. In our RPC
benchmarks every caller makes a single call and awaits its result; batching happens automatically in
the transport, and only when calls happen to queue up. The fair counterpart of our numbers is the
"none" rows; the pipelined rows are a best case that a real application rarely reaches.

Redis also scales less than linearly here: one core handles 1.30M pipelined requests per second, but
four saturated cores handle 2.48M, or 621K per core. Its I/O threads take over reading, parsing and
writing, while command execution stays on a single thread.

Each figure is the best of two 10-second runs. Totals for multi-client runs are the sum of per-client
rates, so they are accurate to within a few percent.

---

**More details:** these numbers come from the benchmark report in ActualLab.Fusion.Samples, which also
lists the exact commands, additional frameworks, and the full interception/microbenchmark breakdown:
[**Benchmarks.md**](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md).
