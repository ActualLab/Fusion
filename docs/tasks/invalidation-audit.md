# Distributed Invalidation Audit

This document records potential correctness and operational risks found while reviewing distributed invalidation. It is intended as a discussion document; the items below are not all confirmed defects.

**Verification status (2026-07-13):** items 1-7 were all verified against the current code and hold as written; the decisive references are noted per item. Items 8+ are additional findings from that verification pass, each labeled with confidence (confirmed = every link of the failure chain read in code; plausible = mechanism confirmed, trigger conditions uncommon).

## 1. Invalidation replay must not fail

`InvalidatingCommandCompletionHandler.TryInvalidate` catches exceptions thrown while replaying a command, logs them, and returns normally. As a result, completion processing and the operation-log reader treat the invalidation attempt as complete.

The design assumption is stronger than ordinary retry semantics: an invalidation pass must not fail, including through any transitive compute-method call it makes. Invalidation code must therefore be synchronous or otherwise guaranteed to complete, side-effect-free, independent of failure-prone infrastructure, and safe under the current service configuration.

The current behavior is acceptable only while this invariant holds. The remaining question is whether the invariant is sufficiently enforced and tested, especially for transitive calls and future handler changes. Possible safeguards include focused tests, analyzers, or a conservative fallback if an invariant violation is detected.

Relevant code:

- `ActualLab.Fusion/Operations/Internal/InvalidatingCommandCompletionHandler.cs` (verified: `TryInvalidate` catches all exceptions at lines 137-141, logs, and continues)
- Compute-service command handlers using `Invalidation.IsActive`

## 2. Completion listeners must not fail

`OperationCompletionNotifier` adds the operation UUID to `RecentlySeenUuids` before invoking completion listeners. It catches listener failures and still completes notification processing normally.

As with invalidation replay, the design assumption is that operation-completion listeners must not fail. Under this invariant, marking the UUID as seen before dispatch is safe, and suppressing unexpected listener exceptions prevents completion processing from destabilizing the command path.

The current behavior is acceptable while this invariant holds. The remaining question is whether the invariant is sufficiently enforced and tested for every registered listener. If it can be violated, redelivery within the deduplication window is rejected even though processing did not complete. This is especially relevant on the originating host because `DbOperationLogReader` skips operations whose `HostId` matches the local host, assuming local completion already processed them.

Possible safeguards include focused tests for all listener implementations and explicit documentation of their no-failure contract.

Relevant code:

- `ActualLab.Fusion/Operations/OperationCompletionNotifier.cs` (verified: UUID added at lines 58-61 before dispatch; listener failures swallowed at lines 84-95)
- `ActualLab.Fusion.EntityFramework/Operations/LogProcessing/DbOperationLogReader.cs` (verified: local-host skip at lines 31-34)

## 3. Operation reprocessing has a finite retry horizon

When operation-log batch processing fails, the main reader advances its in-memory cursor and schedules background reprocessing for the failed entry. The default operation reprocessing policy makes five attempts, with a per-attempt timeout of 30 seconds. If all attempts fail, reprocessing logs the failure and exits.

Operation entries are not discarded, but the reader cursor has already advanced, so nothing in the same process lifetime appears to schedule another attempt. A sufficiently long transient failure, configuration problem, or rolling-deployment incompatibility can therefore leave that host without the invalidation.

Potential directions include indefinite low-frequency retries, a durable pending/dead-letter mechanism, or retaining failed indexes in a retry set until successful processing.

Relevant code:

- `ActualLab.Fusion.EntityFramework/LogProcessing/DbLogReader.cs` (verified: for operations, the discard step of `Reprocess` is skipped at lines 152-153, so an abandoned entry is never revisited)
- `ActualLab.Fusion.EntityFramework/LogProcessing/DbOperationLogReader.cs` (verified: cursor advances unconditionally at line 64)
- `ActualLab.Fusion.EntityFramework/LogProcessing/IDbLogReader.cs` (verified: 5 tries, 30 s per-try timeout, delays 0.25-1 s at lines 51-54)

## 4. Command serialization couples invalidation to deployment compatibility

`DbOperation` persists the concrete command as polymorphic JSON. Other hosts deserialize that command before replaying its invalidation pass.

Rolling deployments may fail to replay operations when command types are renamed or removed, serialized shapes change incompatibly, polymorphic type names change, or nested-operation and operation-item types are unavailable. Such a compatibility failure manifests as stale cache state rather than only as a failed background job.

Potential safeguards include version-tolerant command contracts, compatibility aliases, and rolling-upgrade tests that write operations with one application version and replay them with the adjacent version.

Relevant code:

- `ActualLab.Fusion.EntityFramework/Operations/DbOperation.cs` (verified: polymorphic Newtonsoft JSON at line 18; `Serializer.Read<ICommand>` at line 53 throws on unknown types, which flows into item 3's finite retry horizon)

## 5. Correctness depends on manually maintained invalidation branches

Each mutating command handler must enumerate every directly affected compute call in its `Invalidation.IsActive` branch. Dependency propagation handles transitive dependants, but the infrastructure cannot discover a directly affected root call that the handler omitted.

Risks include adding a query without updating relevant commands, changing a query's data dependencies, forgetting broad aggregate queries, and accidentally allowing side effects during invalidation replay.

Potential safeguards include command-to-query integration tests, conventions or analyzers for mutating compute-service handlers, and tests proving that each command invalidates both entity-specific and aggregate queries.

Relevant examples:

- `tests/ActualLab.Fusion.Tests/Services/KeyValueService.cs`
- `tests/ActualLab.Fusion.Tests/Services/UserService.cs`

## 6. Initial operation-log positioning depends on wall-clock time

A new operation-log reader starts at the earliest entry whose `LoggedAt` is within `StartOffset`; the default offset is three seconds.

A fresh process normally has no old in-memory computed values, so skipping older operations is intentional. Edge cases may still matter when clocks differ by more than the offset, shards are introduced dynamically into a running host, persistent remote-computed caches participate, or initialization makes cached values available before the reader establishes its position.

A durable per-host/per-shard cursor would provide stronger ordering and recovery semantics, at the cost of checkpoint lifecycle and host-identity management.

Relevant code:

- `ActualLab.Fusion.EntityFramework/LogProcessing/DbOperationLogReader.cs` (verified: `GetStartEntry` filters `LoggedAt >= SystemClock.Now - StartOffset` at lines 133-138)
- `ActualLab.Fusion.EntityFramework/LogProcessing/IDbLogReader.cs`

## 7. Command completion is not a cluster-wide freshness boundary

After a transaction commits, invalidation still has to pass through local completion handling, watcher notification or polling, operation replay on other hosts, and RPC invalidation to clients. During that interval, another host or client may observe a cached pre-command value.

This appears to be an intentional eventual-consistency property, but callers that require cross-host read-your-write semantics need an explicit version or synchronization mechanism rather than treating command completion as confirmation that every dependent cache has been invalidated.

## 8. Index-gap retry horizon is shorter than worst-case commit reordering

Confidence: confirmed mechanism; occurrence requires unusual commit latency.

A `DbOperation` index is assigned by `SaveChangesAsync` but becomes visible to other hosts only at `CommitAsync` (`DbOperationScope.cs`, lines 268-275). If transaction T1 (index N) commits more than a few seconds after a later-indexed T2 already committed, readers on other hosts see the gap at N, schedule gap reprocessing, and exhaust it: the 5-try policy's delays sum to roughly 2.75 s, and `LogEntryNotFoundException` fails each attempt instantly, so the 30 s per-try timeout never extends the window. Once exhausted, index N is skipped forever on those hosts — a cluster-wide missed invalidation. Slow fsync, synchronous replication lag, `ChaosMaker`, or a failover pause can all stretch commit past the horizon.

Relevant code:

- `ActualLab.Fusion.EntityFramework/Operations/DbOperationScope.cs` (lines 268-275)
- `ActualLab.Fusion.EntityFramework/LogProcessing/DbOperationLogReader.cs` (`GetProcessTasks`, lines 82-83)
- `ActualLab.Fusion.EntityFramework/LogProcessing/IDbLogReader.cs` (lines 51-54)

## 9. Identity-cache jumps cause unthrottled gap-reprocess storms

Confidence: confirmed mechanism.

`GetProcessTasks` schedules one fire-and-forget `Task.Run` per missing index between the cursor and each batch entry, with no throttling (`DbOperationLogReader.cs`, lines 82-83; `DbLogReader.ReprocessSafe`, lines 124-143). SQL Server's identity cache skips up to 10 000 bigint values on restart or failover, so a single DB restart can enqueue thousands of concurrent reprocess tasks, each performing up to 5 `DbContext`-creating queries — right when the database is recovering. The main reader loop also stalls until `ReprocessTasks.Count` drops below `BatchSize` (`DbLogReader.cs`, lines 54-61). Capping the number of concurrently scheduled gap tasks (or detecting large jumps and skipping them wholesale) would remove the storm.

## 10. Reader outage longer than the trim age silently loses invalidations

Confidence: confirmed mechanism.

`DbOperationLogTrimmer` deletes operations after `MaxEntryAge` = 30 minutes. A host whose log reader cannot reach the database retries forever while its in-memory computed values remain valid and continue to be served. If the outage (or reader stall) exceeds the trim age, the missed entries are deleted before the reader recovers; the gap-reprocess path then abandons them (item 3), and the host serves stale values indefinitely without restarting. The dedup window (`OperationCompletionNotifier.Options.MaxKnownOperationAge` = 15 min) and trim age are individually reasonable, but nothing ties reader progress to trimming — a durable per-host cursor (item 3) or a "reader lag exceeds trim age" alarm would close this.

Relevant code:

- `ActualLab.Fusion.EntityFramework/Operations/LogProcessing/DbOperationLogTrimmer.cs` (line 23)

## 11. In-doubt commit verification can misreport, leaving the origin host stale

Confidence: confirmed mechanism; requires an in-doubt commit to trigger.

`DbOperationScope.Commit` correctly verifies row existence when `CommitAsync` throws (lines 280-317), but the verification path has three weaknesses:

- It runs on the caller's cancellation token (lines 284, 294, 302). If the token fired while COMMIT was in flight, every verification call throws instantly, the bare `catch { }` at lines 312-314 swallows it, and the commit is reported as failed even though it landed.
- It is single-shot with no retry and no logging, so a still-unreachable database at verification time produces the same false negative.
- When `MustStoreOperation` is false, the fake `DbEvent` verifier row never sets `DelayUntil` (default `0001-01-01`, `DbEvent.cs` lines 46-56), so it satisfies the event trimmer's condition (`DelayUntil <= minDelayUntil && State != New`) immediately instead of surviving for the intended `MaxEntryAge` = 1 h.

A false "not committed" verdict is uniquely damaging on the origin host: local notification is gated on `scope.IsCommitted == true` (`InMemoryOperationScopeProvider.cs`, lines 45-49) and the log reader skips own-host entries (item 2), so every other host invalidates while the origin serves stale values indefinitely — and reports failure to the caller for a mutation that persisted. `OperationReprocessor` then retries with a fresh operation UUID (`OperationReprocessor.cs`, line 152), so the dedup by UUID cannot prevent double execution. Verification should run on `CancellationToken.None` with a short retry, and `DbEvent(Operation)` should set `DelayUntil`.

Related minor observations: `Operation.Index` is not set on the verified-commit path (lines 296-305); `DbOperationFailedException` is dead code.

## 12. Retry-after-manual-commit can double-execute

Confidence: plausible; requires unconventional user code.

`OperationReprocessor.WillRetry` checks only the scope type, never `Scope.IsCommitted` (`OperationReprocessor.cs`, lines 111-112). A handler that calls `scope.Commit()` manually (it is public) and then throws a transient error gets its already-committed-and-invalidated command re-executed. A `IsCommitted == true` guard in `WillRetry` would close this.

## 13. `Operation.Items` is shared mutable state during capture and replay

Confidence: confirmed sharing; harm depends on user code.

`NestedOperationLogger` backs up, swaps, and restores `Operation.Items` non-atomically around each nested command (lines 31-43), and `Operation.NestedOperations` is updated with an unlocked read-modify-write (unlike `Events`, which takes a lock). Two nested commands run in parallel inside one handler interleave these swaps. During replay, `InvalidatingCommandCompletionHandler` reassigns the live `Operation.Items` per nested command (line 120) while completion listeners run concurrently and may read it. Built-in listeners do not read `Items`, so only custom listeners and parallel nested commands are exposed.

## 14. Notification watchers: silent failure and a broken self-skip

Confidence: confirmed; impact is latency/noise only (the 5 s `CheckPeriod` poll bounds staleness).

- `NpgsqlDbLogWatcher.NotifyChanged` swallows every failure without logging or rethrowing (lines 104-109), so `DbOperationCompletionListener`'s `NotifyRetryPolicy` never sees it — a failed NOTIFY is neither retried nor visible. The FileSystem and Redis watchers propagate correctly.
- `RedisDbLogWatcher` publishes `NotifyPayload = ""` while the subscriber skips messages matching `hostId.Id` (lines 46, 59), so the self-notification filter never matches and every host — including the publisher — wakes on every operation. The payload should be `hostId.Id`, mirroring the Npgsql watcher.

## 15. Invalidation replay is silently skipped for non-conventional handlers

Confidence: confirmed behavior; whether it bites depends on user handler style.

`InvalidatingCommandCompletionHandler.IsRequired` demands the final handler be an `IMethodCommandHandler` with exactly two parameters on an `IComputeService` (lines 91-107). A command handled by an interface-style `ICommandHandler<T>` implementation on a compute service — or a method with an extra parameter — gets no invalidation replay and no diagnostic. This is the enforcement gap of item 5 in concrete form; a one-time warning when a compute service's handler shape disqualifies it would make the failure observable.

## 16. Cache-hit update path double-binds the RPC call: the fresh computed is born invalidated

Confidence: confirmed (full chain read).

In `ApplyRpcUpdate`, the call is bound to the cache-served computed at step 3 (`RemoteComputeMethodFunction.cs`, line 339) and then passed to its replacement at step 8 (line 388). The replacement's constructor registers it, which displaces and synchronously invalidates the still-consistent cached computed (`ComputedRegistry.cs`, lines 126-128); its `OnInvalidated` finds `CallSource` already bound and calls `SetInvalidated` on the shared call (`RemoteComputedExt.cs`, lines 26-36); the constructor then observes `call.WhenInvalidated` completed and invalidates the brand-new computed (`RemoteComputed.cs`, lines 57-59). Net effect: whenever a cached value differs from the server value — exactly the case remote caching optimizes — the updated computed self-destructs at birth, dependants immediately re-request (an extra RPC round trip), and a spurious `Cancel` is sent to the server. No staleness results; this fails safe but wastes the hot path.

## 17. Serve-stale-on-disconnect never completes the predecessor's `SynchronizedSource`

Confidence: confirmed skipped completion; the hang applies to `ComputedSynchronizer.Precise`.

Both serve-stale branches in `ComputeRpc` (`RemoteComputeMethodFunction.cs`, lines 174-184 and 204-218) return a new stale computed without executing line 247, the only place a predecessor's `SynchronizedSource` is completed on that path (the only other completion sites are `ApplyRpcUpdate` lines 381 and 390). Once a computed is superseded through a serve-stale branch, its `WhenSynchronized` can never complete — violating the invariant documented in `RemoteComputed.OnInvalidated` (lines 81-91). `ComputedSynchronizer.Safe` caps the damage at its 5 s timeout; `Precise` waits unboundedly, so a caller synchronizing on a computed superseded during a disconnect window hangs permanently, even after reconnect. `ComputedSynchronizer.Safe` also consults only `RpcHub.DefaultPeer` connectivity (line 193), which mismatches multi-peer routing in both directions.

## 18. `Invalidation.Begin` leaks into spawned work with silent misbehavior

Confidence: confirmed mechanism; a user-code footgun rather than a framework bug.

`Task.Run` (or any continuation) started inside an `Invalidation.Begin` scope captures the ExecutionContext, and disposing the scope does not undo captured snapshots. A compute method invoked under the leaked context does not compute: it invalidates the existing computed and silently returns `default(T)` (`ComputedImpl.Helpers.cs`, lines 41-51, 110-113). The same applies to `Invalidated`-event handlers, which run synchronously inside the invalidation pass. Framework code consistently protects itself with `BeginIsolation()`; user code gets neither a guardrail nor a diagnostic. Worth a documentation warning and possibly a debug-mode check, since "silent default plus spurious invalidation" is very hard to attribute in the field.

## 19. Assorted lower-severity observations

- `ConsolidatingComputed.Consolidate` leaves a faulted `_whenConsolidated` task unobserved when `UpdateUntyped` throws (e.g. `RpcRerouteException`), producing `UnobservedTaskException` noise (confidence: plausible).
- A nondeterministic error classified NonTransient (`NullReferenceException`, `InvalidOperationException`, ...) thrown before any dependency was awaited yields a computed with no inbound edges and no auto-invalidation; for a `ComputedState`, the snapshot retains it strongly, so the state is stuck on the error until external invalidation or restart (confidence: plausible).
- `RemoteComputed.Dispose()` is public and, when called on a live consistent computed, unregisters the call without invalidating — the server stops tracking while the client keeps serving the value: permanently stale on misuse (confidence: confirmed, misuse-only).
- A user-supplied `TransiencyResolver` that throws skips `StartAutoInvalidation` after the error result is already committed, permanently caching the error (confidence: speculative).

## Existing strengths

The local dependency graph uses computed-input versions to prevent old computed instances from invalidating newer replacements. The server-to-client RPC path keeps compute calls registered after returning their result, sends a related invalidation system call when the server-side computed becomes invalid, and pessimistically invalidates when reconnection cannot safely preserve the result. Reconnection tests cover several disconnect stages.

The verification pass additionally confirmed these areas sound:

- The registry/GC design has no lost-invalidation path for observable computeds: dependant-to-dependency edges are strong references, dependency-to-dependant edges are version-checked weak lookups, and consistent computeds cannot be unregistered. The version-check claim above holds exactly as stated (`Computed.cs`, lines 313-315, 482-483).
- Register/invalidate races during computation are closed via `InvalidateOnSetOutput` under the computed's lock; add-dependency races on already-invalidated dependencies invalidate the dependant instead of dropping the edge.
- A lost server-to-client `Invalidate` message cannot cause staleness: the server unregisters before sending, so the next reconnect reports the call unknown and the client pessimistically invalidates; half-open connections are bounded by 10 s keep-alives with a 25 s cutoff.
- In-doubt commits are verified by row lookup on a fresh connection (modulo item 11's edges), local completion ordering guarantees invalidation before the command returns, and a lost watcher notification costs at most ~5.5 s via the `CheckPeriod` poll — with no lost-wakeup race in `DbLogReader`.
- Crash between commit and local invalidation is safe: restart clears in-memory state, and other hosts read the log themselves.

The main area for further discussion is the acknowledgement and recovery model around operation replay (items 3, 8-11), followed by deployment compatibility and enforcement of invalidation-handler invariants (items 4, 5, 15), and the serve-stale/synchronization edges of the RPC client path (items 16-17).
