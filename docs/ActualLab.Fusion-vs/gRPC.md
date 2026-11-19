# ActualLab.Fusion vs gRPC Streaming

gRPC is Google's high-performance RPC framework with support for streaming. Both Fusion and gRPC enable efficient client-server communication, but they serve different purposes.

## The Core Difference

**gRPC** is a transport and serialization layer with streaming support. You define services in Protobuf, and gRPC handles efficient binary communication. Streaming is explicit — you decide when to push data.

**Fusion** is a caching and synchronization layer. It offers two models: (1) traditional RPC streaming via `RpcStream<T>` for explicit data streams, and (2) invalidation-based updates where clients observe computed values and automatically receive new data when values change.

## gRPC Streaming Approach

```protobuf
service StockService {
    rpc WatchPrice (PriceRequest) returns (stream PriceUpdate);
}
```

```csharp
// Server
public override async Task WatchPrice(PriceRequest request,
    IServerStreamWriter<PriceUpdate> stream, ServerCallContext context)
{
    while (!context.CancellationToken.IsCancellationRequested)
    {
        var price = await GetCurrentPrice(request.Symbol);
        await stream.WriteAsync(new PriceUpdate { Price = price });
        await Task.Delay(1000); // You decide the update frequency
    }
}

// Client
var stream = client.WatchPrice(new PriceRequest { Symbol = "AAPL" });
await foreach (var update in stream.ResponseStream.ReadAllAsync())
    Console.WriteLine($"Price: {update.Price}");
```

## Fusion Approaches

### Option 1: RpcStream for Explicit Streaming

```csharp
// Server - explicit streaming like gRPC
public class StockService : IStockService
{
    public async Task<RpcStream<StockQuote>> WatchPrices(string[] symbols, CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<StockQuote>();
        _ = Task.Run(async () => {
            while (!ct.IsCancellationRequested)
            {
                foreach (var symbol in symbols)
                    await channel.Writer.WriteAsync(await GetQuote(symbol), ct);
                await Task.Delay(1000, ct);
            }
        });
        return channel.ToRpcStream();
    }
}

// Client
await foreach (var quote in stockService.WatchPrices(["AAPL", "GOOG"], ct))
    Console.WriteLine($"{quote.Symbol}: {quote.Price}");
```

### Option 2: Invalidation-Based Updates

```csharp
// Server - automatic updates via invalidation
public class StockService : IComputeService
{
    [ComputeMethod]
    public virtual async Task<decimal> GetPrice(string symbol, CancellationToken ct)
        => await _priceProvider.GetPrice(symbol, ct);
}

// Client - notified when value changes
var computed = await Computed.Capture(() => stockService.GetPrice("AAPL"));
await foreach (var c in computed.Changes(ct))
    Console.WriteLine($"Price: {c.Value}");
```

## Where Each Excels

### ActualLab.Fusion is better at

- Both explicit streaming (`RpcStream`) and change notifications
- Built-in caching with dependency-based invalidation
- Shared C# interfaces between client and server (no Protobuf translation)
- Reconnection that automatically restores state
- UI synchronization in .NET applications

### gRPC is better at

- Language-agnostic services (Protobuf across Go, Java, Python, etc.)
- Precise control over streaming frequency and timing
- Schema evolution with Protobuf contracts
- Building polyglot microservices
- Wide ecosystem and mature tooling

## Streaming Models Compared

| Aspect | gRPC Streaming | Fusion |
|--------|----------------|--------|
| Explicit streaming | `IServerStreamWriter<T>` | `RpcStream<T>` |
| Change notifications | Not built-in | `Computed.Changes()` |
| Push model | Explicit `WriteAsync` | Both explicit and automatic |
| Caching | None (you implement) | Built-in with dependencies |
| Multiple observers | One stream per client | Shared computed, individual notifications |
| Reconnection | Manual resync | Automatic catch-up |

## When to Use Each

### Choose gRPC when:
- Interoperability with non-.NET services (Go, Java, Python)
- You need precise control over streaming frequency
- Protobuf schema evolution is important
- You're building microservices that span languages

### Choose Fusion when:
- Building .NET applications (especially Blazor)
- You want caching with automatic real-time updates
- Data has complex dependencies (A depends on B depends on C)
- You prefer shared C# interfaces over Protobuf definitions

### Hybrid Approach

You can use both:
- **gRPC** for cross-service communication in a polyglot backend
- **Fusion** for client-facing real-time features

```csharp
// Fusion service that calls gRPC internally
public class StockService : IComputeService
{
    private readonly StockGrpcClient _grpcClient;

    [ComputeMethod]
    public virtual async Task<decimal> GetPrice(string symbol, CancellationToken ct)
    {
        // Call gRPC backend, cache and track dependencies with Fusion
        var response = await _grpcClient.GetPriceAsync(new PriceRequest { Symbol = symbol });
        return response.Price;
    }
}
```

## Performance

ActualLab.Rpc (Fusion's RPC layer) outperforms gRPC in benchmarks. The server is limited to 4 CPUs
in a Docker container and the connections don't use TLS, which is the setup
[grpc_bench](https://github.com/LesnyRumcajs/grpc_bench) uses.

A colored number after a result shows how many times slower it is than the fastest result in the same test:
<span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span> is the fastest, <span style="background: rgb(163,116,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span> is half as fast, <span style="background: rgb(137,39,39); color: #fff; padding: 2px 6px; border-radius: 3px;">↓10×</span> is ten times slower.
The color goes from green (1x) through red (3x) to black (50x or more) on a log scale.

| Benchmark | ActualLab.Rpc | gRPC (grpc-dotnet) |
|---|---|---|
| RPC calls (Sum) | <span style="white-space: nowrap;">5.62M calls/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">625.64K calls/s <span style="background: rgb(146,41,41); color: #fff; padding: 2px 6px; border-radius: 3px;">↓9.0×</span></span> |
| RPC calls (GetUser) | <span style="white-space: nowrap;">4.36M calls/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">602.70K calls/s <span style="background: rgb(164,47,47); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.2×</span></span> |
| RPC calls (SayHello, the grpc_bench message) | <span style="white-space: nowrap;">2.54M calls/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">567.12K calls/s <span style="background: rgb(205,58,58); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.5×</span></span> |
| Streaming (1-byte items) | <span style="white-space: nowrap;">40.19M items/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">15.40M items/s <span style="background: rgb(213,84,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.6×</span></span> |
| Streaming (100-byte items) | <span style="white-space: nowrap;">18.64M items/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">9.42M items/s <span style="background: rgb(161,117,78); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.0×</span></span> |
| Streaming (10KB items) | <span style="white-space: nowrap;">492.15K items/s <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> | <span style="white-space: nowrap;">328.92K items/s <span style="background: rgb(109,150,84); color: #fff; padding: 2px 6px; border-radius: 3px;">↓1.5×</span></span> |

See [Performance Benchmarks](/Performance) for full details and test environment.

### Compared with gRPC Servers in Other Languages

We also ran 26 gRPC servers from grpc_bench on the same machine in the same 4-CPU setup, on current
versions of their libraries and with the load generator tuned until each server is saturated.
`SayHello` calls per second:

| Server | Calls/s |
|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">2.54M <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| rust_ntex (Rust), the fastest gRPC server tested | <span style="white-space: nowrap;">1.06M <span style="background: rgb(197,94,73); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.4×</span></span> |
| java_vertx_grpc (Java, Vert.x) | <span style="white-space: nowrap;">672K <span style="background: rgb(219,62,62); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| dotnet_grpc (grpc-dotnet in grpc_bench) | <span style="white-space: nowrap;">670K <span style="background: rgb(219,62,62); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| java_quarkus (Java, Quarkus) | <span style="white-space: nowrap;">632K <span style="background: rgb(214,61,61); color: #fff; padding: 2px 6px; border-radius: 3px;">↓4.0×</span></span> |
| rust_tonic_mt (Rust, tonic) | <span style="white-space: nowrap;">491K <span style="background: rgb(193,55,55); color: #fff; padding: 2px 6px; border-radius: 3px;">↓5.2×</span></span> |
| go_grpc (Go, grpc-go) | <span style="white-space: nowrap;">340K <span style="background: rgb(161,46,46); color: #fff; padding: 2px 6px; border-radius: 3px;">↓7.5×</span></span> |

The method, the full list of servers, their versions and the effect of TLS are in
[The Fastest .NET RPC Servers vs the Fastest gRPC Servers](../posts/261116-grpc-bench.md).

The two sets of numbers come from different load generators: the gRPC servers were loaded by a Rust
client that replicates grpc_bench's `ghz`, ActualLab.Rpc by RpcBenchmark's own .NET client. The same
RpcBenchmark client measures grpc-dotnet at 567K calls/s, so the SayHello factor in the first table
(↓4.5×) is the like-for-like one.

### Over a Real Network

We also ran the benchmarks over a 5 Gbit/s LAN between two machines, with the server limited to 2 CPUs
(a Docker container in a VM) and a separate client PC. `SayHello` calls per second:

| Server | Calls/s |
|---|---|
| ActualLab.Rpc | <span style="white-space: nowrap;">1.07M <span style="background: rgb(34,197,94); color: #fff; padding: 2px 6px; border-radius: 3px;">=1.0</span></span> |
| rust_ntex (Rust), the fastest gRPC server tested | <span style="white-space: nowrap;">400.7K <span style="background: rgb(217,82,71); color: #fff; padding: 2px 6px; border-radius: 3px;">↓2.7×</span></span> |
| java_vertx_grpc (Java, Vert.x) | <span style="white-space: nowrap;">318.9K <span style="background: rgb(229,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| gRPC (grpc-dotnet, RpcBenchmark client) | <span style="white-space: nowrap;">313.9K <span style="background: rgb(228,65,65); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.4×</span></span> |
| dotnet_grpc (grpc-dotnet in grpc_bench) | <span style="white-space: nowrap;">284.8K <span style="background: rgb(220,63,63); color: #fff; padding: 2px 6px; border-radius: 3px;">↓3.8×</span></span> |
| go_vtgrpc (Go, grpc-go) | <span style="white-space: nowrap;">179.3K <span style="background: rgb(181,51,51); color: #fff; padding: 2px 6px; border-radius: 3px;">↓6.0×</span></span> |

The other call tests and streams show the same pattern: ActualLab.Rpc is 3.4-6.1x faster than gRPC on
calls, and 2.4x (1-byte items) and 1.2x (100-byte items) faster on streams. The 100-byte test is
close to the link's limit. See [Real-Network RPC Benchmarks](/Performance#real-network-rpc-benchmarks)
for the setup and all the results.

## The Key Insight

gRPC streaming is **push-based** — you explicitly send updates via Protobuf-defined services.

Fusion offers **both models**: explicit `RpcStream<T>` streaming when you need it, plus automatic invalidation-based updates for computed values. The key advantage is that C# interfaces serve as your API contract — no Protobuf translation layer needed between client and server.
