# OpenTelemetry monitoring improvements

This plan turns the July 2026 tracing and metrics audit into small, independently tested commits. Each batch must
pass focused tests before commit. After all batches, TodoApp must be exercised through Aspire and every new trace
field and metric must be verified in the dashboard.

## Reuse

### Existing abstractions to reuse

- RPC: `RpcCallTracer`, `RpcDefaultCallTracer`, `RpcOutboundContext`, `RpcActivityInjector`, `RpcHeadersExt`,
  `RpcCallSummary`, `RpcCallTracker`, `RpcInstruments`, and existing reroute/connection lifecycle hooks.
- Fusion: `FusionInstruments`, `ComputedRegistry.MeterSet`, `InvalidatingCommandCompletionHandler`, and
  `OperationReprocessor`.
- CommandR: `CommanderInstruments` and `CommandTracer`.
- Fusion.EntityFramework: `FusionEntityFrameworkInstruments`, `DbOperationLogReader`, and `DbEventLogReader`.
- Tests: the existing RPC local transport fixtures, mutable-route/reconnection fixtures, `ActivityListener`, and
  `MeterListener`.

### Reusability of new components

- General header replacement belongs in `ActualLab.Rpc` next to `RpcHeadersExt`; it is protocol-specific and does
  not belong in `ActualLab.Core`.
- Stable diagnostic tag names and fixed instruments belong in their assembly-level instrument classes, where all
  callers can reuse them.
- Test collectors remain test-local unless several test projects need the same collector. Promote only then.
- No new general-purpose Core abstraction is currently justified.

## Tracing batches

- [x] T1: preserve ambient W3C context when no RPC client activity is created; replace stale caller-supplied trace
  headers without mutating caller-owned arrays. Add propagation and header regression tests.
- [ ] T2: give every rerouted attempt a live client activity and verify the successful attempt is parented correctly.
  Reroute remains expected control flow, not an error.
- [ ] T3: remove the duplicate parent link; add stable RPC semantic attributes and exception events. Do not change
  cancellation or reroute to error status.
- [ ] T4: move inbound completion to include response serialization/enqueue and cover serialization failure.
- [ ] T5: route Fusion.EntityFramework activities through its registered activity source and test source ownership.
- [ ] T6: make command payload capture safe and explicitly opt-in; expose swallowed invalidation failures on the
  enclosing activity.

Cancellation and reroute status semantics are recommendation-only in this work. They are non-error outcomes; the
recommended representation is unset status plus stable events/attributes, subject to a separate API decision.

## Metrics batches

- [ ] M0: correct live ComputedRegistry and RPC transport instance measurements from `ObservableCounter` to
  `ObservableGauge` without renaming them.
- [ ] M1: replace dynamic per-method RPC duration instruments with fixed `rpc.server.call.duration` and
  `rpc.client.call.duration` histograms, both in milliseconds, tagged by stable RPC system and method names. Add
  `rpc.client.reroute.count` on the rare reroute branch.
- [ ] M2: add connection attempt count/duration and connection uptime on lifecycle paths.
- [ ] M3: add aggregate active server/client call gauges and batched client call-event counts.
- [ ] M4: add CommandR execution duration and Fusion operation retry count/delay.
- [ ] M5: retain and verify the existing `db.operation_log.processing.delay`; add event-log processing delay and
  log batch size/duration. Update stale docs to register the EF meter.
- [ ] M6: add invalidation pass duration and command count at the completion-replay scope. Do not instrument
  individual computed invalidations, dependency edges, or other nanosecond-class paths.
- [ ] M7: add persistent remote-computed cache request/lookup and stale-value-served metrics. Do not instrument
  in-memory cache hits.

Metric tags must be bounded. IDs, routes, arguments, command values, error messages, cache keys, and unbounded shard
names are excluded. Instrumentation on hot paths must be listener-gated, scrape-time aggregated, batched, or placed
on existing slow/rare lifecycle paths.

## Verification and commits

For every batch:

1. Add a focused regression or instrument-contract test.
2. Run the smallest relevant test slice, then the affected project build/tests.
3. Commit only that batch on `master`.

After all batches:

1. Run the TodoApp Aspire host with anonymous dashboard access.
2. Exercise RPC success, failure, cancellation, reroute/reconnect, Commander, Fusion invalidation, EF log processing,
   and remote-cache paths. Use a temporary fault injection only when a path cannot be triggered naturally, and
   revert it before committing.
3. Inspect Aspire traces for hierarchy, attributes, events, statuses, links, and completion timing.
4. Inspect Aspire metrics for every new instrument, unit, and expected attribute set, including operation-log lag.
5. Run the final affected test suites and confirm the working tree contains no temporary changes.
