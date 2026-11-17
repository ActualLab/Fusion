---
shortTitle: gRPC and .NET RPC Benchmarks
---

# Benchmarking gRPC Servers Across Languages and RPC Libraries on .NET

**Published:** 2026-11-16 by Alex Yakunin

[grpc_bench](https://github.com/LesnyRumcajs/grpc_bench) is the best-known public benchmark of gRPC
server implementations: dozens of servers in about a dozen languages, each built from its own
Dockerfile and loaded with the same unary call. My own `RpcBenchmark` uses the same message and the same
4-CPU server setup, but until now I compared [ActualLab.Rpc](../PartR.md), the RPC library
Fusion is built on, only with other .NET libraries.

For this post I ran 26 gRPC servers from grpc_bench on the machine I use for my benchmarks and
measured how many calls each can handle on 4 CPUs. Then I ran the RPC libraries for .NET that
`RpcBenchmark` covers (ActualLab.Rpc, SignalR, grpc-dotnet, MagicOnion, StreamJsonRpc and a plain
HTTP API) in the same setup, so both groups can be put into one table. The raw data, including
every load shape I tried, is in
[Benchmarks-gRPC.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks-gRPC.md);
the ActualLab.Rpc side is in
[Benchmarks.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#docker-based-rpc-benchmarks).

## What Was Tested

- **The call:** grpc_bench's `complex_proto` scenario, a unary `SayHello` whose request and reply
  carry the same small structured message. `SayHello` in `RpcBenchmark` uses the same message.
- **The servers:** 26 servers written in Rust, Java, Kotlin, Scala, Go, .NET, C++, Swift, Node.js
  and Python: 22 that grpc_bench already has, <u>upgraded to the current versions of their runtimes
  and libraries</u>, and 4 new ones. The [results table](#results-grpc-servers) lists all of them,
  and [Library and Runtime Versions](#library-and-runtime-versions) lists what they run on.
- **The setup:** the server runs in a Docker container limited to 4 CPUs. Clients run in other
  containers on the same Docker network and use the remaining 28 logical CPUs.
- **<u>No TLS.</u>** grpc_bench runs without it, and so does everything in this post. The question
  here is <u>how much overhead each RPC library adds on the server side</u>, and encryption costs
  the same per byte whichever library sends the bytes. [The Effect of TLS](#the-effect-of-tls)
  shows how much it changes the numbers.
- **The machine:** AMD Ryzen 9 9950X3D (16 cores, 32 logical CPUs), 96 GB RAM, Windows 11 with
  Docker Desktop.
- **The measure:** successful calls per second over a 15-second run after a warmup, with every
  reply decoded, plus the server's CPU usage. 400% means all 4 CPUs are busy.

## What Was Wrong with the Original Test

I started by running the servers the way grpc_bench runs them: one `ghz` process (its Go load
generator) with 50 connections and 1,000 concurrent calls. The fastest server, `rust_ntex`, handled
about 400K calls/s with its 4 CPUs at 320-340%, so that number was not the server's limit.

**The load generator.** It quickly became clear that <u>`ghz` can't saturate the fastest
servers</u>. `ghz` spends about 35 microseconds of CPU time on every call it makes, and `rust_ntex`
spends about 4 microseconds serving one. So one CPU of `rust_ntex` needs about 9 CPUs of `ghz` to
keep it busy, and 4 server CPUs need more than the 28 this machine has left. Even with 28 `ghz`
processes, each at 98% of its CPU, `rust_ntex` stayed at 315% server CPU. No `ghz` setting changed
that.

So I asked Claude to write a load generator in Rust, on `tonic`. It does what a real client does:
a worker sends a call, awaits and decodes the reply, and sends the next one. It needs about
10 microseconds of CPU per call, several times less than `ghz`, and that is what
<u>saturated every server here</u>.

**The load shape.** With grpc_bench's shape (50 connections with 20 calls each) most servers were at
400% CPU and still handled half as many calls as they can. With a few hundred concurrent calls per
connection a server processes them in batches. The best shape is different for every server:
600-900 calls per connection for `rust_ntex` and grpc-dotnet, about 75-110 for Quarkus and Armeria,
and just 4 connections for the C++ servers. So for each server I tried both the Rust client and 28
`ghz` processes, and for the Rust client a set of shapes plus a few more around the best one. I
report the best result without errors.

**The versions.** <u>Most of grpc_bench's servers were pinned to libraries that were 2 to 4 years
old</u>: Vert.x 4.4.4, Quarkus 3.1.2, Armeria 1.20.3, grpc-java 1.58, gRPC C++ 1.51, Node.js 20.
The first round of results described those versions, not the frameworks as they are today. So
<u>every server was moved to the current release of its runtime and gRPC library</u>, and four
servers were added: grpc-swift 2, a Native AOT build of grpc-dotnet, and two frameworks from CloudWeGo,
Volo (Rust) and Kitex (Go). The upgrade moved some results by 20-25%: Armeria, grpc-java and
Node.js got that much faster, while Vert.x 5 and grpc-go 1.85 got about 14% slower than the versions
they replaced.

**The .NET settings.** grpc_bench's .NET server set no runtime options, so it ran with the DATAS
garbage collector mode on. Turning DATAS off, which is what my benchmark does, gives it 3-5% more at
the cost of about 270 MB of memory.

The upgraded servers, the new ones and the Rust client are all in the
[`upgrade-2026-11` branch](https://github.com/ActualLab/grpc_bench/tree/upgrade-2026-11) of a fork
of grpc_bench, proposed upstream as a pull request.

Everything here runs on one machine, so the calls never leave the Docker network. I'll follow up
with the same tests over a real network, with the clients on a separate machine.

## Results: gRPC Servers

`SayHello`, 4-CPU server, no TLS, successful calls per second.

| Server | grpc_bench shape | Tuned | Tuned shape (connections x calls each) | Slower than the fastest |
|--------|------------------|-------|----------------------------------------|-------------------------|
| rust_ntex (Rust, ntex) | 384K | **1,057K** | 20 x 600 | 1.0x |
| java_vertx_grpc (Java, Vert.x) | 350K | 672K | 28 x 338 | 1.6x |
| dotnet_grpc (.NET, grpc-dotnet) | 281K | 670K | 20 x 900 | 1.6x |
| java_quarkus (Java, Quarkus) | 320K | 632K | 28 x 75 | 1.7x |
| dotnet_grpc_aot (.NET, grpc-dotnet, Native AOT) | 271K | 613K | 20 x 600 | 1.7x |
| scala_akka (Scala, Akka gRPC) | 236K | 532K | 28 x 150 | 2.0x |
| rust_tonic_mt (Rust, tonic) | 267K | 491K | 28 x 300 | 2.2x |
| java_armeria (Java, Armeria) | 242K | 485K | 28 x 112 | 2.2x |
| rust_volo_grpc (Rust, Volo) | 243K | 428K | 28 x 225 | 2.5x |
| java_hotspot_grpc_pgc (Java, grpc-java) | 206K | 399K | 28 x 338 | 2.7x |
| java_micronaut_workstealing (Java, Micronaut) | 213K | 387K | 28 x 150 | 2.7x |
| go_kitex_grpc (Go, Kitex) | 213K | 374K | 28 x 150 | 2.8x |
| go_vtgrpc (Go, grpc-go + vtprotobuf) | 201K | 357K | 28 x 450 | 3.0x |
| go_grpc (Go, grpc-go) | 197K | 340K | 28 x 300 | 3.1x |
| rust_grpcio (Rust, gRPC C core) | 144K | 269K | 28 x 150 | 3.9x |
| kotlin_grpc (Kotlin, grpc-kotlin) | 158K | 267K | 28 x 450 | 4.0x |
| swift_grpc (Swift, grpc-swift 1) | 149K | 243K | 28 x 150 | 4.3x |
| cpp_grpc_mt (C++) | 102K | 227K | 4 x 240 | 4.7x |
| cpp_asio_grpc (C++, asio-grpc) | 89K | 212K | 4 x 240 | 5.0x |
| scala_pekko (Scala, Pekko gRPC) | 197K | 197K | 50 x 20 | 5.4x |
| go_vtconnect (Go, ConnectRPC + vtprotobuf) | 57K | 109K | 28 x 150 | 9.7x |
| go_connect (Go, ConnectRPC) | 56K | 107K | 28 x 150 | 9.9x |
| java_micronaut (Java, Micronaut, default executor) | 45K | 68K | 28 x 150 | 16x |
| node_grpcjs_st (Node.js, single process) | 35K | 59K | 28 x 150 | 18x |
| swift_grpc2 (Swift, grpc-swift 2) | 16K | 16K | 50 x 20 | 66x |
| python_async_grpc (Python, asyncio, single process) | 4K | 4K | 50 x 20 | 257x |

- Tuning the load roughly doubles almost every result (1.5x to 2.8x).
- Every multi-threaded server is at 382-404% of its 4 CPUs at its tuned shape.
- The tuned figure is the best of two clients. Most servers do better with the Rust client; Akka,
  Kitex, grpcio, Swift, ConnectRPC, Micronaut and Node.js do better with `ghz`.
- Native AOT doesn't make grpc-dotnet faster here: the AOT build is 3-8% slower than the JIT build.
- Five servers stop working or start returning errors under high concurrency, so their tuned shape
  is a modest one: the two C++ servers and the Python one (gRPC's C core cancels calls once too
  many are pending), grpc-swift 2 (it allows 100 concurrent calls per connection) and Pekko (its JVM
  exits with a stack overflow).
- The fastest gRPC server on 4 CPUs handles about a million `SayHello` calls per second.

## Results: ActualLab.Rpc and Other .NET Libraries

The same machine and the same 4-CPU server container, no TLS, .NET 11.0.0, the client of
`RpcBenchmark` in a second container with 24 CPUs. Each library ran with fixed parameters: its best
known number of connections and of workers per connection, verified against one nearby set. A
worker sends a call, waits for the reply and sends the next one, so workers per connection is the
number of calls in flight on a connection. The best of 6 tries of 5 seconds is listed.

### Calls

| Library | Workers per connection x connections | Sum | GetUser | SayHello |
|---------|------------------------------------|-----|---------|----------|
| ActualLab.Rpc | 451 x 54 | **5.62M** | **4.36M** | **2.54M** |
| SignalR | 213 x 93 | 2.71M | 2.24M | 999K |
| gRPC (grpc-dotnet) | 82 x 30 | 626K | 603K | 567K |
| MagicOnion | 78 x 23 | 583K | 563K | 518K |
| StreamJsonRpc | 237 x 74 | 397K | 306K | 134K |
| HTTP | 100 x 36 | 146K | 147K | 130K |

`Sum` adds two integers, `GetUser` returns a small object, `SayHello` is the grpc_bench message.
ActualLab.Rpc uses the `msgpack6c` serialization format here.

### Streams

Items per second. A stream has 1,800 items; the name of the test is the item size in bytes.

| Library | Workers per connection x connections | Stream1 | Stream100 | Stream10K |
|---------|----------------------------------------|---------|-----------|-----------|
| ActualLab.Rpc | 5 x 54 | **40.19M** | **18.64M** | **492K** |
| gRPC (grpc-dotnet) | 15 x 38 | 15.40M | 9.42M | 329K |
| SignalR | 1 x 108 | 12.01M | 6.97M | 239K |
| StreamJsonRpc | 4 x 53 | 163K | 163K | 95K |

## The Combined Table

`SayHello`, 4-CPU server, no TLS: the five fastest gRPC servers and ActualLab.Rpc.

| Server | Client and shape (connections x calls each) | Calls/s |
|--------|---------------------------------------------|---------|
| **ActualLab.Rpc** | RpcBenchmark, 54 x 451 | **2.54M** |
| rust_ntex | Rust client, 20 x 600 | 1.06M |
| java_vertx_grpc | Rust client, 28 x 338 | 672K |
| dotnet_grpc | Rust client, 20 x 900 | 670K |
| java_quarkus | Rust client, 28 x 75 | 632K |
| dotnet_grpc_aot | Rust client, 20 x 600 | 613K |

<ClientOnly>
  <BarChart
    title="SayHello on a 4-CPU server, no TLS (Million calls/s)"
    :labels="['SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [2.54], backgroundColor: '#22c55e' },
      { label: 'rust_ntex', data: [1.06], backgroundColor: '#f97316' },
      { label: 'java_vertx_grpc', data: [0.67], backgroundColor: '#ef4444' },
      { label: 'dotnet_grpc', data: [0.67], backgroundColor: '#f59e0b' },
      { label: 'java_quarkus', data: [0.63], backgroundColor: '#a855f7' },
      { label: 'dotnet_grpc_aot', data: [0.61], backgroundColor: '#6b7280' }
    ]"
    :yMax="3"
    yLabel="M calls/s"
  />
</ClientOnly>

ActualLab.Rpc handles 2.4x more `SayHello` calls than the fastest gRPC server here and 3.8x more
than grpc-dotnet, the gRPC implementation for the same runtime.

This is the same call, the same message, the same machine and the same 4 CPUs for the server. The
load is not the same, though, and the difference favors the gRPC servers; see
[How the Two Clients Load a Server](#how-the-two-clients-load-a-server) below.

## Important Remarks

### How the Two Clients Load a Server

In both clients a worker sends a call and waits for the reply before sending the next one, so the
number of calls open on a connection is capped by the number of workers on it. The difference is in
how the calls are timed and grouped.

- **The Rust client lets the server shape the load.** It is a faster replica of `ghz`, the tool
  grpc_bench uses: the same model of a fixed number of connections and of concurrent calls, the
  same payload and the same report. A connection and all of its workers live on one thread.
  Replies read in one batch resume their workers together, and the next calls leave in one write.
  <u>The server keeps getting batches of the size it has just produced</u>, exactly when it is
  ready for them. The connection and call counts were also searched per server.
- **The `RpcBenchmark` client just runs workers.** N workers are spread over M connections as tasks
  on the shared thread pool. Each resumes on whichever thread is free and sends its next call at
  once, independently of the others. <u>The calls on a connection arrive as an irregular
  stream</u>, and the worker and connection counts were the ones I already knew, checked against
  one nearby set.

So `RpcBenchmark`'s load is closer to real life, where callers neither coordinate with each other
nor time their calls to suit the server.

The shaping is worth a lot. The same Rust client with one multi-threaded runtime instead of a
runtime per CPU gives `rust_ntex` 667K instead of 900K, and grpc-dotnet gets 567K from
`RpcBenchmark` against 670K from the tuned Rust client.

### The Effect of TLS

grpc_bench runs without TLS. `RpcBenchmark` always used TLS, so I added an option and made no TLS
the default (`-tls` turns it on).

<u>I measured the effect of TLS on .NET only</u>, that is, on Kestrel and the .NET libraries
running on it. I didn't run the other grpc_bench servers with TLS, so the numbers below say nothing
about Rust, Java or Go.

How much faster each test runs without TLS, with the same parameters:

| Test | ActualLab.Rpc | SignalR | gRPC (.NET) | MagicOnion |
|------|---------------|---------|-------------|------------|
| Sum | +10% | +9% | +17% | +18% |
| GetUser | +4% | +9% | +19% | +20% |
| SayHello | +3% | +7% | +20% | +17% |
| Stream1 (1-byte items) | +5% | +11% | +7% | - |
| Stream100 (100-byte items) | +27% | +16% | +19% | - |

- **TLS matters least for calls over WebSockets:** 3-10% for ActualLab.Rpc and SignalR.
- **<u>It matters most for gRPC calls on .NET</u>:** 17-20% for gRPC and MagicOnion, both of which
  run over HTTP/2.
- **For streams it grows with the item size,** because the cost is per byte: up to 27% with
  100-byte items.

So on .NET, a gRPC result measured without TLS is about 20% higher than the same test with TLS. All
the figures in this post are measured without it, for every library.

## Library and Runtime Versions

Every server runs on the newest stable release of its runtime and gRPC library as of 2026-11-16,
with the exceptions listed below the table. The last column is what grpc_bench pinned before.

| Server | Runtime | gRPC library | Before the upgrade |
|--------|---------|--------------|--------------------|
| rust_ntex | Rust 1.100 | ntex 4.9.1, ntex-grpc 4.1.1 | ntex 4.6.0 (2 weeks old) |
| rust_tonic_mt | Rust 1.100 | tonic 0.14.9, tokio 1.54.1 | tonic 0.14.5 (8 months old) |
| rust_volo_grpc (new) | Rust 1.100 | volo-grpc 0.12.2 | - |
| rust_grpcio | Rust 1.100 | grpcio 0.13.0 | the same; its last release is from 2023 |
| java_vertx_grpc | JDK 25 | Vert.x 5.2.3, grpc-java 1.85.1 | Vert.x 4.4.4 (3.4 years old) |
| java_quarkus | JDK 25 | Quarkus 3.40.1 | Quarkus 3.1.2 (3.4 years old) |
| java_armeria | JDK 25 | Armeria 1.43.0 | Armeria 1.20.3 (4 years old) |
| java_hotspot_grpc_pgc | JDK 25 | grpc-java 1.85.1 | grpc-java 1.58.0 (3.1 years old) |
| java_micronaut (both) | JDK 25 | Micronaut 5.2.6 | Micronaut 4.0.0 and 3.5.3 |
| kotlin_grpc | JDK 25 | grpc-kotlin 1.5.0, Kotlin 2.4.21 | grpc-kotlin 1.3.0, Kotlin 1.7.10 |
| scala_akka | JDK 25 | Akka 2.8.8, Akka gRPC 2.3.4 | Akka 2.8.2 (3.5 years old) |
| scala_pekko | JDK 25 | Pekko 1.7.1, Pekko gRPC 1.2.0 | Pekko 2.0 milestones |
| dotnet_grpc | .NET 11.0.0 | Grpc.AspNetCore 2.85.0, DATAS off | .NET 10, 2.76.0 (10 months old) |
| dotnet_grpc_aot (new) | .NET 11.0.0, Native AOT | Grpc.AspNetCore 2.85.0, DATAS off | - |
| go_grpc | Go 1.27.4 | grpc-go 1.85.0 | grpc-go 1.79.3 (7 months old) |
| go_vtgrpc | Go 1.27.4 | grpc-go 1.85.0, vtprotobuf 0.6.0 | grpc-go 1.63.2 (2.6 years old) |
| go_connect, go_vtconnect | Go 1.27.4 | connect-go 1.21.1 | connect-go 1.16.1 |
| go_kitex_grpc (new) | Go 1.27.4 | Kitex 0.16.4 | - |
| cpp_grpc_mt | GCC 16.2 | gRPC C++ 1.85.2 | gRPC C++ 1.51.1 (4 years old) |
| cpp_asio_grpc | GCC 16.2 | gRPC C++ 1.85.2, asio-grpc 3.7.0 | asio-grpc 1.0.0 |
| swift_grpc | Swift 6.4.2 | grpc-swift 1.27.6 | Swift 6.0.3 (1.9 years old) |
| swift_grpc2 (new) | Swift 6.4.2 | grpc-swift-2 2.4.4 | - |
| node_grpcjs_st | Node.js 26.13.1 | @grpc/grpc-js 1.14.7 | Node.js 20.3.1 (3.4 years old) |
| python_async_grpc | Python 3.15 | grpcio 1.85.1 | Python 3.13 |

Not on the very latest version: Akka is at 2.8.8, the newest release on Maven Central (later
releases need a license token for Akka's own repository); Pekko is on its stable 1.x line, because
Pekko gRPC has no stable 2.x release; the JVM servers run on JDK 25, the current LTS release.

The .NET side of the comparison runs on .NET 11.0.0 with ActualLab.Fusion 15.1.38,
Grpc.AspNetCore 2.85.0, SignalR 11.0.0, MagicOnion 7.11.0 and StreamJsonRpc 2.26.150, all of which
were the latest releases at the time. The load generators are `ghz` 0.114.0, the version grpc_bench
uses, and the Rust client on tonic 0.14.9.
