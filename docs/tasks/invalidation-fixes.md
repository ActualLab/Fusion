# Distributed Invalidation — Course of Action

Companion to [invalidation-audit.md](invalidation-audit.md). This document records the **agreed** approach for each audit item, along with the alternatives that were considered and explicitly rejected or deferred. Implementing agents: the choices below are deliberate decisions, not defaults — do not substitute a rejected alternative without raising it first. Item numbers reference the audit document.

Status legend: **decided** — approach agreed, ready to implement; **pending** — options on the table, decision not made yet; **✅ shipped** — implemented and merged, with the fixing commit hash noted inline.

Shipped so far (all on `master`, 2026-07-14): `78bdb0dd` item 16, `376ea552` item 17, `72eb4c83` docs-only batch, `fab0f349` item-19 residuals + `IState.Invalidated`, `64979788` `KeyConflictStrategy` #4049. Everything not marked ✅ below remains pending implementation.

## Item 11: In-doubt commit verification (`DbOperationScope.Commit`)

Status: **decided**.

### Agreed course of action

1. **Fix the verifier row lifetime.** `DbEvent(Operation)` ctor sets `DelayUntil = model.LoggedAt`, so the fake commit-verifier row survives the event trimmer's `MaxEntryAge` (1 h) as intended instead of being trimmable from the moment it is committed.

2. **Harden the verification read.** The verification block in `DbOperationScope<TDbContext>.Commit` (the `catch` around `Transaction.CommitAsync`) changes as follows:
   - The verification is executed through a **retry policy** (`ActualLab.Resilience.IRetryPolicy`), wrapped around the create-context + `FindAsync` sequence.
   - The caller's `cancellationToken` is **not** passed into verification. Instead, the policy's `TryTimeout` provides the per-attempt cancellation: `RetryPolicy.Apply` already creates a linked, timeout-derived token per try and passes it to the task factory — verification code uses *that* token, with `CancellationToken.None` as the outer token. This makes verification immune to the command's cancellation while keeping every attempt bounded by a configurable timeout.
   - The policy is exposed as a **configurable option** following the established options pattern: a `CommitVerificationPolicy` (name may be adjusted to match surrounding conventions) `IRetryPolicy` property on `DbOperationScope.Options`, with a conservative default (small try count, a few-second per-try timeout, short exp backoff). Users override it the same way as every other policy option (e.g. `DbOperationLogReaderOptions.ReprocessPolicy`).
   - The silent `catch { }` around verification is replaced with an **error log** on final failure. Behavior on inconclusive verification stays as today: rethrow the original commit error.

### Rejected alternatives

- **Bare `CancellationToken.None`** for the verification calls: correct in spirit (the read decides correctness and must not be aborted by the caller), but unbounded — a hung database would block `Commit` indefinitely inside an error path. Rejected in favor of the policy's timeout-derived token.
- **Ad-hoc CTS with a hardcoded timeout**: subsumed by the retry policy's `TryTimeout`, which gives the same boundedness plus configurability and retries in one mechanism.
- **Changing the event trimmer predicate** (e.g. also requiring `LoggedAt <= minLoggedAt`): fixes the class of "forgot `DelayUntil`" bugs but touches the trim query whose shape matches the `(DelayUntil, State)` index and changes behavior for all events. The one-line ctor fix is sufficient.
- **A dedicated verifier row type** instead of the fake `~op-` `DbEvent`: cleaner separation, but a schema addition to solve what one line solves. Not worth it now.

### Deferred (separate decisions, not part of this item's implementation)

- **Distinct "commit in doubt" exception** (possibly repurposing the currently-dead `DbOperationFailedException`) thrown when verification is inconclusive, treated as non-retriable by `OperationReprocessor` — surfaces "unknown outcome" honestly instead of risking double execution.
- ~~Stable operation UUID across `OperationReprocessor` retries~~ — decided separately, see "Item 11 follow-up" below.
- Cleanups noted in the audit: `Operation.Index` is not set on the verified-commit path; `DbOperationFailedException` is dead code (unless repurposed above).

## Item 11 follow-up: stable operation UUID across `OperationReprocessor` retries

Status: **decided**.

### Agreed course of action

Preserve `Operation.Uuid` across reprocessor retries: the reprocessor resets operation state but keeps the UUID assigned to the logical command execution (today `context.ChangeOperation(null)` produces a fresh UUID per attempt). On commit, a unique-constraint violation on `Uuid` becomes a signal: verify by UUID lookup → row exists → the previous attempt actually committed → report success instead of executing the command's effects twice. Combined with item 11's hardened verification, this makes DB-level double execution structurally impossible for in-doubt commits.

Implementation caveats to address:

- Handler side effects outside the DB still run twice on retry — unavoidable; document.
- `RecentlySeenUuids` dedup then correctly suppresses a duplicate completion notification — verify this in tests rather than assuming it.
- Check `DbEvent` UUID conflict-strategy interplay on the retry path.

### Rejected alternatives

- **Deterministic UUID assigned at command entry** (in `CommandContext` before the pipeline runs): same effect, different plumbing; not needed unless operation identity must survive scope recreation entirely.
- **Relying on item 11's verification alone**: leaves a residual double-execution window for no reason given how cheap UUID preservation is.

## Item 12: `OperationReprocessor.WillRetry` ignores `IsCommitted`

Status: **decided**.

### Agreed course of action

Add the guard: no retry when `operation.Scope is { IsCommitted: true }` — a committed operation must never re-enter the pipeline regardless of what threw afterward. When the guard suppresses a retry that would otherwise have happened, **log a dedicated reason** (i.e., make "retry denied because the operation is already committed" explicitly distinguishable in logs from ordinary no-retry outcomes).

Rejected alternative: docs-only guidance ("don't call `scope.Commit()` manually / don't throw after it") — `Commit` is public; the framework should enforce its own invariant.

## Item 3 (residual): failed-entry abandonment in the operation log

Status: **decided**.

### Agreed course of action

Entries that are present but fail processing (in practice: `ToModel()` deserialization failures, since `NotifyCompleted` swallows listener errors) join the same per-shard pending set as item 8's gaps after the quick `ReprocessPolicy` is exhausted — but with a **bounded retry budget** instead of the full trim-age horizon: roughly **10 further attempts on the regular cadence** (configurable), to avoid a flood of repeated errors from permanently-failing entries. After the budget is exhausted, the entry is abandoned with a single **Error-level** log (loss must be loud — same principle as item 10), not one error per attempt.

### Rejected / deferred alternatives

- **Retrying until the trim-age horizon** (as gaps do): a permanently-failing entry would emit errors for 30 minutes — the flood outweighs the marginal self-heal benefit beyond ~10 attempts.
- **Durable dead-letter table**: survives restarts but adds schema and lifecycle; deferred, same as reader-progress-aware trimming (item 10).
- **Keep 5-tries-and-drop, add Error log only**: loses self-healing for medium-length transient failures.

## Item 4: Deployment compatibility of serialized commands

Status: **✅ shipped** in `72eb4c83` — documentation only; no code change.

Document the compatibility contract explicitly: command types (including nested-operation and operation-item types) must remain deserializable for the operation trimmer's `MaxEntryAge` (30 min by default) past their last producer — i.e., do not rename/remove a command type and deploy within that window; stage such changes across releases. Violations become immediately visible through item 3's Error-level abandonment logging rather than manifesting as silently stale caches.

### Rejected / deferred alternatives

- **Type-alias hook in the serialization binder** (old-name → type mapping for planned renames): a reasonable escape hatch, but add it on demand when a real rename needs it — not up front.
- **Rolling-upgrade round-trip tests in CI** (serialize with version N, deserialize with N+1): most rigorous, but requires CI plumbing against a previous release; not justified yet.
- **Version-tolerant binary format for the operation log**: massive migration for a 30-minute-windowed compatibility problem; rejected.

## Item 13: `Operation.Items` / `NestedOperations` shared mutable state

Status: **decided**.

### Agreed course of action

Make the `Operation.NestedOperations` read-modify-write in `NestedOperationLogger` locked, matching the locking style `Operation.Events` already uses. That closes the parallel-nested-commands race on capture.

**No detached/shallow copy for the invalidation replay** in `InvalidatingCommandCompletionHandler`: by the time the invalidation pass runs, the operation's item bags are supposed to be stable — they were already snapshotted/serialized during capture and commit — so replaying over the live `Operation` instance is acceptable by design. The `Items` pointer cycling during replay is not treated as a defect.

### Rejected alternatives

- **Replay over a detached `Operation` copy**: unnecessary given the stability-by-then invariant above.
- **Fully concurrency-safe capture** (per-branch item bags merged at completion): overkill.

## Item 8 (+9): Index-gap retry horizon / gap-reprocess storms

Status: **decided**.

### Agreed course of action

**Per-shard pending-gap set with batched re-checks**, replacing the current per-index fire-and-forget reprocess tasks for gaps. Details:

1. **Pending-gap set.** `DbOperationLogReader` keeps unresolved gap indexes per shard. Gaps are added when `GetProcessTasks` detects missing indexes; they leave the set when the entry appears (process it) or when the gap outlives the horizon (natural choice: the operation trimmer's `MaxEntryAge` — past that the entry would be trimmed anyway). The set gets a size cap and, ideally, a metric/log for drops. This also eliminates item 9's storm: an identity jump becomes set entries re-checked in batches, not thousands of concurrent `Task.Run`s.

2. **Batched re-check query.** All pending gaps for a shard are re-checked with a single `pendingGaps.Contains(e.Index)` query, chunked (e.g. 256-512 indexes per query) to stay well below provider parameter limits (SQL Server: 2100). EF translation is efficient across the supported matrix: EF 10 translates parameterized-collection `Contains` to a padded multi-scalar-parameter `IN (@p1, @p2, ...)` (plan-cache friendly, cardinality-aware — new EF 10 default, replacing EF 8/9's `OPENJSON`); Npgsql uses native array `= ANY(@p)`; pre-EF8 TFMs inline constants — acceptable for small chunks. No custom SQL needed.

3. **Re-check must not block on uncommitted rows.** Use the existing `DbHint` / `WithHints` mechanism to skip locked rows where the provider needs it. Note the current state: MVCC snapshot reads (PostgreSQL, MySQL/InnoDB, SQL Server with RCSI) never block on uncommitted inserts, so no hint is required there; the blocking risk is locking-read configurations (SQL Server without RCSI), for which no `DbHintFormatter` currently exists — hints are a no-op without a formatter, which is acceptable for now but should be documented.

4. **Adaptive check period.** While the pending-gap set is non-empty, the reader must not wait for the normal `CheckPeriod` (5 s). Two tiers, both configurable options on `DbOperationLogReaderOptions`:
   - `GapCheckPeriod` (default **1 s**) — applies while any pending gap is *young* (age below the next option). This bounds invalidation latency in the real race this fix targets: a slow commit landing after later-indexed entries were already processed. Sub-second polling was considered and rejected: the log watcher's change notification already wakes the reader the moment the slow commit lands (the committing host notifies post-commit), so the fast poll is only the fallback for lost notifications and crashed-mid-commit writers — 1 s is a good bound for that; faster multiplies per-shard DB load for marginal gain.
   - `GapFastCheckAge` (default ~**1 min**, name to be aligned with codebase conventions) — once every pending gap is older than this, the gaps are almost certainly dead (aborted transactions and identity-cache jumps burn indexes permanently), so re-checks fall back to the normal `CheckPeriod` cadence until the horizon expires them. This prevents a dead gap from sustaining 1 s polling for the full 30-minute horizon.
   - Implementation note: the reader's existing early-wake machinery (`sleepUntil` / `timeoutCts.CancelAfter` in `ProcessNewEntries`) supports this without new loop structure; watcher wake-ups re-check pending gaps for free.

### Rejected alternatives

- **Gap-specific reprocess policy** (low-frequency per-index retries spanning minutes): minimal change, but keeps the per-index task model (does not fix item 9) and still has an arbitrary retry cliff.
- **Eliminating index-visibility ambiguity at the source** (gapless publish watermark, DB-specific snapshot checks such as `pg_snapshot_xmin`): strongest guarantee, but DB-specific, invasive, and costly for commit concurrency. Reserved for the future if the pending-set approach proves insufficient.

## Item 17: Serve-stale never completes predecessor's `SynchronizedSource`

Status: **✅ shipped** in `376ea552` — confirmed by failing tests (2026-07-14), then fixed (approach A + the `Safe` routed-peer fix; optional defense-in-depth B not added).

### Confirmation

`FusionRpcServeStaleTest` (channel-pair transport + `InMemoryRemoteComputedCache`; `RpcInboundCallDelayer` holds calls open for the mid-call race) reproduces both serve-stale branches:

- `SupersededStaleComputedMustSynchronizeTest` (not-connected-at-entry branch): two serve-stale generations while disconnected, then reconnect + resync. The direct predecessor of the final recompute synchronizes (the normal-exit completion works), but the superseded stale computed's `WhenSynchronized` **times out even after reconnect and successful resync** — the permanent `ComputedSynchronizer.Precise` hang.
- `MidCallDisconnectStaleComputedMustSynchronizeTest` (disconnect-racing-`SendRpcCall` branch): same outcome.

Note from writing the tests: a delay configured on the DI-resolved service proxy does not reach the instance serving inbound calls — server-side delays in such tests must use `RpcInboundCallDelayer`.

### Agreed course of action

**A. Chain the source to the successor.** Serve-stale computeds share (or complete-on-completion-of) the predecessor's `SynchronizedSource`, so whichever successor finally confirms against the server completes the whole chain. Preserves the "completed = confirmed against server" meaning for every waiter. Optionally add **B** as defense-in-depth: `ComputedSynchronizer.Precise` re-resolves the current computed from the registry instead of waiting on a superseded instance's source.

Also included: fix `ComputedSynchronizer.Safe` to consult the computed's actual routed peer rather than `RpcHub.DefaultPeer`.

### Rejected alternatives

- **B alone** (fix only the consumer): leaves the `RemoteComputed` invariant broken for every other `WhenSynchronized` consumer.
- **C. Complete the predecessor's source in the serve-stale branch**: it would make "synchronized" no longer mean "confirmed against server".

## Item 16: Cache-update path double-binds the RPC call

Status: **✅ shipped** in `78bdb0dd` — confirmed by failing test (2026-07-14), then fixed via approach A (explicit call hand-off marker).

### Confirmation

`KeyValueServiceWithCacheTest.CacheHitUpdateWithChangedValueTest` (two client service providers sharing one cache; server value changed between them; a server-side `RpcInboundCallCounter` middleware counts wire-level `Get` calls) surfaces all three symptoms in one run:

- the computed produced by the update pass is **born invalidated** (`IsConsistent()` is false);
- a follow-up capture returns a **different instance** instead of a registry hit;
- the server receives **3 wire-level calls instead of 2** — the extra RPC round trip. A body-level counter cannot observe it: the server-side compute cache still holds the consistent value, so the method body never re-executes; counting must happen at the RPC middleware level.

### Agreed course of action

**A. Explicit call hand-off marker.** Before step 8 registers the successor, mark the call as handed off (flag or successor reference on `RpcOutboundComputeCall`); `BindToCallFromOnInvalidated` skips `SetInvalidated` for handed-off calls. The displaced cached computed is still invalidated, but no longer poisons the shared call.

### Rejected alternatives

- **B. Invalidate the cached computed explicitly before `Register`** with an `InvalidationSource` that `RemoteComputed.OnInvalidated` recognizes as "superseded, don't touch the call": same idea via invalidation source instead of call state; makes `InvalidationSource` load-bearing control flow.
- **C. Give the successor its own call**: reintroduces the extra RPC round trip by design.

## Item 10: Reader outage longer than trim age

Status: **decided**.

### Agreed course of action

**Detect coverage loss → pessimistic local sweep, logged as an error.**

1. **Detection.** On recovery, the reader detects that its cursor points below the oldest surviving log entry and the missing range was trimmed (i.e., invalidation coverage was lost for this host).
2. **Remediation.** Invalidate all locally registered computeds (full registry sweep) — the same pessimism the RPC layer applies on unsafe reconnect. A one-time recompute stampede on the affected host is the accepted price for guaranteed correctness.
3. **Logging.** The detection and the sweep **must be logged at Error level** — this is an abnormal condition operators need to see, and it doubles as the lag alarm.

### Rejected / deferred alternatives

- **Reader-progress-aware trimming** (hosts persist heartbeat + last-processed index; trimmer trims below `min(live hosts' cursors)` with a hard age cap): avoids the stampede and is a stepping stone toward durable per-host cursors (audit items 3/6), but adds a table and host-liveness lifecycle. Deferred — revisit only if the sweep stampede proves painful in practice.
- **Alarm only** (metric/log without remediation): subsumed by the error logging in the agreed approach.

## Cheap wins batch (items 14, 15, 19)

Status: **decided** (item 19 ✅ shipped in `fab0f349`; items 14 & 15 still pending):

- Item 14 (**pending**): `NpgsqlDbLogWatcher.NotifyChanged`: log + rethrow so `DbOperationCompletionListener.NotifyRetryPolicy` actually applies.
- Item 14 (**pending**): `RedisDbLogWatcher`: publish `NotifyPayload = hostId.Id` so the self-notification skip works, mirroring the Npgsql watcher.
- Item 15 (**pending**): one-time **Information-level** log (explicitly not Warning) when a compute service's final handler shape disqualifies invalidation replay (`InvalidatingCommandCompletionHandler.IsRequired`).
- Item 19 (**✅ shipped** in `fab0f349`): observe/log the faulted `_whenConsolidated` task in `ConsolidatingComputed.Consolidate`.

## Item 19 residuals: RPC/local hardening

Status: **✅ shipped** — residual #1 (docs) in `72eb4c83`; residuals #2 and #3 in `fab0f349`. Note: #3 was implemented as a new dedicated `NonTransientErrorInvalidationDelay` option (default 30 s; `TimeSpan.MaxValue` for `MutableState` to preserve its manual-error semantics) rather than repurposing `AutoInvalidationDelay`; `ComputedState` inherits it via `StateOptions.ComputedOptions` (no `ComputedState.cs` change). Test-suite compatibility for the 30 s default was verified — no test encodes an "errors cache forever" assumption that breaks. Follow-up in `c6e23027`: `NonTransientErrorInvalidationDelay` is also settable per method via `[ComputeMethod(NonTransientErrorInvalidationDelay = …)]` (mapped in `ComputedOptions.Get` like the other delays, inherited by `RemoteComputeMethodAttribute`), documented in `docs/PartF-CO.md`.

### Agreed course of action

1. **`RemoteComputed.Dispose()` on a live computed** — documentation only. Nothing practical can be done in code; the method stays public (finalizer path needs it). Document that explicitly disposing a live consistent `RemoteComputed` orphans it (server stops tracking, client keeps serving the value) and must not be done.

2. **Throwing user `TransiencyResolver`** — treat a resolver failure as a **Transient** error classification and log it (if feasible at that point in `Computed.IsTransientError` / `StartAutoInvalidation`), so a broken resolver can no longer suppress auto-invalidation scheduling and permanently cache an error.

3. **Non-transient error auto-invalidation** — set a **nonzero default delay (30 s)** for auto-invalidation of error results classified non-transient, in **both default `ComputedOptions` and the default `ComputedState` options** (not state-specific: general computed options). Rationale: a nondeterministic error classified NonTransient (race-induced NRE etc.) in a dependency-less computed currently stays cached until external invalidation or restart; a 30 s error horizon fixes that class without causing invalidation cascades (errors rarely have wide dependant graphs). Explicit requirement: **scan the test suite for compatibility with this default change** — it is not expected to break tests, but that must be verified, not assumed.

## Completion-command failure is terminal for external operations

Status: **decided**. (Verification-pass finding adjacent to audit items 2/3; not in the audit doc's numbered list.)

### Agreed course of action

**Propagate + unmark, feeding the existing retry machinery.** When the completion command produced by `CompletionProducer` fails, the failure is treated as notification failure: the operation's UUID is removed from `RecentlySeenUuids` (`TryRemove` exists) and the failure surfaces to the `NotifyCompleted` caller, so `DbOperationLogReader.Process` throws and the entry enters the item-3-residual bounded retry path (~10 attempts on regular cadence, single Error on abandonment). This converts the "completion listeners must never fail" assumption (audit items 1/2) into defense-in-depth backed by real redelivery for external operations.

### Rejected alternatives

- **Local retry inside `CompletionProducer`** (a small `RetryPolicy` around `context.Call`): no notifier API change, but retries in place without the budget/abandonment-logging machinery and doesn't help other listeners.
- **Docs-only** (rely on the items-1+2 no-fail test harness): leaves the delivery gap.

## `IState.Invalidated` event can skip a generation

Status: **✅ shipped** in `fab0f349` (with a regression test that fails without the fix).

Fire on publish: in `SetComputed` (or immediately after the snapshot is published), if the just-published computed is already invalidated, raise the `Invalidated` event for it — restoring per-generation event delivery when a dependency invalidates the state's computed while it is still computing. Alternatives (docs note; leave as-is since `UpdateCycle` self-corrects) rejected in favor of the small code fix.

## `ComputedSource.Computed` transiently exposes a `Computing` instance

Status: **partially shipped** — docs note shipped in `72eb4c83`; the verification task (audit every registry/published-slot fetch for a `Computing`-state assumption) remains **pending**.

Document that direct `.Computed` access may observe a `Computing` instance whose `Output`/`Value` accessors throw while an update is in flight; use `Use`/`Update` (or check `ConsistencyState`) instead. Publish-after-compute was rejected: the early publish exists so the in-flight instance is reachable for invalidate-while-computing.

**Verification task (part of implementation):** audit every place in Fusion that fetches a computed from the registry or a published slot (`GetExistingComputed()`, `source.Computed`, snapshot accessors, etc.) and confirm none assumes the fetched instance is past the `Computing` state — i.e., that no internal call path can hit the consistency-state assert on `Output`/`Value` because of the early publish.

## `MutableState` semantic nits

Status: **✅ shipped** in `72eb4c83` — docs only. Document both behaviors next to `MutableState`/`InvalidationDelay`: (a) `Set` with the same reference no-ops — in-place mutation followed by re-`Set` produces no invalidation (value-equality semantics); (b) a nonzero `InvalidationDelay` makes `Set` effectively eventual — the old value remains `Consistent` for the delay after `Set` returns. Debug-time same-reference guard rejected as noise-prone.

## Synchronous invalidation propagation recursion depth

Status: **✅ shipped** in `72eb4c83` — docs note only. Document that invalidation propagates by synchronous recursion, so dependency chains are expected to stay shallow (depth on the order of 10^3 risks stack overflow). No code change: dependency graphs are supposed to be shallow, and the propagation loop is the hottest path in the library — heap-based fallback reserved for a real report, full iterative traversal rejected.

## `OperationCompletionNotifier` assertion log enrichment

Status: **decided**. The `isLocal != isFromLocalAgent` assertion-failure log gets context: `operation.Uuid`, `operation.HostId`, the local `HostId.Id`, and the command type — so the (already-abnormal) condition is diagnosable when it fires.

## Won't-fix (recorded deliberately)

Status: **decided** — no action on any of these; recorded so they are not re-reported by future reviews:

- **KeepAlive retention race** (`RenewTimeouts` racing `CancelTimeouts` can retain an invalidated computed up to `MinCacheDuration`): bounded memory retention, no correctness impact; a guard cannot fully close the TOCTOU window and would add hot-path code.
- **Unbounded `Register` spin loop** in `ComputedRegistry`: purely speculative contention concern; no termination bug found.
- **`WeakReferenceSlim.Target` memory-ordering nit**: theoretical wrong-object resolution on weakly-ordered hardware; the feature is compiled out by default (`UseWeakReferenceSlim=false`). Revisit the ordering if that flag is ever enabled for production builds.

## `KeyConflictStrategy` race on `_events` inserts (external report: Actual-Chat/actual-chat#4049)

Status: **✅ shipped** in `64979788` — with a `ConcurrentSkipStrategyTest` regression test (8 racing producers). Implemented exactly as agreed; the events flush now goes through `FlushEvents` with EF auto-savepoints kept enabled on the master context. Refinement vs. the raw plan: a batch carrying any `Fail` event bypasses the retry entirely so a `Fail` conflict surfaces immediately (original exception/semantics), rather than being retried first. Verified all event tests (SQLite + InMemory: Fail/Skip/Update/ConcurrentSkip) pass in isolation.

Deterministic-UUID operation events (e.g. delay-quantized `FlowResumeEvent` with `KeyConflictStrategy.Skip`) race concurrent producers on the `_events` PK: `DbOperationScope.Commit` resolves `Skip`/`Update` via check-then-insert (`FindAsync` → `Add`), so two concurrent transactions both find nothing, both insert, one hits a unique violation — failing the entire command transaction, which is then retried by `OperationReprocessor` (re-executing all handler side effects) and logging errors for what is a normal race.

### Agreed course of action

**Catch → resolve → retry loop around the events flush in `Commit`, relying on EF Core's automatic savepoints.** Split the events flush from the operation-row flush (same transaction). On `DbUpdateException` (unique violation) or `DbUpdateConcurrencyException`:

- EF Core (5+) automatically creates a savepoint before each `SaveChanges` inside an explicit transaction and rolls back to it on failure — so the transaction is not doomed, even on PostgreSQL. No manual savepoint management is needed: catch, re-`FindAsync` the conflicting UUIDs, apply the strategy in memory, retry `SaveChanges`.
- Per-strategy resolution: `Fail` → rethrow (conflict fails the command — intended); `Skip` → drop the conflicting add; `Update` → existing `State == New` check then update, else the existing `KeyConflictResolver.Error`.
- Resolved conflicts are logged at **Debug** (not Warning/Error) — this is normal operation for deterministic-UUID events, and eliminating the log noise is part of the point.
- Isolation note: this works at any isolation level — unique-constraint checks ignore snapshots, so raising isolation cannot avoid the race; recovery is the only option, and savepoints are how recovery works on PostgreSQL (statement errors doom PG transactions otherwise). All supported providers (PostgreSQL, SQLite, MariaDB, SQL Server) support savepoints via the EF `DbTransaction` API; SQL Server's MSDTC limitation is irrelevant here; the InMemory test provider has no transactions but also no unique-constraint races.

This removes the command-level retry (no double side effects), the error noise, and the need for app-side workarounds — once shipped, actual-chat can drop its `ON CONFLICT DO NOTHING` annotation and the accompanying 8 migrations.

### Rejected / deferred alternatives

- **Per-statement conflict handling via a provider seam** (raw `INSERT ... ON CONFLICT DO NOTHING / DO UPDATE ... WHERE state = New` in the Npgsql package, dialect equivalents elsewhere): avoids the conflict rather than recovering from it, no savepoints involved — but requires per-provider SQL generation and a raw-insert path bypassing EF change tracking. **Deferred** as an optimization if conflict volume ever makes the retry round trips matter (~5/day today).
- **Entity-level blanket `ON CONFLICT DO NOTHING`** (the actual-chat PR's approach): rejected at the framework level — it silently breaks `KeyConflictStrategy.Fail` (conflict must fail loudly) and the insert leg of `Update`, and requires papering over EF's affected-rows bookkeeping.
- **Serializing producers via advisory locks on the event UUID**: provider-specific, adds a round trip to every event insert to solve a rare race; also ineffective at REPEATABLE READ, where the pre-check's snapshot predates the competing commit.

## Enforcement & docs batch (audit items 1, 2, 5, 6, 7)

Status: **partially shipped** in `72eb4c83` — the documentation deliverables are done: the no-fail contract (items 1+2), the command-to-query invalidation test pattern **with a real compiling `.cs` snippet** (item 5), the `StartOffset` assumptions (item 6), and the completion-vs-cluster-freshness note (item 7). Still **pending**: the code deliverables — the "must-not-throw" test-support harness for `IOperationCompletionListener` / the invalidation pass (items 1+2), and the per-command invalidation test convention (item 5).

### Agreed course of action

- **Items 1+2 (no-fail invariants of invalidation passes and completion listeners):** add a test-support helper/convention that exercises each registered `IOperationCompletionListener` and the invalidation pass under a "must not throw" harness, and state the no-fail contract explicitly in `docs/` (not as code comments).
- **Item 5 (manually maintained invalidation branches):** document the command-to-query invalidation test pattern — each mutating command has a test proving both entity-specific and aggregate queries invalidate — **with a real `.cs` example embedded via the docs code-snippet system** (mdsnippets; see the docs snippets authoring guide), not an inline untested listing. The item-15 one-time INFO log covers the silent-skip half. An analyzer was considered and deferred: high effort, and the test convention captures most of the value.
- **Item 6 (wall-clock start positioning):** documentation only — record the `StartOffset` assumptions (fresh process has no stale in-memory state; reader must establish its position within `StartOffset` of the first compute call; clock drift must stay below `StartOffset`). The worst consequence is remediated by item 10's coverage-loss sweep; durable per-host cursors remain deferred.
- **Item 7 (command completion is not a cluster-wide freshness boundary):** documentation only — an explicit note that completion does not imply every dependent cache is invalidated cluster-wide, and cross-host read-your-write semantics require an explicit version/synchronization mechanism.

## Item 18: `Invalidation.Begin` ExecutionContext capture in user code

Status: **✅ shipped** in `72eb4c83` — documentation only; no code change. The ambient-context flow is intended `AsyncLocal` behavior, not a defect. Add a short note to the invalidation docs covering the user-code trap: work spawned inside an invalidation scope (or an `Invalidated` handler) captures `CallOptions.Invalidate`, so compute methods it calls later silently return `default(T)` and spuriously invalidate instead of computing; the guardrail is `Computed.BeginIsolation()`.
