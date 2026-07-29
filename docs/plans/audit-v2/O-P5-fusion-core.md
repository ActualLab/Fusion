# P5 — Fusion core: Computed, State, invalidation, operations, client cache

Reviewer partition: `src/ActualLab.Fusion/` except `Session/`.
Verification artifacts: `tmp/review-r2/p5-repro/` (mini console app referencing the
published `ActualLab.Fusion` / `ActualLab.Generators` **14.1.78** NuGet packages; the main
working tree was neither built nor modified).

---

### F1. `Computed.CopyDependenciesTo` never advances `ArrayBuffer.Count`, so `ComputedSynchronizer` never inspects dependencies and always reports "synchronized"

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (reproduced against published 14.1.78 packages)
- **Category:** logic / cache-coherence (stale data)
- **Location:** `src/ActualLab.Fusion/Computed.cs:556`
  (consumers: `src/ActualLab.Fusion/ComputedSynchronizer.cs:46`,
  `src/ActualLab.Fusion/ComputedSynchronizer.cs:74`,
  `src/ActualLab.Fusion/Internal/ComputedImpl.Helpers.cs:7`)
- **What:** `CopyDependenciesTo` copies the dependency set into the buffer's backing array
  but never increases `buffer.Count`. `ArrayBuffer<T>.EnsureCapacity` does not touch
  `_count` either. Every consumer then iterates `0 .. buffer.Count` — i.e. iterates
  nothing — so `ComputedSynchronizer.IsSynchronized(Computed)` returns `true` and
  `WhenSynchronized(Computed)` returns `Task.CompletedTask` for *any* computed that is not
  itself an `IRemoteComputed` / `IStateBoundComputed` / `IHasSynchronizationTarget`,
  regardless of the state of its dependencies. `ComputedImpl.CopyAllDependenciesTo` is a
  complete no-op.
- **Why it matters / attack path:** `ComputedSynchronizer` is the mechanism that lets a
  Fusion client distinguish "this value came from the local `RemoteComputedCache` and has
  not been confirmed by the server yet" from "this value is confirmed". The typical client
  pattern is a *local* compute method (or a chain of them) that aggregates several remote
  compute calls; that aggregate computed is a plain `ComputeMethodComputed<T>`, so it takes
  the recursive branch. Because the recursion is dead, `computed.WhenSynchronized(...)` /
  `ComputedSynchronizer.Precise.Synchronize(...)` complete immediately and the application
  renders/acts on unconfirmed, possibly long-stale disk-cache values believing they are
  synchronized. The same hole makes `ComputedSynchronizer.Precise` behaviourally identical
  to `ComputedSynchronizer.None` for aggregate computeds. Combined with the
  serve-stale-on-disconnect path (`RemoteComputeMethodFunction.cs:175-191` and
  `:205-234`), the application has no working way to detect that it is looking at stale
  data.
- **Evidence:**

  ```csharp
  // src/ActualLab.Fusion/Computed.cs:556
  protected internal void CopyDependenciesTo(ref ArrayBuffer<Computed> buffer)
  {
      lock (Lock) {
          var count = buffer.Count;
          buffer.EnsureCapacity(count + _dependencies.Count);
          _dependencies.CopyTo(buffer.Buffer.AsSpan(count));   // <-- buffer.Count never updated
      }
  }
  ```

  ```csharp
  // src/ActualLab.Fusion/ComputedSynchronizer.cs:44-53
  var usedBuffer = ArrayBuffer<Computed>.Lease(false);
  computed.CopyDependenciesTo(ref usedBuffer);
  var usedArray = usedBuffer.Buffer;
  for (var i = 0; i < usedBuffer.Count; i++) {   // usedBuffer.Count == 0 -> loop never runs
      if (!IsSynchronized(usedArray[i]))
          return false;
  }
  return true;                                    // always
  ```

  `ArrayBuffer<T>.EnsureCapacity` (`src/ActualLab.Core/Collections/ArrayBuffer.cs`) only
  rents a new array and copies `Span` (= the first `Count` items); it does not change
  `Count`.

  Repro output (`tmp/review-r2/p5-repro`, `Svc.Outer()` awaits `Svc.Inner()`):

  ```
  buffer.Count after CopyDependenciesTo = 0
  buffer.Buffer[0] = ComputeMethodComputed<Int32>(Svc.Inner()-Hash=..., State: Consistent)
  GetDependencies().Length = 1
  CopyAllDependenciesTo -> buffer2.Count = 0
  TestSynchronizer.IsSynchronized(outer) = True (expected False), visits = 1
  WhenSynchronized(outer).IsCompleted = True (expected False)
  ```

  (`TestSynchronizer` is a `ComputedSynchronizer` subclass that returns `false` for the
  `Inner()` computed; the traversal visits only the root, so the "unsynchronized"
  dependency is never seen.)

  History: introduced no later than `c508487ef` (2024-06-29, `fix: many perf. improvements`);
  the predecessor `CopyUsedTo` had the same defect, so this has never worked.
- **Fix:** set the count after copying:

  ```csharp
  var count = buffer.Count;
  var depCount = _dependencies.Count;
  buffer.EnsureCapacity(count + depCount);
  _dependencies.CopyTo(buffer.Buffer.AsSpan(count));
  buffer.Count = count + depCount;
  ```

  Note that fixing this activates two dormant hazards in `ComputedSynchronizer`, which must
  be addressed in the same change: the DFS in `IsSynchronized`/`WhenSynchronized` has (a) no
  visited-set, so a diamond-shaped dependency graph is traversed exponentially, and (b) no
  depth bound, so a deep chain can overflow the stack (it also leases one pooled
  `ArrayBuffer` per recursion level). Convert it to an explicit worklist with a
  `HashSet<Computed>`/reference-set of visited nodes and a node budget.

---

### F2. `RemoteComputeMethodFunction.ComputeRpc` dereferences `RpcCacheInfoCapture.Call` without a null check — NRE whenever the outbound call fails before the cache key is captured

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (defect is certain by inspection; the triggering connection race
  was not reproduced end-to-end)
- **Category:** logic / robustness (error masking)
- **Location:** `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:244`
  (root cause: `src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:49` — `lock (Call!.Lock)`)
- **What:** `ComputeRpc` calls `cacheInfoCapture.HasKeyAndValue(...)` for every non-cancellation
  outcome of the RPC call. `HasKeyAndValue` starts with `lock (Call!.Lock)`, and
  `RpcCacheInfoCapture.Call` is only assigned inside `CaptureKey`, which only runs from
  `RpcOutboundCall.SendRegistered()` / `RegisterCacheKeyOnly()`. If the call fails before it
  is ever put on the wire, `Call` is still `null` and the caller gets a
  `NullReferenceException` instead of the real error.
- **Why it matters / attack path:** Concrete failure scenario on any Fusion client with a
  `RemoteComputedCache` registered (i.e. `RemoteComputedCacheMode.Cache`, the
  `ComputedOptions.ClientDefault` when a cache is present):
  1. `ComputeRpc` checks `peer.ConnectionState.Value.IsConnected(...)` and finds the peer
     connected (`RemoteComputeMethodFunction.cs:175`), so it proceeds to `SendRpcCall`.
  2. The connection drops in the window before `RpcOutboundCall.Invoke()` re-checks
     (`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:95`), so `Invoke` takes the
     `CompleteAsync()` slow path and awaits `WhenConnectedOrReroute(ConnectTimeout, ...)`.
  3. That await throws `TimeoutException` (`ActualLab.Rpc/Internal/Errors.cs:108` →
     `new TimeoutException(...)`) or the peer's terminal error — neither is an
     `OperationCanceledException`, so the guard at `RemoteComputeMethodFunction.cs:238`
     does not fire.
  4. `SetError(error, null)` records the error in the capture but never sets `Call`
     (`RpcCacheInfoCapture.CaptureErrorFromLock` touches only `ValueOrError`).
  5. `cacheInfoCapture.HasKeyAndValue(...)` → `lock (Call!.Lock)` → NRE.

  The NRE escapes `ProduceComputedImpl` (`TryReprocessServerSideCancellation` returns
  `MustThrow` for non-OCE at `:498`), so the compute method call fails with an unrelated
  `NullReferenceException`, no `Computed` is registered, and each retry repeats the same
  behaviour. Connection flapping / server restarts make this reachable in normal operation.
  A second, rarer path is a synchronous throw out of
  `input.MethodDef.InterceptorAsyncInvoker.Invoke(...)` (`:440`), which leaves both `call`
  and `cacheInfoCapture.Call` null.
- **Evidence:**

  ```csharp
  // src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:236-244
  var (result, call) = await sendTask.ConfigureAwait(false);
  var (value, error) = result;
  if (error is OperationCanceledException e)
      throw e;
  RpcCacheEntry? cacheEntry = null;
  if (cacheInfoCapture is not null && cacheInfoCapture.HasKeyAndValue(out var cacheKey, out var cacheValueOrError)) {
  ```

  ```csharp
  // src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:44-56
  public volatile RpcOutboundCall? Call;   // set only in CaptureKey
  ...
  public bool HasKeyAndValue(out RpcCacheKey? key, out object? valueOrError)
  {
      lock (Call!.Lock) {   // NRE when the call was never sent
  ```

  Note that the sibling background path already guards against exactly this shape —
  `ApplyRpcUpdate` bails out at `:337` with `if (call is null) { ... return; }` — but
  `ComputeRpc` has no equivalent guard.
- **Fix:** make `RpcCacheInfoCapture.HasKeyAndValue` null-safe
  (`var call = Call; if (call is null) { key = null; valueOrError = null; return false; }`),
  and additionally add a `call is null` early-out in `ComputeRpc` mirroring
  `ApplyRpcUpdate:337` so an un-sent call is treated as "no cache info" rather than a hard
  failure.

---

### F3. `ApplyRpcUpdate` is a fire-and-forget task with no catch-all; any throw becomes an unobserved task exception

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (by inspection — the code path has no exception handling)
- **Category:** logic / unobserved-exception
- **Location:** `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:300`
  (method body `:306-410`)
- **What:** `ComputeCachedOrRpc` starts the background "confirm the cached value against the
  server" task with `_ = ExecutionContextExt.Start(ExecutionContextExt.Default, () => ApplyRpcUpdate(...))`.
  `ApplyRpcUpdate` has `try`/`catch` only around step 1 (`WhenConnectedChecked`); steps 5–8
  (`cacheInfoCapture.RequireKeyAndValue`, `InputLocks.Lock`, `UpdateCache` →
  `IRemoteComputedCache.Set/Remove`, `NewRemoteComputed`) run unguarded. Any exception
  there faults a task nobody awaits.
- **Why it matters / attack path:** The whole point of the cached-then-updated flow is that
  the background task either confirms the cached computed or invalidates it. If the task
  dies mid-way — e.g. `RequireKeyAndValue` throwing the F2 NRE, or a user-supplied
  `IRemoteComputedCache.Set` implementation throwing (disk full, DB locked) — then:
  - `SynchronizedSource` is never completed, so `ComputedSynchronizer.Precise` waits forever
    on `WhenSynchronized` for this computed;
  - the failure is invisible (no log entry, only `TaskScheduler.UnobservedTaskException`).
  The consequence is bounded only because step 3 already bound the computed to the call, so
  a later server-side invalidation still invalidates it — but if the call itself faulted
  before the wire (F2), the "cached value confirmed?" question is never answered.
  In `#if DEBUG` builds the `Debug.Assert(!call.IsHandOffPending, ...)` at `:406` is another
  unguarded throw site on the same path.
- **Evidence:**

  ```csharp
  // :300-303
  _ = ExecutionContextExt.Start(
      ExecutionContextExt.Default,
      () => ApplyRpcUpdate(input, cache, cachedComputed, peer));
  return cachedComputed;
  ```

  ```csharp
  // :376-409 — no try/catch from here on
  cacheInfoCapture.RequireKeyAndValue(out var cacheKey, out var cacheValueOrError);
  ...
  using var releaser = await InputLocks.Lock(input).ConfigureAwait(false);
  ...
  cacheEntry = UpdateCache(cache, cacheKey, cacheValue, value, cachedComputed);
  ...
  remoteCachedComputed.SynchronizedSource.TrySetResult();
  ```
- **Fix:** wrap the whole `ApplyRpcUpdate` body in `try { ... } catch (Exception e) { ... }`
  that logs the failure, invalidates `cachedComputed`
  (`InvalidateToProduceError(cachedComputed, e, ...)`) and completes `SynchronizedSource`
  (`TrySetResult()` or `TrySetException(e)`) so waiters are released. Alternatively make the
  discarded task observe itself via a `.ContinueWith` failure handler.

---

### F4. `InMemoryRemoteComputedCache` has no eviction — unbounded memory growth on any long-lived client

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (by inspection)
- **Category:** leak / dos
- **Location:** `src/ActualLab.Fusion/Client/Caching/InMemoryRemoteComputedCache.cs:21`
  (`private readonly ConcurrentDictionary<RpcCacheKey, RpcCacheValue?> _cache = new();`)
- **What:** The cache registered by `FusionBuilder.AddInMemoryRemoteComputedCache()`
  (`FusionBuilder.cs:472`) grows monotonically. Entries are only ever added (`Flush`,
  `:35`) or removed when the corresponding call returned an error (`RemoteComputeMethodFunction.UpdateCache`,
  `:478-481`) or by an explicit `Clear()`. There is no size cap, no TTL and no LRU.
- **Why it matters / attack path:** Concrete failure scenario: a client (or a server acting
  as a distributed-service client) invokes a remote compute method whose arguments carry a
  high-cardinality value — a document id, a search string, a paging cursor. The cache key is
  `RpcCacheKey(methodDef.FullName, serializedArguments)`
  (`src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:74`), so every distinct argument tuple
  becomes a permanent entry holding the full serialized response bytes. `ComputedRegistry`
  self-prunes via weak references, so `Computed` instances disappear, but their cached
  payloads do not. Over hours/days this is an unbounded leak; on a long-lived process this
  ends in OOM. Note the contrast with `ComputedOptions.MinCacheDuration` and
  `ComputedGraphPruner`, which bound every *other* Fusion cache.
- **Evidence:** `InMemoryRemoteComputedCache` in full is 45 lines; `Fetch`/`Flush`/`Clear`
  are the only operations on `_cache`, and none of them bounds it:

  ```csharp
  protected override Task Flush(Dictionary<RpcCacheKey, RpcCacheValue?> flushingQueue)
  {
      foreach (var (key, entry) in flushingQueue) {
          if (entry is null) _cache.Remove(key, out _);
          else _cache[key] = entry;      // unconditional insert
      }
      return Task.CompletedTask;
  }
  ```
- **Fix:** back `_cache` with a bounded/evicting store — `ActualLab.Core` already ships
  `RecentlySeenMap<TKey, TValue>` (used by `OperationCompletionNotifier`,
  `src/ActualLab.Fusion/Operations/OperationCompletionNotifier.cs:48`) and the
  `ActualLab.Caching` primitives. At minimum add `MaxEntryCount` / `MaxEntryAge` options to
  `InMemoryRemoteComputedCache.Options` and evict on `Flush`.

---

### F5. `Computed.TrySetOutput` publishes the `Consistent` flag before the output value

- **Severity:** LOW
- **Confidence:** PLAUSIBLE (write ordering is objectively wrong; I could not construct a
  reachable read path in the framework that skips the `GetValuePromise()` lock)
- **Category:** race
- **Location:** `src/ActualLab.Fusion/Computed.cs:378-379`
- **What:** Inside the lock, `_state` is flipped to `Consistent` *before* `_output` is
  assigned. Readers of `ConsistencyState` / `Output` / `Value` do not take the lock
  (`Computed.cs:79-97`, `:130-148`), so a reader that observes `Consistent` between the two
  stores reads the *initial* output — `Computed<T>.DefaultResult` = `default(T)`
  (`Computed.Typed.cs:12`) — i.e. `null` / `0` / `false` instead of the real value.
- **Why it matters / attack path:** The exposure window is two instructions wide, and every
  in-framework consumer I traced funnels through `Computed.GetValuePromise()` →
  `GetOrCreateValuePromise()` (`Computed.cs:202-207`), which takes the same monitor and
  therefore blocks until the producer leaves `TrySetOutput`. The residual risk is
  *application* code and public API surface that reads `computed.Value` / `.Output`
  directly after `ComputedImpl.TryUseExisting` returns `true` (e.g. via
  `Computed.UpdateUntyped()` → `ComputedInput.GetOrProduceComputed` → the
  `TryUseExisting` fast path at `ComputedInput.cs:61`). Because
  `ComputeMethodComputed<T>` registers itself in `ComputedRegistry` from its constructor
  (`Interception/ComputeMethodComputed.cs:24`), other threads can and do hold a reference
  to a still-`Computing` instance, so the pairing is real. Silent `default(T)` in a
  cached-authorization or cached-user-lookup method is the worst case (e.g.
  `Task<bool> IsBanned(...)` observing `false`).
- **Evidence:**

  ```csharp
  // src/ActualLab.Fusion/Computed.cs:374-380
  lock (Lock) {
      if ((_state & ConsistencyStateMask) != 0)
          return false;

      state = _state |= (int)ConsistencyState.Consistent; // flag published first
      _output = output;                                   // data published second
  }
  ```

  ```csharp
  // src/ActualLab.Fusion/Computed.cs:84-90 — unsynchronized read of the pair
  public Result Output {
      get {
          this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
          return _output;
      }
  }
  ```
- **Fix:** assign `_output` first, then flip the flag
  (`_output = output; state = _state |= (int)ConsistencyState.Consistent;`). If lock-free
  reads of `ConsistencyState` are meant to be a supported contract, also make the `_state`
  write a `Volatile.Write` and the read a `Volatile.Read` so the ordering holds on weak
  memory models.

---

### F6. `IFusionTime.Now(TimeSpan updatePeriod)` accepts an unclamped caller-supplied invalidation period

- **Severity:** LOW
- **Confidence:** CONFIRMED (by inspection)
- **Category:** dos (minor)
- **Location:** `src/ActualLab.Fusion/Extensions/Internal/FusionTime.cs:35-38`,
  `src/ActualLab.Fusion/Extensions/Internal/FusionTime.cs:81-82`
- **What:** `TrimInvalidationDelay` clamps only the *upper* bound
  (`TimeSpanExt.Min(delay, Settings.MaxInvalidationDelay)`). A zero or negative
  `updatePeriod` makes `ComputedExt.Invalidate(delay, ...)` take the
  `delay <= TimeSpan.Zero` branch (`ComputedExt.cs:62`) and invalidate the computed the
  instant it is produced; a sub-second positive value allocates a dedicated
  `CancellationTokenSource(delay)` + timer per computed (`ComputedExt.cs:78`).
- **Why it matters / attack path:** `AddFusionTime()` registers `IFusionTime` via
  `fusion.AddService<IFusionTime, FusionTime>()` with `RpcServiceMode.Default`
  (`Extensions/FusionBuilderExt.cs:16`), so in an app configured with
  `AddFusion(RpcServiceMode.Server)` the service is exposed over RPC to every connected
  client. A client can then call `Now(TimeSpan.Zero)` and get a compute result that is
  invalidated immediately, producing a produce→invalidate→notify cycle per call. There is no
  server-side amplification (the client must re-issue each call), and `GetMomentsAgo` with an
  extreme `Moment` degenerates the same way via the `(int)` cast at
  `FusionTime.cs:48` going negative — so this is a resource-waste/robustness issue rather
  than a true DoS. It is still a framework-shipped service with an unvalidated client input.
- **Evidence:**

  ```csharp
  public virtual Task<Moment> Now(TimeSpan updatePeriod)
  {
      Computed.GetCurrent().Invalidate(TrimInvalidationDelay(updatePeriod));  // no lower clamp
      return Task.FromResult(Clock.Now);
  }
  ...
  protected virtual TimeSpan TrimInvalidationDelay(TimeSpan delay)
      => TimeSpanExt.Min(delay, Settings.MaxInvalidationDelay);
  ```
- **Fix:** add a `MinUpdatePeriod` option (e.g. 100 ms) and clamp both ends:
  `TimeSpanExt.Min(TimeSpanExt.Max(delay, Settings.MinUpdatePeriod), Settings.MaxInvalidationDelay)`.
  In `GetMomentsAgo`, clamp `delta` to a sane range before the `int` cast.

---

### F7. `SharedRemoteComputedCache.Instance` is a mutable process-wide static shared by every service provider

- **Severity:** LOW
- **Confidence:** CONFIRMED (by inspection)
- **Category:** info-leak (conditional)
- **Location:** `src/ActualLab.Fusion/Client/Caching/SharedRemoteComputedCache.cs:12`,
  `:16-18`
- **What:** `public static RemoteComputedCache Instance { get; set; }` is assigned with
  `Instance ??= instanceFactory.Invoke()`, so the *first* `IServiceProvider` in the process
  that resolves `SharedRemoteComputedCache` wins, and every subsequent service provider
  silently reuses that instance regardless of its own configuration.
- **Why it matters / attack path:** In a process that hosts more than one Fusion client
  container — a MAUI/desktop app with multiple signed-in accounts, a host running several
  tenant-scoped clients, or an integration-test process — all of them share one cache
  keyed only by `(methodFullName, serializedArguments)`. If two containers are configured
  against different credentials/endpoints and a compute method's arguments do not include a
  session/tenant discriminator, container B can be served container A's cached response.
  Sharing is the documented intent of the type, but there is no guard (no
  `IsSameConfiguration` check, no per-container prefix) and the failure mode is silent.
  I did not find an in-repo configuration where two differently-scoped containers coexist,
  hence LOW.
- **Evidence:**

  ```csharp
  public static RemoteComputedCache Instance { get; set; } = null!;
  public SharedRemoteComputedCache(Func<RemoteComputedCache> instanceFactory)
      => Instance ??= instanceFactory.Invoke();
  ```
- **Fix:** at minimum, throw (or log a warning) when a second, differently-configured
  factory result is discarded; better, key the shared cache per `Options.Version` /
  container identity, or fold the container's identity into `RpcCacheKey.Name`.

---

### F8. `Invalidation.TrackingMode = WholeChain` retains an unbounded chain of invalidated `Computed` instances

- **Severity:** LOW
- **Confidence:** CONFIRMED (by inspection)
- **Category:** leak
- **Location:** `src/ActualLab.Fusion/InvalidationSource.cs:68-71`,
  `src/ActualLab.Fusion/Computed.cs:266-269`, `src/ActualLab.Fusion/Computed.cs:332`
- **What:** With `InvalidationTrackingMode.WholeChain`, `new InvalidationSource(Computed)`
  stores the invalidating `Computed` itself, and `Computed.Invalidate` persists it into
  `_invalidationSource`. Each invalidated computed therefore holds a strong reference to its
  invalidator, which holds a reference to *its* invalidator, and so on. Nothing ever clears
  `_invalidationSource`.
- **Why it matters / attack path:** On a busy server, one live computed at the tail of an
  invalidation chain pins the entire historical chain of already-invalidated computeds (and,
  through them, their `ComputedInput` → `Invocation` → argument objects). `ComputedGraphPruner`
  and `ComputedRegistry.PruneUnsafe` cannot help — these are strong references, not
  registry/weak ones. The default is `OriginOnly` (`Invalidation.cs:11`), which stores the
  origin's already-flattened `Value` (usually a `string`), so this only bites operators who
  turn on whole-chain diagnostics — typically exactly when they are already debugging a
  production incident.
- **Evidence:**

  ```csharp
  // InvalidationSource.cs:68-71
  public InvalidationSource(Computed value)
      => Value = Invalidation.TrackingMode is InvalidationTrackingMode.WholeChain
          ? value                              // strong ref to the invalidator
          : value.InvalidationSource.Value;
  ```

  ```csharp
  // Computed.cs:332-337 — the chain is built during propagation
  var nextSource = new InvalidationSource(this);
  _dependants.Apply(nextSource, static (source, usedByEntry) => { ... c.Invalidate(immediately: false, source); });
  ```
- **Fix:** store a `WeakReference<Computed>` (or a `ComputedRef`, which already exists at
  `src/ActualLab.Fusion/Internal/ComputedRef.cs`) instead of the raw `Computed` in
  `WholeChain` mode, and/or bound the retained chain depth. Also document `WholeChain` as a
  short-lived diagnostic mode.

---

## Out-of-partition findings

- **`RpcCacheInfoCapture.HasKeyAndValue` / `RequireKeyAndValue` are not null-safe** —
  `src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:49` and `:60` unconditionally
  dereference `Call`. This is the root cause of F2 and lives in the P2/P3 partition; the fix
  belongs there.
- **`RemoteComputed` has a finalizer that performs RPC bookkeeping**
  (`src/ActualLab.Fusion/Client/RemoteComputed.cs:63-73`): `~RemoteComputed() => Dispose()`
  calls `RpcOutboundCall.CompleteAndUnregister(...)`, which touches the peer's outbound-call
  registry, calls `CancellationTokenRegistration.Dispose()` (which *blocks* if the
  registration's callback is concurrently running on another thread) and can send a `Cancel`
  system call — all on the finalizer thread. `Dispose()` also never calls
  `GC.SuppressFinalize(this)`, so the finalizer still runs after an explicit dispose. I
  verified that `RpcTransport.Send` itself is non-blocking
  (`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:77`), which is why I rate this
  low, but a stalled finalizer thread halts *all* finalization process-wide. Suggested:
  `GC.SuppressFinalize` in `Dispose()`, and have the finalizer only queue the cleanup
  (`ThreadPool.UnsafeQueueUserWorkItem`) rather than perform it inline.

---

## Areas examined

Read in full (all of `src/ActualLab.Fusion/` except `Session/` and `obj/`):

- **Computed core:** `Computed.cs`, `Computed.Typed.cs`, `Computed.Static.cs`,
  `ComputedExt.cs`, `ComputedExt.ConsistencyState.cs`, `ComputedInput.cs`,
  `ComputedRegistry.cs`, `ComputedSource.cs`, `ComputedSourceExt.cs`,
  `ComputedSynchronizer.cs`, `ComputedVersion.cs`, `ComputeContext.cs`,
  `ComputeFunction.cs`, `ComputeFunctionExt.cs`, `ConsistencyState.cs`,
  `ConsolidatingComputed.cs`, `CallOptions.cs`, `ComputeMethodAttribute.cs`,
  `RemoteComputeMethodAttribute.cs`, `RemoteComputedCacheMode.cs`, `IComputeService.cs`,
  `ComputeServiceExt.cs`.
- **Invalidation:** `Invalidation.cs`, `InvalidationSource.cs`, `InvalidationSourceFormat.cs`,
  `InvalidationTrackingMode.cs`, `Internal/InvalidatedHandlerSet.cs`,
  `Internal/WhenInvalidatedClosure.cs`, `Operations/Internal/InvalidationFlags.cs`.
- **Internals:** `Internal/ComputedImpl.cs`, `Internal/ComputedImpl.Helpers.cs`,
  `Internal/ComputedGraphPruner.cs`, `Internal/ComputeContextScope.cs`,
  `Internal/ComputedSynchronizerScope.cs`, `Internal/ComputedRef.cs`,
  `Internal/ComputedSourceComputed.cs`, `Internal/ComputedOutputEqualityComparer.cs`,
  `Internal/FusionHub.cs`, `Internal/FusionModuleInitializer.cs`,
  `Internal/FuncComputedState.cs`, `Internal/FuncComputedStateEx.cs`,
  `Internal/SkipComputedRegistration.cs`, `Internal/SpecialTasks.cs`,
  `Internal/IHasInvalidationTarget.cs`, `Internal/IHasSynchronizationTarget.cs`.
- **Interception:** `Interception/ComputeMethodComputed.cs`, `ComputeMethodDef.cs`,
  `ComputeMethodFunction.cs`, `ComputeMethodInput.cs`, `ComputeServiceInterceptor.cs`,
  `ComputedOptionsProvider.cs`, `ConsolidatingComputeMethodFunction.cs`.
- **State:** `State/State.cs`, `ComputedState.cs`, `MutableState.cs`, `StateBoundComputed.cs`,
  `StateSnapshot.cs`, `StateExt.cs`, `StateFactory.cs`, `StateFactoryExt.cs`,
  `StateOptions.cs`, `StateCategories.cs`, `StateEventKind.cs`, `UpdateDelayer.cs`,
  `FixedDelayer.cs`, `State/Internal/*` (all 5 files).
- **Client / remote cache:** `Client/Interception/RemoteComputeMethodFunction.cs`,
  `RemoteComputeServiceInterceptor.cs`, `Client/RemoteComputed.cs`, `RemoteComputedExt.cs`,
  `Client/Caching/RemoteComputedCache.cs`, `RemoteComputedCache.Static.cs`,
  `FlushingRemoteComputedCache.cs`, `InMemoryRemoteComputedCache.cs`,
  `SharedRemoteComputedCache.cs`, `IRemoteComputedCache.cs`,
  `Client/Internal/{AlwaysSynchronized, RpcComputeCallType, RpcComputeSystemCallSender,
  RpcComputeSystemCalls, RpcInboundComputeCall, RpcOutboundComputeCall}.cs`.
- **RPC integration:** `Rpc/RpcComputeMethodDef.cs`, `RpcComputeServiceDef.cs`,
  `RpcInboundComputeCallHandler.cs`, `RpcOptionsExt.cs`, `RpcRegistryOptionsExt.cs`.
- **Operations:** `Operations/Completion.cs`, `IOperationCompletionListener.cs`,
  `OperationCompletionNotifier.cs`, `Operations/Internal/{CompletionProducer,
  CompletionTerminator, InMemoryOperationScope, InMemoryOperationScopeProvider,
  InvalidatingCommandCompletionHandler, NestedOperationLogger, Errors,
  FusionOperationsCommandHandlerPriority}.cs`,
  `Operations/Reprocessing/{OperationReprocessor, OperationReprocessorExt}.cs`.
- **UI / Diagnostics / Extensions / Blazor / Config / Testing:** `UI/*` (all 6 files),
  `Diagnostics/FusionMonitor.cs`, `Diagnostics/Internal/InvalidationPathCounter.cs`,
  `Extensions/{FusionBuilderExt, IFusionTime, RpcPeerRawState, RpcPeerState,
  RpcPeerStateMonitor}.cs`, `Extensions/Internal/FusionTime.cs`, `Blazor/*` (all comparers),
  `Configuration/{ComputedOptions, ComputedCancellationReprocessingOptions,
  FusionDefaultDelegates}.cs`, `Testing/ComputedTest.cs`, `Trimming/*`.
- **Wiring:** `FusionBuilder.cs`, `FusionRpcServiceBuilder.cs`, `ServiceCollectionExt.cs`,
  `ServiceProviderExt.cs`, `UI/ServiceProviderExt.cs`.

Read as supporting context (outside the partition):
`src/ActualLab.Core/Collections/ArrayBuffer.cs`,
`src/ActualLab.Core/Async/ExecutionContextExt.cs`,
`src/ActualLab.Core/Net/RetryDelayer.cs`,
`src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcPeerConnectionState.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs`,
`src/ActualLab.Rpc/RpcPeer.cs`, `src/ActualLab.Rpc/Internal/Errors.cs`,
`src/ActualLab.Rpc/RpcClientPeerReconnectDelayer.cs`.

Analyses performed and found **clean** (no finding):
- Dependency-graph mutation ordering in `Computed`: `AddDependency` / `AddDependant` /
  `RemoveDependant` / `PruneDependants` / the unlocked "instant invalidation" block are
  consistently gated on `ConsistencyState`, and the invalidating thread's lock
  acquire/release provides the needed happens-before for the unlocked reads.
- Lock ordering `MutableState.Lock → Computed monitors`: the invalidation propagation loop
  (`Computed.cs:330-338`) runs *outside* `lock(this)`, and `MutableState.CreateComputed` →
  `SetComputed` re-entry terminates because the predecessor is already `Invalidated`
  (traced end-to-end). No cycle found.
- `ComputedRegistry.Register` spin loop: each iteration makes progress; no livelock or
  unbounded spin identified.
- `ComputedRegistry` storage growth: bounded by live computeds plus a ≤2× dead-entry
  overhang enforced by `UpdatePruneCounterThreshold`.
- Per-peer isolation of `ComputeMethodInput` equality (proxy identity + full argument
  comparison, `ComputeMethodInput.cs:53-62`) — no cross-session sharing found; the
  registry hash is `string`-hash-derived and therefore not attacker-predictable.
- `RpcComputeSystemCalls.Invalidate` can only reach `peer.OutboundCalls` of the sending
  peer, so a client cannot invalidate another client's calls.
- `RpcInboundComputeCall`'s client-controlled `IsRegularCall`
  (`Context.Message.CallTypeId`) only lets a caller *opt out* of invalidation tracking; the
  inbound call type itself is chosen from the server-side `methodDef.CallType`
  (`RpcInboundCall.cs:39`), so the wire cannot force a compute call type onto a
  non-compute method.
- `WhenInvalidatedClosure` registration/CTS lifecycle in
  `RpcInboundComputeCall.ProcessStage2` — no leak found on either the invalidated or the
  cancelled path.
- `InvalidateWhenReconnected` awaiting a stale `RpcPeerConnectionState.WhenConnected`:
  correct, because `RpcPeer.SetConnectionState` calls `oldState.MarkConnected(newState)`
  (`RpcPeer.cs:637`).
- `FlushingRemoteComputedCache`'s manual `Monitor.Enter/Exit` around the flush handoff:
  balanced, and `FlushCts` is always replaced under the lock before the old one is used.
- `InvalidationPathCounter` key cardinality is bounded (code locations / command types /
  method names) and reset every collection period.

---

## Areas NOT examined

- `src/ActualLab.Fusion/Session/` — explicitly excluded from P5 (owned by P6). The
  Session JSON/MessagePack/Newtonsoft/TypeConverter files under
  `src/ActualLab.Fusion/Internal/Session*.cs` were also skipped for the same reason,
  even though they live in a P5 folder — they are session-serialization surface and
  belong with P6.
- `ActualLab.Rpc` internals beyond what was needed to prove/disprove a P5 finding: the
  outbound/inbound call registries, `RpcCacheKey`/`RpcCacheValue` construction and hashing,
  argument serialization, peer/handshake/reconnect state machine (P1/P2/P3).
- `ActualLab.CommandR` pipeline itself (handler resolution, `CommandContext`,
  `Operation`/`IOperationScope` contracts) — I read the Fusion-side handlers but treated
  the CommandR core as P8's scope. In particular `Completion.New`'s
  `typeof(Completion<>).MakeGenericType(command.GetType())`
  (`Operations/Completion.cs:44`) is only as safe as the operation-log deserializer's type
  resolution, which is P3/P6 territory.
- `ActualLab.Core` primitives used by `Computed` — `Timeouts` / `GenericTimeoutSlot` /
  `TimeoutSet`, `AsyncLockSet`, `StochasticCounter`, `RefHashSetSlim3` /
  `HashSetSlim3`, `AsyncState<T>`, `RecentlySeenMap` (P7). I assumed their documented
  thread-safety; `ArrayBuffer<T>` is the one exception, which I did read (F1).
- Blazor component integration outside `src/ActualLab.Fusion/Blazor/` — i.e.
  `src/ActualLab.Fusion.Blazor` and `ComputedStateComponent` (P6).
- Dynamic behaviour under load: I did not run the repository's test suite, benchmarks, or
  any stress/concurrency harness. F5 in particular is a nanosecond-scale window that would
  need a dedicated stress rig (and probably an ARM64 host) to observe, which is why it is
  rated LOW/PLAUSIBLE rather than higher.
- The other reviewers' outputs (`tmp/review-r2/codex-P5.log` etc.) were deliberately not
  read, to keep this pass independent.
