### F1. Clean completion of `UnbufferedPushSequence` can throw `SemaphoreFullException`

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:43`, `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:96`
- **What:** The close path completes the current item and unconditionally releases a semaphore whose maximum count is one. The enumerator also unconditionally releases that semaphore before observing the completed item, so ordinary producer/consumer completion orderings attempt two releases without an intervening wait.
- **Why it matters / attack path:** If an enumerator is waiting with no producer, its release at line 96 leaves the semaphore at one; `Complete()` then reaches line 43 and throws `SemaphoreFullException`. Conversely, if `Complete()` runs before enumeration, line 43 leaves the count at one and the first `MoveNextAsync` throws at line 96. A caller therefore cannot reliably close and drain this public async sequence, and a normal shutdown path can fail instead of completing.
- **Evidence:** `_pushAllowed` is created as `new(0, 1)` at `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:10`. The `ChannelClosedException` branch completes the item at `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:41` and then calls `_pushAllowed.Release()` at `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:43`; both enumerator loops also release before awaiting at `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:96` and `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:107`. The close-path catch at `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:45` handles only `ObjectDisposedException`, not the deterministic `SemaphoreFullException`.
- **Fix:** Make semaphore release non-throwing for the close race (the existing `ActualLab.Async.SemaphoreSlimExt.ReleaseSilently` can be reused), and avoid publishing a second permit after completion. Add regressions for completion before enumeration, while the consumer is waiting, and while a producer is waiting; a small explicit handshake state machine would be preferable if simply suppressing redundant releases makes permit ownership unclear.

### F2. `AsyncTaskMethodBuilder` task bridges corrupt cancellation

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:113`, `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:131`, `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.Untyped.cs:111`, `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.Untyped.cs:129`
- **What:** The generic and untyped `(Try)SetFromTask` helpers distinguish only faulted tasks from all other tasks. A canceled generic source throws before completing the target, whereas a canceled untyped source is reported as successful completion.
- **Why it matters / attack path:** A consumer using `SetFromTaskAsync<T>` or `TrySetFromTaskAsync<T>` to bridge a cancellable operation gets a target task that never completes when the source is canceled: the discarded continuation faults with `TaskCanceledException`, and no one transitions the target. The untyped overloads produce the opposite false-success result. This can wedge request cancellation, shutdown, or coordination code waiting for the bridged task.
- **Evidence:** The generic helper checks `task.Exception` at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:115` and otherwise evaluates `task.GetAwaiter().GetResult()` as the argument to `SetResult` at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:118`; for a canceled task, that expression throws before `target.SetResult` runs. Its async wrapper discards the continuation task at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:133`, so the exception is unobserved and the returned `target.Task` at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:136` remains pending. The untyped helper checks the same nullable `Exception` property at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.Untyped.cs:113` and directly calls `target.SetResult()` at `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.Untyped.cs:116`, turning cancellation into success. The analogous `TaskCompletionSource` helper correctly checks `task.IsCanceled` first at `src/ActualLab.Core/Async/TaskCompletionSourceExt.cs:68`.
- **Fix:** Check `task.IsCanceled` before `task.Exception` in all four sync helpers and transition the builder with `SetCanceled`/`TrySetCanceled` (or an `OperationCanceledException`) rather than calling `GetResult` or `SetResult`. Ensure the async wrappers cannot leave their returned target pending, and add generic/untyped cancellation tests for both `Set` and `TrySet`.

### F3. Disposing a custom-clock timer does not cancel its delay

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** leak
- **Location:** `src/ActualLab.Core/Time/ClockExt.cs:32`
- **What:** For every clock other than `SystemClock`, `Timer` creates an async Rx subscription that awaits `clock.Delay(dueIn)` without a cancellation token. Disposing the subscription detaches the observer but cannot cancel the underlying delay or release the async state machine early.
- **Why it matters / attack path:** Repeatedly creating long-lived `CpuClock`, `TestClock`, or other custom-clock timers and disposing them before they fire leaves each delay/timer and its state machine alive until the original due time. A reconnecting component or UI that replaces timers can therefore accumulate retained tasks, timer registrations, and observer state over time even though every subscription was disposed correctly.
- **Evidence:** The non-`SystemClock` branch uses `Observable.Create<long>(async observer => ...)` at `src/ActualLab.Core/Time/ClockExt.cs:32` and awaits `clock.Delay(dueIn)` with no token at `src/ActualLab.Core/Time/ClockExt.cs:35`. The adjacent custom-clock `Interval` implementation uses the cancellation-aware `(observer, ct)` overload at `src/ActualLab.Core/Time/ClockExt.cs:56` and passes `ct` to `clock.Delay` at `src/ActualLab.Core/Time/ClockExt.cs:63`, demonstrating the missing lifetime link in `Timer`.
- **Fix:** Use the cancellation-aware `Observable.Create<long>(async (observer, ct) => ...)` overload, pass `ct` to `clock.Delay`, and treat cancellation caused by subscription disposal as normal termination rather than calling `OnError`. Add a test with a tracking clock proving that subscription disposal cancels the outstanding delay.

### F4. `FenwickTree.Increment(-1, ...)` spins forever

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Core/Collections/FenwickTree.cs:43`
- **What:** `Increment` does not validate its index. For the specific invalid value `-1`, the initial increment produces zero and the Fenwick-tree step `index += index & -index` remains zero forever.
- **Why it matters / attack path:** Any consumer that forwards an invalid external index to this public collection method permanently occupies the calling thread in a synchronous, non-cancellable loop. No in-repository production caller currently exposes this path directly, so this is not claimed as a pre-auth Fusion-server DoS, but it is a deterministic denial-of-service primitive for library consumers.
- **Evidence:** `index++` at `src/ActualLab.Core/Collections/FenwickTree.cs:45` maps `-1` to `0`; the loop condition at `src/ActualLab.Core/Collections/FenwickTree.cs:46` is true for every nonempty tree, and `0 & -0` is zero, so the step at `src/ActualLab.Core/Collections/FenwickTree.cs:48` never changes `index`. Other negative values generally throw on array access, making `-1` an easy-to-miss infinite-loop boundary.
- **Fix:** Validate with `(uint)index >= (uint)Count` and throw `ArgumentOutOfRangeException` before modifying the index. Add tests for `-1`, other negative values, `Count`, and a valid last index.

### F5. Expired `RecentlySeenMap` entries can remain present indefinitely

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Collections/RecentlySeenMap.cs:29`, `src/ActualLab.Core/Collections/RecentlySeenMap.cs:47`, `src/ActualLab.Fusion/Operations/OperationCompletionNotifier.cs:59`
- **What:** Time eviction runs only after a new key has been added successfully. `TryGet` never prunes, and `TryAdd` returns immediately for an existing key, so retrying the same key can never discover that its prior entry has expired.
- **Why it matters / attack path:** `OperationCompletionNotifier` uses this map as the bounded UUID deduplicator for operation-completion/cache-invalidation notifications. In a long-lived but otherwise quiescent process, an operation UUID received again after `MaxKnownOperationAge` is still rejected at line 59 and its listeners are skipped; `DbOperationLogReader` upcasts the returned `Task<bool>` to `Task` at `src/ActualLab.Fusion.EntityFramework/Operations/LogProcessing/DbOperationLogReader.cs:34`, so the false result is ignored and the replay is treated as successful processing. The entry becomes eligible again only after an unrelated unique key triggers pruning or someone calls `Prune` explicitly.
- **Evidence:** `TryAdd` detects `_map.TryAdd` failure at `src/ActualLab.Core/Collections/RecentlySeenMap.cs:29` and returns before its sole `Prune()` call at `src/ActualLab.Core/Collections/RecentlySeenMap.cs:33`. `TryGet` is a direct dictionary lookup at `src/ActualLab.Core/Collections/RecentlySeenMap.cs:22`. The time cutoff is applied only inside `Prune` at `src/ActualLab.Core/Collections/RecentlySeenMap.cs:58`.
- **Fix:** Prune before duplicate lookup in `TryAdd`, while retaining the post-add capacity pruning, and prune before `TryGet` if that method promises time-bounded visibility. The type is currently externally synchronized by `OperationCompletionNotifier`; document that requirement or add internal synchronization while changing these compound operations. Add a deterministic-clock test that retries the same key after expiry without inserting another key.

## Areas examined

- `src/ActualLab.Core/Async/`: task/builder completion, cancellation conversion and linked-token lifetime, wait/suppress/collect helpers, async-enumerable adapters, `AsyncState`, `TaskCompletionHandler`, `TaskCoalescer`, `BatchProcessor`, and worker/disposal lifecycle.
- `src/ActualLab.Core/Locking/` and `Concurrency/`: `AsyncLock`, `AsyncLockSet`, semaphore/file locks, lock-free entry retirement, `ConcurrentPool`, `StochasticCounter`, interlocked helpers, and dedicated-thread scheduling.
- `src/ActualLab.Core/Channels/`: channel copy/transform/connect paths, completion/cancellation flags, channel wrappers, and `UnbufferedPushSequence`.
- `src/ActualLab.Core/Collections/`, `Pooling/`, and `Caching/`: pooled buffers/owners, heap/radix/ring/Fenwick structures, concurrent and mutable collections, property bags, recently-seen eviction, span/varint helpers, leases/pools, file/memoizing caches, and reference holders.
- `src/ActualLab.Core/Time/`, `Resilience/`, and `Net/`: clock implementations and Rx adapters, tick/timer sets, timeout slots, retry sequences/policies/delayers, test clocks, and connector lifecycle/disposal.
- `src/ActualLab.Core/Scalability/`, `OS/`, and `Mathematics/`: hash-ring and shard-map builders, runtime/hardware snapshots, bit/radix/combinatoric/prime/Fenwick-related boundary arithmetic.
- Supporting call paths and tests were read where needed, particularly operation notification/log processing, RPC frame-length callers of span helpers, and existing async/locking/collection/timer regression coverage.

## Areas NOT examined

- The mechanically generated `src/ActualLab.Core/Collections/Fixed/FixedArray.cs` variants were sampled across representative sizes rather than read line-by-line; their repeated layout/accessor code has no independent async, locking, eviction, or resource-lifetime state.
- Repetitive generated serializer formatter bodies under `Collections/Internal` were sanity-checked only for ownership/concurrency interactions; hostile serialization semantics belong to P3.
- Code outside P7 was not reviewed as an audit target. Only narrow callers, base contracts, tests, and documentation needed to validate or reject P7 findings were read.
- No build or runtime experiment was run in the main working tree. The five findings above are deterministic from source-level state transitions, so no additional worktree or published-package repro was needed.
