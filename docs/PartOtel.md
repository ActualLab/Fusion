# OpenTelemetry: Metrics and Tracing

Fusion, ActualLab.Rpc, and CommandR are instrumented with the standard
.NET diagnostics primitives — [`System.Diagnostics.Metrics.Meter`] for
metrics and [`System.Diagnostics.ActivitySource`] for distributed tracing.
Because these are the same primitives [OpenTelemetry] consumes, wiring
Fusion into an observability pipeline (OTLP, Prometheus, Aspire dashboard,
Application Insights, Google Cloud, …) is just a matter of *naming the
meters and activity sources you want to collect*. There is nothing
Fusion-specific to install.

This page covers:

- The meters and activity sources Fusion ships with, and the metrics they emit
- How to enable them in an [.NET Aspire] host (as in the `TodoApp` sample)
- A production-grade setup (as used by [Voxt](https://voxt.ai))

[`System.Diagnostics.Metrics.Meter`]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics.meter
[`System.Diagnostics.ActivitySource`]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activitysource
[OpenTelemetry]: https://opentelemetry.io/
[.NET Aspire]: https://learn.microsoft.com/en-us/dotnet/aspire/


## Instruments Overview

Every instrumented ActualLab assembly exposes a single `Meter` and a single
`ActivitySource`, both named after the assembly, via a static
`*Instruments` class:

| Assembly | Meter / ActivitySource name | Instruments class |
|----------|-----------------------------|-------------------|
| `ActualLab.Rpc` | `ActualLab.Rpc` | `RpcInstruments` |
| `ActualLab.Fusion` | `ActualLab.Fusion` | `FusionInstruments` |
| `ActualLab.CommandR` | `ActualLab.CommandR` | `CommanderInstruments` |
| `ActualLab.Interception` | `ActualLab.Interception` | `InterceptionInstruments` |

The `Meter` / `ActivitySource` names are what you register with your
OpenTelemetry pipeline (`AddMeter(...)` / `AddSource(...)`). To avoid
hard-coding string literals you can reference the runtime names directly —
e.g. `RpcInstruments.Meter.Name` or `FusionInstruments.ActivitySource.Name`.

The metric names below all use OpenTelemetry's [dotted semantic-convention
naming][semconv] (`rpc.server.duration`, `computed.registry.node.count`, …),
so they slot cleanly into dashboards that follow the same convention.

[semconv]: https://opentelemetry.io/docs/specs/semconv/


## RPC Metrics (`ActualLab.Rpc`)

RPC metrics are the most valuable ones for most apps — they tell you the
rate, latency, and error profile of every inbound call your server handles.

### Aggregate server metrics

These roll up **all** inbound calls, following the
[OpenTelemetry RPC metric conventions][rpc-semconv]:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `rpc.server.duration` | Histogram | ms | Duration of inbound RPC calls |
| `rpc.server.error.count` | Counter | | Inbound calls that failed with an error |
| `rpc.server.cancellation.count` | Counter | | Inbound calls that were cancelled |
| `rpc.server.incomplete.count` | Counter | | Inbound calls that never completed |

[rpc-semconv]: https://opentelemetry.io/docs/specs/semconv/rpc/rpc-metrics/

### Per-method server metrics

For each RPC method, the default call tracer (`RpcDefaultCallTracer`) also
emits the same set of instruments, prefixed with the method's full name —
`rpc.server.{Service}/{Method}`:

| Metric | Kind | Unit |
|--------|------|------|
| `rpc.server.{Service}/{Method}.call.duration` | Histogram | ms |
| `rpc.server.{Service}/{Method}.error.count` | Counter | |
| `rpc.server.{Service}/{Method}.cancellation.count` | Counter | |
| `rpc.server.{Service}/{Method}.incomplete.count` | Counter | |

Per-method metrics are high-cardinality: a busy service can define hundreds
of methods. In production you typically **keep only the ones you care about**
using an OpenTelemetry [view][views] — see [Production Setup](#production-setup) below.

[views]: https://opentelemetry.io/docs/specs/otel/metrics/sdk/#view

### Transport metrics

The frame-based transports (WebSocket, pipe, …) expose channel and
throughput metrics under `rpc.{transport}.transport`:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `rpc.{transport}.transport.count` | ObservableCounter | | Live transport/channel instances |
| `rpc.{transport}.transport.incoming.item.count` | Counter | | Items received |
| `rpc.{transport}.transport.outgoing.item.count` | Counter | | Items sent |
| `rpc.{transport}.transport.incoming.frame.size` | Histogram | By | Incoming frame size |
| `rpc.{transport}.transport.outgoing.frame.size` | Histogram | By | Outgoing frame size |


## Fusion Metrics (`ActualLab.Fusion`)

Fusion's core metrics describe the health of the `ComputedRegistry` — the
in-memory store of every `Computed<T>` and the dependency graph connecting
them. They are emitted under the `computed.registry` prefix:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `computed.registry.key.count` | ObservableCounter | | Registered `Computed<T>` keys |
| `computed.registry.node.count` | ObservableCounter | | Nodes in the dependency graph |
| `computed.registry.edge.count` | ObservableCounter | | Edges in the dependency graph |
| `computed.registry.pruned.key.count` | Counter | | Pruned `Computed<T>` instances |
| `computed.registry.pruned.disposed.count` | Counter | | Pruned disposable `Computed<T>` instances |
| `computed.registry.pruned.edge.count` | Counter | | Pruned dependency edges |
| `computed.registry.prunes.key-cycle.count` | Counter | | Key prune cycles run |
| `computed.registry.prunes.node-edge-cycle.count` | Counter | | Node & edge prune cycles run |
| `computed.registry.prunes.key-cycle.duration` | Histogram | ms | Key prune cycle duration |
| `computed.registry.prunes.node-cycle.duration` | Histogram | ms | Node prune cycle duration |
| `computed.registry.prunes.edge-cycle.duration` | Histogram | ms | Edge prune cycle duration |

The three observable `*.count` gauges are the ones to watch on a dashboard:
a steadily climbing `node.count` or `edge.count` that never comes back down
usually points at a caching / invalidation issue (see
[Memory Management](PartF-MM.md)). The prune metrics tell you whether the
registry's background pruning is keeping up.


## CommandR Metrics (`ActualLab.CommandR`)

CommandR does not emit counters by default; its instrumentation is
trace-oriented — see [Tracing](#tracing) below. The `ActualLab.CommandR`
meter still exists so you can register it once and attach your own
command-level instruments to it if you add any.


## Tracing

All three layers create [`Activity`] spans on their `ActivitySource`, so a
single logical operation can be followed across the commander, the compute
graph, and RPC hops:

| Source | Spans |
|--------|-------|
| `ActualLab.CommandR` | One span per top-level command (`CommandTracer`); errors are recorded on the span |
| `ActualLab.Rpc` | `in.{Service}/{Method}` (server kind) and `out.{Service}/{Method}` (client kind) spans per call |
| `ActualLab.Fusion` | Spans around operation-log processing, event-log processing, entity-resolver batches, and invalidation |

RPC **propagates the trace context across the wire**: outbound calls inject
the current `Activity` context into the RPC message headers, and inbound
calls extract it, so a client span and the matching server span end up in the
same trace.

[`Activity`]: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity

### When RPC call tracing is active

The default `CallTracerFactory` only attaches the per-method tracer on the
**server** side (`RuntimeInfo.IsServer`). Clients therefore don't pay for
per-method metric/activity bookkeeping unless you opt in by supplying your
own factory via `RpcDiagnosticsOptions.CallTracerFactory`. All of this is
still gated by the usual OpenTelemetry rule: an `ActivitySource` produces
spans only when a listener (your tracer provider) is subscribed to it, and a
`Meter`'s instruments record only when a `MeterProvider` collects them. If
you register nothing, the instrumentation is effectively free.


## Enabling It in an Aspire Host

The `TodoApp` sample uses the standard **.NET Aspire** `ServiceDefaults`
project. Its `ConfigureOpenTelemetry` method is the minimal, canonical way
to turn everything on — note the `AddMeter(...)` / `AddSource(...)` lines for
the ActualLab assemblies:

```csharp
public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
{
    builder.Logging.AddOpenTelemetry(logging => {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
            metrics.AddMeter("ActualLab.Rpc");
            metrics.AddMeter("ActualLab.CommandR");
            metrics.AddMeter("ActualLab.Fusion");
            metrics.AddMeter("Samples.TodoApp"); // Your own meter(s)
        })
        .WithTracing(tracing => {
            tracing.SetSampler(_ => new AlwaysOnSampler());
            tracing.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            tracing.AddSource("ActualLab.Rpc");
            tracing.AddSource("ActualLab.CommandR");
            tracing.AddSource("ActualLab.Fusion");
            tracing.AddSource("Samples.TodoApp"); // Your own activity source(s)
        });

    builder.AddOpenTelemetryExporters();
    return builder;
}
```

The exporter is enabled by convention: if `OTEL_EXPORTER_OTLP_ENDPOINT` is
set (Aspire injects it automatically when the host is launched from the
Aspire AppHost), the OTLP exporter is added and everything shows up in the
**Aspire dashboard**.

```csharp
private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
    if (useOtlpExporter)
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    return builder;
}
```

Each service project then calls `AddServiceDefaults()` from its host's
`Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // Wires up OpenTelemetry, health checks, service discovery
```

### Required packages

The `ServiceDefaults` project references the standard OpenTelemetry
packages — nothing ActualLab-specific:

```xml
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
```


## Production Setup

In a real deployment you rarely want *every* per-method RPC metric — there
are too many, and most are noise. [Voxt](https://voxt.ai) registers the same
meters and sources but adds two production refinements: **trace sampling**
and a **metric view** that drops all but a handful of high-value per-method
histograms.

Registering by the runtime name avoids typos and keeps the registration in
sync with the assemblies:

```csharp
otel.WithMetrics(meter => meter
    .AddMeter(RpcInstruments.Meter.Name)        // "ActualLab.Rpc"
    .AddMeter(CommanderInstruments.Meter.Name)  // "ActualLab.CommandR"
    .AddMeter(FusionInstruments.Meter.Name)     // "ActualLab.Fusion"
    // ... your own per-assembly meters ...
    .AddOtlpExporter(cfg => { /* endpoint, batching, protocol */ })
    .AddView(instrument => {
        // Keep only *.duration (ms) per-method RPC metrics, and only for a
        // curated set of hot methods; drop the rest to cut cardinality.
        if (instrument.Meter == RpcInstruments.Meter && instrument.Name.StartsWith("rpc.server.")) {
            if (instrument.Unit != "ms")
                return MetricStreamConfiguration.Drop;
            if (!instrument.Name.StartsWith("rpc.server.IChats/Get")
                && !instrument.Name.StartsWith("rpc.server.IAuthors/Get"))
                return MetricStreamConfiguration.Drop;
        }
        return null; // Keep everything else as-is
    })
);

otel.WithTracing(tracer => tracer
    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRate)))
    .SetErrorStatusOnException()
    .AddSource(RpcInstruments.ActivitySource.Name)
    .AddSource(CommanderInstruments.ActivitySource.Name)
    .AddSource(FusionInstruments.ActivitySource.Name)
    // ... your own per-assembly sources ...
    .AddAspNetCoreInstrumentation(opt => {
        // Exclude noisy, high-volume endpoints (RPC websocket, health checks, _blazor, ...)
        opt.Filter = ctx => !IsExcludedPath(ctx.Request.Path);
    })
);
```

Two things worth copying from this setup:

- **Filter the RPC transport endpoints out of ASP.NET Core tracing.**
  Fusion's own RPC websocket/HTTP endpoints (`/rpc/ws`, `/rpc/http`, …) are
  long-lived and extremely high-volume; tracing them at the ASP.NET Core
  level is pure noise. Exclude them and rely on the `ActualLab.Rpc` spans
  instead, which trace at the *call* granularity.

- **Use a metric view to bound RPC per-method cardinality.** Keep the
  aggregate `rpc.server.*` metrics always; keep per-method histograms only
  for the endpoints you actually watch on dashboards.


## Summary

| To collect... | Register meter/source | Key names |
|---------------|-----------------------|-----------|
| RPC call rate, latency, errors | `ActualLab.Rpc` | `rpc.server.duration`, `rpc.server.error.count` |
| RPC per-method breakdown | `ActualLab.Rpc` | `rpc.server.{Service}/{Method}.call.duration` |
| RPC transport throughput | `ActualLab.Rpc` | `rpc.{transport}.transport.*` |
| Compute graph size & pruning | `ActualLab.Fusion` | `computed.registry.node.count`, `computed.registry.edge.count` |
| Command spans | `ActualLab.CommandR` | (traces only) |
| Distributed traces across RPC | all three `ActivitySource`s | `in.*` / `out.*` spans |

Because Fusion relies on the built-in .NET metrics/tracing primitives, all of
this works with any OpenTelemetry-compatible backend — the Aspire dashboard,
Prometheus + Grafana, Jaeger, Application Insights, Google Cloud Operations,
and so on — with no additional Fusion configuration.
