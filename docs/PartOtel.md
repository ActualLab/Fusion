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
| `ActualLab.Fusion.EntityFramework` | `ActualLab.Fusion.EntityFramework` | `FusionEntityFrameworkInstruments` |
| `ActualLab.CommandR` | `ActualLab.CommandR` | `CommanderInstruments` |
| `ActualLab.Interception` | `ActualLab.Interception` | `InterceptionInstruments` |

The `Meter` / `ActivitySource` names are what you register with your
OpenTelemetry pipeline (`AddMeter(...)` / `AddSource(...)`). To avoid
hard-coding string literals you can reference the runtime names directly —
e.g. `RpcInstruments.Meter.Name` or `FusionInstruments.ActivitySource.Name`.

The metric names below all use OpenTelemetry's [dotted semantic-convention
naming][semconv] (`rpc.server.call.duration`, `computed.registry.node.count`, …),
so they slot cleanly into dashboards that follow the same convention.

[semconv]: https://opentelemetry.io/docs/specs/semconv/


## RPC Metrics (`ActualLab.Rpc`)

RPC metrics are the most valuable ones for most apps — they tell you the
rate, latency, and error profile of every inbound call your server handles.

### Call metrics

RPC call instruments are fixed and use the bounded `rpc.method` attribute for
method-level breakdowns. This avoids creating a separate instrument for every
method while preserving aggregate and per-method views:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `rpc.server.call.duration` | Histogram | ms | Duration of inbound RPC calls |
| `rpc.client.call.duration` | Histogram | ms | Logical outbound call duration, including reroutes |
| `rpc.client.reroute.count` | Counter | `{reroute}` | Outbound calls rerouted by the RPC layer |
| `rpc.server.call.open` | ObservableGauge | `{call}` | Open inbound calls by stage |
| `rpc.client.call.open` | ObservableGauge | `{call}` | Open outbound calls by stage |
| `rpc.client.call.event.count` | Counter | `{event}` | Batched delayed, resend, and timeout observations |
| `rpc.server.error.count` | Counter | | Inbound calls that failed with an error |
| `rpc.server.cancellation.count` | Counter | | Inbound calls that were cancelled |
| `rpc.server.incomplete.count` | Counter | | Inbound calls that never completed |

Call durations and server outcome counters carry `rpc.system.name` and
`rpc.method`. Failed calls also carry `error.type`. Reroutes carry
`rpc.method`, `rpc.method.kind`, and `rpc.routing.mode`.
The client call-event counter uses the bounded `rpc.call.event` attribute;
open-call gauges use `rpc.call.stage` (`pending`, `result_ready`, or `invalidated`) and carry no peer identity. Their
cached values are refreshed by the existing call-maintenance pass no more often than
`RpcDiagnosticsOptions.OpenCallMetricsPeriodProvider`; metric collection itself never scans call tables. The default
period is 5 minutes for server peers and 1 minute for client peers.

Connection attempts exist only on client peers. Established connection lifetime is useful on both sides and uses
separate client and server instruments:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `rpc.client.connection.attempt.count` | Counter | `{attempt}` | Completed client connection attempts |
| `rpc.client.connection.attempt.duration` | Histogram | ms | Client connection attempt duration |
| `rpc.client.connection.uptime` | Histogram | ms | Client-side established connection lifetime |
| `rpc.server.connection.uptime` | Histogram | ms | Server-side established connection lifetime |

Their bounded attributes are `rpc.connection.kind` and `outcome` (`success`, `error`, or `cancel`).

[views]: https://opentelemetry.io/docs/specs/otel/metrics/sdk/#view

### Transport metrics

The frame-based transports (WebSocket, pipe, …) expose channel and
throughput metrics under `rpc.{transport}.transport`:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `rpc.{transport}.transport.count` | ObservableGauge | | Live transport/channel instances |
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
| `computed.registry.key.count` | ObservableGauge | | Registered `Computed<T>` keys |
| `computed.registry.node.count` | ObservableGauge | | Nodes in the dependency graph |
| `computed.registry.edge.count` | ObservableGauge | | Edges in the dependency graph |
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

Fusion also reports operation retry behavior:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `operation.retry.count` | Counter | `{retry}` | Retry outcomes |
| `operation.retry.delay` | Histogram | ms | Delay before a scheduled retry |

Both use `command.name` and `transiency`; the counter also uses `outcome`.

Invalidation replay is measured once per pass rather than on each computed
invalidation or dependency edge:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `invalidation.pass.duration` | Histogram | ms | Completion-replay pass duration |
| `invalidation.pass.command.count` | Histogram | `{command}` | Commands attempted in one pass |

Both use the bounded `command.name` and `outcome` attributes.

Persistent remote-computed cache access is measured only on the asynchronous
cache lookup path; ordinary in-memory `ComputedRegistry` hits remain
uninstrumented:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `remote_computed.cache.request.count` | Counter | `{request}` | Persistent cache lookup requests |
| `remote_computed.cache.lookup.duration` | Histogram | ms | Persistent cache lookup duration |
| `remote_computed.cache.stale_value.count` | Counter | `{request}` | Cached values served during disconnection |

Request count and lookup duration use the bounded `outcome` attribute
(`hit`, `miss`, `error`, or `cancel`). The stale-value counter uses
`operation` (`connection_check` or `active_call`) to distinguish a value
served while already disconnected from one served when an active call loses
its connection.


## Fusion Entity Framework Metrics (`ActualLab.Fusion.EntityFramework`)

The Entity Framework integration reports how promptly operation and event
logs are consumed and how its database batches behave:

| Metric | Kind | Unit | Attributes | Meaning |
|--------|------|------|------------|---------|
| `db.operation_log.processing.delay` | Histogram | ms | `shard`, `path` | Applied-operation lag |
| `db.event_log.processing.delay` | Histogram | ms | `shard`, `path` | Eligible-event lag |
| `db.log.batch.size` | Histogram | `{entry}` | `log.kind`, `outcome` | Entries read in one batch |
| `db.log.batch.duration` | Histogram | ms | `log.kind`, `outcome` | End-to-end duration of one log batch |

The delay histogram's `path` is bounded to `batch`, `gap`, or `reprocess`
for operations and to `batch` or `reprocess` for events. Batch `log.kind` is
`operation` or `event`; `outcome` is `success` or `error`. Event delay starts
when an event becomes eligible rather than when it was logged, so an
intentional scheduled delay does not look like processing lag.


## CommandR Metrics (`ActualLab.CommandR`)

CommandR reports command execution latency independently of trace sampling:

| Metric | Kind | Unit | Meaning |
|--------|------|------|---------|
| `command.execution.duration` | Histogram | ms | Command handler pipeline duration |

Attributes are `command.name`, `command.kind`, `command.scope`, and `outcome`.


## Tracing

The instrumented layers create [`Activity`] spans on their `ActivitySource`, so a
single logical operation can be followed across the commander, the compute
graph, and RPC hops:

| Source | Spans |
|--------|-------|
| `ActualLab.CommandR` | One span per top-level command (`CommandTracer`); errors are recorded on the span |
| `ActualLab.Rpc` | `in.{Service}/{Method}` (server kind) and `out.{Service}/{Method}` (client kind) spans per call |
| `ActualLab.Fusion` | Spans around compute and invalidation work |
| `ActualLab.Fusion.EntityFramework` | Log processing and entity-resolver batch spans |

RPC **propagates the trace context across the wire**: outbound calls inject
the current `Activity` context into the RPC message headers, and inbound
calls extract it, so a client span and the matching server span end up in the
same trace.

Command and invalidation spans report bounded command name/kind attributes by
default. Command values are omitted. To capture them explicitly, register
`CommandTracer.Options` or `InvalidatingCommandCompletionHandler.Options`
with `CaptureCommandPayload = true`; payload formatting is still skipped when
the listener requests propagation data only. Swallowed invalidation replay
failures mark the enclosing span as an error with `invalidation.partial_failure`
and `invalidation.failure.count`.

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
            metrics.AddMeter("ActualLab.Fusion.EntityFramework");
            metrics.AddMeter("Samples.TodoApp"); // Your own meter(s)
        })
        .WithTracing(tracing => {
            tracing.SetSampler(_ => new AlwaysOnSampler());
            tracing.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            tracing.AddSource("ActualLab.Rpc");
            tracing.AddSource("ActualLab.CommandR");
            tracing.AddSource("ActualLab.Fusion");
            tracing.AddSource("ActualLab.Fusion.EntityFramework");
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
    .AddMeter(FusionEntityFrameworkInstruments.Meter.Name)
                                                // "ActualLab.Fusion.EntityFramework"
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
    .AddSource(FusionEntityFrameworkInstruments.ActivitySource.Name)
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

- **Use metric views to choose RPC aggregation.** Drop `rpc.method` for a
  service-wide view, or retain it for bounded per-method latency and error
  reporting. Do not add route, peer, call ID, or argument attributes.


## Summary

| To collect... | Register meter/source | Key names |
|---------------|-----------------------|-----------|
| RPC call latency and errors | `ActualLab.Rpc` | `rpc.server.call.duration`, `rpc.client.call.duration` |
| RPC reroutes | `ActualLab.Rpc` | `rpc.client.reroute.count` |
| RPC open calls and maintenance | `ActualLab.Rpc` | `rpc.*.call.open`, `rpc.client.call.event.count` |
| RPC connection health | `ActualLab.Rpc` | `rpc.client.connection.attempt.*`, `rpc.*.connection.uptime` |
| RPC transport throughput | `ActualLab.Rpc` | `rpc.{transport}.transport.*` |
| Compute graph size & pruning | `ActualLab.Fusion` | `computed.registry.node.count`, `computed.registry.edge.count` |
| Operation-log lag | `ActualLab.Fusion.EntityFramework` | `db.operation_log.processing.delay` |
| Event-log lag | `ActualLab.Fusion.EntityFramework` | `db.event_log.processing.delay` |
| Database log batch health | `ActualLab.Fusion.EntityFramework` | `db.log.batch.size`, `db.log.batch.duration` |
| Command execution | `ActualLab.CommandR` | `command.execution.duration` and command spans |
| Fusion retries and invalidation | `ActualLab.Fusion` | `operation.retry.*`, `invalidation.pass.*` |
| Persistent remote cache | `ActualLab.Fusion` | `remote_computed.cache.*` |
| Distributed traces across RPC | RPC, CommandR, Fusion, and EF sources | `in.*` / `out.*` spans |

Because Fusion relies on the built-in .NET metrics/tracing primitives, all of
this works with any OpenTelemetry-compatible backend — the Aspire dashboard,
Prometheus + Grafana, Jaeger, Application Insights, Google Cloud Operations,
and so on — with no additional Fusion configuration.
