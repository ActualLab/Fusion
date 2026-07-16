# Fusion Hot-Path Optimization Plan

Date: 2026-07-16

Status: source review complete; implementation pending

## Goal

Reduce the cost of Fusion's most frequently used local operations without changing
observable behavior:

- cached compute-method calls;
- cache misses and recomputation;
- raw and cascading invalidation;
- uncontended and contended `AsyncLock` acquisition;
- uncontended keyed acquisition and waiter handoff in `AsyncLockSet`.

The candidate gains below are source-review estimates, not measured results. Each
change must be benchmarked independently and retained only when its end-to-end
benchmark improves by more than 1% without a correctness or allocation regression
outside its stated trade-off.

Generated-proxy method dispatch is intentionally separated into
[Proxy Method Slots and Array-Based Interceptor Dispatch](proxy-method-slots.md).

## Current Reference Points

The existing BenchmarkDotNet runs provide these approximate baselines:

| Scenario | Mean | Allocation | Unit |
|---|---:|---:|---|
| Cached compute call, `long` | 29.6 ns | 104 B | one cache hit |
| Recompute, `long` | 2.876 us | 1,144 B | one recomputation |
| Raw invalidation | 695 ns | 105 B | one invalidation |
| Cascade, K=2, N=63 | 17.7 us | - | whole tree |
| Cascade, K=3, N=40 | 23.63 us | - | whole tree |
| Cascade, K=4, N=85 | 16.33 us | - | whole tree |

The cascade results correspond to approximately 281 ns, 591 ns, and 192 ns per
node respectively, or about 307 ns per node when pooled. Lock and lock-set
baselines still need dedicated benchmarks.

## Correctness Constraints

- `AsyncLocal` state used for compute locking and lock reentry detection must
  preserve its current `ExecutionContext` isolation.
- Incomplete `ValueTask` instances must be converted to `Task` at most once when
  the operation needs multiple observations or awaits. Completed `ValueTask`
  instances should stay on their allocation-free path.
- Registry removal must not expose a key as absent before its computed value has
  entered the invalidated state.
- Invalidation must preserve cancellation-token normalization, proxy-target
  handling, dependency ordering, and every supported `Task`/`ValueTask` result
  shape.
- `AsyncLockSet` entries must still reject racing users safely, remove themselves
  from the dictionary, and preserve cancellation, reentry, and waiter-scheduling
  behavior.

## Reuse

### Existing abstractions to reuse

- Reuse `SimpleAsyncLock` at call sites where reentry detection is demonstrably
  unnecessary instead of introducing a second unchecked lock implementation.
- Reuse the completed-`ValueTask<T>` branch in
  `InterceptedObjectAsyncInvokerFactory<T>` as the model for the completed-
  `Task<T>` fast path.
- Extend the existing computed-input equality and registry machinery rather than
  adding a parallel cache. The alternate lookup must have exactly the same hash
  and equality semantics as `ComputeMethodInput`.
- Reuse the existing `MethodDef.DefaultResult` and typed method metadata in the
  invalidation fast path.
- Extend the existing BenchmarkDotNet project and its shared operation-count
  conventions for all measurements.

### Reusability and placement of new components

- A stack-only alternate computed-input lookup key belongs beside
  `ComputeMethodInput` in `ActualLab.Fusion`; its equality contract is specific to
  Fusion's computed registry and is not generally reusable in `ActualLab.Core`.
- Completed-task dispatch belongs in `ActualLab.Interception`, beside the existing
  async invoker factories. It should not be introduced as a Fusion-local adapter.
- Lock lifecycle changes belong inside the existing `ActualLab.Core` lock types.
  No new public lock abstraction is planned.
- New benchmark fixtures remain local to the benchmark project. Their measurement
  helpers should be shared within that project unless a production caller also
  needs the same abstraction.

## Relationship to Proxy Method Slots

The separate [proxy method slots](proxy-method-slots.md) task changes generated
proxy dispatch, interceptor binding, and the association between proxy methods and
`MethodDef` instances. Its relevant dependencies are:

- The alternate computed-registry lookup **may depend on the final method-slot
  design**. `ComputeMethodInput` currently hashes and compares `MethodDef` by
  reference, while slot bindings may change where method definitions are created
  or shared. Finalize its lookup key only after the binding's `MethodDef` identity
  contract is stable.
- The invalidation-specialized interceptor result path **may depend on the final
  method-slot design**. Slot resolution is likely the right place to select the
  final compute/invalidation handler, so implementing both independently could
  duplicate or immediately replace interceptor routing work.
- The completed-`Task<T>` invoker fast path touches `MethodDef` invoker factories,
  but does not depend on method indexing. It only requires merge coordination if
  both tasks edit the same files.
- Registry state transitions, computed leaf paths, invalidation graph traversal,
  `AsyncLock`, and `AsyncLockSet` are independent of proxy method slots.

Cached-call, recompute, and invalidation percentage estimates use the current proxy
dispatch cost. Rebaseline them after method-slot dispatch lands; an unchanged
absolute saving will represent a different percentage of a faster total operation.

## Cached Compute-Method Hit

### 1. Use an alternate registry lookup without allocating the persistent key

Every hit currently constructs a `ComputeMethodInput`, although the registry only
needs a temporary lookup view until a miss is confirmed. On .NET 9/10, extend the
registry comparer with `IAlternateEqualityComparer<TAlternate, ComputedInput>` and
use `ConcurrentDictionary.GetAlternateLookup` with a stack-only value key. Create
the persistent `ComputeMethodInput` only on a miss.

The alternate key must include the cached hash, `MethodDef`, proxy, and the
arguments excluding `CancellationToken`, with exactly the existing reference and
argument equality rules.

Expected whole-hit improvement: **8-18%**, medium confidence. Expected allocation
reduction: approximately **72 B per hit**, high confidence. The 72 B estimate is
the x64 size of the avoided `ComputeMethodInput`: object header, base `Function`
reference, cached hash/padding, method and proxy references, and four argument-list
references. The current 104 B/hit should fall to roughly 32 B/hit if no replacement
allocation appears.

Status: **Closed — retained.**

Remarks: repeated .NET 10 post-change runs measured 25.53-26.02 ns and
32 B for the `long` case, compared with 35.98 ns and 104 B in the same run and
29.6 ns and 104 B in the historical baseline. The allocation reduction is exactly
72 B per hit; the result is about 12.8% faster even against the historical timing.
Focused tests cover argument differences, cancellation-token exclusion, and proxy
identity. The alternate key preserves the current reference identity of
`MethodDef` and may require mechanical adaptation after proxy method slots land.

### 2. Split leaf fast paths from cold operation handling

Keep `Computed.GetValuePromise` and the cache-hit portion of
`ComputedRegistry.OnOperation` small enough for the JIT to inline, moving event,
diagnostic, and unusual-state work into no-inline slow helpers.

Expected whole-hit improvement: **4-9%**, medium-low confidence.

Combined cache-hit target: **10-22%**, approximately 23-27 ns from the current
29.6 ns. This is a target range, not the sum of individual estimates.

## Cache Miss and Recompute

### 1. Add a completed `Task<T>` invoker fast path

`InterceptedObjectAsyncInvokerFactory<T>` already handles a synchronously completed
`ValueTask<T>` directly, but its `Task<T>` path uses a continuation even when the
task is already complete. Read a successfully completed `Task<T>` directly and
retain the existing asynchronous continuation path for incomplete tasks.

Expected whole-recompute improvement: **5-10%**, high confidence, with an expected
reduction of roughly 70-100 B per operation.

Status: **Closed — retained.**

Remarks: the focused completed-`Task<Unit>` adapter improved from 51.53 ns and
144 B to 3.674 ns and 24 B per call, a 92.9% component improvement with 120 B
removed. The end-to-end recompute benchmark changed from 1.683 us and 1,144 B to
1.480 us and 944 B; its short timing sample was noisy, but the 200 B allocation
reduction was deterministic. Eight focused tests cover completed, incomplete,
faulted, and canceled generic and non-generic tasks.

### 2. Use a synchronous `ValueTask<Computed>` production core

Retain the public `Task<Computed>` contract but allow the internal production path
to complete synchronously without creating a new `Task`. The asynchronous adapter
must retain the current `AsyncLocal` lock marker behavior: simply removing `async`
from the current method is unsafe because its `ExecutionContext` isolation is part
of the locking protocol.

Expected whole-recompute improvement: **2-5%**, medium confidence, potentially
avoiding one task-sized allocation.

### 3. Try the normal registry insertion before entering collision handling

The normal case is a missing key. Attempt `TryAdd` first, and enter the current
state/collision loop only when another computed value already occupies the key.
Preserve registration callbacks, remote-computed handoff, and synchronous
predecessor invalidation.

Expected whole-recompute improvement: **1-2%**, medium-low confidence.

Combined recompute target: **8-15%**, approximately 2.45-2.65 us from the current
2.876 us. Lock-set improvements may contribute another **2-6%** to the whole
recompute path.

## Invalidation

### 1. Add an invalidation-specialized interceptor result path

After performing the same input lookup and invalidation, return the method's
already-cached `MethodDef.DefaultResult`. Avoid the general compute-option routing,
`GetValueOrDefaultAsTask`, and untyped default-task dictionary lookup.

Expected raw-invalidation improvement: **4-8%**, medium-high confidence.

Status: **Closed — retained.**

Remarks: after increasing the end-to-end fixture to 1,024 disjoint 100-call
batches, two baseline and two optimized runs produced aggregate means of 68.86 ns
and 61.78 ns and aggregate medians of 66.49 ns and 63.61 ns, respectively. That is
a 10.3% mean and 4.3% median improvement; allocation was unchanged in the isolated
run. The fast path remains inside the resolved `ComputeMethodFunction` handler, so
it does not duplicate proxy-slot routing. Tests cover `Task<T>`, `ValueTask<T>`,
canceled call tokens, completed default results, and actual invalidation.

### 2. Carry the registry slot through unregistration

Carry the registry's weak-reference token from lookup to unregistration so the
remove path does not repeat `TryGetValue` and `TryGetTarget`. Use an atomic
conditional remove only after the computed value is invalidated.

Expected raw-invalidation improvement: **3-7%**, medium confidence.

### 3. Add an empty-leaf finalization path

When invalidation handlers, dependencies, and dependants are all empty, skip their
enumeration and clearing machinery. Retain the current nested `try/finally`
structure for every non-empty case.

Expected raw-invalidation improvement: **2-5%**, medium confidence.

Combined raw-invalidation target: **8-16%**, approximately 584-639 ns from the
current 695 ns.

### Cascade-specific edge cleanup

An internal `InvalidateFrom(parent, source)` path can avoid asking a child to
remove the initiating edge from a parent that already owns and clears that edge.
The parent must be carried separately from `InvalidationSource`, because
origin-only propagation can collapse the public source to the origin.

Expected cascade improvement: **8-20%**, with negligible raw-invalidation impact.

## AsyncLock

### Benchmark shapes

- Concrete typed uncontended acquire/release.
- Interface-based uncontended acquire/release, reported separately because the
  current adapter and boxed releaser have different costs.
- One owner and one waiter on the same lock, measuring framework handoff after
  release rather than arbitrary owner hold time.
- Checked and unchecked/reentry-free modes where both are production-relevant.

### Candidates

1. Use existing `SimpleAsyncLock` at proven unchecked call sites: **5-12%**
   uncontended, medium confidence.
2. Outline reentry and incomplete-wait paths so the concrete uncontended path can
   inline: **3-8%** uncontended, medium-low confidence.
3. Avoid redundant `ExecutionContext` capture in the incomplete-wait helper while
   preserving caller-context restoration and reentry state: **5-15%** of software
   handoff overhead, medium-low confidence.
4. If interface acquisition is important, replace its always-async adapter with a
   completed bridge: **15-30%** interface-based uncontended improvement, with no
   expected effect on concrete calls.

Contended percentages apply only to the software handoff interval after release.

## AsyncLockSet

### Benchmark shapes

- Uncontended acquisition and release for a key absent from the set.
- Repeated acquisition of the same key without contention.
- One owner and one waiter on the same key, measuring handoff after release.
- A mixed-key case to reveal dictionary and entry-lifecycle costs independently
  of same-key contention.

### Candidates

1. Fuse release with `EndUse`: when the holder is the sole user, close, remove,
   and dispose the entry while the semaphore is still acquired instead of
   releasing a semaphore that is immediately disposed. Racing users must either
   increment before close or observe closed state and retry. Expected improvement:
   **8-15%** uncontended, medium-high confidence.
2. Replace the monitor-protected entry lifecycle with one interlocked
   open/refcount/closed state while preserving retry and conditional removal.
   Expected improvement: **8-18%** uncontended and **1-5%** handoff, medium
   confidence.
3. Avoid redundant `ExecutionContext` capture in the internal incomplete-wait
   helper. Expected improvement: **5-12%** of handoff overhead, medium-low
   confidence.

The first two changes overlap; their combined target is **15-25%**, not their sum.
A custom inline async gate could potentially improve uncontended acquisition by
25-40%, but it is not a first-line change because it recreates cancellation,
reentry, disposal, and waiter-scheduling semantics already supplied by
`SemaphoreSlim`.

## Changes Not Recommended

- Do not retain keyed lock entries indefinitely; that changes the lock set's
  memory behavior.
- Do not use striped keyed locks; false contention is an observable performance
  regression.
- Do not enable synchronous waiter continuations without proving scheduling and
  stack-safety equivalence.
- Do not pool mutable invocation arguments or computation contexts until their
  ownership and `ExecutionContext` lifetime are proven.
- Do not remove the compute-production async boundary without preserving its
  `AsyncLocal` isolation.

## Proposed Implementation Order

1. Completed `Task<T>` invoker fast path.
2. Invalidation-specialized default-result path.
3. Fused `AsyncLockSet` release and close.
4. Alternate computed-registry lookup.
5. Registry-token unregistration.
6. Remaining leaf-path, continuation, and lifecycle refinements.

Each item should be a separate, revertible commit after benchmark validation.
Registry changes from different workstreams must be serialized to avoid agents
editing the same state machine concurrently.

The invalidation-specialized result path and alternate registry lookup may depend
on proxy method slots and should follow its binding and `MethodDef` identity
decisions when that work is active in parallel.

## Parallel Workstreams

Implementation can be split into four workstreams:

1. Cached hit: alternate registry lookup, then leaf-path restructuring.
2. Recompute: completed-task dispatch, then production and registration fast paths.
3. Invalidation: specialized result handling, leaf finalization, and cascade edge
   cleanup.
4. Locks: add lock/lock-set benchmarks, then implement and measure lifecycle and
   handoff changes.

The registry-token work overlaps cached lookup, recomputation registration, and
invalidation. Assign it to one workstream only after the independent changes land.

## Validation

For every candidate:

1. Add or update focused correctness tests before changing behavior-sensitive code.
2. Run the smallest affected test project, then the CI solution filter when the
   local change warrants it.
3. Compare BenchmarkDotNet results on .NET 10 with the same job, argument shape,
   invocation count, and operation normalization.
4. Inspect generated code or JIT disassembly when the claim depends on inlining,
   devirtualization, branching, or allocation elimination.
5. Keep the commit only for a repeatable improvement greater than 1%; record
   allocation and tail-latency changes as well as the mean.
6. Run contention and race tests for every lock, registry, or invalidation-state
   change, including cancellation and interceptor reassignment where applicable.
