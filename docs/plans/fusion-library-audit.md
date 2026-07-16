# Fusion Library Audit

This document records correctness, reliability, security, API-contract, concurrency, performance, compatibility, and test-coverage gaps found while reviewing every non-test C# project in ActualLab.Fusion. Tests are evidence for the audit, not its boundary: existing tests are read alongside production code, and important behavior without a test is recorded even when the current suite is green. The TypeScript workspace is explicitly out of scope; it has a separate completed port audit.

**Audit status (2026-07-15): complete.** Every non-test C# project under `src`, `samples`, `build`, and `docs` was reviewed; TypeScript and test-only projects were excluded as audit subjects. The audit records 124 open findings plus explicitly separated investigations. Deterministic missing coverage was closed with focused correct-contract tests and centrally executed; these audit slices intentionally remain red until the corresponding production fixes are implemented.

**Build baseline (2026-07-15):** `ActualLab.Core` builds successfully across all nine declared targets (`net10.0` through `netcoreapp3.1` and `netstandard2.0/2.1`). The complete default-target solution, including all library, sample, docs, build, and test projects, builds with zero errors. Remaining build warnings are recorded in the validation log.

Confidence labels:

- **Confirmed** — the complete failure chain is established from current source, or an executable reproduction demonstrates it.
- **High** — the implementation and trigger are established, but no focused reproduction has been run yet.
- **Plausible** — a concrete mechanism exists, but reachability or impact still needs verification. Plausible items remain in the investigation notes until promoted; they are not counted as findings.

Finding IDs use the production project or cluster abbreviation listed in the project inventory. Every confirmed finding carries current status, confidence, source/test evidence, impact, and Recommended/Alternative actions.

## Method

Each project passes the same audit gates:

1. Inventory its public surface, internal clusters, dependencies, conditional compilation, generated code, and target-framework branches.
2. Map implementation types and risky paths to existing tests; identify behavior that is untested or tested only indirectly.
3. Review state machines, ownership and disposal, cancellation, exception paths, concurrency, serialization boundaries, compatibility code, and unbounded retention.
4. Trace cross-project contracts into callers and implementations rather than judging files in isolation.
5. Run the narrowest useful existing tests, then add temporary probes only when static evidence cannot settle a suspected issue. Temporary artifacts belong under `tmp/`.
6. Re-read every promoted finding adversarially and record counter-evidence or intentional-contract explanations.

## Reuse

### Existing abstractions to reuse

The audit uses `docs/api-index.md` and, when a proposed fix needs deeper discovery, `docs/api-index-full.md` to avoid recommending helpers that already exist. Relevant catalogued primitives include `Result<T>`, `Option<T>`, `AsyncLock`, `AsyncLockSet<TKey>`, `WorkerBase`, `BatchProcessor<T, TResult>`, `AsyncState<T>`, `TransiencyResolver`, `RetryDelayer`, `RetryPolicy`, `TimerSet<T>`, `ConcurrentTimerSet<T>`, the serializer interfaces, and the RPC/Fusion state and tracker abstractions.

### Reusability of new components

This audit itself introduces no runtime component. For any later fix that requires a new reusable primitive, the report will compare feature-local placement with `ActualLab.Core` and recommend shared placement when the contract is broadly useful.

## Project inventory and progress

Production code under `src/` currently contains 22 C# projects and about 97K C# lines. C# samples, build tooling, and documentation executables are audited after the shipped libraries because they exercise integration contracts but have a different risk profile.

| Cluster | Projects/packages | Status |
|---|---|---|
| Core | `ActualLab.Core` | Source audit and legacy-target regression run complete |
| Interception and generation | `ActualLab.Interception`, `ActualLab.Generators` | Source audit complete; regression/build repros recorded |
| Command pipeline | `ActualLab.CommandR` | Source audit and central regression run complete |
| RPC | `ActualLab.Rpc`, `ActualLab.Rpc.Server`, `ActualLab.Rpc.Server.NetFx` | Source audit and central regression run complete |
| Serialization | `ActualLab.Serialization.NerdbankMessagePack` | Source audit and representative regression run complete |
| Fusion kernel and services | `ActualLab.Fusion`, `ActualLab.Fusion.Ext.Contracts`, `ActualLab.Fusion.Ext.Services` | Source audit and central deterministic regressions complete |
| Web and authentication | `ActualLab.Fusion.Blazor`, `ActualLab.Fusion.Blazor.Authentication`, `ActualLab.Fusion.Server`, `ActualLab.Fusion.Server.NetFx` | Source audit and central deterministic regressions complete |
| Persistence and infrastructure | `ActualLab.Fusion.EntityFramework`, `.Npgsql`, `.Redis`, `ActualLab.Redis` | Source audit and central deterministic regressions complete |
| Supporting libraries | `ActualLab.Plugins`, `ActualLab.RestEase`, `ActualLab.Testing` | Source audit and central regression run complete |
| Integration surfaces | non-test samples, `build`, and executable docs tooling | Source audit and final builds complete |

Test projects and test-only runners are excluded as audit subjects, but their code is reviewed as evidence. Generated `bin/` and `obj/` output is excluded except when compiler-generated output is necessary to understand a warning or runtime contract.

## Severity overview

- **RPC authorization bypasses** — RPC1 dispatches an arbitrary first method before handshake validation; RPC2 lets frontend peers invoke backend-only services. FUS14 independently ignores an explicit request not to expose the backend endpoint.
- **Tenant/user isolation failures** — FUS13 authorizes longer user/session namespaces through raw prefix matching; INTG3's optional Todo service ignores sessions entirely.
- **Process crash/hang/DoS** — INT1 can terminate the process through generated struct invocation IL; CORE1 permanently spins after a synchronous disposal failure; RPC12 accepts unbounded fragmented WebSocket messages; CORE2 and RPC10 can leave downstream operations permanently incomplete.
- **Silent data loss/corruption** — CORE3/4 can expose or retain pooled contents, CORE7 drops ring-buffer items, RPC8 corrupts hash-key semantics, PERS1 can delete young operation-log rows, and PERS3 can duplicate supposedly atomic sequence numbers.
- **Lifecycle and completion false-success** — FUS2/FUS6, RPC4/5, SUP2, INTG2/10, and TESTING3 complete or exit before required work/cleanup succeeds.
- **Security-relevant redirects/session handling** — FUS9 continues downstream work after an invalid-session redirect, FUS10 turns malformed cookies into server errors, FUS11 accepts suffixes in attacker-controlled hosts, and FUS15/16 accept external redirect targets.
- **Framework state/invalidation integrity** — FUS20-25 expose wrong state values, infinite invalidation-source enumeration, swallowed cancellation, skipped/retained subscribers, and observer exceptions that break computation.
- **Provider/configuration hazards** — PERS2 merges typed Redis configurations; PERS6-8 generate invalid/crashing Npgsql SQL; RPC9 and FUS27 leave supposedly frozen or enabled configurations mutable/incomplete.
- The remaining findings are medium/low boundary, diagnostics, compatibility, resource-lifetime, API-shape, generator, and sample robustness defects documented in their project sections.

## A. ActualLab.Core

Maintainer implementation approval: the recommended action is approved for every actionable CORE item unless its section records a more specific decision. CORE8 is invalid, CORE25 is ignored, CORE26 requires an efficient implementation trial and maintainer review, CORE28 uses the chosen always-create API direction, and CORE29 is limited to truthful pre-cancellation. CORE20's performance constraint and CORE22's usage-scan safety verdict remain mandatory implementation guidance.

### Audit coverage

- Build/package configuration: all declared target frameworks build; publish/trimming behavior remains under investigation.
- Async, locking, concurrency, channels, and worker lifecycle: complete.
- Collections, pooling, and caching: complete.
- Time, timers, retry, and networking: complete.
- Serialization, API wrappers, conversion, reflection, and compatibility: source review and pre-.NET 7 cancellation regression complete.
- IO, text, hashing, diagnostics, DI, and remaining utilities: complete.

### Investigation notes

- **AOT-1 — generated MessagePack resolver emits IL2055 twice.** Plausible compatibility gap. The default build enables the trim analyzer and reports calls to `Type.MakeGenericType` that cannot be statically analyzed. Determine whether supported NativeAOT/trimming scenarios preserve the constructed formatter types, whether the warning is expected upstream generator behavior, and whether a publish-level test exists before promoting this item.
- Broad exception catches and synchronous `GetAwaiter().GetResult()` calls were inventoried. Most occur in completion fast paths or deliberate exception suppression and are not findings without a traced failure mechanism.
- `UnbufferedPushSequence<T>.Complete(error)` was probed because its iterator handles `ChannelClosedException` specially. The arbitrary error is correctly rethrown through `Result<T>.Value`; this is not a finding.

### CORE1. `SafeAsyncDisposableBase` spins forever after a synchronous disposal-override failure

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source: `src/ActualLab.Core/Async/SafeAsyncDisposableBase.cs:18-42`. Both entry points first change `_isDisposing` from 0 to 1 and only assign `_disposeTask` after calling the abstract `DisposeAsync(true)` override. If that call throws synchronously, `_isDisposing` remains 1 and `_disposeTask` remains null.
- Failure: every later `DisposeAsync` takes the already-disposing branch and spins until `_disposeTask` becomes non-null. No code can publish it after the initiating call has unwound, so the loop is permanent and consumes a thread. `Dispose()` leaves the same poisoned state, although later synchronous `Dispose()` calls return instead of spinning.
- Reproduction: a minimal subclass whose override throws synchronously produced `dispose: first call threw` followed by `dispose: second call completed=False` after a 500 ms deadline. The probe is under ignored `tmp/fusion-audit-probes` and is not production code.
- Reachability: the type is public and designed for subclassing. Current in-repo overrides are mostly `async` methods or return already-created tasks, which normally convert failures into faulted tasks; this lowers immediate in-repo frequency but does not satisfy the base-class contract for external or future subclasses.
- Impact: shutdown or cleanup can hang permanently after the original disposal error, obscuring that error and potentially blocking host termination.
- **Resolution:** `StartDispose` now converts a synchronously thrown override exception into a faulted task, publishes it to `_disposeTask`, and returns that same task while preserving the single-invocation protocol. Both `Dispose()` and `DisposeAsync()` therefore leave a terminal task for later callers instead of poisoning the instance.
- **Validation:** focused tests cover both entry points, require the faulted task to be published and reused, and verify the override runs once; both pass without invoking the former infinite-spin path.
- **Alternative:** replace the two-field spin protocol with a single atomically published `TaskCompletionSource`/task representing ownership and completion. This makes publication ordering explicit but is a larger change for a small base class.

### CORE2. Pre-cancellation escapes `ConcurrentTransform` and leaves the destination channel open

Status: **completed**.

Confidence: **Confirmed** by executable probe; present in both overloads.

- Source: `src/ActualLab.Core/Channels/ChannelExt.Transforms.cs:71-122` and `:124-175`. Worker bodies catch cancellation, optionally copy it to the destination, and suppress it when `ChannelCopyMode.Silently` is set. However, each worker is started with `Task.Run(Worker, cancellationToken)`.
- Failure: if the token is already canceled, `Task.Run` returns a canceled task without invoking `Worker`. The worker's cancellation handler never runs; `Task.WhenAll` throws from the outer method even for `CopyAllSilently`, and the post-`WhenAll` completion path is skipped. The destination writer remains open, so a consumer awaiting its completion can hang.
- Reproduction: with a pre-canceled token, both a source and destination unbounded channel, concurrency 2, and `CopyAllSilently`, the probe produced `transform: cancellation escaped` and `transform: target completed=False`.
- Tests: `tests/ActualLab.Tests/Channels/TransformTest.cs` covers successful synchronous and asynchronous transformations and concurrency timing, but no cancellation or error path exposes this boundary.
- Impact: callers relying on the advertised silent/copy flags see an unexpected exception, while downstream readers receive neither cancellation nor completion.
- **Resolution:** both overloads now schedule workers with `CancellationToken.None`; the existing in-worker waits and copy-mode-aware handlers remain the sole cancellation boundary, including when the supplied token is already canceled.
- **Validation:** focused pre-cancellation tests for the synchronous and asynchronous transformer overloads pass with `CopyAllSilently` and confirm that the destination channel reaches completion without cancellation escaping.
- **Alternative:** keep cancellation-aware scheduling but wrap worker creation/`Task.WhenAll` in the same copy-mode-aware cancellation handling and complete the writer there. This duplicates the worker policy and is easier for the two overloads to drift.

### CORE3. `ArrayBuffer<T>.CopyTo` copies capacity rather than `Count`

Status: **completed**.

Confidence: **Confirmed** by source and executable probe.

- Source: `src/ActualLab.Core/Collections/ArrayBuffer.cs:153-154` calls `Buffer.CopyTo(array.AsSpan(arrayIndex))`. `Buffer` is the full array rented from `ArrayPool<T>.Shared`; the logical contents are `Span`, which is limited to `Count`.
- Failure: a buffer containing two items with a 16-element lease throws `ArgumentException` when copied to a correctly sized two-element destination because it attempts to copy all 16 elements. If the destination is large enough, it copies unused pooled slots as well, including stale values not belonging to the logical buffer.
- Tests: `ArrayBufferTest` exercises `ToArray`, `ToList`, mutation, growth, and clearing, but never calls `CopyTo`.
- Impact: the method violates collection copy semantics, can unexpectedly throw, and can expose stale pooled values to the destination.
- **Resolution:** `CopyTo` now copies `Span`, limiting the operation to the logical `Count` while preserving the destination offset.
- **Validation:** the regression copies a two-item buffer into a correctly sized destination and passes with exactly those two values.
- **Alternative:** use `Buffer.AsSpan(0, Count).CopyTo(...)`; equivalent but duplicates the `Span` property.

### CORE4. Pool-buffer resize ignores `MustClear`

Status: **completed**.

Confidence: **Confirmed** by dependency contract and recording-pool probe; duplicated in both implementations.

- Source: `src/ActualLab.Core/Collections/ArrayPoolBuffer.cs:303-304` and `RefArrayPoolBuffer.cs:289-290` call CommunityToolkit's `Pool.Resize(ref array, newSize)` without its optional `clearArray` argument. The resolved 8.4.0 API defaults that argument to `false`, while both buffer types expose and otherwise honor `MustClear`.
- Failure: when a `mustClear: true` buffer grows, the old array is returned to the pool uncleared. A recording pool observed `False` for the resize return and `True` only for final disposal.
- Scope: the default constructor enables `MustClear` for reference-containing types, so this is not limited to callers explicitly opting in.
- Impact: old references remain retained by the pool and potentially visible to another renter in the same process; sensitive payloads are not scrubbed as the API promises. It also increases object-lifetime and memory-retention pressure.
- **Resolution:** both `ResizeBuffer` implementations now pass `MustClear` to CommunityToolkit's pool resize operation, so a growth return follows the same clearing policy as final release.
- **Validation:** recording-pool regressions pass for both `ArrayPoolBuffer<T>` and `RefArrayPoolBuffer<T>`, observing `clearArray=true` on resize and final release.
- **Alternative:** replace the extension call with explicit rent/copy/return using `MustClear`; more code with no clear benefit unless custom capacity behavior is also needed.

### CORE5. Oversized pool-buffer size hints wrap to a 16-element span

Status: **completed**.

Confidence: **Confirmed** by executable probe; duplicated in `ArrayPoolBuffer<T>` and `RefArrayPoolBuffer<T>`.

- Source: `ArrayPoolBuffer.cs:210-215, 333-334` and `RefArrayPoolBuffer.cs:195-200, 293-294`. Capacity is computed with unchecked integer addition and by casting the next power-of-two `ulong` back to `int`, then applying `Math.Max(MinCapacity, ...)`.
- Failure: `GetSpan(int.MaxValue)` requests a next power of two of 2^31; the cast becomes `int.MinValue`, and `Math.Max` converts that to 16. The probe returned a 16-element span for a 2,147,483,647-element size hint. A nonzero position can also overflow earlier in `_position + sizeHint` and skip resizing entirely.
- Contract: `IBufferWriter<T>.GetSpan/GetMemory` require a nonzero size hint to be satisfied with at least that much writable space or rejected; returning a smaller buffer is invalid.
- Impact: large or attacker-influenced lengths produce late, confusing failures in serializers/writers instead of a deterministic range/capacity exception. Span bounds preserve memory safety, but consumers can fail after partial work.
- **Resolution:** both implementations now share `ArrayPoolBufferCapacity`, which validates nonnegative hints, computes required capacity without integer wraparound, rejects capacities above the supported `1 << 30` limit, and only then rounds with `Bits.GreaterOrEqualPowerOf2`.
- **Validation:** focused regressions pass for both buffer types with a nonzero position and `int.MaxValue` size hint, producing deterministic `ArgumentOutOfRangeException` results instead of undersized spans.
- **Alternative:** use `Array.MaxLength` plus `ArrayPool<T>.Rent` validation as the bound. This follows runtime array limits but still needs checked addition.

### CORE6. `BinaryHeap` source constructor ignores its comparer while building the heap

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source: `src/ActualLab.Core/Collections/BinaryHeap.cs:33-38`. The constructor initializes `_heap` with `source.OrderBy(..., _comparer)` before assigning `_comparer = comparer ?? Comparer<TPriority>.Default`; the readonly field therefore has its default null value during ordering, which means the default comparer is used.
- Failure: a heap constructed from priorities 1, 2, 3 with a descending comparer reports 1 as its minimum; under that comparer, 3 must be the root/minimum. Later mutations use the supplied comparer against an already-invalid heap.
- Tests: `BinaryHeapTest` covers incremental `Add` with the default comparer only; it does not exercise the source constructor or a custom comparer.
- Impact: `PeekMin`, `ExtractMin`, enumeration, and later additions can return the wrong order for every non-default comparer.
- **Recommended:** assign `_comparer` first and order the source with that assigned comparer. The current O(n log n) sort is otherwise a valid heap representation.
- **Alternative:** materialize and heapify bottom-up with the comparer. This improves construction complexity but is a broader algorithm change.
- Implementation: the source constructor now initializes `_comparer` before using it to order the source.
- Validation: `BinaryHeapSourceConstructorMustUseCustomComparer` failed with priority 1 before the fix and passes with
  priority 3 after it; the surrounding `BinaryHeapTest` suite also passes.

### CORE7. Wrapped `RingBuffer<T>` enumeration drops items

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source: `src/ActualLab.Core/Collections/RingBuffer.cs:43-51`. When `_end < _start`, the enumerator linearizes the end by adding `Capacity`. `Capacity` is the mask and equals backing-array length minus one; wrapping requires adding the full backing-array length.
- Failure: in a capacity-3 buffer, enqueue 1/2/3, pull 1, and enqueue 4. `ToArray()` correctly returns `2,3,4`, while enumeration returns only `2,3`.
- Tests: `RingBufferTest` compares enumeration before its mutations, then performs pull-and-push operations that restore the same head/tail positions. It does not enumerate a genuinely wrapped state, so the existing test passes without exposing the loss.
- Impact: LINQ and `foreach` silently omit elements while indexed access and `ToArray` disagree, producing data loss in consumers with no exception.
- **Recommended:** add `_buffer.Length` (equivalently `Capacity + 1`) when linearizing `_end`.
- **Alternative:** implement enumeration over `Count` and use `(_start + index) & Capacity`; slightly simpler to reason about and naturally matches the indexer, with one mask operation per item.
- Implementation: wrapped enumeration now advances `_end` by the full backing-array length.
- Validation: `RingBufferMustEnumerateEveryWrappedItem` failed with `2,3` before the fix and passes with `2,3,4`
  after it; the surrounding `RingBufferTest` suite also passes.

### CORE8. `VersionSet` caches data derived from a caller-mutable dictionary

Status: **invalid — accepted mutable-input contract**.

Confidence: **Confirmed by maintainer clarification**.

- Source: `src/ActualLab.Core/Collections/VersionSet.cs:20-23, 28-50`. The public primary constructor stores an arbitrary `IReadOnlyDictionary<string, Version>` by reference. `Value` and `HashCode` are lazily cached, while `Count`, the indexer, and equality continue reading the live dictionary.
- Verdict: callers are responsible for not mutating the supplied dictionary after cached values are observed. The behavior is accepted and is not an actionable audit finding.

### CORE9. `TimerSet<T>` priority cursor overflows while heap priorities remain monotone

Status: **completed**.

Confidence: **High**; the overflow and post-overflow failure are established directly from the state transition, but the natural-duration boundary has not been waited out in a test.

- Source: `src/ActualLab.Core/Time/TimerSet.cs:10, 30-32, 94-108, 124-125`. Heap priorities and public priority APIs are `long`, but `_minPriority` is `int` and increments unchecked once per quantum. `TimerSetOptions.MinQuanta` is declared as 10 ms but never validated or applied.
- Failure: after `int.MaxValue`, `_minPriority` wraps negative while `RadixHeapSet.MinPriority` remains near `int.MaxValue`. Extraction with the negative cursor returns no due entries; adding a normal current priority is clamped against the negative cursor, then rejected by the radix heap because it is below the heap's monotone minimum. The worker swallows its terminal exception through `WorkerBase`, so the set silently stops firing.
- Reachability: at the declared but unenforced 10 ms minimum, overflow occurs after about 248.6 days. An allowed 100 ns `TickSource` causes the run loop to catch up thousands of quanta per scheduler tick, making overflow reachable in minutes. Long-lived server processes can also hit the boundary with ordinary low quanta.
- Tests: timer tests cover seconds of scheduling with 10–100 ms tick sources; no boundary manipulates or validates the priority cursor.
- Impact: existing timers stop firing and new timer registration begins throwing after the boundary, potentially disabling keep-alive, cache expiry, or application timeouts.
- **Recommended:** make `_minPriority` a `long` and enforce a supported quanta/range invariant in `TimerSetOptions` construction. The existing `MinQuanta` should either be enforced or removed in favor of an explicit documented bound; ensure the radix heap's 45 buckets cover the chosen lifetime.
- **Alternative:** retain an `int` rolling cursor and periodically rebase the heap/start epoch before overflow. This is substantially more complex and riskier than matching the existing long-priority API.
- Implementation: `_minPriority` is now a `long`, and `TimerSetOptions.TickSource` rejects periods shorter than the
  existing 10 ms `MinQuanta`. At that minimum, the 45-bucket heap covers more than five millennia of priorities.
- Validation: a deterministic test seam advances the cursor and radix heap across `int.MaxValue`; it failed against
  the old `int` field and passes after the fix. A second regression confirms sub-minimum quanta are rejected, and the
  surrounding `ConcurrentTimerSetTest` suite passes.

### CORE10. `ClockExt.Interval(IEnumerable<TimeSpan>)` shares one enumerator across subscribers

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source: `src/ActualLab.Core/Time/ClockExt.cs:55-76`. `intervals.GetEnumerator()` executes before `Observable.Create`, so every subscription closes over the same mutable enumerator; the first subscription also disposes it.
- Failure: an observable built from two zero-length intervals emitted `0,1` to its first subscriber and completed its second subscriber with no values. Concurrent subscribers additionally race `MoveNext`/`Current` and disposal on an enumerator that is not required to be thread-safe.
- Tests: `ClockTest.IntervalTest` creates one subscription via `ToEnumerable`; it does not resubscribe to the same observable and therefore does not expose the shared state.
- Impact: an API that otherwise behaves like a cold Rx interval becomes single-use, causing silently missing ticks or concurrency failures in independent consumers.
- **Recommended:** create and dispose the interval enumerator inside the `Observable.Create` subscription delegate so each subscriber gets independent state.
- **Alternative:** explicitly publish/share the observable and document it as hot/single-sequence. This would be a surprising contract change and still requires synchronization for concurrent subscribers.
- Implementation: each `Interval(IEnumerable<TimeSpan>)` subscription now creates and disposes its own enumerator.
- Validation: `EnumerableIntervalMustBeColdPerSubscription` failed because the second subscription was empty before
  the fix and passes with `0,1` from both subscriptions after it; the surrounding `ClockTest` suite also passes.

### CORE11. Optional-parameter overloads make convenience calls ambiguous

Status: **completed**.

Confidence: **Confirmed** by compiler diagnostic and overload signatures.

- Source: `src/ActualLab.Core/IO/FilePathExt.cs:86-99, 104-114` exposes `WriteText` and `WriteLines` with one overload containing an optional `CancellationToken`, then a second whose extra argument and token are also optional. `src/ActualLab.Core/Locking/FileLock.cs:25-29` repeats the same shape for static `Lock`. The generic `RetryPolicyExt.Run` and `RunIsolated` pairs at `src/ActualLab.Core/Resilience/RetryPolicyExt.cs:60-75, 90-107` likewise differ only by an optional `RetryLogger` inserted before an optional token.
- Failure: the intended convenience calls `path.WriteText(contents)`, `path.WriteLines(lines)`, `FileLock.Lock(path)`, `policy.Run<T>(factory)`, and `policy.RunIsolated<T>(factory)` each match two overloads equally. A probe using `WriteLines` failed with CS0121 and named both overloads; the other pairs have the same overload-resolution shape.
- Reachability: in-repo production code does not call these extensions, so the build stays green. They are public helpers whose shortest, most discoverable invocation is unusable by consumers.
- Impact: source consumers get a compile-time error for the advertised convenience API and must discover a non-obvious named or positional argument to disambiguate it.
- **Recommended:** make the extra `encoding`/`retryIntervals` argument non-optional on the longer overloads, leaving the short overloads as the unique default entry points. Existing calls that explicitly pass the extra argument remain source-compatible.
- **Alternative:** remove the short overloads and retain optional encoding. This makes the natural call compile, but is a binary compatibility break and removes the explicit cancellation-token position.
- Implementation: the longer `FilePathExt`, `FileLock`, and generic `RetryPolicyExt` overloads now require their
  encoding, retry-interval, or retry-logger argument while retaining the same binary signatures.
- Validation: the compile-contract consumer produced five CS0121 diagnostics before the fix and now lives in the
  standard `ActualLab.Tests` build graph, which compiles successfully using all five natural convenience calls. All
  nine `ActualLab.Core` target frameworks build.

### CORE12. `FilePath.WriteLines` leaves stale bytes when replacing a longer file

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source before the fix: `src/ActualLab.Core/IO/FilePathExt.cs:110-128` opened the target with `File.OpenWrite`, which uses `FileMode.OpenOrCreate`, writes from position zero, and never truncates the stream after the new content.
- Failure: replacing the text `this is much longer` with one line containing `x` produced `x` followed by the untouched suffix `s is much longer`. The result is neither the old nor requested line sequence.
- Tests before the fix: there were no direct `FilePathExt.WriteLines` tests or in-repo production callers. This lack of reachability does not change the public file-replacement contract.
- Impact: configuration, manifests, generated source, or other line-oriented files become silently corrupted whenever new output is shorter than their prior contents.
- **Implemented:** `WriteLines` now opens the output through `FileStream` with `FileMode.Create`, `FileAccess.Write`, and `FileShare.Read`, truncating stale content while preserving concurrent readers.
- **Validation:** `WriteLinesMustTruncateExistingFile` failed against the old implementation and now passes; the complete nine-target `ActualLab.Core` build also passes.
- **Alternative:** retain `OpenWrite` and call `SetLength(writer.Position)` after flushing the text writer. This is more fragile on exceptions and duplicates the approach already avoided by `File.WriteAllTextAsync`.

### CORE13. Generic `GetServiceOrCreateInstance<T>` drops activation arguments

Status: **completed**.

Confidence: **Confirmed** by source and executable probe.

- Source before the fix: `src/ActualLab.Core/DependencyInjection/ServiceProviderExt.cs:66-76`. Both overloads accepted `params object[] arguments`, but the generic overload omitted them when calling the non-generic overload and the non-generic overload omitted them when calling `CreateInstance`.
- Failure: resolving an unregistered class whose only constructor takes an integer through either overload threw `InvalidOperationException` because activation saw no applicable constructor argument.
- Reachability: current in-repo callers use no explicit arguments, so they are unaffected. The public generic API advertises argument-aware activation but cannot perform it.
- Impact: consumer activation fails at runtime for types requiring caller-supplied constructor values; optional or alternate constructors may instead be selected with unintended values, making the defect less obvious.
- **Implemented:** the generic overload now forwards `arguments` to the non-generic overload, which forwards them to `CreateInstance`; registered-service resolution remains unchanged.
- **Validation:** `GenericActivationMustForwardExplicitArguments` failed through the generic API before the fix and now verifies both generic and non-generic activation; the surrounding `ServiceProviderExtTest` suite passes.
- **Alternative:** remove the generic overload's params parameter. That makes the actual contract honest but is a source/API break and leaves the useful operation available only through the non-generic method.

### CORE14. `ApiArray<T>.Empty.WithMany` throws instead of adding items

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source before the fix: `src/ActualLab.Core/Api/ApiArray.cs:40-48, 173-188`. Empty arrays are intentionally represented by the default struct value, and the `Items` property normalizes its null backing field to a shared empty array. `WithMany`, unlike the other mutation-style helpers, read the primary-constructor parameter/backing field `items` directly.
- Failure: `ApiArray<int>.Empty.WithMany(1, 2)` throws `NullReferenceException` while evaluating `items.Length`. The same occurs for the default value and for the `addInFront` overload.
- Tests: `ApiArrayTest.WithManyTest` starts from a five-item instance and covers front/back order, but never starts from `ApiArray<T>.Empty`, even though default-as-empty is a central type contract.
- Impact: ordinary collection-building code fails specifically at its empty base case, including reducers that append batches to a default-initialized API value.
- **Implemented:** `WithMany` now captures the normalized `Items` array and uses it for sizing and both front/back copy paths.
- **Validation:** `EmptyApiArrayMustSupportWithMany` failed against the default backing field and now passes for both append and prepend operations; the full `ApiArrayTest` suite passes.
- **Alternative:** special-case `IsEmpty` and construct directly from `newItems`. This avoids the null access but duplicates front/back logic unnecessarily.

### CORE15. Empty `ApiMap` and `ApiSet` formatting leaks acquired builders from the thread-local pool

Status: **completed**.

Confidence: **Confirmed** by source inspection and executable regression.

- Source before the fix: `src/ActualLab.Core/Api/ApiMap.cs:108-118` and `ApiSet.cs:108-116` acquired a `StringBuilder` through `StringBuilderExt.Acquire`, but their empty-collection fast paths returned `sb.ToString()` rather than `sb.ToStringAndRelease()`. Non-empty paths released correctly. `StringBuilderExt` documents an acquire/release thread-local cache with up to 16 builders per thread.
- Failure: every empty collection `ToString` removes a reusable builder from the current thread's cache (or allocates a fresh one) and lets it become garbage instead of returning it. Repeated logging/diagnostics of empty API collections therefore defeats the intended allocation optimization.
- Impact: unnecessary allocation and GC pressure on a potentially common diagnostics path. This is a performance/resource-management defect, not retained-memory growth because abandoned builders remain collectible.
- **Implemented:** both empty fast paths now return through `ToStringAndRelease()`.
- **Validation:** deterministic fresh-thread pool-identity tests independently failed for `ApiMap` and `ApiSet` before the fix and now pass, without relying on timing or sampled allocation counts; `StringBuilderExtTest.BasicTest` also passes.
- **Alternative:** wrap each method in `try/finally` and release in the `finally`, which also protects against item `ToString` exceptions but is a broader hot-path change.

### CORE16. `HashRing` misroutes hashes when comparer subtraction overflows

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source before the fix: `src/ActualLab.Core/Scalability/HashRing.cs:61-72` implemented the binary-search comparer as `x.Hash - y.Hash`, then mapped the sign to -1 or 1. Signed subtraction can overflow and reverse the ordering for hashes on opposite sides of the integer range.
- Failure: a ring containing nodes hashed to `int.MinValue` and `int.MaxValue` routed both a query for `int.MaxValue` itself and a query for -1 to the `int.MinValue` node. Both should select `int.MaxValue` under the ring's documented higher-or-equal search behavior.
- Tests: `HashRingTest` uses three ordinary xxHash outputs and tests `hash ± 1`; it does not force the signed-overflow boundary.
- Impact: a small subset of the 32-bit hash space is deterministically routed to the wrong node. That breaks consistent ownership and can create avoidable cache misses, duplicate work, or cross-node disagreement when peers use a correct comparison.
- **Implemented:** `ItemComparer` now compares hashes relationally, avoiding overflow while preserving the intentional nonzero equality result used by the lower-bound insertion search.
- **Validation:** `HashRingMustHandleExtremeSignedHashes` failed for the signed range extremes before the fix and now passes; the full `HashRingTest` suite passes.
- **Alternative:** replace `Array.BinarySearch` plus its intentionally nonconforming comparer with a small explicit lower-bound search. This is easier to specify and handles duplicate hashes clearly, at the cost of custom search code.

### CORE17. Maglev shard-map construction divides by zero for one shard

Status: **completed**.

Confidence: **Confirmed** by executable probe.

- Source: `src/ActualLab.Core/Scalability/Internal/MaglevShardMapBuilder.cs:21-31`. For every positive shard count it computes `hash % (shardCount - 1) + 1`; when `shardCount == 1`, the divisor is zero.
- Failure: `Build(1, ["node"])` throws `DivideByZeroException` instead of assigning the sole shard to node zero.
- Tests: shard-map tests exercise larger maps and balance/disruption properties; the minimal nonempty map is not covered.
- Impact: callers cannot use the Maglev strategy for a valid single-shard configuration, which is a common development, migration, or scale-down state.
- **Resolution:** the builder now returns `[0]` immediately for a nonempty one-shard map, before permutation skip calculation.
- **Validation:** the focused one-shard regression failed with `DivideByZeroException` before the fix and now passes; the broader shard-map balance, reallocation, and stress suite also passes.
- **Alternative:** define a general permutation helper that returns `[0]` for size one and use it for every node. This centralizes the mathematical boundary but is more machinery than the fix requires.

### CORE18. Signed arbitrary-radix conversion mishandles `long` boundaries

Status: **completed**.

Confidence: **Confirmed** for formatting by executable probe and for parsing by checked-range analysis.

- Source: `src/ActualLab.Core/Mathematics/MathExt.cs:134-158, 194-223`. Signed formatting calls `Math.Abs(number)`, which throws for `long.MinValue`. Parsing accumulates into `long` with unchecked multiplication/addition and applies the sign afterward, with no overflow detection.
- Failure: `MathExt.Format(long.MinValue, 10)` throws `OverflowException`. Conversely, representations above `long.MaxValue` can wrap while `TryParseInt64` still returns `true`, violating the meaning of a try-parse API.
- Tests: `ParseFormatTest` covers small positive/negative integers and random values but omits `long.MinValue`, `long.MaxValue`, and out-of-range text.
- Impact: IDs or counters spanning the signed 64-bit domain cannot reliably round-trip, and malformed/oversized input can be accepted as a different valid number.
- **Resolution:** signed formatting now derives an unsigned magnitude without negating `long.MinValue`; parsing accumulates left-to-right into `ulong` against separate `long.MaxValue` and `2^63` limits, rejecting overflow and a bare sign.
- **Validation:** the boundary regression round-trips both signed extremes in radices 2, 10, and 64 and rejects values immediately outside the signed range; the existing randomized radix suite also passes.
- **Alternative:** delegate base-10 conversion to the runtime and retain custom checked logic only for other radices. This reduces one boundary but leaves two implementations to keep consistent.

### CORE19. `Factorial` fails to reuse cached lower factorials

Status: **completed**.

Confidence: **Confirmed** by source inspection.

- Source: `src/ActualLab.Core/Mathematics/MathExt.cs:45-63`. The downward search decrements `i` but calls `Factorials.TryGetValue(n, out f)` on every iteration rather than looking up `i`.
- Failure: if the exact requested factorial is absent, the loop performs `n` identical failed dictionary lookups and then recomputes every multiplication from 1 through `n`, even when `Factorials[n - 1]` and all smaller results are cached. A sequence of increasing requests repeats increasingly expensive `BigInteger` work.
- Tests: `MathExtTest` verifies values and one call at 1000, but does not exercise incremental cache reuse or performance.
- Impact: workloads using `Cnk` or increasing factorials can turn the intended memoized computation into quadratic-scale large-integer multiplication and lock occupancy, blocking all concurrent callers on the static cache lock.
- **Resolution:** the downward search now probes the decreasing index `i` and resumes multiplication at the first cached prefix.
- **Validation:** a deterministic cache-work regression seeds only `24!`, requests `25!`, and confirms the operation adds only key 25; before the fix it repopulated every key from 1 through 23. The full factorial suite passes.
- **Alternative:** keep only the largest cached `(n, value)` pair and extend it monotonically. This uses less dictionary memory but no longer accelerates requests below the maximum unless separately handled.

### CORE20. `RandomStringGenerator` is biased and cannot select alphabet entries beyond byte range

Status: **completed**.

Confidence: **Confirmed** by source analysis.

- Source: `src/ActualLab.Core/Generators/RandomStringGenerator.cs:42-57` maps one random byte to each character. Power-of-two alphabets use masking; every other alphabet uses `byte % alphabetLength`.
- Failure: modulo reduction is biased whenever the alphabet length does not divide 256 (including the common 62-character alphanumeric alphabet). For alphabets longer than 256 characters, the byte is always below the alphabet length, so indices 256 onward are never emitted at all despite being accepted by the constructor/method.
- Reachability: `DefaultAlphabet` has 64 characters and is unbiased, but `Base32Alphabet`, `Base16Alphabet`, and arbitrary custom alphabets are public options; a 62-character token alphabet is conventional.
- Impact: custom token/key generation has less entropy than its length implies, and large alphabets silently produce an incomplete character set. This matters for security-sensitive identifiers because the type explicitly uses cryptographic randomness.
- Maintainer constraint: the fix is required, but this is a frequently used hot path. Preserve the current power-of-two fast path, avoid per-character random-number calls and allocations, fill randomness in batches, and benchmark throughput plus allocations against the current implementation.
- **Resolution:** alphabets of power-of-two length up to 256 retain the original one-byte mask path. Other byte-sized alphabets use batched rejection sampling, while larger alphabets consume batched little-endian `uint` samples with masking for powers of two and rejection sampling otherwise. All paths reuse the existing pooled byte buffer and avoid per-character RNG calls or managed helper allocations.
- **Validation:** deterministic RNG tests distribute two complete byte cycles evenly across a three-character alphabet, reach index 256 in a 257-character alphabet, and confirm the default 512-character fast path uses one RNG fill with no more than 2 KiB total allocation. A release microbenchmark of the common 32-character default-alphabet path measured a 194.0 ns/op baseline and 193.5 ns/op after the fix (five 200,000-operation samples, medians), with allocations unchanged at 88 B/op. The existing generator suite passes.
- **Alternative:** restrict alphabets to power-of-two lengths no greater than 256. This preserves the fast byte mapping but is a significant and unnecessarily narrow public-contract change.

### CORE21. Inheritance cast converters violate `TryConvert` and untyped source contracts

Status: **completed**.

Confidence: **Confirmed** by source inspection.

- Source: `src/ActualLab.Core/Conversion/Internal/CastToDescendantConverter.cs:13-22` implements both try methods with direct casts, so an object of the declared base type but wrong runtime subtype throws. `CastToBaseConverter.cs:13-22` returns the untyped input unchanged without checking that it is a `TSource` at all.
- Failure: `Converter<object, string>.TryConvert(new object())` throws `InvalidCastException` rather than returning `Option.None<string>()`. Conversely, `Converter<int, object>.TryConvertUntyped("not an int")` reports the unrelated string as a successful conversion, and `ConvertUntyped` does the same despite declaring `int` as its source type.
- Tests: `ConverterProviderTest.CastTest` covers only inputs that already satisfy the target/source runtime types. Other converter tests establish that failed `TryConvert` should return `None`, making the cast behavior inconsistent with the same public abstraction.
- Impact: generic conversion pipelines cannot safely probe convertibility, and callers using the untyped API may receive a value that never matched the converter's declared source type.
- **Resolution:** cast converters now pattern-match their declared runtime source/target types for try operations and return `None` on mismatch; untyped throwing conversion explicitly validates the declared source type.
- **Validation:** the regression now covers typed and untyped descendant mismatch plus untyped base-converter source mismatch, including the throwing `ConvertUntyped` contract; the full converter-provider suite passes.
- **Alternative:** document cast converters as throwing and make all `TryConvert` callers catch cast exceptions. This contradicts the established converter contract and spreads exception-based control flow.

### CORE22. `Base64UrlEncoder` uses the wrong RFC alphabet and accepts truncated Unicode input

Status: **completed**.

Confidence: **Confirmed** by focused regression tests.

- Source: `src/ActualLab.Core/Text/Base64UrlEncoder.cs:25-39` maps `/` to `-` and `+` to `_`; RFC 4648 Base64URL requires `+` to `-` and `/` to `_`. The decoder at `:58-69` casts every UTF-16 character to `byte` without rejecting values above ASCII.
- Failure: encoding bytes `FB FF` returns `_-8` instead of the standard `-_8`. Decoding can accept a non-ASCII character when its low byte happens to be valid Base64, creating alternate textual representations.
- Impact: tokens are incompatible with standard Base64URL implementations, and noncanonical Unicode spellings can bypass string-level allowlists, cache keys, or validation while decoding to accepted bytes.
- Usage scan: all non-test C# projects were searched. Direct calls exist only in `GuidExt` and `ByteString`. The sole in-repository runtime consumer is `RpcClientPeer.ClientId`, which is passed through HTTP/WebSocket query strings or the loopback connection and used as an opaque server-peer identifier without decoding. `GuidExt.FromBase64Url` and both `ByteString` Base64URL wrappers have no non-test in-repository callers, so the decoder has no current internal production path.
- Safety verdict: implementing the recommended standards-correct fix is safe based on actual usage across the C# projects. The RPC client-ID text will change for affected GUIDs, but each ID is generated for a new peer and all server paths treat it opaquely; no persisted or decoded in-repository contract depends on the current swapped alphabet.
- Resolution: corrected both alphabet mappings and made decoding reject non-ASCII and standard-Base64 alphabet characters. Regression tests cover the RFC 4648 spelling, the inverse decode, non-ASCII input, and `+`/`/` rejection.
- **Recommended:** swap the two alphabet mappings in both directions and reject every input character outside the allowed ASCII Base64URL alphabet (plus only explicitly supported padding).
- **Alternative:** delegate to a runtime Base64URL implementation on supported targets and retain a corrected compatibility implementation for older targets.

### CORE23. `TypeRef.TypeName` throws for `None` and unqualified type names

Status: **completed**.

Confidence: **Confirmed** by focused regression test.

- Source: `src/ActualLab.Core/Reflection/TypeRef.cs:38-41` slices `AssemblyQualifiedName` at the first comma without handling `IndexOf` returning -1.
- Failure: `TypeRef.None.TypeName` throws `ArgumentOutOfRangeException`; the same happens for a caller-created `TypeRef` containing a type name without an assembly suffix.
- Impact: default-value-safe metadata code fails during logging, diagnostics, or optional type decoration.
- Resolution: `TypeName` now returns the full stored name when it has no assembly separator. Regression tests cover both `TypeRef.None` and an unqualified name.
- **Recommended:** return the whole value when no comma exists, naturally producing an empty name for `None`.
- **Alternative:** reject non-assembly-qualified constructor input and special-case only `None`; narrower but less tolerant than the current public string conversion suggests.

### CORE24. `MemberwiseCopier` attempts to write readonly properties and invoke indexers without arguments

Status: **completed**.

Confidence: **Confirmed** by two focused regression tests.

- Source: `src/ActualLab.Core/Reflection/MemberwiseCopier.cs:35-41` enumerates all selected properties and blindly calls parameterless `GetValue`/`SetValue`.
- Failure: an ordinary get-only property throws `ArgumentException` because no setter exists; an indexer throws `TargetParameterCountException`. Either aborts copying before later valid members are processed.
- Impact: the default public copier fails on common .NET object shapes and can leave targets partially updated.
- Resolution: the default policy now requires readable, writable, non-indexed properties while preserving an explicit filter's ability to opt into another policy. Regression tests cover readonly and indexed properties.
- **Recommended:** include only non-indexed properties with both readable and writable accessors unless a caller's explicit filter opts into a different policy.
- **Alternative:** expose separate configurable read/write/index policies; more flexible but unnecessary for restoring safe default behavior.

### CORE25. Version generators wrap from `long.MaxValue` to `long.MinValue`

Status: **ignored — maintainer direction**.

Confidence: **Ignored by maintainer direction**.

- Source: `ClockBasedVersionGenerator.cs:12-17` and `CpuTimestampBasedVersionGenerator.cs:12-17` use unchecked `++currentVersion` when the clock-derived value is not greater.
- Verdict: the `long.MaxValue` exhaustion boundary is ignored and is not an actionable audit finding.

### CORE26. Newtonsoft byte deserialization always claims the entire buffer

Status: **implemented — awaiting maintainer review**.

Confidence: **Confirmed** by focused concatenated-value test.

- Source: `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:82-89` hard-codes `readLength = data.Length` after deserializing one value.
- Failure: reading the first value from `1\n2` reports all three bytes consumed, so the second serialized value becomes unreachable through the `IByteSerializer` framing contract.
- Impact: composed/framed serializers skip trailing values or frames and cannot safely use the reported advancement.
- Maintainer constraint: first try the consumed-length fix, but the implementation must remain efficient and must be reviewed by the maintainer before acceptance. Avoid copying, per-byte rescanning, and allocation-heavy position tracking; benchmark the chosen path when its cost is not self-evident.
- Trial implementation: a private UTF-8 `TextReader` decodes the input memory directly into the buffer supplied by `JsonTextReader`. It retains only the latest refill's byte and line origin, then maps `JsonTextReader.LineNumber`/`LinePosition` back to the exact byte offset. It does not copy the input, rescan input bytes, or allocate a position map. Newline-free chunks and final-position lookups use vectorized `IndexOfAny` fast paths; only the latest bounded character chunk is inspected when line breaks require it.
- Measurement: a local Release-mode, warmed microcheck on .NET 10 compared the trial with the previous `StreamReader` path. A small object measured about 1.46 us and 2,752 B per call versus 1.51 us and 6,000 B; a 5 KB JSON string measured about 7.27 us and 41,176 B versus 7.64 us and 44,424 B. These are directional local measurements rather than a committed BenchmarkDotNet suite, but they show no observed throughput or allocation regression.
- Tests: focused regressions cover `1\n2`, UTF-8 content followed by CRLF and another value, and a 5,000-character first value spanning multiple reader refills. The implementation remains subject to the requested maintainer efficiency review.
- **Recommended:** track the actual UTF-8 byte position consumed by the JSON reader, accounting for `StreamReader` buffering, or implement an efficient span/sequence-aware reader.
- **Fallback if the implementation is rejected:** preserve `readLength = data.Length` and explicitly document that this overload consumes one complete buffer rather than supporting concatenated/framed values. Rejecting trailing non-whitespace data remains an optional stricter variant of that documented contract.

### CORE27. `Sampler.ToConcurrent` divides by zero at one shard and wastes the last shard otherwise

Status: **completed**.

Confidence: **Confirmed** by focused test and source analysis.

- Source: `src/ActualLab.Core/Diagnostics/Samplers.cs:35-51` rounds concurrency to a power of two, sets `concurrencyMask = concurrencyLevel - 1`, then indexes with `threadId % concurrencyMask`.
- Failure: level 1 performs modulo zero. At every larger level, indices range only from zero through `concurrencyLevel - 2`, so the final duplicated sampler is never selected; negative managed thread IDs would also be unsafe if the runtime ever supplied one.
- Impact: the minimum explicit configuration crashes, and larger configurations provide less concurrency isolation than requested.
- Resolution: indexing now uses `Environment.CurrentManagedThreadId & concurrencyMask`, matching the power-of-two array and supporting one shard. The focused one-shard regression now passes.
- **Recommended:** use `threadId & concurrencyMask` for the power-of-two array.
- **Alternative:** use positive modulo by `concurrencyLevel`; clearer for arbitrary sizes but gives up the existing power-of-two optimization.

### CORE28. `GetApplicationTempDirectory` exposes a redundant `createIfAbsents` option

Status: **completed**.

Confidence: **Direction confirmed by maintainer decision**.

- Source before the fix: `src/ActualLab.Core/IO/FilePath.Extras.cs:52-63` created the derived directory whenever it was absent without checking the method's `createIfAbsents` argument.
- Decision: remove `createIfAbsents` from the API and always create the application temp directory when it is missing. The existing always-create behavior is the intended contract; the defect is the misleading unused parameter.
- **Implemented:** removed `createIfAbsents`, updated every C# caller, retained unconditional directory creation, and documented that the returned application directory exists whenever the method succeeds.
- **Validation:** the API-shape regression failed while reflection exposed `{ string, bool }` and now passes with the single optional `string appId` parameter. All nine `ActualLab.Core` target frameworks and the HelloCart caller build pass; Todo Host's full C# compile graph passes independently of its unavailable `npm` build step.

### CORE29. Pre-.NET 7 `StreamReader` cancellation overloads ignore their token

Status: **completed**.

Confidence: **Confirmed** by compatibility-source inspection and conditional net6 regression test.

- Source before the fix: `src/ActualLab.Core/Compatibility/StreamReaderExt.cs:8-20` provided token overloads for older targets but delegated to tokenless `ReadToEndAsync`/`ReadLineAsync` without a pre-cancellation check.
- Failure: a token canceled before the call is silently ignored on supported legacy targets, unlike the same API shape on modern targets.
- Impact: multi-target consumers get framework-dependent cancellation behavior and may continue blocking/reading after cancellation.
- **Implemented:** both overloads return `Task.FromCanceled<T>` only when cancellation predates the call; otherwise they return the underlying tokenless read task unchanged.
- **Validation:** the conditional net6 regression failed before the fix and now passes for both `ReadLineAsync` and `ReadToEndAsync`; the complete nine-target `ActualLab.Core` build passes.
- Constraint: do not wrap the non-cancellable underlying read with a cancellation-aware wait helper. Such a wrapper would report cancellation while the I/O task continues, falsely implying that the operation itself was canceled. Once the read starts, preserve its actual completion and document that legacy targets cannot interrupt the underlying I/O.

## B. ActualLab.Fusion

### Maintainer implementation decisions

Every FUS finding uses its **Recommended** action unless an item below records an explicit override. FUS24, FUS25, FUS26, and FUS31 require explicit maintainer review of the implementation before acceptance; FUS27 and FUS32 are intentionally ignored.

- **FUS15 and FUS16:** use one shared, replaceable DI service for redirect validation in the render-mode and authentication endpoints. Prefer a `RedirectUrlChecker` delegate if checking is the only operation; use a `RedirectUrlHandler` class only if multiple operations or state are useful. The default only needs to support the repository samples and may accept every URL. A stricter policy or normalization API is optional only when it does not complicate registration or sample behavior.
- **FUS19:** use non-throwing extraction. More than one `session` query value is treated as no query session, after which the existing ambient-session fallback may apply; do not select the first duplicate.
- **FUS24 and FUS25:** preserve state integrity and allow later subscribers to run after an earlier subscriber throws. Keep the normal non-throwing path highly efficient, without unnecessary allocation or synchronization overhead.
- **FUS26:** keep `Computed.GetDependants` highly efficient and avoid unnecessary allocations, retries, and lock contention.
- **FUS27:** ignore; the current feature-builder wiring is intended.
- **FUS28:** use separate caches for computed-state and mutable-state categories.
- **FUS30:** remove `UseInitializedAsyncRenderPoint` instead of implementing the unused render point.
- **FUS31:** apply the recommended handler-unsubscription fix, subject to explicit maintainer review.
- **FUS32:** ignore; do not change the dispatcher execution-context policy.
- **FUS33:** seal `DefaultParameterComparer`.

### Audit coverage

- Computed registry, graph pruning, and global lifecycle: complete.
- Computed/state consistency state machines and invalidation: complete.
- Compute-method interception and remote computed caching: complete.
- Operations, sessions, UI helpers, diagnostics, and service registration: complete.

### FUS1. `ComputedRegistry.ChangeGraphPruner` can never change the graph pruner

Status: **completed**.

Confidence: **Confirmed** by source and focused regression test.

- Source: `src/ActualLab.Fusion/ComputedRegistry.cs:221-230`. Inside the static lock, the method assigns `prevGraphPruner = _graphPruner` and immediately checks `if (prevGraphPruner == _graphPruner) return false`. That condition is necessarily true because neither value can change while the lock is held.
- Failure: the assignment `_graphPruner = graphPruner` is unreachable. `ComputedGraphPruner.OnRun` therefore cannot replace and dispose a prior worker, and callers cannot disable or swap the global pruner through the only internal change method.
- Test: `tests/ActualLab.Tests/Audit/FusionAuditRegressionTest.cs` constructs a non-autoactivated replacement, expects the change to succeed and the prior instance to be returned, then restores the original in `finally`. The central run reached the method and failed because it returned `false`.
- Impact: creating a replacement pruner silently leaves the original registered, defeating configuration/lifecycle control and potentially leaving the new worker running without registry ownership.
- **Resolution:** `ChangeGraphPruner` now compares the installed pruner with the requested argument, returning false only when no change is needed and otherwise publishing the replacement under the existing static lock.
- **Validation:** the focused regression first failed because the replacement was rejected, then passed while verifying the replacement, previous-value result, and restoration of the original pruner.
- **Alternative:** remove the helper and manage the singleton solely through DI/hosted-service ownership. This is a broader lifecycle redesign and does not address current callers directly.

### FUS2. `FlushingRemoteComputedCache.Flush` completes before the persistent flush

Status: **completed**.

Confidence: **Confirmed** by source and gated regression test.

- Source: `src/ActualLab.Fusion/Client/Caching/FlushingRemoteComputedCache.cs:72-136`. Public `Flush()` returns `FlushTask`, which represents `DelayedFlush`. `DelayedFlush` waits for any prior flush, moves the queue, assigns the actual persistence operation to `FlushingTask`, clears `FlushTask`, and returns without awaiting the newly assigned `FlushingTask`.
- Failure: callers awaiting `Flush()` are released as soon as the write is scheduled, not when `Flush(Dictionary<...>)` completes. `InMemoryRemoteComputedCache.Clear` awaits this incomplete barrier and then clears `_cache`; the scheduled flush can run afterward and repopulate entries that `Clear` promised to remove.
- Test: `FusionAuditRegressionTest.FlushMustWaitForPersistentWrite` uses a gated derived cache, waits until the protected flush starts, and asserts the public task remains incomplete until the gate opens.
- Impact: shutdown/durability barriers can lose queued writes, and `Clear` can leave stale cache entries through a deterministic race. Persistent derived caches can report success before I/O failure is observable.
- **Resolution:** `DelayedFlush` now captures the persistence task selected while swapping queues under the existing lock and awaits that exact task after releasing the lock. Public flush completion therefore includes persistence completion and propagates its failure without extending the critical section.
- **Validation:** the gated regression first observed `Flush()` completing before persistence, then passed with the public task pending until the gate opened. A second regression verifies that the same public task propagates the exact persistence exception.
- **Alternative:** have `Flush()` await both the scheduling task and the current `FlushingTask` in a loop until no queued work remains. This gives a stronger drain guarantee but needs care with writes arriving concurrently.

### FUS3. `ByValueParameterComparer.Instance` is an instance of the UUID comparer

Status: **completed**.

Confidence: **Confirmed** by source and focused regression test.

- Source: `src/ActualLab.Fusion/Blazor/ByValueParameterComparer.cs:6-11`. The singleton property is declared as `ByUuidParameterComparer` and initialized with `new()`, rather than being self-typed.
- Failure: consumers selecting the by-value comparer through its public singleton receive UUID-specific comparison behavior and cannot treat the singleton as `ByValueParameterComparer`, contradicting the documented by-value comparer semantics. The documentation describes the comparer behavior but does not separately document the singleton property.
- Test: `FusionServicesAuditRegressionTest.ByValueComparerInstanceShouldCompareByValue` fails its runtime-type assertion with `ByUuidParameterComparer`.
- **Resolution:** `Instance` is now self-typed and initialized with `ByValueParameterComparer`, restoring the advertised by-value semantics.
- **Validation:** the focused regression first received `ByUuidParameterComparer`, then passed its runtime-type and equal-distinct-string assertions.

### FUS4. `Session.WithTags` discards the session identifier

Status: **completed**.

Confidence: **Confirmed** by source and focused regression test.

- Source: `src/ActualLab.Fusion/Session/Session.cs:67-75`. When an existing tag delimiter is found, the method keeps `id[startIndex..]`, which is precisely the tag suffix, rather than the base identifier before it.
- Failure: replacing tags on `session-id&a=1` produces `&a=1&b=2`; clearing tags attempts to construct a session from only the old tag suffix. The result no longer identifies the original session and can violate the session-ID invariant.
- Test: `FusionServicesAuditRegressionTest.WithTagsShouldReplaceExistingTags` expected `session-id&b=2` and received `&a=1&b=2`.
- **Resolution:** `WithTags` now retains the identifier prefix before the first tag delimiter and appends only the requested replacement tags; clearing tags likewise returns the base session identifier.
- **Validation:** the focused regression first produced `&a=1&b=2`, then passed with `session-id&b=2` and with the base `session-id` when clearing tags.

### FUS5. `Session.WithTag` throws when the replaced tag is followed by another tag

Status: **completed**.

Confidence: **Confirmed** by source and focused regression test.

- Source: `src/ActualLab.Fusion/Session/Session.cs:77-93`. The multi-tag branch slices the suffix from `startIndex + id.Length`, an index necessarily beyond the string whenever the matched tag does not start at zero.
- Failure: updating a non-final tag throws `ArgumentOutOfRangeException` instead of preserving following tags.
- Test: `FusionServicesAuditRegressionTest.WithTagShouldReplaceATagWithoutDroppingFollowingTags` reproduces the exception with `session-id&a=1&b=2`.
- **Resolution:** `WithTag` now replaces a non-final tag in place by concatenating the prefix, replacement tag/value, and suffix beginning at the located `endIndex`; the final-tag and absent-tag paths retain their existing append behavior.
- **Validation:** the focused regression first threw `ArgumentOutOfRangeException`, then passed with `session-id&a=3&b=2` while preserving the following tag.

### FUS6. In-memory command completion returns before asynchronous scope handlers finish

Status: **completed**.

Confidence: **Confirmed** by source and a gated ordering regression test.

- Source: `src/ActualLab.Fusion/Operations/Internal/InMemoryOperationScopeProvider.cs:35-48` discards the `ValueTask` returned by `scope.DisposeAsync()`. `InMemoryOperationScope.DisposeAsync` at `src/ActualLab.Fusion/Operations/Internal/InMemoryOperationScope.cs:52-70` awaits registered completion handlers when they are asynchronous.
- Failure: the outer command can finish while its scope completion handler is still running. Handler failures are logged after the caller has observed success, and callers cannot use command completion as a lifecycle barrier.
- Test: `FusionServicesAuditRegressionTest.InMemoryOperationShouldAwaitCompletionHandlers` gates an async handler and confirms the command task completes before the gate is released.
- **Resolution:** the provider now awaits both `Commit` and `DisposeAsync`, reusing the existing scope lifecycle contract so command completion remains an ordering barrier for asynchronous completion handlers.
- **Validation:** the gated regression failed when the command completed before its handler was released and now passes; the focused Fusion services regression run passes all three FUS6-8 cases, and all nine `ActualLab.Fusion` target frameworks build.

### FUS7. The just-disconnected state is invalidated using the just-connected period

Status: **completed**.

Confidence: **Confirmed** by source and focused state regression test.

- Source: `src/ActualLab.Fusion/Extensions/RpcPeerStateMonitor.cs:169-173`. The disconnected branch checks `JustDisconnectedPeriod` but schedules invalidation with `JustConnectedPeriod - disconnectedFor`.
- Failure: differing configuration values shorten, lengthen, or make the delay negative. A negative delay immediately invalidates the computed state and can cause unnecessary recomputation instead of honoring the configured `JustDisconnectedPeriod` window. Public C# documentation does not define this timing contract.
- Test: `FusionServicesAuditRegressionTest.JustDisconnectedStateShouldRemainValidForItsConfiguredPeriod` configures a 20 ms connected period and 200 ms disconnected period; a state only 50 ms old is already invalidated.
- **Resolution:** the disconnected branch now subtracts `disconnectedFor` from `JustDisconnectedPeriod`, matching the period used by its state transition check.
- **Validation:** the focused state regression failed with an immediately invalidated computed and now remains consistent for the configured just-disconnected window.

### FUS8. `FusionMonitor` drops the first unregistration in every category

Status: **completed**.

Confidence: **Confirmed** by source and focused metrics regression test.

- Source: `src/ActualLab.Fusion/Diagnostics/FusionMonitor.cs:197-209`. The missing-category branch initializes an unregistration tuple to `(0, 0)`.
- Failure: the first sampled unregistration for each category is silently omitted, undercounting invalidations and skewing diagnostic ratios.
- Test: `FusionServicesAuditRegressionTest.FusionMonitorShouldCountTheFirstUnregistration` invokes the event path once and observes `(0, 0)` instead of `(0, 1)`.
- **Resolution:** the missing-category branch now initializes the unregistration tuple to `(0, 1)`.
- **Validation:** the focused event-path regression failed with `(0, 0)` and now records the first sampled unregistration as `(0, 1)`.

### FUS9. `SessionMiddleware` ignores the invalid-session handler's short-circuit result

Status: **completed**.

Confidence: **Confirmed** by source and focused middleware regression test.

- Source: `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:80-86` awaits `InvalidSessionHandler` but discards its `bool`; `InvokeAsync` at lines 60-64 always invokes the next middleware. The option's source-level contract defines `true` as redirect/reload without invoking the next middleware; the public Fusion documentation is silent on this detail.
- Failure: the default redirect path and custom rejection handlers cannot stop downstream endpoint execution. A response may be redirected while protected application logic still runs.
- Test: `FusionWebAuditRegressionTest.InvalidSessionHandlerShouldBeAbleToShortCircuitThePipeline` configures a rejecting validator and a handler returning `true`; the next delegate is still called once.
- **Resolution:** session resolution now carries the handler decision through a private request feature; `InvokeAsync` returns before assigning a session or calling the next delegate when short-circuiting is requested. This preserves the existing public virtual method signature, avoids shared middleware state, and adds no `Items` dictionary allocation to the normal request path.
- **Validation:** the focused middleware regression failed with one downstream invocation and now records none; it also verifies that the short-circuited response does not issue a replacement session cookie.

### FUS10. A malformed session cookie turns an anonymous request into a server error

Status: **completed**.

Confidence: **Confirmed** by source and focused middleware regression test.

- Source: `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:67-73` constructs `new Session(sessionId)` outside the validator try/catch at lines 78-92.
- Failure: any syntactically invalid client-controlled cookie, such as a one-character session ID, throws `ArgumentOutOfRangeException` and aborts the request instead of being rejected and replaced with a fresh session.
- Test: `FusionWebAuditRegressionTest.MalformedSessionCookieShouldBeReplaced` supplies `FusionAuth.SessionId=x` and observes the constructor exception.
- **Resolution:** cookie parsing and validator failures now enter the same invalid-session path. The configured policy is invoked, and a replacement session and cookie are produced only when the policy allows request processing to continue.
- **Validation:** the malformed-cookie regression failed with `ArgumentOutOfRangeException` and now passes, verifies one invalid-session callback, a replacement cookie, and a fresh resolved session. The two focused middleware cases and nine surrounding operation/RPC tests pass together, and all seven `ActualLab.Fusion.Server` target frameworks build.

### FUS11. The subdomain extractor accepts a configured suffix in the middle of an unrelated host

Status: **completed**.

Confidence: **Confirmed** by source and focused host regression test.

- Source: `src/ActualLab.Fusion.Server/Middlewares/HttpContextExtractors.cs:48-53` uses the first `IndexOf(subdomainSuffix)` and returns the preceding text without verifying that the suffix reaches the end of the host.
- Failure: `tenant.example.com.attacker.test` is accepted as subdomain `tenant` for suffix `.example.com`. If this extractor feeds session tags, shard selection, or tenant isolation, an attacker-controlled host can be classified as a trusted tenant host.
- Test: `FusionWebAuditRegressionTest.SubdomainExtractorShouldRequireTheConfiguredSuffixAtTheEnd` expected no match and received `tenant`.
- **Resolution:** configured suffixes now require an ordinal end-of-host match and are sliced from the verified boundary; the default `"."` mode retains first-label extraction.
- **Validation:** the focused regression rejects the attacker-controlled trailing host, accepts a legitimate configured-suffix host, and preserves default first-label extraction.

### FUS12. Expired key-value entries remain visible to `Count` and `ListKeySuffixes`

Status: **completed**.

Confidence: **Confirmed** in both in-memory and database implementations by a shared regression test.

- Source: `src/ActualLab.Fusion.Ext.Services/Extensions/Services/InMemoryKeyValueStore.cs:83-113` enumerates dictionary keys without inspecting `ExpiresAt`. `DbKeyValueStore.cs:100-140` filters only by prefix, while its `Get` path at lines 86-97 correctly treats expired values as absent.
- Failure: the same expired key is absent from `Get` but counted and listed until a background cleanup physically removes it. Callers observe mutually inconsistent views, and pagination slots can be consumed by invisible entries.
- Test: `KeyValueStoreTestBase.ExpiredItemsShouldNotAppearInQueries` advances the test clock beyond expiry; both derived implementations return `Count == 1` after `Get` has returned null.
- **Resolution:** both providers now filter expired entries before counting, ordering, and paging, using the same expiry boundary as `Get`; physical cleanup remains an optimization.
- **Validation:** the shared provider regression advances the clock, confirms the expired value is absent, and verifies that a one-item page still returns the live entry; it passes for both in-memory and database stores.

### FUS13. Sandboxed key prefixes are vulnerable to prefix confusion

Status: **completed**. Confidence: **Confirmed by source and focused isolation regression test**.

- Source: `SandboxedKeyValueStore.KeyChecker.cs:19-24,34,43` authorizes raw ordinal `StartsWith`; default prefixes in `SandboxedKeyValueStore.cs:24-27` have no terminating delimiter.
- Failure: user ID `12` is authorized for `@user/123/private`, escaping into the namespace of a longer matching ID. The same class of collision applies to session IDs/configured prefix formats.
- Test: `FusionServiceBoundaryAuditRegressionTest.SandboxedStoreShouldRejectKeysFromUsersWithLongerMatchingIds` observes no rejection.
- **Resolution:** `KeyChecker` now treats the formatted session and user prefixes as authorization roots and accepts only the exact root or a delimiter-separated descendant, preserving the public default formats.
- **Validation:** the focused regression derives both roots from the default formats, rejects `@user/123/private` for user `12`, and retains exact-root and child access for both scopes.

### FUS14. `AddWebServer(false)` still exposes the backend WebSocket endpoint

Status: **completed**. Confidence: **Confirmed by source and registration regression test**.

- Source: `src/ActualLab.Fusion.Server/FusionBuilderExt.cs:17-21` always calls `fusion.Rpc.AddWebSocketServer(true)` instead of forwarding `exposeBackend`; the underlying RPC builder correctly honors its argument.
- Failure: applications explicitly disabling backend exposure still register it.
- Test: `FusionServiceBoundaryAuditRegressionTest.AddWebServerShouldHonorDisabledBackendExposure` resolves options with `ExposeBackend == true`.
- **Resolution:** `AddWebServer(bool)` now forwards `exposeBackend` to `AddWebSocketServer`.
- **Validation:** the focused service-registration regression passes with backend exposure disabled.

### FUS15. Render-mode switching accepts an external redirect target

Status: **completed**. Confidence: **Confirmed by source and endpoint regression test**.

- Source: `RenderModeEndpoint.cs:27-37,53-56` returns caller-controlled `redirectTo` unchanged; the MVC controller also redirects to it.
- Failure: the public endpoint can be used as an open redirect for phishing/token-flow chaining.
- Test: `FusionServiceBoundaryAuditRegressionTest.RenderModeEndpointShouldNotRedirectToExternalUrls` receives `https://attacker.example/path` unchanged.
- **Resolution:** render-mode redirects now pass through the shared `RedirectUrlChecker` DI delegate, whose default uses ASP.NET Core's local-URL check; rejected and missing targets fall back to `~/`. `DefaultRedirectUrlCheckerFactory` accepts the service provider and is registered with `AddSingleton`, so the framework default replaces earlier registrations while a later application registration can override it. The endpoint's parameterless constructor invokes the factory with the empty service provider.
- **Validation:** the endpoint regression first returned the external attacker URL, then passed with `~/`; the registration regressions verify both default-factory precedence over an earlier checker and shared-policy replacement by a later checker.

### FUS16. Authentication endpoints also accept external return URLs

Status: **completed**. Confidence: **Confirmed by source and endpoint integration test**.

- Source: `AuthEndpoints.cs:29-38,41-53` copies caller `returnUrl` directly into `AuthenticationProperties.RedirectUri` for sign-in and sign-out.
- Failure: supported authentication handlers can redirect a completed flow to an attacker-controlled origin.
- **Resolution:** sign-in and sign-out now use the same `RedirectUrlChecker` delegate as render-mode switching, with `/` as their fixed fallback. `AddAuthEndpoints` injects the existing Fusion.Server registration, while the original direct constructor invokes `DefaultRedirectUrlCheckerFactory` with the empty service provider.
- **Validation:** a recording authentication service first received external redirect URIs, then received `/` for both sign-in and sign-out. A delegate registered after the default factory was resolved by both endpoint families and observed one call from each while preserving its allowed URL.

### FUS17. A database key-value batch with duplicate new keys creates duplicate entities

Status: **completed**. Confidence: **Confirmed by source and shared provider regression test**.

- Source: `DbKeyValueStore.cs:36-54` loads existing keys once, then creates/adds a new entity for every absent item without adding it to the lookup.
- Failure: duplicate new keys in one command generate duplicate primary-key inserts, while the in-memory provider uses last-write-wins semantics.
- **Resolution:** the database provider coalesces items into an ordinal key map before querying or attaching entities; later batch items replace earlier ones, matching the in-memory provider's last-write-wins behavior.
- **Validation:** the shared provider regression writes a duplicate new key and observes the final value in both providers; before the fix, the database provider failed while attaching the second entity.

### FUS18. A malformed explicit session binding falls back to the ambient session

Status: **completed**. Confidence: **Confirmed by source and model-binding integration test**.

- Source: `SessionModelBinder.cs:29-37` catches construction/value-provider errors and invokes its default-session fallback rather than marking binding failed.
- Failure: a request that explicitly supplies an invalid target session can silently operate on the caller's ambient session, changing the meaning of the request.
- **Resolution:** `SessionModelBinder` now detects `ValueProviderResult.None` before conversion and uses the ambient session only for absent input or the explicit default-session sentinel. Any exception or invalid explicit value produces `ModelBindingResult.Failed` without consulting the ambient resolver.
- **Validation:** the malformed explicit-session regression first bound the ambient session and then passed with a failed binding; the paired absent-input regression continues to bind the ambient session.

### FUS19. Duplicate session query parameters crash RPC peer setup

Status: **completed**. Confidence: **Confirmed by source and focused regression test**.

- Source: `RpcPeerOptionsExt.cs:28-31` reads `query["session"].SingleOrDefault()`.
- Failure: a request with duplicate session query values throws during handshake instead of producing a controlled rejection.
- **Resolution:** the connection factory now reads a query session only when exactly one value is present. More than one value is treated as no query session without enumeration exceptions, so the normal ambient-session fallback remains in control and no duplicate value is selected.
- **Validation:** the focused regression first threw `InvalidOperationException` for two query values, then passed with a session-bound connection carrying the ambient cookie session rather than either query value.

### FUS20. Untyped `State.LastNonErrorValue` returns a `Computed`, not its value

Status: **completed**. Confidence: **Confirmed by source and contract regression test**.

- Source: `State/State.cs:104-107` returns `_snapshot.LastNonErrorComputed`; typed implementations return `.Value`.
- Failure: casting the same state to `IState` changes the property from the payload to an implementation object.
- **Resolution:** the untyped property now returns `LastNonErrorComputed.Value`, matching the typed state contract.
- **Validation:** the contract regression failed with a `StateBoundComputed<int>` and now returns the `int` payload; the surrounding state tests pass.

### FUS21. Nonempty `InvalidationSource` enumeration never terminates

Status: **completed**. Confidence: **Confirmed by source and bounded enumeration test**.

- Source: `InvalidationSource.cs:115-121` tests constant `this.IsNone` instead of the advancing local `source.IsNone`.
- Failure: every nonempty chain yields endless `None` values after its end.
- **Resolution:** enumeration now tests the advancing `source` local and stops when the chain reaches `None`.
- **Validation:** the bounded single-entry regression failed by yielding a second item and now reports exactly one.

### FUS22. Tracker-free `UpdateDelayer` swallows cancellation

Status: **completed**. Confidence: **Confirmed by source and pre-cancellation regression test**.

- Source: `State/UpdateDelayer.cs:36-39` uses `SilentAwait` and returns in the null-tracker branch, while the tracker branch propagates cancellation.
- **Resolution:** the tracker-free branch now awaits `Task.Delay` normally, preserving its cancellation result without adding another check or task wrapper.
- **Validation:** the pre-cancellation regression failed without an exception and now propagates `OperationCanceledException`.

### FUS23. `ComputedSynchronizer.Synchronize` treats cancellation as timeout

Status: **completed**. Confidence: **Confirmed by source and focused regression test**.

- Source: `ComputedSynchronizer.cs:100-102,116-118` silently awaits synchronization and returns the computed for any fault/cancellation.
- Failure: caller cancellation is reported as a successful synchronization result.
- **Resolution:** both overloads now await `WhenSynchronized` normally. The safe synchronizer's intended timeout remains a successful completion of its delay path, while cancellation and arbitrary faults retain their exceptional results.
- **Validation:** focused cancellation and fault regressions both failed by returning a computed and now propagate their original exceptions through both synchronization overloads; all nine `ActualLab.Fusion` target frameworks build.

### FUS24. One throwing invalidation subscriber blocks and permanently retains later subscribers

Status: **implemented — awaiting maintainer review**. Confidence: **Confirmed by source and focused event regression test**.

- Source: `Computed.cs:300-305` invokes the compact handler set as one operation; a throw skips remaining callbacks and the subsequent `_invalidated = default`. Already-invalidated computeds refuse handler removal at lines 119-125.
- Failure: infrastructure observers can miss invalidation, and the complete subscriber set remains retained.
- **Resolution:** `InvalidatedHandlerSet` now isolates and logs each subscriber failure inside its existing single, inline-array, and hash-set storage branches, allowing every later subscriber to run. `Computed.Invalidate` clears the complete handler set in `finally`, including exceptional lifecycle paths.
- **Performance rationale:** the compact zero/one/many representation is unchanged. The non-throwing path adds only zero-cost exception regions around direct delegate calls: it performs no collection copy, wrapper-delegate creation, allocation, lock, or additional synchronization. Logger resolution and error formatting occur only after a subscriber throws.
- **Validation:** the event regression failed after calls `{1, 2}` and now observes `{1, 2, 3}` plus empty retained storage. Dedicated tests exercise single-delegate, inline-array, and hash-set failure isolation, and repeated non-throwing invocations of all three representations report zero allocated bytes. The focused contract run passes 6/6 and the surrounding state/invalidation run passes 25/25.

### FUS25. A throwing `ComputedSource.Updated` subscriber breaks computation while the source lock is held

Status: **implemented — awaiting maintainer review**. Confidence: **Confirmed by source and focused update regression test**.

- Source: `ComputedSource.cs:133-141` invokes public handlers directly under the lock; this occurs before the producer's protected computation try block.
- Failure: an observer exception escapes, leaving the newly published computed in `Computing` state and failing the update.
- **Resolution:** subscriptions now maintain a copy-on-write invocation array; `SetComputed` publishes and snapshots it under the source lock, then each subscriber is invoked and failure-isolated after leaving that lock. Logging failure is isolated as well, so observer infrastructure cannot corrupt the producer.
- **Efficiency:** subscription changes pay the invocation-list copy cost; the update path performs no handler-list allocation, retry, or extra lock acquisition and loops directly over the stable snapshot. With no subscribers it reads the shared empty array and performs no dispatch work.
- **Validation:** the focused regression confirms the throwing handler runs outside the monitor, a later handler still runs, the computed reaches `Consistent`, and a subsequent generation updates normally.

### FUS26. `Computed.GetDependants` allocates from an unlocked stale count

Status: **implemented — awaiting maintainer review**. Confidence: **Confirmed by source and deterministic concurrency regression test**.

- Source: `Computed.cs:416-422` reads count and allocates outside the lock, then copies under the lock.
- Failure: a concurrent add can grow the set between those steps and make `CopyTo` target too small.
- **Resolution:** `GetDependants` now reads the count, allocates the exact result array, and copies the set while holding the existing computed lock once.
- **Efficiency:** the method takes one existing lock, makes one exact-size allocation, and performs one copy; it has no retries, excess capacity, secondary synchronization, or extra contention beyond extending the original copy critical section across count/allocation.
- **Validation:** eight readers queued behind the computed lock while a dependant is added now receive the same one-entry snapshot; the old placement deterministically threw `IndexOutOfRangeException` from `HashSetSlim3.CopyTo`.

### FUS27. Feature builders leave pre-registered implementations incompletely wired

Status: **ignored — maintainer direction**. Confidence: **Ignored by maintainer direction**.

- Source: `FusionBuilder.cs:420-447,456-464` returns when a concrete operation reprocessor/cache is already registered, before adding its interface alias, handlers, transiency resolver, or shared wrapper.
- Failure: users following normal DI override patterns get a partially enabled feature.
- Tests: pre-registering the standard implementation leaves both `IOperationReprocessor` and `IRemoteComputedCache` absent.
- **Recommended:** make only concrete registration conditional; always add the remaining idempotent feature wiring.

### FUS28. Computed and mutable component state categories share one cache slot

Status: **completed**. Confidence: **Confirmed by source and focused category regression test**.

- Source: `ActualLab.Fusion.Blazor/Components/ComputedStateComponent.Static.cs:8-9,30-34` uses one component-type-keyed cache for two distinct category functions.
- Failure: the first call wins; `MixedStateComponent` can label both states `.MutableState`, degrading diagnostics and category-based behavior.
- **Resolution:** computed and mutable state categories now use separate component-type caches while retaining the existing concurrent cache configuration.
- **Validation:** the focused regression requests the mutable category first and then confirms the computed category remains distinct with its expected suffix.

### FUS29. Two net8+ Blazor unsafe accessors target the wrong runtime fields

Status: **completed**. Confidence: **Confirmed by source metadata and practical runtime regression coverage**.

- Source: `ComponentExt.cs:24-29` maps `HasPendingQueuedRenderGetter` to `_initialized` and `RendererGetter` to `_hasPendingQueuedRender`; unsafe accessors are enabled for net8-net10.
- Failure: render suspension mutates initialization state, while disposal detection reaches a field of the wrong owner/type.
- **Recommended:** target `_hasPendingQueuedRender` and RenderHandle's `_renderer` respectively, with metadata/runtime smoke tests per supported framework.
- **Resolution:** the net8+ unsafe accessors now target `ComponentBase._hasPendingQueuedRender` and `RenderHandle._renderer`, matching the established reflection fallback.
- **Validation:** focused tests verify both accessor attributes and exercise render-suspension field restoration plus unassigned-render-handle disposal detection on supported runtimes.

### FUS30. `UseInitializedAsyncRenderPoint` is declared and enabled but never used

Status: **completed**. Confidence: **Confirmed by source and API-shape regression test**.

- Source: the flag is defined/included in `UseAllRenderPoints`, but `ComputedStateComponent.cs:48-72` awaits incomplete initialization before entering the parameter flow and first render point.
- Failure: default options cannot produce the advertised intermediate render during asynchronous initialization.
- **Recommended:** implement the render point or remove/rename the option to avoid a false contract.
- **Resolution:** removed `UseInitializedAsyncRenderPoint` and its inclusion in `UseAllRenderPoints`, leaving only render points implemented by the component lifecycle flow.
- **Validation:** the API-shape regression confirms the unused flag is absent and `UseAllRenderPoints` is exactly the union of the two remaining render-point flags.

### FUS31. `MixedStateComponent` retains disposed components through anonymous mutable-state handlers

Status: **implemented — awaiting maintainer review**. Confidence: **Confirmed by source and lifecycle/retention regression test**.

- Source: `MixedStateComponent.cs:21-28` installs a capturing anonymous `Updated` handler; `StatefulComponent` disposal releases only the computed state.
- Failure: a supplied/shared mutable state retains disposed components and continues triggering recomputation.
- **Recommended:** retain the delegate and unsubscribe during component disposal.
- **Resolution:** `MixedStateComponent` now retains its mutable-state handler, unsubscribes it before delegating to the existing computed-state disposal path, and clears the retained delegate.
- **Validation:** the lifecycle regression disposes a component backed by a shared mutable state, verifies the subscriber is removed, and confirms the component is no longer retained.

### FUS32. Dispatcher execution-context policy is cached globally from the first renderer

Status: **ignored — maintainer direction**. Confidence: **Ignored by maintainer direction**.

- Source: `DispatcherInfo.cs:14-35` caches one verdict based only on the first dispatcher's type name; computed component dispatch selection consumes it globally.
- Failure: mixed renderer/dispatcher types in one process can inherit the wrong execution-context behavior.
- **Recommended:** cache per dispatcher type.

### FUS33. Custom parameter comparers derived from `DefaultParameterComparer` are ignored

Status: **completed**. Confidence: **Confirmed by source and focused API-shape regression test**.

- Source: `ComponentInfo.cs:57-58,78-81` classifies any `DefaultParameterComparer` subtype as non-custom and can take the standard path without invoking it. The parameter-comparison documentation advertises arbitrary per-parameter comparer types in custom mode, and both the public non-sealed base type and provider accept derived types.
- Test: the former derived-class regression demonstrated the ambiguity; the replacement contract verifies that the built-in type is sealed and retains its immutable/value comparison behavior.
- **Resolution:** `DefaultParameterComparer` is now sealed, making every externally supplied comparer necessarily distinct from the built-in `is not DefaultParameterComparer` fast-path marker.
- **Validation:** the API-shape regression confirms the type is sealed and its singleton still accepts equal immutable values while rejecting distinct mutable references.

## C. ActualLab.CommandR

### Audit coverage

All 54 production C# files were reviewed across builder/DI and trimming setup; command/context/result lifecycle; local, prepared, and event execution; handler discovery, filtering, ordering, resolution, and chains; command-service interception; operations/events; diagnostics; and inbound/outbound RPC rerouting. Focused tests live in `tests/ActualLab.Tests/CommandR/CommandRExecutionAuditTest.cs`; the serial run completed with one clearing smoke pass and two defect-confirming failures in this class.

### CMDR1. `CommandHandlerChain` exposes mutable storage while caching an index into it

Status: **ignored — maintainer direction**.

Confidence: **Ignored by maintainer direction**.

- Source: `src/ActualLab.CommandR/Configuration/CommandHandlerChain.cs:9-35`. The constructor retains the caller's `CommandHandler[]`, `Items` returns that same array publicly, and `_finalHandlerIndex` is computed only once.
- Failure: mutating the constructor array or the array returned by `Items` can replace/reorder handlers without updating `_finalHandlerIndex`. The focused test replaces the sole final handler with a filter after construction; `FinalHandler` then resolves through stale pipeline metadata.
- Impact: cached command execution chains can call the wrong handler, treat a filter as final, or otherwise diverge from the validation/order computed at construction. External mutation can affect all later commands sharing the cached chain.
- **Verdict:** the mutable-storage behavior is accepted and requires no implementation change.
- **Recommended:** own immutable storage by cloning into a private array and expose `IReadOnlyList<CommandHandler>`/`ReadOnlySpan` or an immutable array.
- **Alternative:** recompute final-handler metadata on every access. This tolerates mutation but preserves a surprising mutable configuration contract and adds hot-path work.

### CMDR2. Inbound RPC command middleware evaluates its filter twice

Status: **completed**.

Confidence: **Confirmed** by focused regression test (observed two calls, expected one).

- Source: `src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:20-25` invokes `Filter(methodDef)` in an initial guard and again after checking `RpcMethodKind.Command`.
- Failure: every accepted command method evaluates a caller-provided predicate twice. A stateful predicate can accept the first evaluation and reject the second; an expensive predicate doubles discovery/setup work.
- Test: `CommandRExecutionAuditTest.RpcInboundCommandFilterMustBeEvaluatedOnce` observed an invocation count of 2, and `RpcInboundCommandFilterMustNotBeEvaluatedForQueries` observed one invocation for a query.
- Impact: custom middleware filters can behave inconsistently and unexpectedly fall through to the next middleware, while side effects occur twice.
- **Resolution:** the method-kind check now precedes the single `Filter` invocation in the combined guard, avoiding predicate work entirely for non-command methods.
- **Validation:** the focused regressions failed with two command-path invocations and one query-path invocation before the fix; they pass with one and zero respectively. The full CommandR test namespace passes (16 tests), and the multi-target `ActualLab.CommandR` build succeeds.
- **Alternative:** cache the first result in a local. This fixes duplication but retains the unnecessary filter evaluation for non-command methods.

### CMDR3. Malformed RPC command diagnostics log an array as the parameter count

Status: **completed**.

Confidence: **Confirmed** by focused regression test and source inspection.

- Source: `src/ActualLab.CommandR/Rpc/RpcCommandHandler.cs:111-118`. The message template uses `{ParameterCount}`, but the supplied value is `methodDef.ParameterTypes` rather than its `Length`.
- Failure: configuration errors print the parameter-type array representation where an integer count is promised, obscuring the actionable mismatch.
- Impact: low runtime risk, but degraded diagnostics on the exact path used to identify invalid distributed command signatures.
- **Resolution:** the diagnostic now passes `methodDef.ParameterTypes.Length` to the `{ParameterCount}` property.
- **Validation:** the focused capturing-logger regression rendered the parameter type list before the fix and passes with the numeric count `3` after it. The full CommandR test namespace passes (16 tests), and the multi-target `ActualLab.CommandR` build succeeds.
- **Alternative:** rename the template property to `ParameterTypes` and retain the array, trading the count for richer structured detail.

## D. ActualLab.Interception and ActualLab.Generators

### Audit coverage

The Interception runtime and proxy generator were reviewed across method definitions and converters, invoker factories, interceptor selection/binding, proxy lookup/activation, built-in interceptors, invocation dispatch, generated argument-list operations, trimming keepers, generator syntax filtering, proxy method emission, diagnostics, and source hint production. `InterceptionAuditRegressionTest` contains a failing nested-type contract test and a skipped process-crash test; isolated generator compile cases live under ignored `tmp/interception-generator-audit` and are recorded in validation.

### INT1. Dynamic invocation of a struct instance method can crash the process

Status: **open**.

Confidence: **Confirmed** by isolated test-host crash (`0xC0000005`).

- Source: `src/ActualLab.Interception/ArgumentList.cs:94-130` and the generated argument-list template emit `Unbox_Any` for a value-type target, then `Callvirt` for an instance method. `Unbox_Any` leaves a value, while a struct instance call requires a managed address.
- Failure: creating/invoking the dynamic delegate for a zero-argument struct instance method aborted the .NET test host with access violation `0xC0000005`; it did not produce a catchable managed exception.
- Test: `InterceptionAuditRegressionTest.ArgumentListInvokerMustSupportValueTypeTargets` preserves the correct contract but is skipped because activating it destabilizes the entire suite. The crash was confirmed before the skip was added.
- Impact: reflected value-type targets can terminate the process, making this a denial-of-service boundary rather than a normal invocation error.
- **Recommended:** emit address-preserving struct invocation IL and use `Call` where required; add a subprocess-isolated regression for all generated argument-list arities.
- **Alternative:** reject value-type instance methods before dynamic code generation with a clear exception. Safer than a crash but narrower than the public invoker contract.

### GEN1. Proxy parameters lose passing modifiers and identifier escaping

Status: **open**.

Confidence: **Confirmed** by isolated compile repros.

- Source: `src/ActualLab.Generators/ProxyTypeGenerator.cs:242-245` recreates parameters using only their name and type; it does not preserve `RefKind` or escape keyword identifiers.
- Failure: a proxy for `Increment(ref int)` fails CS0535 because the generated member does not implement the interface. A parameter named `@event` is emitted as raw `event`, producing malformed-member compiler errors.
- Impact: valid proxy contracts fail consumer builds with unrelated C# diagnostics.
- **Recommended:** render parameters from symbol semantics, preserving `ref`/`out`/`in` and using Roslyn-safe escaped identifiers in declarations and arguments.
- **Alternative:** emit a purpose-built diagnostic rejecting unsupported shapes; clearer than broken source but needlessly excludes ordinary C# signatures.

### GEN2. Diamond interface methods are emitted twice

Status: **open**.

Confidence: **Confirmed** by isolated compile repro (CS0111).

- Source: `ProxyTypeGenerator.cs:378-403` deduplicates by `IMethodSymbol` identity. Identical callable signatures inherited through separate base interfaces are distinct symbols.
- Failure: two base interfaces declaring the same `Task Run()` produce two identical proxy methods.
- Impact: common diamond interface composition makes proxy generation unusable.
- **Recommended:** deduplicate by effective callable signature, including name, generic arity, parameter types/ref kinds, and return compatibility.
- **Alternative:** explicitly implement colliding base-interface members when distinct metadata must be preserved.

### GEN3. Proxy source hint names collide across generic arity

Status: **open**.

Confidence: **Confirmed** by isolated compile repro (CS8785).

- Source: `src/ActualLab.Generators/ProxyGenerator.cs:97-101` builds the hint from namespace and simple type name, omitting generic arity and containing-type identity.
- Failure: `IArityProxy` and `IArityProxy<T>` request the same hint. Roslyn reports generator failure; the build can still exit zero while required proxies are missing.
- Impact: consumers can receive runtime missing-proxy failures after an apparently successful build.
- **Recommended:** derive a deterministic unique hint from full metadata identity, including containing types and generic arity.
- **Alternative:** append a stable hash of the fully qualified symbol identity.

### GEN4. Proxy methods with more than ten parameters generate uncompilable code

Status: **open**.

Confidence: **Confirmed** by central isolated build (CS1501 plus a follow-on lambda error).

- Source: `ProxyTypeGenerator.cs:289-293` emits `ArgumentList.New` for any arity, while runtime factories stop at ten items.
- Failure: an eleven-parameter interface generates a call to a nonexistent overload.
- Impact: a valid interface silently crosses an undocumented generator/runtime limit and breaks the consumer build.
- **Recommended:** extend runtime and generator together, or generate a general array-backed representation above specialized arities.
- **Alternative:** report a targeted generator diagnostic for arity above ten.

### GEN5. Nested proxy types are silently ignored

Status: **open**.

Confidence: **Confirmed** by focused regression test.

- Source: `src/ActualLab.Generators/ProxyGenerator.cs:27-35` accepts declarations only directly under namespaces or the compilation unit.
- Failure: a nested interface implementing `IRequiresAsyncProxy` compiles, but `Proxies.TryGetProxyType` returns null. The active regression test fails at that lookup.
- Impact: consumers get a late runtime proxy failure for a declaration the generator silently skipped.
- **Recommended:** generate nested proxies with full containing-type handling.
- **Alternative:** emit a targeted diagnostic explaining the namespace-level restriction.

### Investigation notes

- **GEN-I1 — mutable incremental-generator state.** `_processedTypes` is generator-instance state mutated inside syntax transforms and cleared only during `Initialize`, including one clear before deferred transforms run. This plausibly suppresses changed partial types on later incremental updates. It remains an investigation until a two-run `GeneratorDriver` test confirms stale output.

## E. Persistence and Redis infrastructure

### Audit coverage

All production C# files in `ActualLab.Fusion.EntityFramework`, `ActualLab.Fusion.EntityFramework.Npgsql`, `ActualLab.Fusion.EntityFramework.Redis`, and `ActualLab.Redis` were inspected. Seven deterministic API/provider gaps have focused regression tests; concurrency and lifecycle findings are source-confirmed and need specialized integration or stress harnesses.

### PERS1. Operation-log trimming can delete entries younger than the retention cutoff

Status: **open**. Confidence: **Confirmed by source**.

- Source: `src/ActualLab.Fusion.EntityFramework/LogProcessing/DbOperationLogTrimmer.cs:93-130`. The trimmer finds the newest expired row by `LoggedAt`, then deletes solely on `Index <= lastCandidate.Index` without retaining the age predicate.
- Failure: index and timestamp order can diverge through concurrent commits, delayed event flushes, or clock skew. A young row with a lower index is then deleted, which can make other hosts miss its invalidations.
- **Recommended:** retain `LoggedAt < minLoggedAt` in both delete paths and use index only for deterministic batching.

### PERS2. Typed Redis registrations all resolve the last untyped connector

Status: **open**. Confidence: **Confirmed by source and focused DI regression test**.

- Source: `src/ActualLab.Redis/ServiceCollectionExt.cs:64-123`. Every typed registration also registers the same untyped `RedisConnector`, and each `RedisDb<TContext>` resolves that untyped service.
- Failure: multiple typed Redis contexts share the last connector/configuration rather than their own endpoints.
- Test: `PersistenceAuditRegressionTest.TypedRedisDatabasesShouldRetainTheirOwnConnectors` resolves two typed databases and finds the same connector instance.
- **Recommended:** key the connector by context type and inject `RedisConnector<TContext>` or an equivalent typed holder.

### PERS3. `RedisSequenceSet.Next` reset is not atomic

Status: **open**. Confidence: **Confirmed by source; stress/integration test needed**.

- Source: `src/ActualLab.Redis/RedisSequenceSet.cs:12-25`. Increment, range check, unconditional reset, and second increment are separate Redis operations.
- Failure: concurrent callers can interleave two resets and return the same sequence number, violating the advertised atomic sequence contract.
- **Recommended:** perform the complete conditional reset/increment in one Redis Lua script or transaction with a compare condition.

### PERS4. The `DbEvent` resolver is registered with the wrong key type

Status: **open**. Confidence: **Confirmed by source and focused DI regression test**.

- Source: `src/ActualLab.Fusion.EntityFramework/DbOperationsBuilder.cs:62-64` registers `IDbEntityResolver<long, DbEvent>`, while `DbEvent.Uuid` is the string primary key.
- Failure: the intended string resolver is absent; constructing the long resolver attempts to build an incompatible key expression.
- Test: `PersistenceAuditRegressionTest.OperationsShouldRegisterTheEventResolverWithItsStringKey` cannot resolve `IDbEntityResolver<string, DbEvent>`.
- **Recommended:** register the resolver with `string`.

### PERS5. Repeated save disabling leaves a context read-only after re-enabling

Status: **open**. Confidence: **Confirmed by source and focused regression test**.

- Source: `src/ActualLab.Fusion.EntityFramework/DbContextExt.cs:64-71`. Every disable adds the same `SavingChanges` handler; enable removes only one subscription.
- Failure: disable/disable/enable still throws on `SaveChanges`, which is especially hazardous for pooled contexts.
- Test: `PersistenceAuditRegressionTest.SaveChangesGuardShouldBeIdempotent` reproduces the remaining guard.
- **Recommended:** make state idempotent, using a tracked flag rather than event-subscription count.

### PERS6. Npgsql locking clauses are emitted in caller order

Status: **open**. Confidence: **Confirmed by source and focused SQL-generation regression test**.

- Source: `src/ActualLab.Fusion.EntityFramework.Npgsql/Internal/NpgsqlHintQuerySqlGenerator.cs:57-73` groups by the clause prefix without sorting the groups.
- Failure: wait-then-lock input emits invalid `FOR SKIP LOCKED UPDATE` rather than `FOR UPDATE SKIP LOCKED`.
- Test: `PersistenceProviderAuditRegressionTest.NpgsqlHintsShouldBeOrderedByClauseKind` observes the invalid order.
- **Recommended:** sort parsed groups by their numeric clause kind before concatenation.

### PERS7. Default Npgsql notification channels are unsafe unquoted identifiers

Status: **open**. Confidence: **Confirmed by source and focused formatter regression test**.

- Source: `NpgsqlDbLogWatcherOptions.cs:16-23` incorporates arbitrary shard text, while `NpgsqlDbLogWatcher.cs:56-57` interpolates it directly into `LISTEN`/`NOTIFY`.
- Failure: valid shard names such as `us-west` produce invalid SQL; quotes and overlength names also lack escaping/normalization.
- Test: `PersistenceProviderAuditRegressionTest.DefaultNpgsqlChannelNamesShouldBeValidUnquotedIdentifiers` produces `AuditDbContext_String_us-west`.
- **Recommended:** quote through the provider's identifier helper or map inputs to a bounded safe identifier.

### PERS8. An empty custom Npgsql hint crashes SQL generation

Status: **open**. Confidence: **Confirmed by source and focused parser regression test**.

- Source: `NpgsqlHintQuerySqlGenerator.cs:50-61` accepts the empty token following `HINTS:` and indexes `x[0]`.
- Failure: an empty hint throws `IndexOutOfRangeException` during query generation.
- Test: `PersistenceProviderAuditRegressionTest.EmptyNpgsqlCustomHintShouldBeIgnored` reproduces the exception.
- **Recommended:** reject or remove empty tokens before grouping.

### PERS9. `RedisStreamer` can hot-spin on its start marker

Status: **open**. Confidence: **Confirmed by source; Redis integration test needed**.

- Source: `src/ActualLab.Redis/RedisStreamer.cs:45-80`. Encountering `StartedStatus` continues without advancing `position`.
- Failure: while the marker is the only entry, the reader repeatedly fetches that same row rather than waiting for append notification.
- **Recommended:** advance the stream position for every consumed control entry before continuing.

### PERS10. File-system log watchers leak their `FileSystemWatcher`

Status: **open**. Confidence: **Confirmed by source**.

- Source: `src/ActualLab.Fusion.EntityFramework/LogProcessing/FileSystemDbLogWatcher.cs:37-51`. Disposal releases only the observable subscription, not the underlying enabled watcher.
- Failure: OS handles and event machinery remain alive after shard watcher disposal.
- **Recommended:** disable and dispose `Watcher` in `DisposeAsyncCore`, after disposing the subscription.

### PERS11. Per-shard service providers have no disposal owner

Status: **open**. Confidence: **Confirmed by source**.

- Source: `src/ActualLab.Fusion.EntityFramework/Sharding/ShardDbContextBuilder.cs:115-124` builds a provider per shard and returns only its `IDbContextFactory`.
- Failure: provider-owned pooled factories and disposable dependencies live indefinitely, even when shard factories are no longer used.
- **Recommended:** retain provider ownership in a disposable shard-factory entry and dispose it when the factory is evicted or the root factory stops.

### PERS12. `DbWaitHint` singletons have the wrong runtime type

Status: **open**. Confidence: **Confirmed by source and focused API regression test**.

- Source: `src/ActualLab.Fusion.EntityFramework/DbHints.cs:32-35` declares `NoWait` and `SkipLocked` as `DbLockingHint` instances.
- Failure: the public API type and record equality semantics do not match their declared wait-hint role.
- Test: `PersistenceAuditRegressionTest.WaitHintsShouldHaveTheWaitHintRuntimeType` receives `DbLockingHint`.
- **Recommended:** construct `DbWaitHint` instances.

## F. Supporting libraries

### Audit coverage

All 84 production C# files in `ActualLab.Plugins`, `ActualLab.RestEase`, `ActualLab.Serialization.NerdbankMessagePack`, and `ActualLab.Testing` were inspected. Six deterministic findings have focused tests; malformed nested-wire, plugin failure, and host-lifecycle cases remain source-confirmed pending specialized harnesses.

### SUP1. `TestIdFormatter` collapses formatted IDs to the empty string

Status: **open**. Confidence: **Confirmed by source and focused regression test**.

- Source: `src/ActualLab.Testing/TestIdFormatter.cs:32-46` calls `ToStringAndRelease()` and then reads the released builder's now-zero `Length` to slice the result.
- Failure: selected ID parts are removed, causing supposedly unique external-resource/test IDs to collide.
- Test: `SupportingProjectsAuditRegressionTest.TestIdFormatterShouldIncludeSelectedParts` expected `alpha` and received an empty string.
- **Recommended:** capture the relevant length before release, or trim the returned string directly.

### SUP2. Finite polling sequences silently succeed after every assertion fails

Status: **open**. Confidence: **Confirmed by source and focused regression test**.

- Source: `src/ActualLab.Testing/TestExt.cs:39-54,72-87`. Both sync and async `When` variants discard assertion failures while intervals remain, then return normally if a finite sequence ends before cancellation.
- Failure: a test can report success although its condition was never satisfied.
- Test: `SupportingProjectsAuditRegressionTest.FinitePollingSequenceShouldNotHideLastAssertion` observes no final error.
- **Recommended:** retain the last assertion exception and throw it when the interval sequence ends.

### REST1. Scalar query serialization ignores the supplied format provider

Status: **open**. Confidence: **Confirmed by source and culture regression test**.

- Source: `src/ActualLab.RestEase/Internal/RestEaseRequestQueryParamSerializer.cs:36-46` uses parameterless `ToString()` for non-date value types despite `RequestQueryParamSerializerInfo.FormatProvider`.
- Failure: decimal/floating query values change with process culture and can be rejected or misinterpreted by servers.
- Test: the invariant serializer emits `1,5` under `fr-FR` rather than `1.5`.
- **Recommended:** use `IFormattable.ToString(null, info.FormatProvider)` with the existing scalar special cases.

### REST2. Complex query objects with indexers throw during serialization

Status: **open**. Confidence: **Confirmed by source and focused regression test**.

- Source: the same serializer at lines 78-84 invokes every public property's getter without checking index parameters.
- Failure: common types exposing indexers throw `TargetParameterCountException` instead of serializing their ordinary properties.
- **Recommended:** exclude indexed and unreadable properties.

### REST3. Rejected HTTP 500 responses are not disposed

Status: **open**. Confidence: **Confirmed by source and disposal regression test**.

- Source: `src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandler.cs:17-22` translates the response to an exception and throws without disposing it.
- Failure: content streams/connections are leaked because the caller never receives the response to dispose it.
- **Recommended:** dispose the response before throwing, after extracting all error information.

### NERD1. `Option<T>` and `ApiOption<T>` readers leave declared array elements unread

Status: **open**. Confidence: **Confirmed by source and representative alignment regression test**.

- Source: `OptionNerdbankConverter.cs:10-18` and `ApiOptionNerdbankConverter.cs:10-18` accept any positive array length but consume only one item.
- Failure: an extended/malformed option corrupts the containing reader's alignment, so following values are read from inside the option.
- Test: `SupportingProjectsAuditRegressionTest.OptionConverterShouldConsumeItsWholeDeclaredArray` reports two consumed bytes for a three-byte declared array.
- **Recommended:** require exactly one item for `Some`, or explicitly skip extension fields before returning.

### NERD2. Embedded `RpcObjectId` parsing has the same alignment defect

Status: **open**. Confidence: **Confirmed by source**.

- Source: `RpcStreamNerdbankConverter.cs:67-76` neither validates its required two fields nor skips fields after index one.
- Failure: extended object IDs leave the outer map reader positioned inside the ID, corrupting the remainder of the RPC stream value.
- **Recommended:** validate the minimum shape and consume/skip the complete declared array.

### PLUGIN1. File-system plugin cache identity omits a result-changing option

Status: **open**. Confidence: **Confirmed by source**.

- Source: `FileSystemPluginFinder.cs:31-33,61-68,109-111`. `DetectIndirectAssemblyDependencies` changes generated metadata but is absent from the file/timestamp cache key.
- Failure: finders sharing a cache directory can retrieve metadata produced under the opposite dependency-detection policy.
- **Recommended:** include all result-affecting settings and an algorithm/schema version in the cache identity.

### PLUGIN2. Failed plugin-host construction leaks its service provider

Status: **open**. Confidence: **Confirmed by source**.

- Source: `PluginHostBuilder.cs:46-53` builds the provider without a cleanup path if finder resolution or host startup throws.
- Failure: singleton disposables created before the failure remain alive.
- **Recommended:** wrap construction in try/catch and dispose the provider on failure before rethrowing.

### PLUGIN3. `ReflectionTypeLoadException` aborts plugin discovery

Status: **open**. Confidence: **Confirmed by source**.

- Source: `FileSystemPluginFinder.cs:93-105` enumerates `Assembly.ExportedTypes` but catches `TypeLoadException`, `FileNotFoundException`, and `FileLoadException`, not the common aggregate `ReflectionTypeLoadException`.
- Failure: one partially unloadable assembly aborts the complete scan rather than yielding its loadable exported types or a controlled exclusion.
- **Recommended:** handle `ReflectionTypeLoadException`, record loader errors, and use its non-null `Types` where policy permits.

### PLUGIN4. Missing declared dependencies surface as incidental dictionary errors

Status: **open**. Confidence: **Confirmed by source**.

- Source: `PluginSetInfo.cs:66-69` indexes every declared dependency through `dPlugins[t]` without validation.
- Failure: inconsistent metadata produces a context-free `KeyNotFoundException` rather than a plugin-resolution diagnostic.
- **Recommended:** validate dependency closure and report the plugin and missing `TypeRef` explicitly.

### TESTING1. One “all serializers” overload omits two serializers

Status: **open test-infrastructure gap**. Confidence: **Confirmed by source comparison**.

- Source: `SerializationTestExt.cs:31-48` omits `UniSerialized` and `TypeDecoratingUniSerialized`; assertion overloads at lines 51-96 include them.
- Failure: equality-based tests can claim all-serializer coverage while silently skipping two wire formats.
- **Recommended:** share one serializer matrix across overloads.

### TESTING2. .NET Framework OWIN dependency scopes resolve from the root

Status: **open**. Confidence: **Confirmed by source**.

- Source: `src/ActualLab.Testing/Compatibility/OwinWebApiServer.cs:146-174` returns the root resolver from `BeginScope`; `Web/TestWebHost.cs:101-105` expects scope validation.
- Failure: scoped services are effectively root singletons and are not disposed per request.
- **Recommended:** create and own a real DI scope in `BeginScope`.

### TESTING3. Serving cleanup completes before host disposal

Status: **open**. Confidence: **Confirmed by source**.

- Source: `src/ActualLab.Testing/Web/TestWebHost.cs:70-87` schedules `host.Dispose()` in an unobserved `Task.Run`, publishes a fresh host, and returns from async cleanup.
- Failure: cleanup exceptions are lost and subsequent tests can overlap still-live resources.
- **Recommended:** await asynchronous/synchronous host disposal within the cleanup callback before publishing completion.

### Investigation notes

- **NERD-I1 — custom serializer isolation.** `TypeDecoratingUniSerializedNerdbankConverter.cs:20-39` hardcodes the global `DefaultTypeDecorating` serializer for nested values. This may bypass an owning serializer's custom converters, but it remains an investigation until a custom-context regression demonstrates divergence.

## G. RPC

### Audit coverage

All production C# files in `ActualLab.Rpc`, `ActualLab.Rpc.Server`, and `ActualLab.Rpc.Server.NetFx` were inspected. Eleven runtime/API gaps are represented by twelve focused correct-contract test cases; WebSocket size and legacy-server lifecycle findings remain source-confirmed.

### RPC1. An arbitrary RPC method is dispatched before the handshake is validated

Status: **open**. Confidence: **Confirmed by source and end-to-end regression test**.

- Source: `src/ActualLab.Rpc/RpcPeer.cs:306-324` sends the local handshake, then passes the first inbound message to `ProcessMessage` before casting the result to the handshake call. `RpcInboundCall.Process` at `Calls/RpcInboundCall.cs:89-142` deserializes and invokes the resolved method synchronously.
- Failure: a peer can make any registered ordinary method its first message; the handler runs before the cast fails and the connection is rejected.
- Test: `RpcHandshakeAuditTest.FirstNonHandshakeMessageMustNotBeDispatched` sends a state-mutating method first and observes the mutation.
- **Recommended:** resolve and validate the exact system-handshake method/call shape before ordinary dispatch, and deserialize it through a handshake-only path.

### RPC2. Frontend peers can invoke backend-only RPC services

Status: **open**. Confidence: **Confirmed by source and end-to-end authorization regression test**.

- Source: `RpcServiceRegistry.cs:41-57` builds one server resolver containing all server methods; `RpcInboundContext.cs:36-61` resolves inbound references against it. No inbound check compares `context.Peer.Ref.IsBackend` with `MethodDef.IsBackend`.
- Failure: a connection on the public frontend path can dispatch an `IBackendService`, bypassing `ExposeBackend`/`BackendRequestPath` isolation and the documented guarantee that backend services are not public RPC endpoints.
- Test: `RpcHandshakeAuditTest.FrontendPeerMustNotDispatchBackendMethod` invokes a backend service through a frontend peer and increments its call counter.
- **Recommended:** use separate frontend/backend resolvers or reject backend method definitions before deserialization/invocation on non-backend peers.

### RPC3. Received handshake protocol versions are never validated

Status: **open**. Confidence: **Confirmed by source and handshake regression test**.

- Source: `RpcHandshake.CurrentProtocolVersion` is written by `RpcPeer`, but no receive path compares the remote value.
- Failure: an unsupported/incompatible peer is marked connected and fails later in less diagnosable ways.
- Test: `UnsupportedHandshakeProtocolVersionMustBeRejected` completes a version-mismatched handshake without an error.
- **Recommended:** reject unsupported versions during the handshake before publishing a connected state.

### RPC4. Frame-transport enqueue failures never reach the send handler

Status: **open**. Confidence: **Confirmed by source and focused transport regression test**.

- Source: `RpcFrameBasedTransport.cs:78-84` discards `ChannelWriter.WriteAsync` whenever `TryWrite` fails.
- Failure: completed/full/canceled channels fault an unobserved `ValueTask`; `message.SendHandler` is never invoked, leaving calls without completion notification.
- Test: `SendAfterCompletionReportsFailureToHandler` observes a null handler error after sending to a completed channel.
- **Recommended:** await the slow path and call the send handler exactly once with success or the enqueue exception.

### RPC5. Simple-channel transport reports success before enqueue succeeds

Status: **open**. Confidence: **Confirmed by source and bounded-channel regression test**.

- Source: `RpcSimpleChannelTransport.cs:40-55` serializes into a pooled owner, calls `CompleteSend(success)`, and then discards `WriteAsync`.
- Failure: a canceled/full bounded channel reports false success and leaks the never-enqueued pooled frame owner.
- Test: `SimpleChannelSendReportsCanceledEnqueue` receives no cancellation error.
- **Recommended:** complete the send only after a successful enqueue; dispose the frame and report the exception otherwise.

### RPC6. `RpcPeerRef` null equality operators violate the equality contract

Status: **open**. Confidence: **Confirmed by source and API regression test**.

- Source: `RpcPeerRef.cs:125-130` makes `null == null` false and `null != null` true.
- **Recommended:** implement the standard reference/null operator pattern before delegating to value equality.

### RPC7. `RequireBackend` is inverted

Status: **open**. Confidence: **Confirmed by source and API regression test**.

- Source: `RpcPeerRefExt.cs:30-33` returns non-backend references and throws `BackendRpcPeerRefExpected` for backend references.
- **Recommended:** negate the current condition.

### RPC8. `RpcCacheKey` caches a hash over caller-mutable bytes

Status: **open**. Confidence: **Confirmed by source and dictionary regression test**.

- Source: `RpcCacheKey.cs:26-42` retains caller `ReadOnlyMemory<byte>`, caches its initial hash, and rereads current bytes for equality.
- Failure: mutating the backing array after insertion makes the key unreachable and can corrupt remote-computed cache dictionaries.
- Test: `RpcCacheKeyMustSnapshotMutableArgumentData` demonstrates divergent hash/equality behavior.
- **Recommended:** copy argument bytes at the ownership boundary or use an immutable owned representation.

### RPC9. Frozen RPC configuration still reflects mutations through the original dictionary

Status: **open**. Confidence: **Confirmed by source and focused regression test**.

- Source: `RpcConfiguration.cs:37-48` wraps the existing mutable services dictionary in `ReadOnlyDictionary` without copying it.
- Failure: a retained pre-freeze reference can race or falsify registry construction after configuration is advertised as immutable.
- **Recommended:** snapshot into a new dictionary under the freeze lock before wrapping it.

### RPC10. Non-positive stream flow-control values cause division by zero or permanent stalls

Status: **open**. Confidence: **Confirmed by source and construction-contract theory**.

- Source: `RpcStream.cs:31-34` exposes unvalidated `AckPeriod`/`AckAdvance`; `MaybeSendAck` at lines 363-366 uses modulo by `AckPeriod`, while shared-stream advancement depends on positive `AckAdvance`.
- Failure: zero period divides by zero; zero advance prevents any item from becoming sendable and waits forever. Values cross the wire.
- **Recommended:** reject non-positive values at construction/deserialization and guard buffer arithmetic overflow.

### RPC11. `RpcFrameDelayers.Yield` ignores its handshake-frame parameter

Status: **open**. Confidence: **Confirmed by source and focused behavior test**.

- Source: `RpcFrameDelayers.cs:13-26,55-56` accepts `handshakeFrameCount` but uses a hard-coded static threshold of two.
- Failure: callers cannot configure the advertised exemption from yielding.
- **Recommended:** validate and capture the supplied threshold in the returned delegate.

### RPC12. Fragmented WebSocket messages can force unbounded allocation

Status: **open**. Confidence: **Confirmed by source; adversarial transport test needed**.

- Source: `RpcWebSocketTransport.cs:96-149` grows an `ArrayPoolBuffer` for every fragment until `EndOfMessage`; unlike pipe/stream transports, no inbound maximum frame size is enforced. `MaxBufferSize` controls retained capacity, not accepted message size.
- Failure: a remote peer can stream an arbitrarily large fragmented message and exhaust process memory.
- **Recommended:** configure and enforce a hard inbound message limit while accumulating fragments, closing the socket with an appropriate status on violation.

### RPC13. The .NET Framework server resolves the peer reference twice

Status: **open**. Confidence: **Confirmed by source**.

- Source: `ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs` invokes `PeerRefFactory` during request validation and again in the accepted WebSocket callback; the second result is not passed through the same `RequireServer` validation.
- Failure: a stateful/custom factory can validate and disconnect peer A but establish peer B, bypassing the checked identity.
- **Recommended:** capture the single validated peer reference and pass it into the callback.

### RPC14. The .NET Framework server has no clear WebSocket disposal owner

Status: **open**. Confidence: **High-confidence source finding; OWIN ownership should be verified**.

- Source: the NetFx handler never disposes `wsContext.WebSocket`/its owner while constructing a transport with `OwnsWebSocketOwner = false`; the ASP.NET Core counterpart explicitly disposes the socket in `finally`.
- Failure: accepted sockets may retain resources after peer termination.
- **Recommended:** establish one explicit owner and dispose in the server callback unless the OWIN host contract demonstrably owns it.

### RPC15. Conditional weak-reference tracker abort can leak a `GCHandle`

Status: **open**. Confidence: **Confirmed for the affected conditional implementation by source**.

- Source: the conditional `WeakReferenceSlim` tracker path allocates a handle that is not released when object tracking aborts before normal teardown.
- Failure: repeated failed/aborted tracking leaks unmanaged handle-table entries.
- **Recommended:** release the handle on every abort/removal path and add a target-framework-specific allocation regression.

### Investigation notes

- **RPC-I1 — WebSocket client timeout-owner race.** Timeout and normal completion appear able to compete for WebSocket ownership. The path needs a deterministic gated socket test before promotion.

## H. Build, docs executable, and C# samples

### Audit coverage

All 102 non-generated C# source files in `build`, `docs`, and the non-test sample projects were inspected. These surfaces are classified separately because some intentionally demonstrate failure injection, shared sample credentials, infinite streams, or short-lived process ownership. Findings below are limited to behavior that defeats automation, leaks isolation, or contradicts the sample's own reusable contract.

### INTG1. OAuth client secrets are committed in the Todo sample

Status: **invalid — intentional sample configuration**. Confidence: **Confirmed by maintainer clarification**.

- Source: `samples/TodoApp/Host/HostSettings.cs:24-32` contains Microsoft and GitHub client IDs and client secrets consumed by the sample's enabled OAuth providers.
- Verdict: this is a runnable sample, and the shared OAuth credentials are intentionally checked in so users can run it without provisioning their own provider applications. This is not an actionable audit finding.

### INTG2. The build driver returns a success exit code after target failure

Status: **open**. Confidence: **Confirmed by source; subprocess smoke test needed**.

- Source: `build/Program.cs:203-218` catches `TargetFailedException` and all other exceptions without rethrowing or setting a nonzero process exit code.
- Failure: failed build, test, pack, or publish targets can appear successful to CI and release automation.
- **Recommended:** preserve friendly logging but return a nonzero exit code or rethrow after logging; add an invalid/failing-target subprocess test.

### INTG3. The optional in-memory Todo API ignores session isolation

Status: **open**. Confidence: **Confirmed by source**.

- Source: `samples/TodoApp/Services/InMemoryTodoApi.cs:9,13-20,34-38,51-64` stores one global item list and ignores every `Session`; `Host/Program.cs:211` presents it as a registration alternative.
- Failure: users can read, update, and delete each other's todos when the alternative is enabled. Non-atomic immutable-list replacement also lets concurrent commands overwrite one another.
- **Recommended:** partition by authenticated session/user/folder using existing session and command abstractions, and serialize or atomically update each partition.

### INTG4. MultiServerRpc mutates previously published computed results

Status: **open**. Confidence: **Confirmed by source**.

- Source: `samples/MultiServerRpc/Service.cs:36-40,52-62` returns its internal list, then modifies that same list in place. `samples/MiniRpc/Program.cs:124-132` demonstrates the correct copy-on-write pattern.
- Failure: cached callers and serializers can observe a value change before invalidation and race list mutation, undermining Fusion's immutable-result expectation.
- **Recommended:** store/publish immutable snapshots or replace the list on every mutation.

### INTG5. Clearing the local-storage computed cache erases unrelated browser data

Status: **design/documentation review — hardening recommendation, not a confirmed defect**. Confidence: **Implementation and documentation confirmed; intended storage ownership is unclear**.

- Source: `samples/TodoApp/UI/Services/LocalStorageRemoteComputedCache.cs:52-55` calls `_storage.Clear()` despite maintaining a cache-specific `_keyPrefix`.
- Concern: clearing Fusion's cache wipes all local-storage entries owned by the application or other components. However, the current local-storage cache documentation presents this exact `_storage.Clear()` implementation as the complete example.
- **Recommended:** decide whether the cache owns the entire local-storage namespace. If it does, close this item; otherwise enumerate and remove only keys under the cache prefix and update the documentation example with the same behavior.

### INTG6. Corrupt local cache entries turn cache misses into application failures

Status: **open**. Confidence: **Confirmed by source**.

- Source: the same cache at lines 30-38 lets invalid Base64 or incompatible MemoryPack data escape.
- Failure: one stale/corrupt browser entry repeatedly breaks reads instead of self-healing as a miss.
- **Recommended:** catch data-format/deserialization errors, remove the bad scoped key, log at an appropriate level, and return null.

### INTG7. Build cleanup can silently reuse stale artifacts

Status: **open**. Confidence: **Confirmed by source**.

- Source: `build/Program.cs:87-95,323-336` suppresses directory-deletion failures and proceeds when the directory still exists.
- Failure: pack/publish can consume stale outputs after a failed clean, producing non-reproducible artifacts.
- **Recommended:** verify the directory is absent/empty after cleanup and fail the target if stale artifacts remain.

### INTG8. Blank docs selection runs nothing instead of all parts

Status: **open**. Confidence: **Confirmed by source**.

- Source: `docs/Program.cs:57-68` splits blank input into an array containing `""`, so the documented Enter/default interaction matches no part. Unknown names also exit successfully.
- **Recommended:** treat empty/whitespace input as the complete part list and fail clearly on unknown names.

### INTG9. MiniRpc and MultiServerRpc spin forever after stdin closes

Status: **open**. Confidence: **Confirmed by source; subprocess smoke test needed**.

- Source: MiniRpc `Program.cs:62-70` and MultiServerRpc `Program.cs:81-89` convert EOF/null to an empty command and continue.
- Failure: redirected/headless execution hot-spins after input closes; observer tasks are also fire-and-forget.
- **Recommended:** treat EOF as shutdown and await/coordinate observer termination.

### INTG10. MiniRpc and MultiServerRpc suppress server failures

Status: **open**. Confidence: **Confirmed by source**.

- Source: MiniRpc `Program.cs:36-41` and MultiServerRpc `Program.cs:41-46` log startup/runtime exceptions and return normally.
- Failure: automated smoke runs cannot detect bind failures or server crashes from the process status.
- **Recommended:** return a nonzero exit or rethrow after logging unexpected server failures.

### INTG11. MeshRpc retains process-lifetime peer/provider state

Status: **open, low severity for a short stress sample**. Confidence: **Confirmed by source**.

- Source: `samples/MeshRpc/RpcHostPeerRef.cs:7-19` uses an unbounded static cache for unique host IDs; `Host.cs:34-43` loses the provider that owns the shared cache.
- Failure: sustained/repeated runs retain peer references and provider-owned resources indefinitely.
- **Recommended:** bound/remove cache entries when hosts leave and give providers an explicit owner/disposal path.

### INTG12. Todo console client dereferences EOF

Status: **open, low severity**. Confidence: **Confirmed by source**.

- Source: `samples/TodoApp/ConsoleClient/Program.cs:8` dereferences the nullable result of `Console.ReadLine()`.
- Failure: redirected or closed stdin terminates with a null-reference error rather than clean shutdown.
- **Recommended:** break on null.

### Contextual observations not promoted to defects

- HelloCart's default database password, database recreation, sensitive logging, chaos injector, and autorunner are explicit local stress-demo settings.
- TodoApp's detailed Blazor errors, sensitive SQL logging, relaxed cookie settings, and opt-in database deletion are visibly testing/development oriented, but deployments should still gate them by environment.
- NativeAot's provider lifetime and first-chance exception logging are acceptable in its short diagnostic process.
- Documentation placeholders, non-disposed snippet providers, and infinite streaming examples are pedagogical rather than reusable production paths.

## Test-coverage gaps

Confirmed boundary gaps are converted into focused regression specifications immediately rather than left as prose-only recommendations. The audit now includes focused classes for Core, Fusion services/state/web, CommandR, RPC, Interception/generation, persistence providers, RestEase/serialization/testing support, and shared key-value semantics. Correct-contract probes fail at the production call sites, while retained clearing/smoke tests pass.

The audit regression tests intentionally assert the correct contract and therefore leave the focused audit slice red until the corresponding production fixes are authorized and implemented. CORE1 and INT1 are not represented by normal in-process tests because their current failure modes respectively spin a worker forever and crash the host process; both need subprocess isolation. CORE9 is a long-duration/overflow boundary requiring a controllable cursor seam. CORE11 and GEN1-5 are compile-time defects covered by isolated compiler/generator builds rather than ordinary runtime tests. CORE15, CORE19, and CORE20 are performance/statistical contracts that need purpose-built allocation, work-count, or distribution tests rather than timing-sensitive unit tests.

## Validation log

- 2026-07-15: `dotnet build src/ActualLab.Core/ActualLab.Core.csproj --no-restore` — passed for `net10.0`; two generated IL2055 warnings.
- 2026-07-15: multitarget restore and `dotnet build src/ActualLab.Core/ActualLab.Core.csproj --no-restore -p:UseMultitargeting=true` — all nine declared target frameworks passed; IL2055 appeared for the generated net9/net10 MessagePack resolver.
- 2026-07-15: broad non-performance `ActualLab.Tests` run started but was interrupted before the result was captured; no conclusion recorded.
- 2026-07-15: temporary core probes — `UnbufferedPushSequence<T>.Complete(error)` correctly rethrows; CORE1 reproduced (second disposal call did not complete after synchronous override failure).
- 2026-07-15: temporary channel probe — CORE2 reproduced with a pre-canceled token and `CopyAllSilently`; cancellation escaped and the destination channel stayed incomplete.
- 2026-07-15: focused `ActualLab.Tests` baseline for channel transforms, batch processing, cancellation-token helpers, and async locks — 12 passed, 0 failed. These tests do not cover CORE1 or CORE2.
- 2026-07-15: temporary collection probes — CORE3 copied a 16-slot lease for two logical items; CORE4's recording pool observed `clearArray=false` on resize despite `mustClear=true`; CORE5 returned a 16-element span for `int.MaxValue`.
- 2026-07-15: additional collection probes — CORE6 returned priority 1 under a descending comparer; CORE7 enumerated `2,3` while `ToArray` returned `2,3,4`; the VersionSet probe confirmed retained cached data after backing-dictionary mutation, but CORE8 was later rejected as an accepted caller contract.
- 2026-07-15: enumerable interval probe — CORE10 emitted `0,1` to the first subscription and no values to the second.
- 2026-07-15: file-path probes — the natural `WriteLines(lines)` call failed compilation with CS0121 (CORE11); after disambiguation, replacing a longer file left its stale suffix (CORE12).
- 2026-07-15: DI activation probe — generic `GetServiceOrCreateInstance<T>(123)` discarded the argument and failed to construct a type whose only constructor requires it (CORE13).
- 2026-07-15: API collection probe — `ApiArray<int>.Empty.WithMany(1, 2)` threw `NullReferenceException` (CORE14).
- 2026-07-15: scalability/math probes — a two-node extreme-hash ring routed `int.MaxValue` to the `int.MinValue` node (CORE16); a one-shard Maglev build divided by zero (CORE17); formatting `long.MinValue` overflowed (CORE18).
- 2026-07-15: focused API, collections, scalability, mathematics, and conversion test slice — 43 passed, 0 failed. The slice covers ordinary paths but omits the boundary inputs recorded in CORE3, CORE6-7, CORE14, CORE16-18, and CORE21.
- 2026-07-15: added `CoreAuditRegressionTest` with 15 boundary tests and ran the focused class — 0 passed, 15 failed. Fourteen actionable failures independently confirmed CORE2-7, CORE10, CORE12-14, CORE16-18, and CORE21; the CORE8 test was subsequently removed after the mutable-input behavior was accepted.
- 2026-07-15: ran `CoreRemainingAuditRegressionTest` serially — 1 passed and 10 failed. The passing dynamic-method setter boundary cleared that suspicion; failures confirmed CORE22-28 and the Newtonsoft/version/member-copying subcases. The two CORE25 exhaustion tests were subsequently removed when that boundary was marked ignored, and the CORE28 no-create test was removed after choosing an always-create API contract.
- 2026-07-15: ran the combined Fusion and CommandR audit slice serially — 1 passed and 4 failed. The passing command-scope disposal smoke test closed that coverage gap; the failures confirmed FUS1-2 and CMDR1-2.
- 2026-07-15: ran `InterceptionAuditRegressionTest` — the nested-proxy test failed as expected for GEN5, while the unsafe struct-invocation reproduction for INT1 remained skipped because the confirmed behavior terminates the test host.
- 2026-07-15: isolated generator builds reproduced GEN1 with CS0535/malformed keyword output, GEN2 with CS0111, GEN3 with CS8785, and GEN4 with CS1501 plus follow-on CS0019.
- 2026-07-15: ran `FusionServicesAuditRegressionTest` serially — the initial ordering assertion was too weak and passed; after strengthening it to wait for either command completion or a 250 ms gate, it failed at the production boundary. The final six-test slice fails all six tests, confirming FUS3-8.
- 2026-07-15: ran `FusionWebAuditRegressionTest` — 0 passed, 3 failed, confirming FUS9-11. A separate SQLite pagination translation smoke test passed and was retained, clearing the suspected `Comparer<T>.Default` translation issue on the current provider.
- 2026-07-15: added a shared expiry contract to the existing in-memory/database key-value test base — both implementations failed at `Count == 1` after `Get` correctly returned null, confirming FUS12.
- 2026-07-15: ran `FusionServiceBoundaryAuditRegressionTest` — 0 passed, 3 failed, confirming sandbox prefix escape (FUS13), ignored backend exposure configuration (FUS14), and external render-mode redirects (FUS15).
- 2026-07-15: ran `PersistenceAuditRegressionTest` — 0 passed, 4 failed, confirming PERS2, PERS4-5, and PERS12.
- 2026-07-15: ran `PersistenceProviderAuditRegressionTest` — 0 passed, 3 failed, confirming invalid Npgsql clause ordering, empty-hint crash, and unsafe channel formatting (PERS6-8).
- 2026-07-15: ran `SupportingProjectsAuditRegressionTest` — 0 passed, 6 failed, confirming SUP1-2, REST1-3, and representative NERD1 alignment corruption.
- 2026-07-15: ran `RpcHandshakeAuditTest` — 0 passed, 12 failed. The tests confirmed RPC1-11, with the stream-flow theory contributing two invalid-input cases.
- 2026-07-15: restored the test graph for all target frameworks and ran the conditional net6 legacy `StreamReader` cancellation test — 0 passed, 1 failed, confirming CORE29. Restore completed from cache but emitted NU1900 because vulnerability metadata was unavailable from NuGet in the restricted environment.
- 2026-07-15: the first `InvalidationSource` regression used deep enumerable equality and crashed the test host through the confirmed infinite sequence; it was replaced with a bounded `Take(2).Count()` assertion. The final `FusionStateContractAuditTest` runtime slice produced 0 passes and 6 failures for FUS20-25 without crashing.
- 2026-07-15: two additional DI registration tests both failed, confirming FUS27 for pre-registered operation-reprocessor and remote-cache implementations.
- 2026-07-15: ran `FusionBlazorCoreAuditRegressionTest` — the original slice produced 0 passes and 3 failures, confirming FUS28 and both incorrect unsafe-accessor mappings in FUS29. A focused FUS33 test was then added and failed because `ShouldSetParameters` returned `true` without honoring a comparer derived from `DefaultParameterComparer`.
- 2026-07-15: built `build`, `docs`, every standalone RPC/sample project, and every Todo C# project serially with `--no-restore` — all completed with zero errors. Todo UI/Aspire transitively built the Host and WebAssembly output; NU1900 and generated Blazor trimming warnings remained environmental/generated.
- 2026-07-15: final `dotnet build ActualLab.Fusion.sln --no-restore -m:1` — passed for the default target graph with 0 errors and 20 warnings. Warnings were unavailable NuGet vulnerability metadata (NU1900), the existing Pomelo/EF Core version constraint (NU1608), and one existing test exception-constructor analyzer warning (RCS1194).

## Recommended remediation order

1. Close remote security boundaries: RPC1-2, FUS13-16, FUS10-11, and RPC12. Add adversarial handshake, backend-isolation, tenant-prefix, redirect, malformed-cookie, host-suffix, and oversized-frame tests to the normal suite.
2. Fix process-fatal/permanent-stall paths: CORE1-2, INT1, RPC10, and transport send completion RPC4-5. Keep crash/spin regressions subprocess-isolated.
3. Protect data and invalidation correctness: PERS1-3, CORE3-7, FUS2/6/12/17/20-27, RPC8-9, NERD1-2, and INTG3-6.
4. Repair generator/API/configuration contracts: GEN1-5, FUS27-33, PERS4-8/12, REST1-3, and the remaining Core boundary findings.
5. Finish resource, diagnostic, compatibility, build/docs, and sample robustness items, then convert source-only concurrency/lifecycle findings into deterministic stress, subprocess, provider, or target-framework tests.

Production fixes were not part of this audit pass. The added tests express the intended contracts and are expected to fail selectively until remediation lands; the complete project still compiles successfully.
