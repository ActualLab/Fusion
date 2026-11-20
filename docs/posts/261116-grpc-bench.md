---
description: 26 gRPC servers from grpc_bench in 10 languages, upgraded to current versions, vs the RPC libraries for .NET 11 (ActualLab.Rpc, SignalR, grpc-dotnet, MagicOnion, StreamJsonRpc). Same call, same 4 CPUs, plus a 5 Gbps LAN test.
image: /img/og/261116-grpc-bench.jpg
---

# The Fastest .NET RPC Libraries vs the Fastest gRPC Libraries

**Published:** 2026-11-16 by Alex Yakunin

<img src="/img/og/261116-grpc-bench.jpg" alt="26 gRPC servers in 10 languages and 6 .NET RPC libraries, tested with the same call on the same 4 CPUs" style="width: 100%; max-width: 900px; border-radius: 8px;" />

> **Disclosure:** I am the author of ActualLab.Rpc and of `RpcBenchmark`, so I am not a neutral party,
> and Claude helped write some of the conclusions. The code is open source, and I welcome you to read it,
> run it and check the results. What I did to keep the comparison fair is in
> [Disclosure and Fairness](#disclosure-and-fairness).

[grpc_bench](https://github.com/LesnyRumcajs/grpc_bench) is the best-known public benchmark of gRPC
server implementations: dozens of servers in about a dozen languages, each built from its own
Dockerfile and loaded with the same unary call. My own `RpcBenchmark` uses the same message and the same
4-CPU server setup, but until now I compared [ActualLab.Rpc](../PartR.md), the RPC library
Fusion is built on, only with other .NET libraries.

For this post I ran 26 gRPC servers from grpc_bench on the machine I use for my benchmarks and
measured how many calls each can handle on 4 CPUs. Then I ran the RPC libraries for .NET that
`RpcBenchmark` covers (ActualLab.Rpc, SignalR, grpc-dotnet, MagicOnion, StreamJsonRpc and a plain
HTTP API) in the same setup, so both groups can be put into one table. Finally, I repeated
the tests over a 5 Gbps LAN, with a 2-CPU server and the client on a separate PC. The raw data, including
every load shape I tried, is in
[Benchmarks-gRPC.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks-gRPC.md);
the ActualLab.Rpc side is in
[Benchmarks.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#docker-based-rpc-benchmarks)
(local) and
[Benchmarks.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#real-network-rpc-benchmarks)
(LAN).

## What Was Tested

- **The call:** grpc_bench's `complex_proto` scenario, a unary `SayHello` whose request and reply
  carry the same small structured message. `SayHello` in `RpcBenchmark` uses the same message.
- **The servers:** 26 servers written in Rust, Java, Kotlin, Scala, Go, .NET, C++, Swift, Node.js
  and Python: 22 that grpc_bench already has, <u>upgraded to the current versions of their runtimes
  and libraries</u>, and 4 new ones. The [results table](#grpc_bench-tests) lists all of them,
  and [Library and Runtime Versions](#library-and-runtime-versions) lists what they run on.
- **The setups:** in the [local setup](#local-tests-4-core-server), the server runs in a Docker
  container limited to 4 CPUs, as in grpc_bench. In the [LAN setup](#5-gbps-lan-2-core-server), the
  client is a separate PC and the server is limited to 2 CPUs: with more, the fastest libraries would
  saturate the 5 Gbps link, and the test would measure the link instead of the library.

**Just want the results?** Jump to [Combined Tables](#combined-tables).

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
[`dev` branch](https://github.com/ActualLab/grpc_bench/tree/dev) of a fork
of grpc_bench, which I'm proposing upstream as a series of pull requests.

The local tests run on one machine, so the calls never leave the Docker network. The
[5 Gbps LAN, 2-Core Server](#5-gbps-lan-2-core-server) section repeats the tests over a LAN, with the
client on a separate machine.

## Local Tests, 4-Core Server

**Server machine:**

| Component | Specification |
|-----------|---------------|
| **CPU** | AMD Ryzen 9 9950X3D (16 cores / 32 logical CPUs) |
| **RAM** | 96 GB DDR5 |
| **OS** | Windows 11, Docker Desktop with the WSL2 backend (32 CPUs, 25 GB) |
| **Server container** | `--cpus 4 --memory 8g`, on a Docker network on the same machine (no published ports) |
| **Clients** | Containers on the same machine, using the 28 logical CPUs the server doesn't use |

### grpc_bench tests

`SayHello`, 4-CPU server, no TLS, successful calls per second. Each server was measured under two loads:

- **Original load:** the one grpc_bench uses: one `ghz` process with 50 connections and 1,000 calls in
  flight (20 per connection). `ghz` had 28 CPUs here, so it wasn't the limit.
- **Optimal load:** the highest error-free result I got for that server. I varied the client (28
  `ghz` processes, or the Rust client), the number of connections, and the number of calls in flight
  per connection. The numbers in parentheses show the winning shape.
- **The factor after a result** is how many times slower that result is than the fastest one in its
  column: <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span> is the fastest, <span style="background: rgb(163,116,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span> is half as fast, <span style="background: rgb(137,39,39); color: #fff; padding: 2px 6px; border-radius: 3px;">↓10×</span> is ten times slower. The color goes from
  green (1x) through red (3x) to black (50x or more) on a log scale. The tables below use the same
  notation.

| Server | Original load, calls/s | Optimal load, calls/s (connections x calls-in-flight) |
|--------|--------------------------------|---------------------------|
| rust_ntex (Rust, ntex) | <span style="white-space: nowrap;">384K <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**1,057K** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span> (20 x 600)</span> |
| java_vertx_grpc (Java, Vert.x) | <span style="white-space: nowrap;">350K <span style="background: rgb(51,186,92); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.1×</span></span> | <span style="white-space: nowrap;">672K <span style="background: rgb(122,142,83); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.6×</span> (28 x 338)</span> |
| dotnet_grpc (.NET, grpc-dotnet) | <span style="white-space: nowrap;">281K <span style="background: rgb(92,160,87); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.4×</span></span> | <span style="white-space: nowrap;">670K <span style="background: rgb(122,142,83); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.6×</span> (20 x 900)</span> |
| java_quarkus (Java, Quarkus) | <span style="white-space: nowrap;">320K <span style="background: rgb(68,176,90); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.2×</span></span> | <span style="white-space: nowrap;">632K <span style="background: rgb(133,135,81); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.7×</span> (28 x 75)</span> |
| dotnet_grpc_aot (.NET, grpc-dotnet, Native AOT) | <span style="white-space: nowrap;">271K <span style="background: rgb(99,156,86); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.4×</span></span> | <span style="white-space: nowrap;">613K <span style="background: rgb(133,135,81); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.7×</span> (20 x 600)</span> |
| scala_akka (Scala, Akka gRPC) | <span style="white-space: nowrap;">236K <span style="background: rgb(125,140,82); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.6×</span></span> | <span style="white-space: nowrap;">532K <span style="background: rgb(163,116,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span> (28 x 150)</span> |
| rust_tonic_mt (Rust, tonic) | <span style="white-space: nowrap;">267K <span style="background: rgb(102,154,85); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.4×</span></span> | <span style="white-space: nowrap;">491K <span style="background: rgb(181,104,75); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.2×</span> (28 x 300)</span> |
| java_armeria (Java, Armeria) | <span style="white-space: nowrap;">242K <span style="background: rgb(120,143,83); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.6×</span></span> | <span style="white-space: nowrap;">485K <span style="background: rgb(181,104,75); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.2×</span> (28 x 112)</span> |
| rust_volo_grpc (Rust, Volo) | <span style="white-space: nowrap;">243K <span style="background: rgb(119,143,83); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.6×</span></span> | <span style="white-space: nowrap;">428K <span style="background: rgb(205,89,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.5×</span> (28 x 225)</span> |
| java_hotspot_grpc_pgc (Java, grpc-java) | <span style="white-space: nowrap;">206K <span style="background: rgb(150,124,79); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">399K <span style="background: rgb(219,80,70); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span> (28 x 338)</span> |
| java_micronaut_workstealing (Java, Micronaut) | <span style="white-space: nowrap;">213K <span style="background: rgb(144,128,80); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.8×</span></span> | <span style="white-space: nowrap;">387K <span style="background: rgb(219,80,70); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span> (28 x 150)</span> |
| go_kitex_grpc (Go, Kitex) | <span style="white-space: nowrap;">213K <span style="background: rgb(144,128,80); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.8×</span></span> | <span style="white-space: nowrap;">374K <span style="background: rgb(226,76,70); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.8×</span> (28 x 150)</span> |
| go_vtgrpc (Go, grpc-go + vtprotobuf) | <span style="white-space: nowrap;">201K <span style="background: rgb(155,121,79); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">357K <span style="background: rgb(239,68,68); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.0×</span> (28 x 450)</span> |
| go_grpc (Go, grpc-go) | <span style="white-space: nowrap;">197K <span style="background: rgb(159,119,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">340K <span style="background: rgb(236,67,67); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.1×</span> (28 x 300)</span> |
| rust_grpcio (Rust, gRPC C core) | <span style="white-space: nowrap;">144K <span style="background: rgb(217,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> | <span style="white-space: nowrap;">269K <span style="background: rgb(217,62,62); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.9×</span> (28 x 150)</span> |
| kotlin_grpc (Kotlin, grpc-kotlin) | <span style="white-space: nowrap;">158K <span style="background: rgb(200,93,73); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.4×</span></span> | <span style="white-space: nowrap;">267K <span style="background: rgb(215,61,61); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.0×</span> (28 x 450)</span> |
| swift_grpc (Swift, grpc-swift 1) | <span style="white-space: nowrap;">149K <span style="background: rgb(211,86,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> | <span style="white-space: nowrap;">243K <span style="background: rgb(208,59,59); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.3×</span> (28 x 150)</span> |
| cpp_grpc_mt (C++) | <span style="white-space: nowrap;">102K <span style="background: rgb(220,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> | <span style="white-space: nowrap;">227K <span style="background: rgb(201,57,57); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.7×</span> (4 x 240)</span> |
| cpp_asio_grpc (C++, asio-grpc) | <span style="white-space: nowrap;">89K <span style="background: rgb(208,59,59); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.3×</span></span> | <span style="white-space: nowrap;">212K <span style="background: rgb(196,56,56); color: #fff; padding: 2px 6px; border-radius: 3px;">↓5.0×</span> (4 x 240)</span> |
| scala_pekko (Scala, Pekko gRPC) | <span style="white-space: nowrap;">197K <span style="background: rgb(159,119,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">197K <span style="background: rgb(189,54,54); color: #fff; padding: 2px 6px; border-radius: 3px;">↓5.4×</span> (50 x 20)</span> |
| go_vtconnect (Go, ConnectRPC + vtprotobuf) | <span style="white-space: nowrap;">57K <span style="background: rgb(170,48,48); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.7×</span></span> | <span style="white-space: nowrap;">109K <span style="background: rgb(139,40,40); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.7×</span> (28 x 150)</span> |
| go_connect (Go, ConnectRPC) | <span style="white-space: nowrap;">56K <span style="background: rgb(169,48,48); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.9×</span></span> | <span style="white-space: nowrap;">107K <span style="background: rgb(138,39,39); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.9×</span> (28 x 150)</span> |
| java_micronaut (Java, Micronaut, default executor) | <span style="white-space: nowrap;">45K <span style="background: rgb(150,43,43); color: #fff; padding: 2px 6px; border-radius: 3px;">↓8.5×</span></span> | <span style="white-space: nowrap;">68K <span style="background: rgb(97,28,28); color: #fff; padding: 2px 6px; border-radius: 3px;">↓16×</span> (28 x 150)</span> |
| node_grpcjs_st (Node.js, single process) | <span style="white-space: nowrap;">35K <span style="background: rgb(129,37,37); color: #fff; padding: 2px 6px; border-radius: 3px;">↓11×</span></span> | <span style="white-space: nowrap;">59K <span style="background: rgb(87,25,25); color: #fff; padding: 2px 6px; border-radius: 3px;">↓18×</span> (28 x 150)</span> |
| swift_grpc2 (Swift, grpc-swift 2) | <span style="white-space: nowrap;">16K <span style="background: rgb(62,18,18); color: #fff; padding: 2px 6px; border-radius: 3px;">↓24×</span></span> | <span style="white-space: nowrap;">16K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓66×</span> (50 x 20)</span> |
| python_async_grpc (Python, asyncio, single process) | <span style="white-space: nowrap;">4K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓96×</span></span> | <span style="white-space: nowrap;">4K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓257×</span> (50 x 20)</span> |

- Choosing the load well roughly doubles almost every result (1.5x to 2.8x).
- Every multi-threaded server is at 382-404% of its 4 CPUs under its optimal load.
- The optimal load is the better of two clients. Most servers do better with the Rust client; Akka,
  Kitex, grpcio, Swift, ConnectRPC, Micronaut and Node.js do better with `ghz`.
- Native AOT doesn't make grpc-dotnet faster here: the AOT build is 3-8% slower than the JIT build.
- Five servers stop working or start returning errors under high concurrency, so their optimal load
  is a modest one: the two C++ servers and the Python one (gRPC's C core cancels calls once too
  many are pending), grpc-swift 2 (it allows 100 concurrent calls per connection) and Pekko (its JVM
  exits with a stack overflow).
- The fastest gRPC server on 4 CPUs handles about a million `SayHello` calls per second.

### RpcBenchmark tests

The same machine and the same 4-CPU server container, no TLS, .NET 11.0.0, the client of
`RpcBenchmark` in a second container with 24 CPUs. Each library ran with fixed parameters: its best
known number of connections and of workers per connection, verified against one nearby set. A
worker sends a call, waits for the reply and sends the next one, so workers per connection is the
number of calls in flight on a connection. The best of 6 tries of 5 seconds is listed.

#### Calls

| Library | Workers per connection x connections | Sum, calls/s | GetUser, calls/s | SayHello, calls/s |
|---------|--------------------------------------|----------------------------------------------------------|--------------------------------------------------------------|---------------------------------------------------------------|
| ActualLab.Rpc | 451 x 54 | <span style="white-space: nowrap;">**5.62M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**4.36M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**2.54M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | 213 x 93 | <span style="white-space: nowrap;">2.71M <span style="background: rgb(170,111,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> | <span style="white-space: nowrap;">2.24M <span style="background: rgb(158,119,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.9×</span></span> | <span style="white-space: nowrap;">999K <span style="background: rgb(208,87,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.5×</span></span> |
| gRPC (grpc-dotnet) | 82 x 30 | <span style="white-space: nowrap;">626K <span style="background: rgb(146,42,42); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.0×</span></span> | <span style="white-space: nowrap;">603K <span style="background: rgb(164,47,47); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.2×</span></span> | <span style="white-space: nowrap;">567K <span style="background: rgb(205,58,58); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.5×</span></span> |
| MagicOnion | 78 x 23 | <span style="white-space: nowrap;">583K <span style="background: rgb(140,40,40); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.6×</span></span> | <span style="white-space: nowrap;">563K <span style="background: rgb(158,45,45); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.7×</span></span> | <span style="white-space: nowrap;">518K <span style="background: rgb(197,56,56); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.9×</span></span> |
| StreamJsonRpc | 237 x 74 | <span style="white-space: nowrap;">397K <span style="background: rgb(107,30,30); color: #fff; padding: 2px 6px; border-radius: 3px;">↓14×</span></span> | <span style="white-space: nowrap;">306K <span style="background: rgb(107,30,30); color: #fff; padding: 2px 6px; border-radius: 3px;">↓14×</span></span> | <span style="white-space: nowrap;">134K <span style="background: rgb(82,23,23); color: #fff; padding: 2px 6px; border-radius: 3px;">↓19×</span></span> |
| HTTP | 100 x 36 | <span style="white-space: nowrap;">146K <span style="background: rgb(22,6,6); color: #fff; padding: 2px 6px; border-radius: 3px;">↓38×</span></span> | <span style="white-space: nowrap;">147K <span style="background: rgb(44,13,13); color: #fff; padding: 2px 6px; border-radius: 3px;">↓30×</span></span> | <span style="white-space: nowrap;">130K <span style="background: rgb(80,23,23); color: #fff; padding: 2px 6px; border-radius: 3px;">↓20×</span></span> |

`Sum` adds two integers, `GetUser` returns a small object, `SayHello` is the grpc_bench message.
ActualLab.Rpc uses the `msgpack6c` serialization format here.

#### Streams

Items per second. A stream has 1,800 items; the name of the test is the item size in bytes.

| Library | Workers per connection x connections | Stream1, items/s | Stream100, items/s | Stream10K, items/s |
|---------|--------------------------------------|--------------------------------------------------------------|----------------------------------------------------------------|----------------------------------------------------------------|
| ActualLab.Rpc | 5 x 54 | <span style="white-space: nowrap;">**40.19M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**18.64M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**492K** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| gRPC (grpc-dotnet) | 15 x 38 | <span style="white-space: nowrap;">15.40M <span style="background: rgb(213,84,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> | <span style="white-space: nowrap;">9.42M <span style="background: rgb(161,117,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span></span> | <span style="white-space: nowrap;">329K <span style="background: rgb(109,150,84); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.5×</span></span> |
| SignalR | 1 x 108 | <span style="white-space: nowrap;">12.01M <span style="background: rgb(230,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.3×</span></span> | <span style="white-space: nowrap;">6.97M <span style="background: rgb(218,81,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> | <span style="white-space: nowrap;">239K <span style="background: rgb(169,112,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> |
| StreamJsonRpc | 4 x 53 | <span style="white-space: nowrap;">163K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓247×</span></span> | <span style="white-space: nowrap;">163K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓114×</span></span> | <span style="white-space: nowrap;">95K <span style="background: rgb(193,55,55); color: #fff; padding: 2px 6px; border-radius: 3px;">↓5.2×</span></span> |

## 5 Gbps LAN, 2-Core Server

**Server machine:**

| Component | Specification |
|-----------|---------------|
| **Host** | AMD Ryzen 7 8745HS (8 cores / 16 logical CPUs), 30 GB RAM, one NVMe SSD, Proxmox VE 9.0.3 (Debian 13, kernel 6.14.8-2-pve) |
| **VM** | Ubuntu 24.04.5 (kernel 6.8.0-149), 16 vCPUs, 26 GB RAM, the only VM running on the host |
| **Server container** | `--cpus 2 --memory 8g` (Docker 29.10.0), published ports |
| **Network** | Realtek RTL8157 USB 5GbE adapter, Linux bridge, virtio NIC with 8 queues |
| **Client** | A separate PC: AMD Ryzen AI MAX+ PRO 395 (16 cores / 32 logical CPUs), 96 GB RAM, Windows 11 Pro, Realtek USB 5GbE adapter |

The same tests, with the client on a separate PC and a 5 Gbit/s LAN between the two machines
(`iperf3` measures 4.74 Gbit/s). The server is a Docker container limited to **2 CPUs**, in a VM on a
Proxmox host, and the client PC has 32 logical CPUs. There is no TLS, and the figures were measured on
2026-11-17. The machines, the network path and the commands are in
[Benchmarks.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#real-network-rpc-benchmarks).

<img src="/img/diagrams/grpc-bench-lan.svg" alt="The LAN setup: client PC, switch, Proxmox host, VM, server container" style="width: 100%; max-width: 900px;" />

These figures are not comparable with the ones above: the server has 2 CPUs instead of 4, its CPU is
slower, and the client is another machine.

**Why 2 CPUs.** The test should measure the libraries, not the network. With 2 CPUs the server is the
limit in every call test and in Stream1, but ActualLab.Rpc already reaches 92% of the link in Stream100,
and Stream10K saturates it at any server speed. Twice the CPUs would move the limit from the server to
the link in more of the stream tests for the fastest libraries, and their results would converge on
the link speed.

### grpc_bench tests

`SayHello`, 2-CPU server, no TLS: 7 gRPC servers, one per language, loaded by the Rust client. Each is
the fastest server of its language in the 4-CPU tests with that client. Each figure is a single 15-second run.

| Server | Client and shape (connections x calls each) | Calls/s |
|---|---|---|
| rust_ntex | Rust client, 28 x 450 | <span style="white-space: nowrap;">**401K** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| java_vertx_grpc | Rust client, 28 x 150 | <span style="white-space: nowrap;">319K <span style="background: rgb(77,170,89); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.3×</span></span> |
| dotnet_grpc | Rust client, 20 x 450 | <span style="white-space: nowrap;">285K <span style="background: rgb(98,157,86); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.4×</span></span> |
| go_vtgrpc | Rust client, 14 x 450 | <span style="white-space: nowrap;">179K <span style="background: rgb(184,103,75); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.2×</span></span> |
| scala_akka | Rust client, 14 x 225 | <span style="white-space: nowrap;">154K <span style="background: rgb(213,85,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| cpp_grpc_mt | Rust client, 2 x 240 | <span style="white-space: nowrap;">151K <span style="background: rgb(216,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> |
| swift_grpc | Rust client, 14 x 600 | <span style="white-space: nowrap;">92K <span style="background: rgb(208,59,59); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.3×</span></span> |

The servers and the shapes of the load are in
[Benchmarks-gRPC.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks-gRPC.md#real-network).

### RpcBenchmark tests

#### Calls

| Library | Workers per connection x connections | Sum, calls/s | GetUser, calls/s | SayHello, calls/s |
|---|---|---|---|---|
| ActualLab.Rpc | 563 x 35 | <span style="white-space: nowrap;">**2.22M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**1.71M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**1.07M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | 213 x 70 | <span style="white-space: nowrap;">1.06M <span style="background: rgb(172,110,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> | <span style="white-space: nowrap;">765K <span style="background: rgb(184,103,75); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.2×</span></span> | <span style="white-space: nowrap;">416K <span style="background: rgb(210,86,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| gRPC (grpc-dotnet) | 84 x 16 | <span style="white-space: nowrap;">364K <span style="background: rgb(179,51,51); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.1×</span></span> | <span style="white-space: nowrap;">356K <span style="background: rgb(199,57,57); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.8×</span></span> | <span style="white-space: nowrap;">314K <span style="background: rgb(228,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| HTTP | 66 x 25 | <span style="white-space: nowrap;">98K <span style="background: rgb(68,19,19); color: #fff; padding: 2px 6px; border-radius: 3px;">↓23×</span></span> | <span style="white-space: nowrap;">104K <span style="background: rgb(94,27,27); color: #fff; padding: 2px 6px; border-radius: 3px;">↓16×</span></span> | <span style="white-space: nowrap;">82K <span style="background: rgb(114,32,32); color: #fff; padding: 2px 6px; border-radius: 3px;">↓13×</span></span> |

Every call test is limited by the server's 2 CPUs, and none of them uses more than a quarter of the link.

#### Streams

Items per second. Stream10K wasn't run, because it saturates the 5 Gbit/s NICs at any server speed.

| Library | Workers per connection x connections | Stream1, items/s | Stream100, items/s |
|---|---|---|---|
| ActualLab.Rpc | 10 x 39 | <span style="white-space: nowrap;">**18.51M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">**5.45M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| gRPC (grpc-dotnet) | 15 x 8 | <span style="white-space: nowrap;">7.79M <span style="background: rgb(195,95,74); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.4×</span></span> | <span style="white-space: nowrap;">4.54M <span style="background: rgb(68,176,90); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.2×</span></span> |
| SignalR | 3 x 11 | <span style="white-space: nowrap;">4.98M <span style="background: rgb(221,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.7×</span></span> | <span style="white-space: nowrap;">2.63M <span style="background: rgb(170,111,77); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.1×</span></span> |

ActualLab.Rpc's Stream100 reaches 92% of the link's ceiling of 5.93M items/s (4.74 Gbit/s with no
framing at all), so it has CPU to spare. gRPC runs out of CPU at 4.54M items/s.

## Combined Tables

The fastest gRPC server in each language, the Native AOT build of grpc-dotnet, ActualLab.Rpc and SignalR,
running the same `SayHello` call, without TLS. The first table is the local test, the second one is the LAN test.

### Local Tests, 4-Core Server

`SayHello`, 4-CPU server in Docker.

| Server | Language | Client and shape (connections x calls each) | Calls/s |
|--------|----------|---------------------------------------------|-----------------------------------------------------|
| **ActualLab.Rpc** | .NET | RpcBenchmark, 54 x 451 | <span style="white-space: nowrap;">**2.54M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| rust_ntex | Rust | Rust client, 20 x 600 | <span style="white-space: nowrap;">1.06M <span style="background: rgb(197,94,73); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.4×</span></span> |
| SignalR | .NET | RpcBenchmark, 93 x 213 | <span style="white-space: nowrap;">999K <span style="background: rgb(208,87,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.5×</span></span> |
| java_vertx_grpc | Java | Rust client, 28 x 338 | <span style="white-space: nowrap;">672K <span style="background: rgb(219,62,62); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| dotnet_grpc | .NET | Rust client, 20 x 900 | <span style="white-space: nowrap;">670K <span style="background: rgb(219,62,62); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| dotnet_grpc_aot | .NET (Native AOT) | Rust client, 20 x 600 | <span style="white-space: nowrap;">613K <span style="background: rgb(212,60,60); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.1×</span></span> |
| gRPC (grpc-dotnet) | .NET | RpcBenchmark, 30 x 82 | <span style="white-space: nowrap;">567K <span style="background: rgb(205,58,58); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.5×</span></span> |
| scala_akka | Scala | ghz, 28 x 150 | <span style="white-space: nowrap;">532K <span style="background: rgb(199,57,57); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.8×</span></span> |
| go_kitex_grpc | Go | ghz, 28 x 150 | <span style="white-space: nowrap;">374K <span style="background: rgb(170,48,48); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.8×</span></span> |
| kotlin_grpc | Kotlin | Rust client, 28 x 450 | <span style="white-space: nowrap;">267K <span style="background: rgb(141,40,40); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.5×</span></span> |
| swift_grpc | Swift | ghz, 28 x 150 | <span style="white-space: nowrap;">244K <span style="background: rgb(133,38,38); color: #fff; padding: 2px 6px; border-radius: 3px;">↓10×</span></span> |
| cpp_grpc_mt | C++ | Rust client, 4 x 240 | <span style="white-space: nowrap;">227K <span style="background: rgb(127,36,36); color: #fff; padding: 2px 6px; border-radius: 3px;">↓11×</span></span> |
| node_grpcjs_st | Node.js | ghz, 28 x 150 | <span style="white-space: nowrap;">59K <span style="background: rgb(13,4,4); color: #fff; padding: 2px 6px; border-radius: 3px;">↓43×</span></span> |
| python_async_grpc | Python | ghz, 50 x 20 | <span style="white-space: nowrap;">4K <span style="background: rgb(0,0,0); color: #fff; padding: 2px 6px; border-radius: 3px;">↓620×</span></span> |

<ClientOnly>
  <BarChart
    title="SayHello on a 4-CPU server, no TLS (Million calls/s)"
    :labels="['SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [2.54], backgroundColor: '#22c55e' },
      { label: 'rust_ntex', data: [1.06], backgroundColor: '#f97316' },
      { label: 'SignalR', data: [1.00], backgroundColor: '#3b82f6' },
      { label: 'java_vertx_grpc', data: [0.67], backgroundColor: '#ef4444' },
      { label: 'dotnet_grpc', data: [0.67], backgroundColor: '#f59e0b' },
      { label: 'dotnet_grpc_aot', data: [0.61], backgroundColor: '#6b7280' },
      { label: 'gRPC (grpc-dotnet)', data: [0.57], backgroundColor: '#a855f7' },
      { label: 'scala_akka', data: [0.53], backgroundColor: '#14b8a6' },
      { label: 'go_kitex_grpc', data: [0.37], backgroundColor: '#06b6d4' },
      { label: 'kotlin_grpc', data: [0.27], backgroundColor: '#8b5cf6' },
      { label: 'swift_grpc', data: [0.24], backgroundColor: '#f43f5e' },
      { label: 'cpp_grpc_mt', data: [0.23], backgroundColor: '#0ea5e9' },
      { label: 'node_grpcjs_st', data: [0.06], backgroundColor: '#84cc16' },
      { label: 'python_async_grpc', data: [0.004], backgroundColor: '#78716c' }
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

### 5 Gbps LAN, 2-Core Server

`SayHello`, 2-CPU server in Docker in a VM, client on a separate PC.

| Server | Language | Client and shape (connections x calls each) | Calls/s |
|--------|----------|---|---|
| **ActualLab.Rpc** | .NET | RpcBenchmark, 35 x 563 | <span style="white-space: nowrap;">**1.07M** <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| SignalR | .NET | RpcBenchmark, 70 x 213 | <span style="white-space: nowrap;">416K <span style="background: rgb(210,86,72); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| rust_ntex | Rust | Rust client, 28 x 450 | <span style="white-space: nowrap;">401K <span style="background: rgb(217,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> |
| java_vertx_grpc | Java | Rust client, 28 x 150 | <span style="white-space: nowrap;">319K <span style="background: rgb(229,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| gRPC (grpc-dotnet) | .NET | RpcBenchmark, 16 x 84 | <span style="white-space: nowrap;">314K <span style="background: rgb(228,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| dotnet_grpc | .NET | Rust client, 20 x 450 | <span style="white-space: nowrap;">285K <span style="background: rgb(220,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| go_vtgrpc | Go | Rust client, 14 x 450 | <span style="white-space: nowrap;">179K <span style="background: rgb(181,51,51); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.0×</span></span> |
| scala_akka | Scala | Rust client, 14 x 225 | <span style="white-space: nowrap;">154K <span style="background: rgb(168,48,48); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.0×</span></span> |
| cpp_grpc_mt | C++ | Rust client, 2 x 240 | <span style="white-space: nowrap;">151K <span style="background: rgb(166,47,47); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.1×</span></span> |
| swift_grpc | Swift | Rust client, 14 x 600 | <span style="white-space: nowrap;">92K <span style="background: rgb(124,35,35); color: #fff; padding: 2px 6px; border-radius: 3px;">↓12×</span></span> |

<ClientOnly>
  <BarChart
    title="SayHello on a 2-CPU server over a 5 Gbps LAN, no TLS (Million calls/s)"
    :labels="['SayHello']"
    :datasets="[
      { label: 'ActualLab.Rpc', data: [1.07], backgroundColor: '#22c55e' },
      { label: 'SignalR', data: [0.42], backgroundColor: '#3b82f6' },
      { label: 'rust_ntex', data: [0.40], backgroundColor: '#f97316' },
      { label: 'java_vertx_grpc', data: [0.32], backgroundColor: '#ef4444' },
      { label: 'gRPC (grpc-dotnet)', data: [0.31], backgroundColor: '#a855f7' },
      { label: 'dotnet_grpc', data: [0.28], backgroundColor: '#f59e0b' },
      { label: 'go_vtgrpc', data: [0.18], backgroundColor: '#06b6d4' },
      { label: 'scala_akka', data: [0.15], backgroundColor: '#14b8a6' },
      { label: 'cpp_grpc_mt', data: [0.15], backgroundColor: '#0ea5e9' },
      { label: 'swift_grpc', data: [0.09], backgroundColor: '#f43f5e' }
    ]"
    :yMax="1.2"
    yLabel="M calls/s"
  />
</ClientOnly>

ActualLab.Rpc handles 2.7x more `SayHello` calls than the fastest gRPC server here and 3.4x more than
grpc-dotnet. The gRPC servers' figures are single 15-second runs, and the `RpcBenchmark` figures are the best of six
5-second tries, which favors the `RpcBenchmark` rows.

## Conclusions

### ActualLab.Rpc Is in a League of Its Own

Even with the [disclaimer](#disclosure-and-fairness) in mind, the gap is hard to overstate.

- **Calls:** it is 2.7x faster over the LAN than `rust_ntex`, the fastest gRPC server.
- **Per core:** that's about 635K calls/s <u>per core, a number nearly every other server here can't
  reach even with 4 cores</u>. And only `rust_ntex` and SignalR need "just" 3 cores to produce the
  same number!
- **Streams:** it processes <u>more than 10M one-byte items per second per core</u> (40.2M/s on
  4 CPUs). It's almost the same over the LAN: 9.3M per core, on a slightly slower CPU.
- **And that's very good news for all other library authors:** if a library written almost
  single-handedly beats every RPC stack in every language here by such a margin, all of them have huge
  room to improve. And many of them eventually will.

### On .NET, SignalR Is the Next Choice

- Over the LAN it is slightly ahead of `rust_ntex` (416K vs 401K calls/s); locally it is 6% behind
  (999K vs 1.06M).
- It is 1.3-1.8x faster than grpc-dotnet.
- On .NET 11 it needs one fix to get there, which `RpcBenchmark` applies; see
  [The .NET 11 Pipe Scheduler Fix](#the-net-11-pipe-scheduler-fix).

### Among gRPC Servers, `rust_ntex` Is the Clear Winner

- Locally it is 1.6x faster than the next ones, Vert.x and grpc-dotnet, and it is the fastest gRPC
  server over the LAN as well.
- That's ntex, not Rust in general: tonic, the most popular Rust gRPC library, is slower than grpc-dotnet.
- grpc-dotnet is 3rd of 26, but SignalR beats it on the same runtime and the same Kestrel. So I suspect
  most of its overhead is in its own HTTP/2 and gRPC layers rather than in .NET, and that it could be a
  lot faster.

### Everything Else Is Far Behind

- **Go:** the fastest Go server (Kitex, 374K) handles about half of what grpc-dotnet does and a third
  of what `rust_ntex` does.
- **C++:** the servers on gRPC's C core are surprisingly slow (227K).
- **Swift, Node.js, Python:** 243K (only 16K with grpc-swift 2), 59K and 4K, i.e. 4-260x slower than
  `rust_ntex`. If your server's RPC throughput matters, avoid them there.

## Important Remarks

### Disclosure and Fairness

I wrote ActualLab.Rpc and `RpcBenchmark`, and some of the conclusions in this post were written by
Claude. You don't have to take my word for the results:

- **The code is open source.** `RpcBenchmark` is in the
  [Fusion.Samples repository](https://github.com/ActualLab/Fusion.Samples/tree/master/src/RpcBenchmark),
  ActualLab.Rpc is in the [Fusion repository](https://github.com/ActualLab/Fusion), and the grpc_bench
  servers and the Rust client are in the [`dev` branch](https://github.com/ActualLab/grpc_bench/tree/dev)
  of the grpc_bench fork.
- **`RpcBenchmark` isn't tuned for ActualLab.Rpc.** Every library gets the same load: workers that send a
  call, wait for the reply and send the next one. Each library runs with its own best known number of
  connections and workers, found the same way.
- **Compared to grpc_bench, the load from `RpcBenchmark` is not in ActualLab.Rpc's favor, nor in favor
  of any other .NET library it tests.** It's the opposite. The next section explains why.

### How the Two Clients Load a Server

In both clients a worker sends a call and waits for the reply before sending the next one. A new call
is queued on a connection only when a reply to one of the queued calls arrives, so the number of calls
open on a connection is capped by the number of workers on it. The difference is in <u>where the
workers run</u>, and that shapes the load.

- **The Rust client keeps a connection and all of its workers on one thread.** It is a faster replica
  of `ghz`, the tool grpc_bench uses: the same model of a fixed number of connections and of
  concurrent calls, the same payload and the same report. Each client thread runs a single-threaded
  runtime with its own connections, and the workers are tasks on it. The client doesn't batch anything
  itself: when the HTTP/2 stack reads several replies from the socket at once, their workers resume one
  after another on the same thread, and each queues its next call. <u>The next calls go out right
  after the replies came in</u>, and there is no cross-thread synchronization, because no other thread
  touches the connection. The client threads share nothing
  except the start barrier, so the load from each thread is independent of the others and more
  uniform, as long as the machine has enough cores to run them in parallel. It does here: the client
  never starts more threads than CPUs, and it had 28 CPUs locally and 16 on the LAN. The connection and call counts were also searched per server.
- **The `RpcBenchmark` client spreads the workers over the thread pool.** N workers are spread over M
  connections as tasks on the shared thread pool. Each reply resumes its own worker individually, on
  whichever thread is free, and the worker sends its next call at once, independently of the others.
  <u>The calls reach a connection from different threads</u>, so the client library has to synchronize
  them and decide how to batch them. Batches still form, but they are less uniform, because the timing
  of the threads is stochastic, and nobody times them to suit the server. The worker and connection
  counts were the ones I already knew, checked against one nearby set.

So `RpcBenchmark`'s load is closer to real life, where callers neither coordinate with each other
nor time their calls to suit the server.

The shaping is worth a lot. The same Rust client with one multi-threaded runtime instead of a
runtime per CPU gives `rust_ntex` 667K instead of 900K, and grpc-dotnet gets 567K from
`RpcBenchmark` against 670K from the Rust client with the optimal load.

### The .NET 11 Pipe Scheduler Fix

.NET 11 changed `PipeScheduler.ThreadPool` (System.IO.Pipelines) to queue its work items with
`preferLocal: true`. Under load, a pipe reader such as SignalR's send loop now runs almost
immediately and flushes tiny batches, so the number of socket sends per call grows several times,
and <u>SignalR's call throughput drops several times with it</u>.

`RpcBenchmark` works around this: at startup, both its server and its client processes replace the
scheduler behind `PipeScheduler.ThreadPool` with one that queues to the global thread pool queue, as
.NET 10 did. The fix is process-wide, so it applies to every .NET library in the test, not only to
SignalR, and the `ActualLab_PipeSchedulerFix=0` environment variable turns it off. Without it,
SignalR's numbers in this post would be much lower. The details are in
[Benchmarks.md](https://github.com/ActualLab/Fusion.Samples/blob/master/Benchmarks.md#net-11-pipe-scheduler-fix),
and the code is
[PipeSchedulerFix.cs](https://github.com/ActualLab/Fusion.Samples/blob/master/src/ActualLab.Benchmarking/PipeSchedulerFix.cs).

### The Effect of TLS

`RpcBenchmark` used to run with TLS. I only noticed that grpc_bench runs without it while preparing this
post. I then turned TLS off by default in `RpcBenchmark` (`-tls` turns it on), because the goal of all
these benchmarks is to show the overhead the RPC library itself adds. TLS adds a fixed overhead of its
own, which makes the differences between libraries less visible.

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
with the exceptions listed below the table.

| Runtime | Server | Library |
|---------|--------|---------|
| .NET 11.0.0 | ActualLab.Rpc (RpcBenchmark) | ActualLab.Fusion 15.1.38 |
| .NET 11.0.0 | HTTP (RpcBenchmark) | ASP.NET Core |
| .NET 11.0.0 | dotnet_grpc | Grpc.AspNetCore 2.85.0 |
| .NET 11.0.0 | gRPC (RpcBenchmark) | Grpc.AspNetCore 2.85.0 |
| .NET 11.0.0 | MagicOnion (RpcBenchmark) | MagicOnion 7.11.0 |
| .NET 11.0.0 | SignalR (RpcBenchmark) | SignalR 11.0.0 |
| .NET 11.0.0 | StreamJsonRpc (RpcBenchmark) | StreamJsonRpc 2.26.150 |
| .NET 11.0.0, Native AOT | dotnet_grpc_aot (new) | Grpc.AspNetCore 2.85.0 |
| GCC 16.2 | cpp_grpc_mt | gRPC C++ 1.85.2 |
| GCC 16.2 | cpp_asio_grpc | gRPC C++ 1.85.2, asio-grpc 3.7.0 |
| Go 1.27.4 | go_connect, go_vtconnect | connect-go 1.21.1 |
| Go 1.27.4 | go_grpc | grpc-go 1.85.0 |
| Go 1.27.4 | go_vtgrpc | grpc-go 1.85.0, vtprotobuf 0.6.0 |
| Go 1.27.4 | go_kitex_grpc (new) | Kitex 0.16.4 |
| JDK 25 | scala_akka | Akka 2.8.8, Akka gRPC 2.3.4 |
| JDK 25 | java_armeria | Armeria 1.43.0 |
| JDK 25 | java_hotspot_grpc_pgc | grpc-java 1.85.1 |
| JDK 25 | kotlin_grpc | grpc-kotlin 1.5.0, Kotlin 2.4.21 |
| JDK 25 | java_micronaut (both) | Micronaut 5.2.6 |
| JDK 25 | scala_pekko | Pekko 1.7.1, Pekko gRPC 1.2.0 |
| JDK 25 | java_quarkus | Quarkus 3.40.1 |
| JDK 25 | java_vertx_grpc | Vert.x 5.2.3, grpc-java 1.85.1 |
| Node.js 26.13.1 | node_grpcjs_st | @grpc/grpc-js 1.14.7 |
| Python 3.15 | python_async_grpc | grpcio 1.85.1 |
| Rust 1.100 | rust_grpcio | grpcio 0.13.0 |
| Rust 1.100 | rust_ntex | ntex 4.9.1, ntex-grpc 4.1.1 |
| Rust 1.100 | rust_tonic_mt | tonic 0.14.9, tokio 1.54.1 |
| Rust 1.100 | rust_volo_grpc (new) | volo-grpc 0.12.2 |
| Swift 6.4.2 | swift_grpc | grpc-swift 1.27.6 |
| Swift 6.4.2 | swift_grpc2 (new) | grpc-swift-2 2.4.4 |

Not on the very latest version: Akka is at 2.8.8, the newest release on Maven Central (later
releases need a license token for Akka's own repository); Pekko is on its stable 1.x line, because
Pekko gRPC has no stable 2.x release; the JVM servers run on JDK 25, the current LTS release.

The `RpcBenchmark` libraries were the latest releases at the time. The load generators are `ghz` 0.114.0, the version grpc_bench
uses, and the Rust client on tonic 0.14.9.

## P.S.

ActualLab.Rpc isn't only a benchmark entry: together with Fusion, it runs [Voxt](https://voxt.ai),
which we're building to be **<u>the best chat for everyday communication</u>**. Here's its quick intro:

<iframe style="width: 100%; max-width: 900px; aspect-ratio: 16 / 9; border: 0; border-radius: 8px;" src="https://www.youtube-nocookie.com/embed/qm9hdcmMKWg" title="Voxt intro" allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" referrerpolicy="strict-origin-when-cross-origin" allowfullscreen></iframe>

In one of the next posts, I'll explain why a chat app needs an RPC library this fast.
