# P7 — ActualLab.Core: async, locking, concurrency, collections, time

Reviewer notes: findings below are ordered most-severe first. Two of them were
verified by execution against the published `ActualLab.Core` 14.1.78 NuGet
package in a throwaway project under `tmp/p7repro/` (the main working tree was
not touched; `tmp/` is git-ignored and `git status` is clean).

---

### F1. `RetryPolicy.Apply` degenerates into a cancellation-immune 100%-CPU spin on `SuperTransient` errors

- **Severity:** HIGH
- **Confidence:** CONFIRMED (reproduced — ~1.9M retry iterations in 5 s, and the
  loop kept running for >4 s after its `CancellationToken` was cancelled)
- **Category:** dos / logic
- **Location:** `src/ActualLab.Core/Resilience/RetryPolicy.cs:104`,
  `src/ActualLab.Core/Resilience/RetryPolicy.cs:106`,
  `src/ActualLab.Core/Resilience/RetryPolicy.cs:58`,
  `src/ActualLab.Core/Time/RetryDelaySeq.cs:53`
- **What:** When `MustRetry` classifies an error as `Transiency.SuperTransient`
  it deliberately does **not** increment `failedTryCount` (line 58). `Apply`
  then computes the backoff as `GetDelay(failedTryCount)`, i.e.
  `RetryDelaySeq.GetDelay(0)`, which returns `TimeSpan.Zero` by contract. The
  zero branch is `await Task.Yield()` — a delay that neither backs off **nor
  observes `cancellationToken`**. The result is an unbounded, un-cancellable hot
  loop that also ignores `TryCount`.
- **Why it matters / attack path:** `Transiency.SuperTransient` is a documented,
  first-class extension point (`Transiency.cs:10` — *"A transient error which
  requires infinite retries"*), and `ActualLab.Core` ships
  `RetryRequiredException : TransientException, ISuperTransientException`
  (`src/ActualLab.Core/Resilience/Exceptions/RetryRequiredException.cs:7`)
  plus a publicly settable `TransiencyResolvers.CoreOnly/PreferTransient`
  so hosting apps can classify their own exceptions this way. Any
  `IRetryPolicy.Apply/Run/RunIsolated` call whose operation keeps raising such
  an error burns one core at ~400k invocations/second, re-executes the wrapped
  operation (a DB query, an RPC call, a log-reader batch) at that rate, and
  cannot be stopped by cancelling the token or by disposing the owning
  worker. In-repo `RetryPolicy` users are `DbOperationLogReader` /
  `DbEventLogReader` reprocess policies
  (`src/ActualLab.Fusion.EntityFramework/LogProcessing/IDbLogReader.cs:62,77`),
  `DbOperationCompletionListener.NotifyRetryPolicy` and
  `DbOperationScope.CommitVerificationPolicy` — all of which run on a
  server-side background loop, so the impact is a server-wide CPU/DoS + a
  process that can never shut down gracefully.
  A second, independent trigger of the same root cause requires no
  `SuperTransient` classification at all: any policy configured with a
  zero-delay sequence (`RetryDelaySeq.Zero`, or `Fixed(TimeSpan.Zero)`) and
  `TryCount == null` produces the same un-cancellable spin.
  Note that the three *other* retry loops in the code base explicitly guard
  against exactly this (`AsyncChainExt.cs:203`, `AsyncChainExt.cs:238`,
  `DbEntityResolver.cs:384` all use `retryDelays[Math.Max(1, tryIndex)]`), so
  `RetryPolicy.Apply` is the odd one out.
- **Evidence:**
  ```csharp
  // RetryPolicy.cs:53-62
  public virtual bool MustRetry(Exception error, ref int failedTryCount, out Transiency transiency) {
      if (!RetryOn.Invoke(error, TransiencyResolver, out transiency))
          return false;
      if (transiency is not Transiency.SuperTransient)
          ++failedTryCount;              // <- stays 0 forever for SuperTransient
      return MustRetry(failedTryCount);  // <- TryCount check therefore never trips
  }

  // RetryPolicy.cs:104-109
  var delay = GetDelay(failedTryCount);          // Delays[0] == TimeSpan.Zero
  retryLogger?.LogRetry(e, failedTryCount, TryCount, delay);
  if (delay > TimeSpan.Zero)
      await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
  else
      await Task.Yield();                        // <- no ct check, no backoff
  ```
  ```csharp
  // RetryDelaySeq.cs:53-54
  if (failureCount <= 0)
      return TimeSpan.Zero;
  ```
  Repro output (`tmp/p7repro`, `ActualLab.Core` 14.1.78,
  `new RetryPolicy(3, RetryDelaySeq.Exp(0.1, 1))`, factory always throws
  `RetryRequiredException`, token cancelled after 1 s):
  ```
  WATCHDOG: still running after 5s; attempts=1914799; ct.IsCancellationRequested=True
  ```
- **Fix:** In `RetryPolicy.Apply`, (a) compute the delay as
  `GetDelay(Math.Max(1, failedTryCount))` so a `SuperTransient` retry still
  backs off, matching `AsyncChainExt`/`DbEntityResolver`; and (b) replace the
  `await Task.Yield()` branch with
  `cancellationToken.ThrowIfCancellationRequested(); await Task.Yield();`
  (or simply call `cancellationToken.ThrowIfCancellationRequested()` at the top
  of the `while (true)` body) so the loop is always cancellable.

---

### F2. `UnbufferedPushSequence.Complete()` / `DisposeAsync()` throws `SemaphoreFullException` in the normal case

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (reproduced)
- **Category:** logic
- **Location:** `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:42`,
  `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:10`,
  `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:96`
- **What:** `_pushAllowed` is a `SemaphoreSlim(0, 1)`. The enumerator releases
  one permit at the top of every iteration (line 96/107), so while the sequence
  is idle-waiting for the next push the semaphore count is already at its
  maximum of 1. The completion path in `Push` then calls `_pushAllowed.Release()`
  unconditionally (line 42) and only guards against `ObjectDisposedException`,
  so the release overflows and throws `SemaphoreFullException` out of
  `Complete()` / `Complete(error)` / `DisposeAsync()`.
- **Why it matters / attack path:** This is the ordinary shutdown path of the
  type: start enumerating, push at least one item, then complete/dispose. The
  exception escapes `IAsyncDisposable.DisposeAsync`, so `await using` over an
  `UnbufferedPushSequence<T>` throws during unwinding and can mask the real
  exception of the enclosing block. The type is public API in `ActualLab.Core`
  but is not used inside this repository, so the blast radius is limited to
  downstream consumers — which is why this is MEDIUM rather than HIGH.
- **Evidence:**
  ```csharp
  // UnbufferedPushSequence.cs:10
  private readonly SemaphoreSlim _pushAllowed = new(0, 1);

  // UnbufferedPushSequence.cs:36-48  (Complete() path)
  if (result.Error is ChannelClosedException e) {
      if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
          return;
      GetItemSource(_item).TrySetException(e);
      try {
          _pushAllowed.Release();          // count is already 1 -> throws
      }
      catch (ObjectDisposedException) { }  // only ODE is tolerated
      return;
  }
  ```
  Repro output (`tmp/p7repro`, `ActualLab.Core` 14.1.78):
  ```
  T1: consumed=1
  T1: Complete() THREW SemaphoreFullException: Adding the specified count to the
      semaphore would cause it to exceed its maximum count.
  ```
  (The item source completes with `RunContinuationsAsynchronously`, so the
  enumerator's `finally { _pushAllowed.Dispose(); }` almost always loses the
  race to `Release()`; when it wins, the ODE path is taken instead — i.e. the
  behaviour is non-deterministic, which is itself a defect.)
- **Fix:** Replace the raw `Release()` with a saturating release, e.g.
  ```csharp
  try { if (_pushAllowed.CurrentCount == 0) _pushAllowed.Release(); }
  catch (ObjectDisposedException) { } catch (SemaphoreFullException) { }
  ```
  or (better) construct the semaphore as `new SemaphoreSlim(0)` (no max) and
  keep the existing `catch (ObjectDisposedException)`.

---

### F3. `BatchProcessor` never replaces a worker that exits, and can wedge permanently

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (code path traced; not reproduced — the exception
  sources that kill a worker are narrow)
- **Category:** logic / dos
- **Location:** `src/ActualLab.Core/Async/BatchProcessor.cs:169`,
  `src/ActualLab.Core/Async/BatchProcessor.cs:257`,
  `src/ActualLab.Core/Async/BatchProcessor.cs:206`,
  `src/ActualLab.Core/Async/BatchProcessor.cs:100`
- **What:** Worker scaling is driven purely from `PlannedWorkerCount`; the
  actual live-worker set (`Workers`) is never reconciled against it. A worker
  that leaves `RunWorker` for any reason other than a `WorkerKiller` item —
  e.g. `reader.WaitToReadAsync()` throwing because the channel was completed
  with an error, or any exception caught by the blanket handler at line 257 —
  is removed from `Workers` by the `ContinueWith` at line 177 and is never
  re-created. If the last worker dies this way, the processor is permanently
  wedged: the unbounded queue keeps accepting items and every `Process()` call
  returns a task that never completes.
- **Why it matters / attack path:** `BatchProcessor` backs
  `DbEntityResolver` (one instance per shard,
  `src/ActualLab.Fusion.EntityFramework/DbEntityResolver.cs:281`), which sits on
  the read path of Fusion compute services. A wedged processor turns every
  entity lookup for that shard into a task that never completes — an
  indefinite hang, not an error, so callers cannot even fail fast. Two
  secondary symptoms of the same gap:
  * `RunWorkerCollector` writes a "measure-only" item into the queue and then
    `await item.ResultTask` (line 206-207) with **no timeout and no
    cancellation token**; if no worker is alive to dequeue it, the auto-scaler
    itself blocks forever and stops adapting.
  * `Reset()` (line 100-114) loops until `Workers.Count == MinWorkerCount`;
    since `AddOrRemoveWorkers(0)` returns immediately without spawning
    anything, a missing worker makes `Reset()` spin (with 50 ms delays)
    forever.
- **Evidence:**
  ```csharp
  // BatchProcessor.cs:141-158 — delta is derived from PlannedWorkerCount only
  var oldPlannedWorkerCount = PlannedWorkerCount;
  PlannedWorkerCount = (oldPlannedWorkerCount + delta).Clamp(wp.MinWorkerCount, wp.MaxWorkerCount);
  ...
  delta = PlannedWorkerCount - oldPlannedWorkerCount;   // 0 when already at the bound
  ...
  if (delta == 0) return;                               // -> no worker is ever re-spawned

  // BatchProcessor.cs:177-183 — the only place Workers shrinks
  _ = workerTask.ContinueWith(static (task, state) => {
      var self = (BatchProcessor<T, TResult>)state!;
      lock (self.Lock) self.Workers.Remove(task);
  }, this, TaskScheduler.Default);

  // BatchProcessor.cs:256-259 — a worker dies silently
  catch (Exception e) {
      Log?.LogError(e, "{BatchProcessor}: Worker failed", GetType().GetName());
  }

  // BatchProcessor.cs:205-207 — collector blocks with no timeout
  var item = new Item(default!, StopToken) { IsMeasureOnlyItem = true };
  await Queue.Writer.WriteAsync(item).ConfigureAwait(false);
  await item.ResultTask.ConfigureAwait(false);
  ```
  Also note `Process()` only bootstraps when `PlannedWorkerCount == 0`
  (line 70-71), which never becomes true again after `Start()`.
- **Fix:** Make `AddOrRemoveWorkers` (or the worker-completion continuation)
  reconcile `Workers.Count` against `PlannedWorkerCount` and respawn the
  difference; and bound the collector's probe with
  `await item.ResultTask.WaitAsync(probeTimeout, StopToken)` so a stalled probe
  cannot kill the auto-scaler.

---

### F4. `CancellationTokenExt.FromTask` disposes a `CancellationTokenSource` from inside its own callback and then unconditionally `Cancel()`s it

- **Severity:** LOW
- **Confidence:** CONFIRMED (by inspection; the failure is an unobserved task
  exception, not a crash)
- **Category:** leak / logic
- **Location:** `src/ActualLab.Core/Async/CancellationTokenExt.cs:71`,
  `src/ActualLab.Core/Async/CancellationTokenExt.cs:79`
- **What:** In the `cancellationToken.CanBeCanceled` branch, a callback is
  registered on the linked CTS's *own* token that disposes that same CTS
  (`CancelAndDisposeSilently`) while the cancellation callbacks are still being
  executed. The `ContinueWith` on the next line then calls `cts.Cancel()`
  without any guard, so when the outer token wins the race the CTS is already
  disposed and `Cancel()` throws `ObjectDisposedException` into a discarded
  continuation task — an unobserved `TaskScheduler.UnobservedTaskException`.
  In the `else` branch the CTS is only disposed when `task` completes, so if
  `task` never completes both the CTS and its continuation leak.
- **Why it matters / attack path:** The only in-repo caller is
  `src/ActualLab.Fusion/Extensions/RpcPeerStateMonitor.cs:111`, which calls this
  once per connection-state transition on the client. Each transition where the
  outer token fires first produces an unobserved exception (log noise; fatal in
  hosts that opt into `ThrowUnobservedTaskExceptions`), and each transition
  whose task never completes leaks a `CancellationTokenSource` + continuation.
- **Evidence:**
  ```csharp
  // CancellationTokenExt.cs:71-77
  var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  var result = cts.Token;
  result.Register(static state => (state as CancellationTokenSource).CancelAndDisposeSilently(), cts);
  _ = task.ContinueWith(_ => cts.Cancel(), TaskScheduler.Default);   // may hit a disposed cts
  return result;
  ```
- **Fix:** Use `cts.CancelSilently()` (or `CancelAndDisposeSilently()`) in the
  continuation instead of raw `Cancel()`, and dispose the CTS from a single
  place (e.g. only from the `ContinueWith`), not from inside its own
  cancellation callback.

---

### F5. `HashSetSlim*` / `SafeHashSetSlim*` / `RefHashSetSlim*` `Add` returns `true` for a duplicate while the set is inline, `false` after it spills

- **Severity:** LOW
- **Confidence:** CONFIRMED (by inspection)
- **Category:** logic
- **Location:** `src/ActualLab.Core/Collections/Slim/HashSetSlim2.cs:44`,
  `src/ActualLab.Core/Collections/Slim/HashSetSlim2.cs:52`,
  `src/ActualLab.Core/Collections/Slim/SafeHashSetSlim2.cs:49`,
  `src/ActualLab.Core/Collections/Slim/SafeHashSetSlim2.cs:57`
  (identical in the `1`/`3`/`4` and `Ref*` variants)
- **What:** On the inline (≤ N items) path `Add` returns `true` when the item is
  already present; once the collection spills into the backing
  `HashSet<T>` / `ImmutableHashSet<T>` the same call returns `false`. `Add`'s
  return value therefore silently changes meaning at the spill boundary.
- **Why it matters / attack path:** No in-repo caller reads the return value
  (`Computer.AddDependency` / `AddDependant`,
  `src/ActualLab.Fusion/Computed.cs:497,514`, ignore it), so this is latent
  today. It is a public `ActualLab.Core` API implementing `IHashSetSlim<T>`,
  and a downstream consumer that uses `Add`'s result to decide "was this newly
  added?" will get a wrong answer for the first N items — a class of bug that
  is very hard to spot because it only shows up for small collections.
- **Evidence:**
  ```csharp
  // HashSetSlim2.cs:36-52
  if (HasSet) return _set!.Add(item);          // false for a duplicate
  ...
  if (EqualityComparer<T>.Default.Equals(_tuple.Item1, item)) return true;   // true for a duplicate
  ...
  if (EqualityComparer<T>.Default.Equals(_tuple.Item2, item)) return true;   // true for a duplicate
  ```
- **Fix:** Return `false` from the inline duplicate branches (matching
  `ISet<T>.Add` semantics), or — if the current behaviour is intentional —
  change the signature to `void Add(T)` so no caller can rely on it.

---

### F6. `TimerSet`'s `RadixHeapSet` is allocated with 45 buckets while `GetBucketIndex` can return up to 64

- **Severity:** LOW
- **Confidence:** PLAUSIBLE (not reachable with the priorities the repo
  currently feeds it; reported because the invariant is unchecked and
  undocumented)
- **Category:** logic
- **Location:** `src/ActualLab.Core/Time/TimerSet.cs:35`,
  `src/ActualLab.Core/Collections/RadixHeapSet.cs:267`
- **What:** `RadixHeapSet<T>.GetBucketIndex` computes
  `64 - LeadingZeroCount(priority ^ MinPriority)`, i.e. a value in `[0, 64]`,
  and indexes `_buckets` with it without a bounds check. `TimerSet` constructs
  the heap with only 45 buckets. Any `AddOrUpdate*` whose quantized priority is
  ≥ `2^44` above the heap's current `MinPriority` therefore throws
  `IndexOutOfRangeException` from inside `lock (_lock)`.
- **Why it matters / attack path:** With the in-repo quanta this is not
  reachable — `Timeouts` uses `Quanta = 2^21` ticks, capping priorities around
  `2^40`, and the `Timeouts.KeepAlive` slot is clamped to `int.MaxValue`
  (`src/ActualLab.Core/Time/Timeouts.cs:68`). But `TimerSetOptions` lets a
  caller pick `TickSource` periods down to `MinQuanta = 10 ms` (`TimerSet.cs:9`),
  at which point a `Moment` near `DateTime.MaxValue` yields a bucket index of
  ~46 and crashes the timer set's `AddOrUpdate` — and, because the throw
  happens inside a public API, it can escape into an arbitrary caller.
- **Evidence:**
  ```csharp
  // TimerSet.cs:35
  private readonly RadixHeapSet<TTimer> _timers = new(45);

  // RadixHeapSet.cs:272-277
  priority ^= MinPriority;
  return 64 - unchecked((int)ulong.LeadingZeroCount((ulong)priority));   // 0..64
  ...
  _buckets[index]   // _buckets.Length == 45
  ```
- **Fix:** Either construct the heap with 65 buckets (the `RadixHeapSet`
  default) or clamp/validate the priority in `TimerSet.FixPriorityFromLock`
  against a documented maximum.

---

## Out-of-partition findings

- **`OperationReprocessor` repeats the F1 zero-delay pattern (P5).**
  `src/ActualLab.Fusion/Operations/Reprocessing/OperationReprocessor.cs:177`
  computes `var delay = Settings.RetryDelays[TryIndex];` while `TryIndex` is
  deliberately *not* incremented for `Transiency.SuperTransient`
  (line 171), so `RetryDelaySeq.GetDelay(0)` returns `TimeSpan.Zero` and the
  command is re-executed in a tight loop. It is less severe than F1 because
  `DelayClock.Delay(TimeSpan.Zero, cancellationToken)` still observes the
  token, but it is the same missing `Math.Max(1, TryIndex)` guard and should be
  fixed together with F1.

---

## Areas examined

All files below were read in full unless noted.

**`src/ActualLab.Core/Locking/`** — `AsyncLock.cs`, `AsyncLockSet.cs`,
`SimpleAsyncLock.cs`, `SemaphoreSlimExt.cs`, `FileLock.cs`, `IAsyncLock.cs`.
I traced the `AsyncLockSet<TKey>.Entry` use-count state machine
(`TryBeginUse`/`EndUse`/`Release`/`Close`) against add/remove/cancel/reentry
interleavings and against `ConcurrentDictionary.TryRemove(key, value)`; I could
not construct a lost-release, double-release, use-after-dispose or stuck-entry
race — it looks correct (no finding).

**`src/ActualLab.Core/Async/`** — `BatchProcessor.cs`,
`BatchProcessorWorkerPolicy.cs`, `ProcessorBase.cs`, `WorkerBase.cs`,
`WorkerExt.cs`, `SafeAsyncDisposableBase.cs`, `AsyncDisposableBase.cs`,
`AsyncDisposable.cs`, `AsyncState.cs`, `AsyncChain.cs`, `AsyncChainExt.cs`,
`AsyncEnumerableExt*.cs` (4 files), `CancellationTokenExt.cs`,
`CancellationTokenSourceExt.cs`, `ExecutionContextExt.cs`,
`AsyncTaskMethodBuilderExt.cs` + `.Untyped.cs`, `TaskCompletionSourceExt.cs`,
`TaskCompletionHandler.cs`, `TaskCoalescer.cs`, `TaskExt.cs`, `TaskExt.Wait.cs`,
`TaskExt.Collect.cs`, `TaskExt.Untyped.cs`, `TaskExt.Suppress.cs`,
`TaskExt.Awaiting.cs`, `TaskExt.ToResult.cs`, `TaskExt.FromResult.cs`,
`TaskExt.YieldDelay.cs`, `ValueTaskExt.cs`, `Temporary.cs`, `SemaphoreSlimExt.cs`,
`Internal/*.cs` (all awaiters + `TaskImpl.cs`).

**`src/ActualLab.Core/Concurrency/`** — `InterlockedExt.cs`,
`StochasticCounter.cs`, `ConcurrentPool.cs`, `DedicatedThreadScheduler.cs`.
I checked `StochasticCounter`'s sampled-increment accounting and
`ConcurrentPool`'s `_size.Reset()`-on-miss behaviour; the counter is
approximate by design and the pool cannot grow unboundedly (no finding).

**`src/ActualLab.Core/Channels/`** — `ChannelExt.cs`, `ChannelExt.Transforms.cs`,
`UnbufferedPushSequence.cs`, `ChannelPair.cs`, `CustomChannel.cs`,
`NullChannel.cs`, `EmptyChannel.cs`, `ChannelCopyMode.cs`.

**`src/ActualLab.Core/Collections/`** — `ArrayBuffer.cs`, `ArrayPoolBuffer.cs`,
`RefArrayPoolBuffer.cs`, `ArrayPoolBufferCapacity.cs`, `ArrayOwner.cs`,
`ArrayPools.cs`, `ArrayExt.cs`, `BinaryHeap.cs`, `RadixHeapSet.cs`,
`RingBuffer.cs`, `RecentlySeenMap.cs`, `FenwickTree.cs`, `ImmutableBimap.cs`,
`ImmutableDictionaryExt.cs`, `ConcurrentDictionaryExt.cs`, `CollectionExt.cs`,
`EnumerableExt.cs`, `ReadOnlyListExt.cs`, `MemoryExt.cs`, `SpanExt.cs`,
`SpanExt.ReadWriteVarUInt.cs`, `SpanExt.ReadWriteLittleEndian.cs`,
`SpanLikeExt.cs`, `BufferWriterExt.cs`, `MutableDictionary.cs`, `MutableList.cs`,
`MutablePropertyBag.cs`, `PropertyBag.cs`, `VersionSet.cs`,
`Legacy/OptionSet.cs`, `Internal/*.cs`, `Slim/HashSetSlim2.cs` +
`Slim/SafeHashSetSlim2.cs` in full (the 1/3/4 and `Ref*` variants are
mechanical copies and were diffed structurally), `Fixed/FixedArray0.cs` and the
head of the generated `Fixed/FixedArray.cs`.
I re-derived the LEB128 fast/slow paths in `SpanExt.ReadWriteVarUInt.cs`
(bounds, 5th/10th-byte overflow rejection, BMI2 path length checks) and found
them correct; `ArrayPoolBufferCapacity` arithmetic does not overflow to a
negative capacity.

**`src/ActualLab.Core/Pooling/`** — `IPool.cs`, `IResourceLease.cs`,
`IResourceReleaser.cs`, `Owned.cs`, `ResourceLease.cs`.

**`src/ActualLab.Core/Caching/`** — `AsyncCacheBase.cs`, `AsyncKeyResolver.cs`,
`AsyncKeyResolverExt.cs`, `CacheExt.cs`, `FileSystemCache.cs`,
`GenericInstanceCache.cs`, `MemoizingCache.cs`, `RefHolder.cs`.

**`src/ActualLab.Core/Time/`** — `TimerSet.cs`, `ConcurrentTimerSet.cs`,
`FixedTimerSet.cs`, `ConcurrentFixedTimerSet.cs`, `Timeouts.cs`,
`GenericTimeoutSlot.cs`, `TickSource.cs`, `Moment.cs`, `CpuTimestamp.cs`,
`CpuClock.cs`, `SystemClock.cs`, `CoarseSystemClock.cs`, `ServerClock.cs`,
`MomentClock.cs`, `MomentClockSet.cs`, `ClockExt.cs`, `TimeSpanExt.cs`,
`DateTimeExt.cs`, `Intervals.cs`, `RandomTimeSpan.cs`, `RetryDelaySeq.cs`,
`Internal/CoarseClockHelper.cs`, `Internal/NonCapturingTimer.cs`,
`Testing/TestClock.cs`, `Testing/TestClockSettings.cs`.
I checked the `TimerSet` catch-up loop, the `_minPriority` vs
`RadixHeapSet.MinPriority` invariant (they cannot desynchronize into the
`ExtractMinSet` `ArgumentOutOfRangeException` branch), the sharding hash of
`ConcurrentTimerSet`/`ConcurrentFixedTimerSet` (`GenericTimeoutSlot` hashes by
handler reference only, so `Remove` lands in the same shard as `Add`), and the
`Timeouts` keep-alive slot math.

**`src/ActualLab.Core/Resilience/`** — `RetryPolicy.cs`, `RetryPolicyExt.cs`,
`RetryLogger.cs`, `Transiency.cs`, `TransiencyResolver.cs`,
`TransiencyResolvers.cs`, `ExceptionFilter.cs`, `ExceptionFilters.cs`,
`ExceptionExt.cs`, `ChaosMaker.cs`, `Internal/*.cs`, `Exceptions/*.cs`,
`ServiceCollectionExt.cs`, `ServiceProviderExt.cs`.

**`src/ActualLab.Core/Scalability/`** — `HashRing.cs`, `ShardMap.cs`,
`ShardMapBuilder.cs`, `Internal/{Greedy,Maglev,Rendezvous}ShardMapBuilder.cs`.
I verified the Maglev permutation loop terminates (`next[n]` cannot run past
`shardCount`) and that the coprime-`skip` search terminates; `HashRing.Span` /
`Segment` index math stays inside the doubled node array. `HashRing`/`ShardMap`
have no in-repo callers, so the empty-ring `DivideByZeroException` in
`HashRing.this[int]` is not reported.

**`src/ActualLab.Core/Net/`** — `Connector.cs`, `RetryDelayer.cs`,
`RetryDelay.cs`, `RetryDelayerExt.cs`, `RetryDelayLogger.cs`, `IRetryDelayer.cs`.

**`src/ActualLab.Core/OS/`** — `HardwareInfo.cs`, `OSInfo.cs`, `RuntimeInfo.cs`.

**`src/ActualLab.Core/Mathematics/`** — `Bits.cs`, `MathExt.cs`, `GuidExt.cs`,
`PrimeSieve.cs`, `Combinatorics.cs` (skimmed).

**Supporting context read outside the partition** (to establish reachability):
`src/ActualLab.Fusion/Computed.cs`, `src/ActualLab.Fusion/ComputedExt.cs`,
`src/ActualLab.Fusion/Operations/OperationCompletionNotifier.cs`,
`src/ActualLab.Fusion/Operations/Reprocessing/OperationReprocessor.cs`,
`src/ActualLab.Fusion.EntityFramework/DbEntityResolver.cs`,
`src/ActualLab.Fusion.EntityFramework/LogProcessing/IDbLogReader.cs`,
`src/ActualLab.Fusion.EntityFramework/Operations/{DbOperationScope,DbOperationCompletionListener}.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs`.

**Non-findings I explicitly ruled out** (recording them so a later pass does not
re-spend time): `AsyncLockSet` entry lifecycle races; `ConcurrentPool` /
`StochasticCounter` overshoot; `TaskCompletionHandler`'s thread-static pool
(fields are copied out before the instance is returned, and it has no in-repo
callers); `RecentlySeenMap` thread-safety (its only caller,
`OperationCompletionNotifier`, holds a lock around every access);
`ExecutionContextExt.Start`'s thread-static handoff; `RadixHeapSet`
`MinPriority` bookkeeping; `MaglevShardMapBuilder` termination;
`MathExt.Format*`/`GuidExt.Format` stack-buffer sizing;
`SpanExt.ReadVarUInt32/64` bounds and overflow rejection.

## Areas NOT examined

- **`src/ActualLab.Core/Collections/Fixed/FixedArray.cs`** (99 KB of generated
  `FixedArray1..N` structs) — I read `FixedArray0`, `FixedArray1` and spot-checked
  the generation pattern (`MemoryMarshal.CreateSpan(ref _item0, N)` + bounds-checked
  `CopyTo`), but did not read all N variants. If one of them has a wrong `N` in
  its `CreateSpan` call it would be an out-of-bounds `Span` — worth a targeted
  grep in a later pass.
- **`Slim/HashSetSlim{1,3,4}.cs`, `Slim/SafeHashSetSlim{1,3,4}.cs`,
  `Slim/RefHashSetSlim{1,2,3,4}.cs`** — structurally diffed against the `2`
  variants rather than read line by line.
- **`src/ActualLab.Core/Mathematics/Combinatorics.cs`** — skimmed only; it has
  no callers reachable from wire input.
- **`src/ActualLab.Core/Time/Internal/{Moment,CpuTimestamp}*Formatter/Converter`
  files** — these are serialization surface and belong to P3.
- **`src/ActualLab.Core/Time/Testing/`** — read but not analysed for defects
  (test-only code, excluded by the brief).
- **Memory-model reasoning for the "Safe*Slim" collections under concurrent
  mutation** — `SafeHashSetSlim*` documents itself as thread-safe, but its
  inline path writes `_count` and a 2/3/4-field tuple non-atomically, so a
  concurrent reader can observe a torn inline state. I did not chase this
  because the only in-repo users (`Computed._dependants` / `_dependencies`)
  mutate and read strictly under `Computed.Lock`. A reviewer owning P5 should
  confirm that invariant holds for every access path.
- **Thread-safety of `MutablePropertyBag` / `PropertyBag` under concurrent
  serialization** — the mutation paths are locked, but `PropertyBag`'s
  deserializing constructor sorts the caller-supplied array **in place**
  (`PropertyBag.cs:71`), which is a shared-array mutation if the array is ever
  reused. I did not find a caller that reuses it; P3 owns the deserializers and
  should confirm.
- No fuzzing / stress testing was performed beyond the two targeted repros.
