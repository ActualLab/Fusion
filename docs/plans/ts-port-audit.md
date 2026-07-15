# TypeScript Port Gap Audit

This document records contract and robustness gaps found while reviewing the TypeScript port (`ts/packages/*`) against the canonical C# implementation. It is a discussion document — the items below describe deviations and defects as observed in code, but the course of action for each is decided separately (a companion doc, same pattern as the earlier invalidation audit/fixes pair).

The port is deliberately partial. The standard applied here is: **every feature the port does implement must be as robust as the C# version and follow the same contracts**, adjusted for the JS realm (single-threaded event loop, promises vs. tasks, `AbortSignal` vs. `CancellationToken`). Wholesale-missing subsystems are listed briefly per section under "Out of scope" and are not treated as defects.

**Audit status (2026-07-14):** produced by five parallel per-area reviews (Fusion kernel, State + React, RPC, Fusion-over-RPC glue, core utilities), each comparing TS and C# sources file-by-file. Every item carries a confidence label from its auditor (*confirmed* = the full failure chain was read in code on both sides; *plausible* = mechanism confirmed, trigger conditions not fully traced). A sample of the highest-severity items (K1, K2, K4, K5, K12, S3, R1, F1, F2, C2) was independently re-verified against current sources. A full adversarial verification pass — the step the invalidation audit got before its fixes — has not been done yet and is the natural next step.

**Test confirmation (2026-07-14):** the five highest-severity items — **K1, K2, K3, F1, R1** — are additionally confirmed by executable reproduction tests that assert the C# contract and fail against the current code (5/5 red): `ts/packages/{fusion,fusion-rpc,rpc}/tests/ts-port-audit-repro.test.ts`. The tests are deliberately left red (uncommitted until the fixes land) and become the regression tests for these items. Those five items carry a **Recommended / Alternative** course-of-action pair instead of a one-line direction; resolutions for all items will be recorded in a companion `ts-port-fixes.md`.

Item numbering: **K** = Fusion kernel, **S** = State layer + React, **R** = RPC layer, **F** = Fusion-over-RPC glue, **C** = core utilities.

## Severity overview

The items most likely to produce user-visible wrong behavior in an app that uses only what the port already ships:

- **Permanent staleness** — K1 (dead dependency edges), K2 (invalidation lost during computation), K3 (dependencies silently dropped after the first `await`), S1 (initial-value computeds never invalidated), F1 (server never sends `Invalidate` for computeds invalidated mid-computation).
- **Hangs** — F2 (pre-result `Invalidate` permanently poisons a compute key's lock), S5 (dispose mid-computation deadlocks `update()` callers), R2 (TypeRef-less `$sys.Error` hangs .NET callers), R12 (no outbound call timeouts).
- **Wire-protocol breaks vs .NET peers** — R1 (V5/V5C header position), R6 (`mempack6` registered as msgpack), R7 (no polymorphism markers), R5 (pre-handshake sends).
- **Process-level failures** — C1/C7/S14 (unhandled promise rejections; crash Node by default), K4 (a throwing invalidation handler halts the cascade and kills `ComputedState` update loops).
- **Resource leaks** — R3 (completed streams never unregister on either peer), K7/K8/K10/S4/F4/F11 (graph edges, locks, listeners, calls, server peers).

---

## A. Fusion kernel

### K1. `addDependency` has no state checks — an edge added to an already-invalidated Computed is silently dead

Confidence: confirmed.

- TS: `ts/packages/fusion/src/computed.ts:218-224` — `addDependency` unconditionally adds the edge. No check that `this` is still Computing, and no check that `dependency` is not Invalidated. Since `invalidate()` early-returns for Invalidated (`computed.ts:170-171`) and its `_dependants` were already cleared (`computed.ts:188`), an edge added to an invalidated dependency never fires.
- C#: `Computed.cs:453-474` — `AddDependency` returns unless the dependant is `Computing`; `AddDependant` (`Computed.cs:477-492`) detects an Invalidated dependency and invalidates the dependant immediately. The comment at `Computed.cs:230-234` documents this as the load-bearing guarantee of `Use`.
- Failure: caller computed C awaits `cf.invoke(...)`; while awaiting, the produced computed D is invalidated. The capture (`compute-function.ts:116`, or `use()`'s `.then` at `computed.ts:126-129`) then adds an edge to the dead D. C completes Consistent with a value derived from stale data and is **never invalidated** afterward. Same hole via `useInconsistent` (`computed.ts:135-140`).
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (fusion) — `parent()` keeps returning the stale composite after the child's data changed.
- **Recommended:** port the C# guards into `Computed.addDependency`: return unless `this` is still `Computing`; if the dependency is `Invalidated`, call `this.invalidate()` (which lands in `_invalidatePending` for a Computing dependant) instead of adding the edge. One primitive, covers every capture site — `captureDependency`, `use()`, `useInconsistent`, `capture` — including future ones.
- **Alternative:** guard at each capture site individually (`ComputeContext.captureDependency`, `use()`'s `.then`, `invoke`'s post-lock capture). Rejected-leaning: several sites today, more tomorrow; the invariant belongs to the graph-mutation primitive, exactly where C# enforces it.

### K2. In-flight computations are invisible to key-based invalidation — `invalidate()` during computation is lost

Confidence: confirmed.

- TS: a Computed enters the registry only in `setOutput` → `_register()` (`computed.ts:147`, `computed-registry.ts:25-29`). `boundMethod.invalidate()` resolves via `ComputedRegistry.get(key)?.invalidate()` (`compute-method.ts:54-57, 80-84`). During recomputation the old computed is already unregistered and the new one not yet registered, so the invalidation is a **no-op**. The `_invalidatePending` machinery (`computed.ts:172-177`) is reachable only via direct references, not via the primary user-facing invalidation path.
- C#: `ComputedMethodComputed` registers in the constructor, while still Computing (`ComputeMethodComputed.cs:18-20`); `Invalidate` on a Computing instance sets `InvalidateOnSetOutput` (`Computed.cs:272-275`), applied in `TrySetOutput` (`Computed.cs:361-367`).
- Failure: `getValue('x')` starts computing and reads `store`; `increment('x')` then mutates `store` and calls `getValue.invalidate('x')` → no-op. The in-flight computation completes with the pre-mutation value and registers as Consistent — the exact write-skew the Computing→pending-invalidate protocol exists to prevent. Callers see the stale value until some unrelated invalidation hits the key.
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (fusion) — the post-invalidation call returns the stale pre-mutation value.
- **Recommended:** register the `Computed` in the registry at creation time, while still `Computing` — matching C# `ComputeMethodComputed`'s constructor registration. `ComputedRegistry.get(key)` then resolves the in-flight instance, and the existing `_invalidatePending` machinery handles the rest. Prerequisite: make `ComputedRegistry.register` invalidate a displaced still-consistent computed (K16), since displacement becomes a live path; the cache fast paths already check `isConsistent`, so returning a Computing instance from `get` is safe.
- **Alternative:** keep registration in `setOutput` and track in-flight computeds per key inside `ComputeFunction`, routing `boundMethod.invalidate()` to them. Rejected-leaning: duplicates registry state and misses any other resolver that goes through `ComputedRegistry.get`.

### K3. Dependency capture is silently lost after the first `await` inside a compute method body

Confidence: confirmed (mechanism fully traced; a by-design JS limitation, but silent).

- TS: `AsyncContext.run` sets the global `AsyncContext.current` only for the **synchronous prefix** of the callback (`async-context.ts:57-65`). `ComputeFunction.invoke` runs the impl via `childAsyncCtx.run(...)` (`compute-function.ts:100-102`) and does not pass `childAsyncCtx` to the impl. A nested compute call made after the impl's first `await` resolves no context (`compute-function.ts:55-58`, `async-context.ts:78-81`) — no dependency is recorded.
- C#: `ComputeContext` is an `AsyncLocal` — it flows across every await; dependencies are always captured.
- Failure: `@computeMethod async total(id) { const a = await this.priceOf(id); const b = await this.qtyOf(id); ... }` — `priceOf` is captured, `qtyOf` is **not**; changing quantity never invalidates `total`. No error, no warning. Every existing TS test makes nested calls only in the sync prefix, so this is untested. A leaked `activate()` (`async-context.ts:47-55`) across an await can additionally attribute dependencies to the *wrong* computed.
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (fusion) — the sync-prefix dependency invalidates `total`, the post-`await` dependency never does.
- **Recommended:** pass the child `AsyncContext` to the impl as a trailing argument (the ctx-in-args strip convention already exists), so compute bodies thread it explicitly into nested calls / `use(ctx)` after awaits. This is the portable floor — in browsers implicit flow is impossible (TC39 AsyncContext isn't shipped). Document the rule loudly: "after the first `await`, dependencies are captured only through the ctx argument."
- **Alternative (composable, Node-only):** back `AsyncContext` with `AsyncLocalStorage` when `node:async_hooks` is available — restores full C# `AsyncLocal` semantics on server/SSR/tests; browsers still need the explicit argument. Doing both is reasonable; a dev-mode warning for "nested compute call with no ambient context" is a possible third layer, but has false-positive risk for legitimate top-level calls.

### K4. `invalidate()` can throw and aborts the invalidation cascade midway

Confidence: confirmed.

- TS: `computed.ts:184-193` — dependant propagation and `onInvalidated.trigger()` run with no try/catch (`events.ts:19-21` doesn't isolate handlers either). A throwing handler propagates up: remaining dependants are never invalidated, `_unregister` is skipped (an Invalidated computed stays registered), and the exception surfaces to whoever called `invalidate()` — e.g. out of `MutableState.set()`, or inside `ComputedState._updateCycle`, whose outer catch (`computed-state.ts:150-154`) logs "UpdateCycle failed and stopped" and **exits the loop permanently**.
- C#: "Invalidate doesn't throw — ever": the whole cascade is wrapped in try/finally/try-catch with logging (`Computed.cs:300-329`); dependants are invalidated in a `finally`. State event handler exceptions are similarly isolated (`State.cs:287-321`).
- Failure: one buggy `onInvalidated` subscriber throws once → half the dependency graph stays consistent-but-stale, and that `ComputedState` silently stops auto-updating for the app's lifetime; UI freezes on the last value.
- Direction: wrap handler invocation and per-dependant propagation in try/catch (log, continue); make dependant propagation + unregister unconditional; match C# ordering (own handlers first, then dependants — TS currently also reverses that order, see K17).

### K5. Every error auto-invalidates after a global 1 s — states included → perpetual 1 Hz invalidate/recompute loops

Confidence: confirmed.

- TS: `computed.ts:24-25, 154-158` — `setOutput` schedules `setTimeout(() => this.invalidate(), 1000)` for **any** error on **any** Computed, including `StateBoundComputed` (states call `setOutput` at `state.ts:87, 92`). `MutableState`'s renewer recreates the computed with the same error output (`mutable-state.ts:7-11`), scheduling the next 1 s invalidation.
- C#: error auto-invalidation is per-`ComputedOptions` with transiency classification (`Computed.cs:374-405, 540-567`); `MutableState` uses `ComputedOptions.MutableStateDefault` — a MutableState holding an error **never** auto-invalidates (`ComputedOptions.cs:21-24`).
- Failure: `state.set(errorResult(e))` → every dependant is invalidated and recomputed every second, forever. Non-transient/terminal errors in compute methods retry at 1 s forever (C# differentiates via `NonTransientErrorInvalidationDelay` = 30 s and `Transiency.Terminal`). Bonus defects: the timer strongly retains the computed for 1 s (defeats WeakRef eviction) and an un-`unref`'d `setTimeout` keeps a Node process alive.
- Direction: suppress error auto-invalidation for `StateBoundComputed` (or introduce a minimal per-computed options object with `errorInvalidationDelay`); cancel the timer when the computed is invalidated by other means.

### K6. Cancellation errors are cached as values — C# contract: OCE must never be cached

Confidence: confirmed (absence verified).

- TS: `compute-function.ts:98-110` — every throw, including `AbortError` from an aborted fetch, becomes `errorResult(e)` and is cached via `setOutput` (until K5's 1 s timer). No AbortSignal exists anywhere in the compute pipeline.
- C#: `ComputedImpl.Helpers.cs:116-159` — an `OperationCanceledException` never becomes a cached consistent output: the computed is invalidated immediately and the error rethrown to the canceled caller only; internal cancellations are reprocessed with retries. `StartAutoInvalidation` instant-invalidates OCE outputs as a second line of defense (`Computed.cs:388-391`).
- Failure: caller A's fetch is aborted (user navigated away); for up to 1 s, callers B/C of the same compute key receive A's `AbortError` from cache — cancellation of one consumer poisons all consumers. See also F3 for the remote-call variant.
- Direction: detect cancellation-shaped errors (`DOMException` name `AbortError`, or a dedicated `OperationCancelledError` in `@actuallab/core`) and either invalidate immediately after `setOutput` or bypass caching and rethrow.

### K7. Invalidated dependants never unlink from their dependencies; no graph pruner

Confidence: confirmed.

- TS: `invalidate()` clears only its own `_dependencies` and `_dependants` (`computed.ts:180-188`); it never removes itself from its dependencies' `_dependants` maps. Entries are `version → WeakRef`; the WeakRef target can be GC'd but the Map entry lives until the *dependency* itself is invalidated.
- C#: on invalidation, each dependency gets `RemoveDependant(this)` (`Computed.cs:309`); plus `PruneDependants` (`Computed.cs:507-526`) driven by `ComputedGraphPruner`.
- Failure: a rarely-changing computed (config, session) used by a frequently recomputing `ComputedState` accumulates one dead `(number, WeakRef)` entry per recompute — tens of thousands over a long SPA session.
- Direction: in `invalidate()`, delete `this._version` from each dependency's `_dependants` before clearing (cheap in JS, no lock concerns).

### K8. `ComputeFunction._locks` is never cleaned — one `AsyncLock` leaked per distinct key, forever

Confidence: confirmed.

- TS: `compute-function.ts:32, 71-77` — locks are created on demand and never deleted.
- C#: shared `AsyncLockSet<ComputedInput>` (`ComputedRegistry.cs:77-81`) releases per-key entries when unused.
- Failure: a compute method keyed by user/entity id in a long-running app → unbounded Map of idle AsyncLocks.
- Direction: after `lock.run` resolves, delete the map entry when the lock has no queued waiters; or implement a small `AsyncLockSet` in `@actuallab/core`.

### K9. `update()` is not isolated — renewal registers a dependency in the ambient compute context

Confidence: confirmed.

- TS: `Computed.update()` → `_renewer()` (`computed.ts:106-114`) → `ComputeFunction.invoke`, which falls back to `AsyncContext.current` (`compute-function.ts:55-58`) and captures the produced computed into that ambient context (`compute-function.ts:116`).
- C#: `UpdateUntyped` wraps production in `BeginIsolation()` (`Computed.cs:207-215`) — updating a computed must not create a dependency edge; only `Use` does.
- Failure: inside a computing method, `someComputed.update()` (or `state.update()/recompute()`) intended as "refresh without depending" silently records a dependency — spurious recomputation storms. This inverts the documented Update/Use distinction.
- Direction: in the renewer path, run `invoke` with a context explicitly cleared of `computeContextKey` (the TS analog of `BeginIsolation`).

### K10. `whenInvalidated(abortSignal)` leaks one abort listener per call on long-lived signals; never settles on an already-aborted signal

Confidence: confirmed.

- TS: `computed.ts:196-216` — the `'abort'` listener (added `{ once: true }`) is never removed when the promise resolves via invalidation. `ComputedState._updateCycle` passes the same `disposeSignal` every iteration (`computed-state.ts:139`), so listeners accumulate one per update. Also: when the signal is *already aborted*, the listener is skipped and the returned promise may never settle (feeds S5); and `ps.reject(abortSignal.reason)` can reject with `undefined`.
- C#: `WhenInvalidatedClosure.OnInvalidated` disposes the CancellationTokenRegistration as soon as invalidation fires (`Internal/WhenInvalidatedClosure.cs:27-31`); already-cancelled tokens throw immediately (`ComputedExt.cs:120-122`).
- Failure: a `ComputedState` updating every few seconds in a day-long session accumulates tens of thousands of listeners on its dispose signal — memory growth plus O(n) abort dispatch.
- Direction: remove the abort listener in the invalidation handler; reject immediately when `abortSignal.aborted`.

### K11. `Computed.capture` deviates: returns the *first* captured computed (C#: last), throws on failed computations (C#: returns the errored computed), and pollutes real dependants with a stub

Confidence: confirmed.

- TS: `computed.ts:28-42` — returns `deps.values().next().value` (first inserted); if `fn` rejects, `capture` rejects; the fake Computing stub is registered in real computeds' `_dependants` (via K1's unchecked `addDependency`) and never cleaned.
- C#: `ComputeContext.TryCapture` deliberately overwrites → last capture wins (`ComputeContext.cs:70-84`); `Computed.Capture` catches non-cancellation exceptions and returns the captured computed if it `HasError` (`Computed.Static.cs:153-169`).
- Failure: `Computed.capture(() => svc.failingMethod())` throws instead of yielding a `Computed` with `hasError` — reactive wrappers (the primary use of capture) can't observe invalidation of the failed computation; with two top-level calls in `fn`, TS returns the wrong one relative to C# docs/samples.
- Direction: track "last captured" explicitly on the capture context; on `fn` rejection, return the captured computed if it has an error, else rethrow.

### K12. `onInvalidated` is a public raw handler set — a handler added after invalidation never fires (C#: fires immediately)

Confidence: confirmed.

- TS: `computed.ts:51, 190-191` — after `trigger()` the set is cleared; `EventHandlerSet.add` on an already-invalidated computed stores a handler that can never fire. Only `whenInvalidated` has the state pre-check.
- C#: the `Invalidated` event's `add` invokes the handler immediately when state is already Invalidated (`Computed.cs:105-117`) — exactly-once regardless of subscription timing.
- Failure: `computed.onInvalidated.add(h)` after an async gap → `h` never runs → a UI subscription never refreshes. This is the exact race the C# `add` accessor exists to close, and the root cause of F1.
- Direction: replace the public field with an `onInvalidated(handler)` method that invokes immediately when already invalidated; keep the set private.

### K13. Default argument keying via `JSON.stringify` produces key collisions

Confidence: confirmed.

- TS: `compute-function.ts:19-22, 41-47` — `JSON.stringify(arg) ?? 'undefined'`: any function, `undefined`, or `Symbol` arg → `'undefined'`; objects without serializable props → `'{}'`; `NaN` → `null`; `Map`/`Set` → `'{}'`. Semantically different arguments collide onto the same cache key.
- C#: `ComputeMethodInput.Equals` compares proxy identity and actual argument values (`ComputeMethodInput.cs:25-27, 48-56`).
- Failure: `getWidget(selectorFn1)` and `getWidget(selectorFn2)` (or two different `Map` args) return each other's cached results — silent wrong answers, not just cache misses.
- Direction: throw (or warn) in `defaultArgToString` for non-JSON-representable args instead of silently colliding; document `argToString` as the escape hatch.

### K14. Same-key reentrant computation deadlocks silently (C#: fails fast)

Confidence: confirmed (mechanism; not runtime-tested).

- TS: `AsyncLock` is non-reentrant with no reentry detection (see C5); a compute function that (indirectly) awaits itself with the same key awaits its own lock forever (`compute-function.ts:72-78`).
- C#: `AsyncLockSet` is created with `LockReentryMode.CheckedFail` (`ComputedRegistry.cs:77-81`) — reentry throws immediately.
- Failure: accidental self-recursion hangs the app with no diagnostics instead of a clear error.
- Direction: track keys currently being computed and throw on same-key reentry (full async-chain detection is impossible without `AsyncLocalStorage`, but the sync-prefix case is detectable).

### K15. Renewer chain permanently retains the generation-1 computed and the original argument objects (low)

Confidence: confirmed. Every later generation reuses gen-1's `_renewer`, whose closure strongly captures the gen-1 computed and the first call's `argsWithoutCtx` (`compute-function.ts:85-89`). Small fixed leak per key; renewals also re-run the impl against the *first* call's argument object identities. C# reconstructs from `ComputeMethodInput` each time.

### K16. `registry.register` overwrites without invalidating a displaced still-consistent computed (low)

Confidence: confirmed. `computed-registry.ts:25-29` vs C# `ComputedRegistry.cs:126-132` (invalidates the displaced target). Unreachable through the single-flight path today, but the invariant is unenforced — and the K2 fix will make this path live.

### K17. Invalidation ordering differs (low)

Confidence: confirmed. TS notifies dependants before the computed's own `onInvalidated` handlers (`computed.ts:184-191`); C# fires own handlers first, dependants in `finally` (`Computed.cs:303-318`). Observable to handlers that inspect dependants' state; fold into the K4 rework.

### Out of scope (kernel)

- Per-input `ComputedOptions` — `MinCacheDuration`/keep-alive timeouts, `AutoInvalidationDelay`, `InvalidationDelay`, `ConsolidationDelay`; TS has only the global `Computed.errorAutoInvalidateDelay`.
- Error transiency classification (`TransiencyResolver`, transient/super-transient/terminal).
- `CallOptions` context machinery (`GetExisting`, `Capture`, `Invalidate` via context; `Invalidation.Begin()` blocks) — TS substitutes bound `.invalidate()` on methods.
- `InvalidationSource` diagnostics.
- `ComputedGraphPruner` (partially covered by K7).
- Cancellation reprocessing (`ComputedCancellationReprocessingOptions`).
- `ComputedSynchronizer`, `ComputedSource`, `ComputedExt.When/Changes` streams, Session, DI/interceptor integration.
- Registry instrumentation (`OnRegister/OnUnregister/OnAccess`, metrics, `InvalidateEverything`).

Note: the TS registry's WeakRef + FinalizationRegistry approach (`computed-registry.ts`) is a sound equivalent of C#'s weak-ref + pruning for *key* storage; the gaps are in graph edges (K1, K7) and registration timing (K2), not key eviction.

---

## B. State layer + React

### S1. `State._update` publishes a new computed without invalidating the one it replaces — dependants of the initial-value computed go stale forever

Confidence: confirmed.

- TS: `state.ts:91-98` — `_update()` just assigns `this._computed = computed`. For `ComputedState` constructed with `initialValue`/`initialOutput`, `_initialize` (`computed-state.ts:47-56`) creates a *consistent* initial computed; the first `_updateCycle` iteration replaces it via `_update` (`computed-state.ts:133`) while the initial computed remains Consistent.
- C#: the initial computed is created **already invalidated** (`State.cs:246`), and `SetComputed` always invalidates `prevSnapshot.Computed` when publishing a successor (`State.cs:258`).
- Failure: a compute function calls `state.use()` before the first real computation finishes → captures a dependency on the consistent initial computed → gets `initialValue`. When the real value arrives, the initial computed is never invalidated, so the dependant is never invalidated — the parent shows the placeholder value forever.
- Direction: make `_update` invalidate the previous `_computed` if still consistent (mirroring `SetComputed`), or create the initial computed pre-invalidated like C# — which also fixes S13 and removes the `_hasOutput` guards.

### S2. `ComputedState.value` silently masks errors by falling back to `lastNonErrorValue`

Confidence: confirmed.

- TS: `computed-state.ts:72-79` — when `_computed.hasError`, `value` returns `_lastNonErrorValue` instead of throwing; `valueOrUndefined` (`computed-state.ts:86-89`) likewise. Base `State.value` and `MutableState` *do* throw on error (`state.ts:33-35`) — the port is internally inconsistent too.
- C#: `State.Value` throws the stored error (`State.cs:99-102`); the stale-value fallback is a separate, explicit API — `LastNonErrorValue` (`State.cs:104-107`, `StateSnapshot.cs:15`).
- Failure: a computer starts failing (server down). C# callers of `state.Value` see the exception; TS callers keep receiving the last good value with no indication anything is wrong. `hasValue` is `false` while `value` returns a value — contradictory.
- Direction: `value`/`output` should rethrow like C#; keep stale-value access on `lastNonErrorValue` only.

### S3. `MutableState.set` invalidates *before* staging the new output — synchronous readers during the cascade observe (and can permanently publish) the old value

Confidence: confirmed.

- TS: `mutable-state.ts:22-25` — `set()` first calls `this._computed.invalidate()` (synchronous cascade to dependants and handlers), then `_update(..., output)`. During the cascade, `this._computed` is still the old computed. If any handler synchronously calls `state.use()`/`update()`, the renewer (`mutable-state.ts:7-11`) publishes a new consistent computed **carrying the OLD output**; `set()`'s own `_update` then replaces that computed without invalidating it (S1), so anything that captured it is stale forever.
- C#: `MutableState.Set` stages `NextOutput = result` **before** invalidating, inside the lock (`MutableState.cs:112-131`); `OnInvalidated` synchronously renews from `NextOutput` (`MutableState.cs:152-162`) — any read during/after invalidation observes the new value.
- Failure: `computed.onInvalidated.add(() => render(state.use()))` — a legitimate C# pattern — renders the old value in TS and detaches the renderer from future updates.
- Direction: introduce a `_nextOutput` field set before `invalidate()`; have `_renewer` build from `_nextOutput` (set = stage + invalidate, renewal path shared), matching C# ordering.

### S4. Unbounded listener/handler accumulation in update-delayer waits

Confidence: confirmed. (The `whenInvalidated` half of this leak is K10.)

- TS: `ui-update-delayer.ts:20-28` — when the delay timer fires normally, `t.changed.remove(onChanged)` is never called; the handler is removed only the next time a UI action becomes active. In an app with frequent invalidations and rare UI actions, `uiActions.changed` grows without bound. Same for its abort listener (`ui-update-delayer.ts:37-45`) and the >10 s branch of `FixedDelayer` (`update-delayer.ts:27-44`).
- C#: `UpdateDelayer` uses a linked CTS cancelled/disposed in `finally` (`UpdateDelayer.cs:46-54`).
- Failure: a dashboard updating once per second leaks ~86k closures/day; long sessions degrade in memory and `changed.trigger()` cost.
- Direction: remove listeners/handlers on the success path of every wait; a shared "await with signal" helper in `@actuallab/core` would centralize this.

### S5. Disposal mid-computation: one post-dispose update is published, then the update cycle hangs; `update()`/`use()` callers hang forever

Confidence: confirmed.

- TS: `computed-state.ts:111-149` — the dispose check happens only at the loop top. If `dispose()` fires while `await this._computer()` is in flight, the cycle still calls `_update` (an observable post-dispose update), then `computed.whenInvalidated(disposeSignal)` with an already-aborted signal — which never settles (K10) → the cycle stays pending indefinitely. `FixedDelayer` with `ms <= 10_000` ignores `abortSignal` entirely (`update-delayer.ts:45-48`), and `_renewer` (`computed-state.ts:39-44`) awaits `whenUpdated()`, which never resolves after disposal — so `state.update()`/`state.use()` on a disposed-mid-cycle state hangs the caller forever.
- C#: `DisposeToken` cancels `WhenInvalidated` and the delay promptly; update-after-dispose is explicitly managed via `GracefulDisposeToken` (`ComputedState.cs:167-193`), and the cycle always terminates.
- Failure: React unmount during an in-flight fetch: the state publishes one more update, and any code awaiting `state.update()` never resolves — silent deadlock of that dependency chain.
- Direction: reject `whenInvalidated` immediately on an aborted signal; check `disposeSignal.aborted` after compute before `_update`; make `whenUpdated`-based waits reject on dispose so `update()`/`use()` fail fast.

### S6. React hooks miss updates between render and effect subscription — classic external-store race

Confidence: confirmed.

- TS: both hooks are hand-rolled (no `useSyncExternalStore`). `use-computed-state.ts:42-74` and `use-mutable-state.ts:19-39` read the value during render, then subscribe inside `useEffect` via `state.whenUpdated()` — a fresh `PromiseSource` (`state.ts:74-76`) resolving only on the *next* `_update`. An update landing between render and effect execution is invisible.
- C#: waits are per-generation — capture `state.Snapshot`, await `snapshot.WhenUpdated()` (`StateSnapshot.cs:62`), already completed if the update happened in the meantime. No lost-wakeup window.
- Failure: `useMutableState` mounts; another component calls `state.set(v1)` in the same tick after render but before effects flush → the UI shows the initial value while `state.value === v1`, until some future `set` — potentially forever.
- Direction: move to `useSyncExternalStore` with event-based subscribe, or compare `state.updateIndex` in the effect against the render-captured index and force-render if they differ. Making `whenUpdated(sinceIndex)` index-based (S11) fixes all non-React consumers too.

### S7. `useComputedState` is permanently broken under React StrictMode (and unsafe under concurrent rendering)

Confidence: confirmed.

- TS: `use-computed-state.ts:33-37` creates/disposes `ComputedState` **during render** (a side effect); effect cleanup disposes the state but leaves it in `stateRef`. StrictMode dev (mount → cleanup → re-mount): the same **disposed** state is re-subscribed — `subscribe()` bails on `state.isDisposed` (`use-computed-state.ts:52,57`) and the component never re-renders; the value freezes. Under concurrent React, a discarded render leaks a running `ComputedState`.
- C#: n/a mechanically; the Blazor counterpart ties state lifetime to component lifecycle callbacks, never render.
- Failure: any app running `<StrictMode>` in development shows `useComputedState` values stuck at the initial value; production behaves differently — hard to diagnose.
- Direction: recreate the state if `stateRef.current.isDisposed` at effect time (or move creation/disposal fully into the effect keyed on deps); never dispose during render.

### S8. No retry backoff: `UpdateDelayer` has no `retryCount`, no `RetryDelays`, no transient-error tracking

Confidence: confirmed.

- TS: `UpdateDelayer` is `(abortSignal?) => Promise<void>` (`update-delayer.ts:2`) — the delay cannot depend on failure history. No `StateSnapshot`, so no `RetryCount`/`ErrorCount`. Error recovery relies solely on the global 1 s auto-invalidation (K5) — a constant ~1 s retry loop forever.
- C#: `UpdateDelayer.Delay(retryCount, ct)` uses `RetryDelays[retryCount]` growing 1 s → 1 min (`UpdateDelayer.cs:30-35, 67-68`); `RetryCount` is computed per snapshot from transient-error classification (`StateSnapshot.cs:40-53`).
- Failure: backend down → every `ComputedState` in the app hammers it once per second indefinitely; C# backs off to one attempt per minute.
- Direction: extend the delayer signature to `(retryCount, abortSignal)` and track a consecutive-error counter in `_updateCycle`; reuse `RetryDelaySeq` from `@actuallab/core`.

### S9. `UIActionTracker` has no post-action instant-update window; the 50 ms buffer is ineffective and adds latency

Confidence: confirmed.

- TS: `ui-action-tracker.ts:12-14` — `isActive` is true only while `_activeCount > 0`. In `run`/`call` (`ui-action-tracker.ts:27-31, 45-49`) the `finally` decrements first, then waits 50 ms, then triggers `changed`: (a) during the 50 ms "buffer", `isActive` is already false, so a `UIUpdateDelayer` consulted when the post-command invalidation arrives waits the full delay — the buffer buys nothing; (b) `await uiActions.call(fn)` returns 50 ms late.
- C#: `AreInstantUpdatesEnabled()` is true while actions run **or** within `InstantUpdatePeriod` (300 ms) after the last result (`UIActionTracker.cs:16, 82-91`); `UpdateDelayer` races `WhenInstantUpdatesEnabled()` against the delay (`UpdateDelayer.cs:42-55`).
- Failure: user clicks "Save"; the server invalidates the dependent query right after the command completes. C#: instant refresh. TS: full configured update delay (plus the 50 ms handler latency).
- Direction: track `lastResultAt`, honor a ~300 ms instant-update window after completion (decrement + timestamp + trigger immediately, no artificial sleep); make `UIUpdateDelayer` consult it.

### S10. `UIUpdateDelayer` skips the delay entirely (no `MinDelay` floor) while a UI action is active — hot-loop risk

Confidence: confirmed.

- TS: `ui-update-delayer.ts:15` — `if (t.isActive) return Promise.resolve();` and the `onChanged` cancel path also resolves with zero residual delay.
- C#: `UpdateDelayer.Delay` enforces `minDelay` (default 32 ms) even when instant updates kick in, with an explicit comment that otherwise updates can consume 100% CPU (`UpdateDelayer.cs:32-35, 58-64`).
- Failure: a long-running UI action while a dependency invalidates on every recompute → microtask-tight recompute loop for the action's duration; frozen tab.
- Direction: still await a small `minDelay` after the instant-update short-circuit.

### S11. `whenUpdated()` is state-level and lossy; never settles on dispose

Confidence: confirmed.

- TS: `state.ts:74-76, 96-97` — one shared `PromiseSource` resolved-and-nulled per update. A consumer looping `await state.whenUpdated()` misses updates landing between resolution and re-subscription. After `dispose()`, pending/future `whenUpdated()`/`whenFirstTimeUpdated()` promises never settle.
- C#: waits are per-snapshot (`StateSnapshot.cs:61-62`) — no missed generation.
- Failure: `await state.whenUpdated(); assert(...)` intermittently misses generations; teardown code awaiting `whenFirstTimeUpdated()` after dispose deadlocks (see also S5).
- Direction: monotonically-versioned wait — `whenUpdated(sinceIndex = updateIndex)` resolving immediately if already past; reject/resolve outstanding waits on dispose.

### S12. State events (`Invalidated`/`Updating`/`Updated`) and `StateSnapshot` counters are absent

Confidence: confirmed (absent in TS).

- TS: no equivalent of the three events or `StateEventKind`; no `UpdateCount`/`ErrorCount`/`RetryCount`/`IsInitial` (only `updateIndex`); `lastNonErrorValue` is stored as a raw value — ambiguous when `T` includes `undefined` (`state.ts:14, 21-23, 94`).
- C#: `State.cs:124-126` — events with exactly-once-per-generation semantics via `StateSnapshot.TryClaimInvalidatedRaise`; counters in `StateSnapshot.cs:16-19`.
- Failure: C# patterns like `state.Invalidated += ...` (used pervasively in Fusion UIs for eager recompute) have no counterpart; `whenInvalidated()` covers one generation with none of the exactly-once machinery.
- Direction: if events stay out of scope, document it; at minimum replace `lastNonErrorValue` with a reference to the last non-error *computed* to remove the `undefined` ambiguity.

### S13. Base `State` members crash with `TypeError` on a `ComputedState` before its first computation

Confidence: confirmed.

- TS: `computed-state.ts:57` — `_computed` stays unset until the first `_update()`. `ComputedState` guards its own getters with `_hasOutput`, but inherited members dereference `this._computed` directly: `use` (`state.ts:53-55`), `useInconsistent`, `update`, `recompute`, `whenInvalidated` → `TypeError`.
- C#: a state always has a computed from construction — an invalidated initial one (`State.cs:240-246`) — so `Use`/`Update` before the first computation just await it.
- Failure: `parentComputer = () => childState.use()` on a freshly created async `ComputedState` → hard crash instead of awaiting the first value.
- Direction: always create the initial computed in the constructor, pre-invalidated (same fix as S1).

### S14. `Promise.race` losers in `_updateCycle` produce unhandled promise rejections

Confidence: confirmed.

- TS: `computed-state.ts:145-148` races `updateDelayer(disposeSignal)` against `_cancelDelaySource`. If the cancel source wins and the state is later disposed, the still-pending delayer promise rejects on abort with nobody attached → `unhandledRejection` (crashes Node by default).
- C#: `UpdateDelayer` awaits with `SuppressCancellationAwait`/`SilentAwait` (`UpdateDelayer.cs:38, 48-50`).
- Failure: SSR/Node test process exits with `ERR_UNHANDLED_REJECTION` when a delayer-driven state is disposed shortly after a user action cancelled its delay.
- Direction: attach a no-op `.catch()` before racing, or make delayers resolve rather than reject on abort (the loop re-checks `disposeSignal` anyway).

### S15. `MutableState.set` lacks the equality short-circuit

Confidence: confirmed. TS always invalidates + republishes (`mutable-state.ts:22-25`); C# returns early on `NextOutput == result` (`MutableState.cs:114-116`). `set(sameValue)` per keystroke triggers full cascades and re-renders where C# is a no-op. Needs `Result.equals` (C9). Direction: compare against current output and return early.

### S16. External invalidation of a `MutableState` is not eagerly renewed

Confidence: confirmed. TS leaves `_computed` invalidated until the next `update()`/`use()` (renewal is lazy, `mutable-state.ts:7-11`); C#'s `OnInvalidated` renews synchronously — a `MutableState` is *never* observed invalidated (`MutableState.cs:152-162`). Mostly benign, but code checking `state.computed.isConsistent` behaves differently, and TS tests codify the lazy behavior (`mutable-state.test.ts:65-81`). Direction: renew synchronously on invalidation, or explicitly document the divergence.

### S17. `useMutableState` render throws if the state holds an error result

Confidence: confirmed. `use-mutable-state.ts:41` returns `state.value`, whose getter throws on error output — and the setter accepts `Result<T>`, so `setter(errorResult(e))` is a supported call. Next render of every component using the state throws, unmounting the tree absent an error boundary. Direction: return `{ value, error }` (as `useComputedState` does) or document the restriction.

### S18. `UIActionTracker.errors` grows unbounded with no dedup

Confidence: confirmed. Every failure is pushed to a plain array; only manual `dismissError` removes entries (`ui-action-tracker.ts:10, 26, 43`). C# dedups same-type/same-message failures within `MaxDuplicateRecency` (1 s) (`UIActionFailureTracker.cs:64-98`). Combined with S8's fixed 1 s retry, an error-toast UI floods and memory grows for the session's lifetime. Direction: port the recency-based dedup; consider a cap.

### Out of scope (state + UI)

- `StateFactory` / DI integration (`StateOptions`, `EventConfigurator`, `Category`) — TS constructs states directly.
- `UICommander` / `UIAction` / commander pipeline — TS reduces this to `uiActions.run/call(fn)`.
- `ComputedState` options: `TryComputeSynchronously`, `FlowExecutionContext`, `GracefulDisposeDelay` — TS has hard dispose only.
- `ComputedSynchronizer` / `IsSynchronized` / `WhenSynchronized` / `WhenNonInitial`.
- Updater-function `Set` overloads (`MutableState.cs:243-286`).

---

## C. RPC layer

### R1. V5/V5C binary format: header block parsed at the wrong position

Confidence: confirmed (byte-level; re-verified).

- TS: `rpc-serialization.ts:293-321` (`deserializeBinaryMessage`) reads the 4-byte argLen, then skips headers, then reads argData. Same in the compact variant at `:473-495`.
- C#: `Serialization/RpcByteMessageSerializerV5.cs:38-66` — the 4-byte prefix is *immediately followed by argument data*; headers are read **after** it. Writers (`WriteWithArgumentData` + `WriteHeaders`, lines 120-147) put headers last. `RpcByteMessageSerializerV5Compact.cs` is identical.
- Failure: any header-bearing binary message from .NET — e.g. a call with an active `Activity` (W3C header injected via `RpcOutboundCall.SendRegistered` → `RpcActivityInjector.Inject`) or middleware-set `ResultHeaders` on `$sys.Ok` — makes TS decode the first bytes of argument data as header lengths; `decodeMulti` throws and the **entire WebSocket frame is dropped** (`rpc-connection.ts:171-174`) — calls hang, stream items vanish. TS never sends headers itself, so it never round-trips its own bug; no TS test has `headerCount > 0`.
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (rpc) — a synthetic frame built per the .NET writer layout (one header after argument data) decodes to `args: []` instead of the actual arguments.
- **Recommended:** fix `deserializeBinaryMessage` (and the compact variant) to read argument data immediately after the 4-byte length prefix and parse headers *after* it, per `RpcByteMessageSerializerV5`; skip them correctly and account for them in `bytesRead`. The TS writer needs no change — it never emits headers (`headerCount` is always 0). The repro test becomes the regression test.
- **Alternative:** additionally surface parsed headers on `RpcMessage` (needed eventually for tracing/activity propagation) — more scope, can follow later. Rejecting header-bearing frames loudly is *not* an option: .NET sends them legitimately (Activity injection), and dropping them must not break the call.

### R2. `$sys.Error` sent without `TypeRef` permanently hangs .NET callers

Confidence: confirmed.

- TS: `rpc-system-call-sender.ts:152-165` — `error()` sends `[{ Message }]`, no `TypeRef`.
- C#: `Infrastructure/RpcSystemCalls.cs:95-104` — `Error` does `error.ToException()!`; `ExceptionInfo.ToException()` returns **null** when `TypeRef` is empty (`ExceptionInfo.cs:32, 57-68`), so `SetError(null!)` throws inside the handler and the `$sys.Error` is effectively swallowed.
- Failure: a .NET peer calls into a TS callee; the TS handler throws → the .NET caller's task hangs (queries: forever; commands: until the 10 s `RunTimeout`). TS's own `sendEnd` already knows this contract — it sends `TypeRef: 'System.Exception'` (`rpc-stream-sender.ts:214-216`).
- Direction: send `{ TypeRef: 'System.Exception', Message }` (or the actual error name) from `RpcSystemCallSender.error`.

### R3. Normally-completed remote streams never send `AckEnd` and never unregister → unbounded leaks on both peers

Confidence: confirmed.

- TS: `rpc-stream.ts:307-313` — `onEnd` only flips `_completed`; the iterator's done-path (`:373-376`) returns without `dispose()`, and JS `for await` does not invoke `return()` on natural exhaustion. The stream stays in `peer.remoteObjects` (a **strong** `Map`, `rpc-remote-object-tracker.ts:3-27`) and no `$sys.AckEnd` is ever sent. Same for the gap-error completion path (`:258-267`).
- C#: `RpcStream.cs:296-313` — `OnEnd` → `CloseFromLock` → sends `$sys.AckEnd` and unregisters (`:334-358`); the tracker uses weak refs (`Infrastructure/RpcObjectTrackers.cs:41-48`).
- Failure: every completed server→client stream (a) leaks an `RpcStream` in the TS client for the peer's lifetime and (b) keeps its id in the keep-alive list (`rpc-peer.ts:595-597`), making the .NET server's `RpcSharedStream` immortal — parked awaiting an ack after `End` (`RpcSharedStream.cs:130-152, 239-253`) and refreshed by `KeepAlive` every 10 s, so the 125 s `ObjectReleaseTimeout` reaper never fires. Memory grows on **both** ends.
- Direction: in `onEnd` (and gap-error completion) mirror C#: send `AckEnd` and unregister/dispose immediately.

### R4. `$sys.End` index is ignored — silent loss of tail items

Confidence: confirmed.

- TS: `rpc-stream.ts:307-313` — `onEnd(index, error)` completes the stream regardless of `index` vs `_nextExpectedIndex`.
- C#: `RpcStream.cs:296-313` — `OnEnd` applies the same ordering rules as items: gap → `SendResetFromLock(_nextIndex)` (re-request the missing range); duplicate → duplicate-ack.
- Failure: after a reconnect, replayed traffic can deliver `End(N)` while items `k..N-1` were dropped with the old connection; TS completes the stream *successfully* with the tail missing — silent data loss where C# recovers via reset-ack.
- Direction: apply the same index gate to `onEnd`.

### R5. System messages can be flushed onto the wire *before* the handshake

Confidence: confirmed (code path; failure against .NET verified by its handshake contract).

- TS: `rpc-connection.ts:265-286` — `_sendRaw` buffers writes while the WS is `CONNECTING` and flushes them in `onopen`, i.e. **before** the peer's run loop sends `$sys.Handshake` (`rpc-peer.ts:859-877`). The abort handler sends `$sys.Cancel` whenever `_connection !== undefined` (`rpc-peer.ts:377-386`), which is true throughout connect/handshake.
- C#: `RpcPeer.Transport` returns null until the handshake completes, with a comment explaining that early writes corrupt the remote handshake (`RpcPeer.cs:29-52`); the server treats the **first** inbound message as the handshake and fails otherwise (`RpcPeer.cs:306-324`).
- Failure: user aborts a deferred call while the socket is still connecting → the buffered `$sys.Cancel` is the first message the .NET server reads → handshake fails → connection drops → reconnect; repeatable under cancellation churn.
- Direction: gate all peer sends on handshake completion (drop or queue system calls until `Connected`), or remove the CONNECTING-buffer in favor of peer-level deferral.

### R6. `mempack6` / `mempack6c` registered as MessagePack formats

Confidence: confirmed.

- TS: `rpc-serialization-format.ts:239-242, 249-252` — `MemoryPackV6`/`MemoryPackV6C` are constructed as msgpack formats and resolvable via `f=` URL keys.
- C#: `Configuration/RpcSerializationFormat.cs:51-57` — `mempack6*` uses **MemoryPack** argument serialization, a completely different byte format.
- Failure: a peer configured with `f=mempack6` connects; the .NET server speaks MemoryPack, TS speaks msgpack — every payload is garbage, surfacing as confusing deserialization errors rather than a clear "unsupported format".
- Direction: don't register mempack keys in TS (let the resolver throw), or map them to an explicit "unsupported" error.

### R7. Binary polymorphism markers (`msgpack6`) not implemented

Confidence: confirmed in code; triggers only for polymorphic types.

- TS: args are encoded/decoded as bare concatenated msgpack values (`rpc-serialization.ts:209-257, 309-321`), no type-marker handling.
- C#: `msgpack6` prefixes every *polymorphic* argument/result with a derived-type marker (`Serialization/RpcByteArgumentSerializerV4.cs:54-98`, `ByteTypeSerializer.cs:65-112`); `$sys.Ok`/`I`/`B` are sent with `needsPolymorphism: true` when the return/item type is abstract/interface/object.
- Failure: any TS-facing method or stream whose declared result/item type is polymorphic in .NET terms → TS decodes marker bytes as msgpack → frame decode error or corrupt values. Works today only because ActualChat's TS-facing APIs avoid polymorphic types — nothing enforces that.
- Direction: implement marker parsing (the `\0\0` ExpectedTypeSpan fast path is trivial) or document/enforce "no polymorphic types on TS-facing methods" and fail loudly.

### R8. Peer-change detection keyed on `RemoteHubId` instead of `RemotePeerId`

Confidence: confirmed.

- TS: `rpc-peer.ts:905-920` compares successive `RemoteHubId`s.
- C#: `Infrastructure/RpcHandshake.cs:24-32` — `GetPeerChangeKind` compares `RemotePeerId`.
- Failure: the .NET server removes an idle `RpcServerPeer` while the client is offline; on reconnect a *new* server peer (new `Id`, same `HubId`) is created. C# clients treat this as `Changed` and reset shared/remote objects; TS treats it as same-peer — `peerChanged` doesn't fire and client-owned `RpcStreamSender`s wait forever for acks the new peer will never send.
- Direction: compare `RemotePeerId` (already parsed into `RemoteHandshake`, `rpc-peer.ts:481-493`).

### R9. Inbound call tracker has no dedup (`GetOrRegister`/`TryReprocess`) — duplicate execution on a TS callee

Confidence: confirmed.

- TS: `rpc-peer.ts:509-521` — `inboundCalls.register(call)` unconditionally overwrites (`rpc-call-tracker.ts:192-194`) and dispatch always runs.
- C#: `Infrastructure/RpcCallTrackers.cs:48-56` + `RpcInboundCall.cs:114-158` — a resent call id reuses the existing call and just re-sends its result.
- Failure: reconnect resends are by design — and TS even double-sends locally: a call issued while disconnected is sent by its deferred `Connected` listener (`rpc-peer.ts:361-367`, triggered synchronously at `:926`) **and** again by the blind resend in `_reconnect` (`:1043-1058`). Against a .NET server this is absorbed; against a TS server the method body executes twice (double side effects for commands).
- Direction: port `GetOrRegister` semantics — attach to the existing dispatch, re-send the result if already computed.

### R10. TS never sends `$sys.Disconnect`

Confidence: confirmed.

- TS: no `disconnect` sender exists in `rpc-system-call-sender.ts`; TS only *handles* it (`rpc-system-call-handler.ts:179-206`). The keep-alive handler also ignores the id list entirely (`rpc-system-call-handler.ts:109-115`), so per-object keep-alive/`ObjectReleaseTimeout` semantics are absent.
- C#: sends it in three places — unknown shared object acked (`RpcSystemCalls.cs:149-159`), unknown ids in keep-alive (`RpcObjectTrackers.cs:300-317`), and `RpcSharedStream.SendDisconnect` for host-mismatch/late/rejected acks (`RpcSharedStream.cs:78-111, 284-289`).
- Failure: a consumer acks a stream whose sender the TS callee no longer tracks → gets nothing back → the remote stream hangs instead of failing fast with `RpcStreamNotFoundException`.
- Direction: add `disconnect()` to the sender; reply with it from the `$sys.Ack` handler when the shared object is unknown; process keep-alive id lists.

### R11. `RpcStreamSender.onAck` skips C#'s validation

Confidence: confirmed.

- TS: `rpc-stream-sender.ts:159-167` — any ack starts the pump; `mustReset` is just "hostId non-empty"; no host equality check; no rejection of resets on `allowReconnect === false`.
- C#: `RpcSharedStream.OnAck` (`RpcSharedStream.cs:78-111`): host mismatch → `SendDisconnect`; a not-yet-started stream only accepts `mustReset && nextIndex == 0`; reset on a non-reconnectable stream → `SendDisconnect`.
- Failure: a consumer that reconnected through a different host keeps a TS sender streaming into the void; a stray/duplicate ack starts a sender C# would refuse to start.
- Direction: port the three OnAck guards (needs R10's `$sys.Disconnect`).

### R12. No outbound call timeouts of any kind

Confidence: confirmed (documented omission, but callers of the implemented API assume liveness).

- TS: no equivalent of the Maintain loop; `rpc-call-tracker.ts:25-28` documents it away. Deferred (`AwaitForConnection`) calls also wait indefinitely (`rpc-peer.ts:355-373`).
- C#: `RpcCallTrackers.cs:108-207` — RunTimeout/DelayTimeout enforcement; commands default to connect 1.5 s / run 10 s (`RpcCallTimeouts.Default.cs:16`); plus `ConnectTimeout` (`RpcOutboundCall.cs:103-115`).
- Failure: connected-but-stuck server (handler deadlock, lost `Ok`) → TS promise pending forever; UI awaits hang. The keep-alive watchdog only covers dead links, not stuck calls.
- Direction: a per-peer interval scanning `outboundCalls` against a coarse run-timeout (even opt-in per method).

### R13. No reconnect backoff and no premature-disconnect penalty

Confidence: confirmed (partly intentional).

- TS: fixed 100 ms delay (`rpc-client-peer-reconnect-delayer.ts:8-12`); `_tryIndex` reset on every successful handshake (`rpc-peer.ts:927`).
- C#: exponential backoff 1→60 s (`RpcClientPeerReconnectDelayer.cs:21-25`) plus `PrematureDisconnectTimeout` — a graceful close within 15 s still bumps the attempt index (`RpcPeer.cs:404-405`, `RpcLimits.cs:18`).
- Failure: a server crash-looping *after* handshake gets re-connected by every TS client ~5-10×/s indefinitely (thundering herd).
- Direction: keep the fixed delay, but don't reset `_tryIndex` when the connection lived less than a premature-disconnect threshold.

### R14. `parseStreamRef` pattern-sniffs every string in every result

Confidence: confirmed code; failure plausible.

- TS: `rpc-stream.ts:58-72` accepts *any* 4-6 part comma string whose parts 2-4 `parseInt` (`hostId` unconstrained); `resolveStreamRefs` (`:465-499`) rewrites such strings into registered `RpcStream`s inside arbitrary result graphs (`rpc-hub.ts:325-332`).
- C#: stream refs are only materialized where the static type says so (`RpcStreamJsonConverter`).
- Failure: JSON transport; a legit user-data string like `"a,1,30,61"` in a DTO silently becomes a broken `RpcStream` (and registers a phantom remote object that keep-alive then advertises).
- Direction: at minimum require `hostId` to parse as a GUID; better, only resolve refs at positions the method def declares as streams.

### R15. `$sys.End` error detection keyed on `Message` truthiness, not `TypeRef`

Confidence: confirmed. TS: `rpc-system-call-handler.ts:146-163` — `error = msg ? new Error(msg) : null`. C#: `error.IsNone` = empty `TypeRef` (`RpcSystemCalls.cs:180-187`, `ExceptionInfo.cs:32`). A stream terminated by an exception with an empty `Message` looks like clean completion. Direction: check TypeRef presence first.

### R16. `$sys.Reconnect` handler skips handshake-index validation

Confidence: confirmed (acknowledged in a comment, `rpc-system-call-handler.ts:65-69`). TS `_handleReconnect` ignores `args[0]`; C# throws `TooLateToReconnect` when `ownHandshake.Index != handshakeIndex` (`RpcSystemCalls.cs:57-70`). A TS callee can reconcile against the wrong connection generation after a rapid double-reconnect; bounded damage (worst case duplicate execution — R9). Direction: TS server peers should track their handshake index and validate.

### R17. `$sys.Cancel` doesn't abort the running handler and the result is still sent

Confidence: confirmed (documented omission). TS removes the tracker entry only (`rpc-system-call-handler.ts:101-107`); the dispatch continues and still sends the result (`rpc-peer.ts:554-570` — the send is not gated on the call still being registered). C# cancels the linked CTS and suppresses the result (`RpcInboundCall.cs:209-210, 246-247`). Direction: thread an `AbortSignal` through `RpcServiceHost.dispatch`; skip the response when the call was removed.

### RPC notes (low)

- `readVarUint` truncates values to 32 bits (`rpc-serialization.ts:151-167`: max 5 bytes, `value >>> 0`) vs C#'s 64-bit VarUInt; an id ≥ 2^35 would desync the frame. Practically unreachable with per-peer counters — note only.
- `msgpack-map-patch.ts` is needed and byte-correct (JS `Map` → msgpack map with typed keys, matching .NET `Dictionary<int, byte[]>` for `$sys.Reconnect`), but it patches `Encoder.prototype` **globally** on module load — worth documenting for host apps.
- Debugger-attached .NET peers use `KeepAlivePeriod = 300 s` (`RpcLimits.cs:46-54`); TS's 25 s watchdog (`rpc-limits.ts:43`) force-closes every 25 s against such a server — dev-environment churn; consider a dev-relaxed timeout.

### Parity confirmed (RPC)

- `RpcSystemCalls` name/arity constants match the C# `$sys` interface exactly; `RpcCallStage` values and `RpcRemoteExecutionMode` flags match; `guidToBase64Url` matches .NET Guid LE layout; xxHash3 method hashes are test-covered.
- `increasing-seq-compressor.ts` is wire-compatible with `Internal/IncreasingSeqCompressor.cs` (byte format verified); the 2^53 cap is fine for call ids.

### Out of scope (RPC)

- `$sys.M` (Match) + hash-based response caching (`RpcCacheInfoCapture`) — TS sends no Hash header, so servers never send `M` to it.
- `$sys.NotFound` handler — .NET servers report unknown methods via the normal `$sys.Error` path, so its absence is inert.
- Reliable call type and stage-based inbound `TryReprocess` beyond stage 0.
- Middleware pipeline (`RpcInboundMiddleware`, argument validators, call delayer).
- API versioning: `VersionSet`, `[LegacyName]`, version-aware `ServerMethodResolver`.
- Routing/rerouting: `RpcPeerRef` routing state, `TryReroute`, `RpcRerouteException` (TS only logs it).
- Diagnostics: `RpcCallLogger`, tracers, meters.
- Full server-side hosting (ASP.NET-equivalent connection handler, auth propagation, stop modes).
- Loopback/local peer kinds and `RpcTestClient`-style transports (TS uses MessageChannel instead).

---

## D. Fusion-over-RPC glue

### F1. Server→client invalidation is wired with a one-shot `onInvalidated.add()` that misses already-invalidated computeds → client stays stale forever

Confidence: confirmed (re-verified).

- TS: `FusionHub._wrapServerMethod` awaits `cf.invoke(...)`, and only *then* subscribes: `computed.onInvalidated.add(() => ... send($sys-c.Invalidate))` (`fusion-hub.ts:182-193`). `EventHandlerSet` has no replay, and `invalidate()` clears its handlers (K12). If the computed is invalidated *during* computation (mutation while a slow async server method runs), the pending-invalidate path re-invalidates **inside** `cf.invoke` before it resolves — the handler is added to a dead event set and **`$sys-c.Invalidate` is never sent**. The same hole exists for the microtask gap between `cf.invoke` resolving and `.add()` executing.
- C#: `RpcInboundComputeCall.ProcessStage2` awaits `computed.WhenInvalidated(token)` — completes immediately if already invalidated — then sends the invalidation (`RpcInboundComputeCall.cs:82-104`). No window exists.
- Failure: user A's `getCount('x')` is computing server-side when user B's mutation lands. A receives the (already stale) value; the server never notifies; A's UI shows the stale value forever.
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (fusion-rpc) — a mutation landing mid-computation produces no `$sys-c.Invalidate`; the client's `whenInvalidated` never completes.
- **Recommended:** replace `computed.onInvalidated.add(...)` in `_wrapServerMethod` with `void computed.whenInvalidated().then(send)` — `whenInvalidated()` already resolves immediately for invalidated computeds, exactly mirroring C# `ProcessStage2`. Cheap and surgical. While touching this code, routing the send through `systemCallSender` with the peer's serialization format (F9) is a natural companion fix.
- **Alternative:** fix the root cause K12 instead — give `onInvalidated` C#'s add-semantics (fire immediately when already invalidated), which repairs every subscriber by construction. Bigger blast radius; best done as its own item, with this site fixed the minimal way first.

### F2. Client receiving `$sys-c.Invalidate` before the result leaves `result` unsettled → permanent hang + poisoned per-key lock

Confidence: confirmed (missing defense); trigger paths plausible rather than everyday.

- TS: the handler does `peer.outboundCalls.remove(relatedId)` and `call.whenInvalidated.resolve()` — nothing else (`fusion-hub.ts:81-87`). If the result hasn't arrived, `outboundCall.result` stays pending forever: a later `$sys.Ok` is dropped (call untracked), reconnect resend never happens, and `rpcImpl`'s `await outboundCall.result` (`fusion-hub.ts:224`) never settles. `ComputeFunction.invoke` holds a per-key `AsyncLock` around the impl, so **every subsequent call of that compute method with those args queues behind the hang forever**. The handler also removes the call *before* the `instanceof RpcOutboundComputeCall` check — a non-compute call with that id would be silently evicted.
- C#: `RpcComputeSystemCalls.Invalidate` uses `Get` (not remove) + type check (`RpcComputeSystemCalls.cs:28-32`); `SetInvalidated` does `ResultSource.TrySetCanceled(...)` so a pre-result invalidation surfaces as OCE (`RpcOutboundComputeCall.cs:162-175`), which the caller retries (`RemoteComputeMethodFunction.cs:483-505`). "Invalidated before bound" is a deliberately handled state.
- Failure: any reordering anomaly (or future TS-server change) that delivers Invalidate first bricks that compute method for the rest of the session — UI queries spin forever with no error.
- Direction: on pre-result invalidation, reject/cancel `call.result` (a dedicated error type), mirroring `TrySetCanceled`; use `get` + type-check before `remove`.

### F3. Rejected remote calls (including cancellation/disconnect errors) are cached as error computeds

Confidence: confirmed. The remote-call variant of K6.

- TS: any rejection of `outboundCall.result` propagates out of `rpcImpl` (`fusion-hub.ts:224`) and is cached via `errorResult(e)` → `setOutput` — including abort-driven `'Call cancelled.'` (`rpc-peer.ts:377-391`) and `'Peer closed.'` from `rejectAll` (`rpc-call-tracker.ts:146-156`). On the error path the `whenInvalidated → computed.invalidate()` wiring is never established (only wired after a successful await, `fusion-hub.ts:226-230`), so error computeds rely solely on the 1 s timer (K5).
- C#: `ComputeRpc` rethrows OCE instead of producing a computed (`RemoteComputeMethodFunction.cs:227-231`); server-side cancellations are retried with backoff; genuine error computeds still track server invalidation (`RpcOutboundComputeCall.SetError`, `:119-146`).
- Failure: one caller cancels (or a peer closes mid-flight); for the next second every other consumer of the same compute key observes a cancellation error as if it were the value.
- Direction: treat cancellation-type rejections as non-cacheable (rethrow without `setOutput`, or invalidate immediately), per C#'s OCE handling.

### F4. Local invalidation of a client computed never releases the outbound call — no computed→call binding, no `$sys.Cancel`, strong-ref leak

Confidence: confirmed.

- TS: only the call→computed direction exists (`fusion-hub.ts:226-230`). Nothing observes the client computed's invalidation. A locally-invalidated computed's stage-3 `RpcOutboundComputeCall` stays registered until server invalidation or reconnect; the server is never told to stop tracking. The tracker's strong ref chain (call → `whenInvalidated.then` closure → `Computed`) also defeats `ComputedRegistry`'s WeakRef/GC design for the old computed.
- C#: `RemoteComputed.OnInvalidated` → `BindToCallFromOnInvalidated` → `CompleteAndUnregister(notifyCancelled: true)` which sends `$sys.Cancel`, letting the server stop waiting (`RemoteComputedExt.cs:26-44`, `RpcOutboundComputeCall.cs:179-186`, `RpcInboundComputeCall.cs:95-99`). Plus a `Dispose`/finalizer escape hatch.
- Failure: an app that locally invalidates (or rapidly re-keys) remote computeds accumulates tracker entries and pinned computeds per peer; the server keeps invalidation-notification state alive for clients that no longer care.
- Direction: after binding, subscribe once to the local computed's invalidation and, if `whenInvalidated` isn't completed, remove the call from the tracker (optionally sending `$sys.Cancel`).

### F5. Cancellation propagation is absent end-to-end in the compute-call glue

Confidence: confirmed (absent in TS).

- TS: `rpcImpl` builds `RpcCallOptions` without a `signal` (`fusion-hub.ts:214-223`) even though `RpcPeer.call` supports one; the compute client surface offers no cancellation input at all. Inbound `$sys.Cancel` also doesn't abort a running TS server handler (R17).
- C#: the caller's `CancellationToken` is threaded through `SendRpcCall` (`RemoteComputeMethodFunction.cs:405-451`); the server links `CallCancelToken` into its invalidation wait (`RpcInboundComputeCall.cs:86-88`).
- Failure: a client can't abandon a slow remote compute call; the call, its lock queue, and the server's work all run to completion regardless.
- Direction: accept an `AbortSignal` (via `AsyncContext` or options) in the compute client path and pass it to `peer.call`; keep the C# semantic that a cancelled compute call is unregistered and its computed not cached (ties into F3).

### F6. Every reconnect (including transient same-peer) invalidates all stage-3 compute calls — refetch storm

Confidence: confirmed (deliberate, test-codified deviation — but with a wasteful protocol interaction).

- TS: `_reconnect` unconditionally self-invalidates any compute call with a completed result (`rpc-peer.ts:1043-1050`), after first *including those same calls* in the `$sys.Reconnect` reconciliation (`rpc-peer.ts:1035-1036`; `getReconnectStage` reports `ResultReady`, `rpc-call-tracker.ts:93-98`). Tests codify this (`fusion-rpc-reconnection.test.ts:121-156`).
- C#: same-peer reconnect reports the stage and the server resumes stage-2 tracking without re-execution (`RpcOutboundComputeCall.GetReconnectStage`, `RpcInboundComputeCall.TryReprocess`); only a *peer change* invalidates completed calls.
- Failure: a 2-second WiFi blip invalidates every remote computed on the client → full refetch burst (C# clients refetch nothing). Against a .NET server, the client also first claims "these calls are alive" via `$sys.Reconnect` and then drops them, leaving the server holding re-armed invalidation tracking for discarded computeds.
- Direction: acceptable as a documented simplification, but at minimum stop reporting soon-to-be-dropped compute calls in `_reconcileReconnect`; longer term, adopt stage-based keep-alive for same-peer reconnects.

### F7. TS server ignores the message's `CallType` — regular calls to compute methods still get invalidation tracking

Confidence: confirmed.

- TS: `_wrapServerMethod` decides by the server-side `methodDef.callTypeId` only (`fusion-hub.ts:171`); `createRpcClient` produces exactly such regular calls (plain `RpcOutboundCall`, `removeOnOk = true` — `rpc-client.ts:72-90`).
- C#: `RpcInboundComputeCall.IsRegularCall` (`RpcInboundComputeCall.cs:19-25`) returns the result immediately and skips invalidation tracking when the caller didn't request a compute call.
- Failure: regular callers cause the server to send invalidation notifications to clients that dropped the call on `$sys.Ok` — wasted messages and bookkeeping per call.
- Direction: pass the inbound `CallType` through the dispatch context; skip invalidation wiring when it isn't `FUSION_CALL_TYPE_ID`.

### F8. No deduplication of remote computeds across proxies — each `addClient` mints a fresh key space

Confidence: confirmed.

- TS: `_createClientMethod` creates a new `ComputeFunction` (unique id — `compute-function.ts:36`) and a new `syntheticInstance` per `addClient` call (`fusion-hub.ts:233-234`); the key embeds both. Two proxies for the same service+peer maintain independent computeds, RPC calls, and invalidation streams for the same logical value.
- C#: client proxies are DI singletons; `ComputeMethodInput` keys by service/method/args so all consumers share one `RemoteComputed`.
- Failure: an app calling `hub.addClient` per component doubles/triples RPC traffic and can show inconsistent values across components between invalidation deliveries.
- Direction: cache the proxy (or the per-method `ComputeFunction`s) per `(peer, serviceDef)` inside `FusionHub.addClient`.

### F9. Server-side invalidation send bypasses the peer's serialization format and `systemCallSender` (low)

Confidence: confirmed code path; impact plausible-low. `_wrapServerMethod` hand-rolls `serializeMessage(...)` — JSON-only (`rpc-serialization.ts:31-41`) — and writes to the captured `context.connection` (`fusion-hub.ts:186-192`), while all other responses go through `hub.systemCallSender` with `peer.serializationFormat`. On a msgpack connection the invalidation goes out as a JSON text frame; TS clients tolerate mixed frames, a .NET client would not. Also uses a possibly-dead captured connection. Direction: add `invalidate()` to `RpcSystemCallSender` (mirroring `RpcComputeSystemCallSender.Invalidate`) and send via the peer's current connection/format.

### F10. `FusionHub._buildServiceDef` drifted from the base implementation (low)

Confidence: confirmed. The override hardcodes `remoteExecutionMode: Default` and skips the base's `noWait → mode 0` and `meta.remoteExecutionMode` honoring (`fusion-hub.ts:146-160` vs `rpc-hub.ts:244-259`). A decorator-declared custom `remoteExecutionMode` is silently ignored when registered through a `FusionHub`. Direction: delegate to `super._buildServiceDef` and only patch `callTypeId` for compute methods.

### F11. Server peers created by `acceptConnection` are never removed from `hub.peers` (low)

Confidence: confirmed. Each accepted connection creates a UUID-ref `RpcServerPeer` (`fusion-hub.ts:118-132`) that stays in `hub.peers` after the connection closes; cleanup only happens on hub `close()`. A long-running TS server leaks one peer (plus trackers) per client connection. C# server peers auto-dispose after a close timeout.

### Parity confirmed (Fusion-over-RPC)

- Invalidation arriving just *after* the result is handled correctly: `whenInvalidated.then(...)` replays on an already-resolved promise, and `invalidate()` during Computing defers via `_invalidatePending` — equivalent to C#'s invalidated-before-bound handling.
- Call keep-alive: `removeOnOk = false` keeps compute calls registered past the result (`rpc-outbound-compute-call.ts:6`), with unregistration on invalidate/reconnect/close — matches C#'s `CompleteKeepRegistered`/`CompleteAndUnregister` shape (modulo F2 and F4).
- Disconnect alone does not invalidate stage-3 calls or captured computeds; reconnect/peer-stop does — matches C#'s peer-change contract, well covered by tests.
- Dependency capture for local-compute-method → remote-compute-method works; server invalidation cascades to dependants.

### Out of scope (Fusion-over-RPC)

- `RemoteComputedCache` / cache-and-swap + `$sys.M` hash validation.
- Serve-stale-on-disconnect + `InvalidateWhenReconnected`.
- Per-method `ComputedOptions` (min cache duration, auto-invalidation, `CancellationReprocessing` retry policy).
- `WhenSynchronized` / `ComputedSynchronizer`.
- Rerouting and Distributed/local-execution mode.
- Server-side stage-based inbound reprocessing (`RpcInboundComputeCall.TryReprocess`) — the TS server reconnect handler is membership-only.

---

## E. Core utilities

### C1. `PromiseSource` rejection with no attached consumer fires `unhandledrejection` (Node: process crash by default)

Confidence: confirmed.

- TS: `promise-source.ts:27-32` — `reject()` rejects `this.promise` with no pre-attached rejection observer. Same for the timer-driven reject in `promise-source-with-timeout.ts:38-42`.
- C#: an unobserved faulted `Task` is benign in .NET; C# code freely does `TrySetException` on sources nobody may await.
- Failure: `rpc-peer.ts:1007/1015` and `rpc-call-tracker.ts:152` reject `call.result` for every tracked call on disconnect/error. Any tracked call whose caller isn't (yet or anymore) awaiting `result` raises `unhandledrejection`; in Node ≥15 that terminates the process by default. The port pattern assumes "reject is always safe".
- Direction: attach a no-op observer in the constructor (`this.promise.catch(() => {})`); consumer chains still see the rejection normally. This exactly reproduces the .NET observed-exception contract.

### C2. `Result` misclassifies `undefined` errors as success

Confidence: confirmed (re-verified).

- TS: `result.ts:17-22` — `hasValue = error === undefined`. So `errorResult(undefined)` yields `hasValue === true`; `resultFrom`/`resultFromAsync` (`result.ts:54-70`) pass a caught `undefined` straight through — `throw undefined` or `reject()` with no reason becomes a *successful* result with value `undefined`. Asymmetric, too: `error: null` IS treated as an error.
- C#: `Result.cs:329-335` — `Error is null` is the discriminator, and the type system guarantees a caught exception is never null; the state is unrepresentable.
- Failure: `compute-function.ts:106-107` wraps the user's compute body in `errorResult(e)`. A dependency that does `throw undefined` (legal JS) gets cached as a valid computed value `undefined` — consumers see a wrong value instead of an error.
- Direction: keep a private `_hasError` flag set explicitly by the error-constructing paths, or normalize nullish errors (`error ?? new Error(...)`) in `errorResult`/`resultFrom*`.

### C3. `RetryDelayer.getDelay` ignores an already-aborted `cancellationSignal`

Confidence: confirmed.

- TS: `retry-delayer.ts:43-70` — no `signal.aborted` pre-check; `addEventListener('abort', ...)` on an already-aborted signal never fires, so the delay runs to completion and *resolves normally* despite cancellation. On a live abort it also rejects with a generic `Error` rather than `signal.reason`.
- C#: `RetryDelayer.cs:46-56` — linked CTS; an already-canceled token throws OCE immediately; only the `cancelDelaysToken` branch completes normally (that half TS gets right).
- Failure: currently latent (`rpc-peer.ts:772` passes no signal), but any future caller passing its stop signal will "successfully" wait out the delay after being stopped, then reconnect a stopped peer.
- Direction: pre-check `signal.aborted` (reject with `signal.reason`); reject with `signal.reason` on abort; thread the peer's stop signal into `rpc-peer.ts:772`.

### C4. `EventHandlerSet.trigger` live-iterates the `Set` — handlers added during dispatch run in the *same* dispatch

Confidence: confirmed.

- TS: `events.ts:19-21` — `for (const handler of this._handlers) handler(arg);` — no snapshot. Consequences: (a) `whenNext()` (`events.ts:27-35`) called from inside a handler of the same set resolves *immediately* with the current event instead of the next; (b) a handler that re-adds handlers can extend the loop; (c) a handler removed by an earlier handler is silently skipped. Also: `Set` dedupes — adding the same function twice invokes once; a C# delegate invokes twice.
- C#: multicast delegates invoke an immutable snapshot — subscribers added during raise are not invoked, removed ones still are. (The TS doc comment at `events.ts:3` itself claims delegate parity.)
- Failure: `EventHandlerSet` is the backbone of RPC state plumbing (`rpc-peer.ts`, `rpc-connection.ts`, `rpc-peer-state-monitor.ts`) and Fusion (`computed.ts`). "In my `stateChanged` handler, await the next state via `whenNext()`" self-resolves with the event being dispatched — a subtle off-by-one in reconnect/state logic.
- Direction: iterate a snapshot (`[...this._handlers]`) — one line; also makes remove-during-dispatch match C#.

### C5. `AsyncLock` has no reentry detection (`LockReentryMode`) and no abort-while-queued

Confidence: confirmed absent.

- TS: `async-lock.ts:10-18` — `acquire()` takes no `AbortSignal`; a queued waiter cannot be cancelled; no reentry tracking. (Fairness and release-on-throw are correct.)
- C#: `Locking/AsyncLock.cs:8-43` — `Lock(CancellationToken)` plus `LockReentryMode.CheckedFail`. Fusion C# uses `CheckedFail` in the exact places the TS port mirrors (`State.cs:59`, `ComputedRegistry.cs:78`).
- Failure: same-key compute recursion deadlocks silently (K14); nothing can time out or abort a queued waiter.
- Direction: add an optional reentry-check mode and an optional `AbortSignal` on `acquire()/run()` that removes the queued resolver on abort.

### C6. No cancellable delay: `delayAsync` takes no `AbortSignal`, so `retry()` can't be aborted

Confidence: confirmed absent.

- TS: `delay.ts:2-6` — bare `setTimeout`; `retry.ts:22-43` accepts no signal.
- C#: every delay in a retry path is cancellable (`Task.Delay(delay, ct)` / `Clock.Delay`).
- Failure: teardown during a `retry()` inter-attempt delay: the timer keeps the Node event loop alive and the next attempt executes against disposed state. `RetryDelayer` had to reimplement abortable delay inline instead of reusing a shared primitive.
- Direction: `delayAsync(ms, signal?)` that clears the timer and rejects with `signal.reason`; thread an optional signal through `retry()`; reuse in `RetryDelayer`.

### C7. `abortPromise` fast path for already-aborted signals contradicts its own caching/observation contract

Confidence: confirmed. `abort-promise.ts:26` returns a *fresh, unobserved* rejected promise per call for an already-aborted signal, while the pending path caches per signal and pre-attaches `.catch()` precisely to avoid unhandled rejections — and the doc promises "same promise per signal". Code that grabs the promise and only races it on a later iteration gets an `unhandledrejection` (Node crash). Direction: route the aborted case through the same cache — 3 lines.

### C8. `RingBuffer` — head-side/tail-side API half missing; constructor semantics differ (low)

Confidence: confirmed absent. TS has `pushTail`/`pushTailAndMoveHeadIfFull`/`pullHead`/`moveHead`/`get` only; C# also has `TryPullHead`, `PullTail`/`TryPullTail`, `PushHead`, indexer setter, `GetSpans` (`RingBuffer.cs:98-146`), and `RingBuffer(minCapacity)` rounds capacity *up* while TS treats it as exact. No caller needs the missing half today (`rpc-stream-sender.ts:260` uses the implemented subset). Direction: add operations when a caller appears; document "exact capacity" as a deliberate deviation.

### C9. `Result` — no equality, no untyped variant (low)

Confidence: confirmed absent. No `equals` (C# `Result<T>.Equals` + operators, `Result.cs:380-388`), no untyped `Result`. C# paths that skip work when `oldResult == newResult` can't be ported faithfully — this is what blocks S15. Direction: add `equals(other, valueComparer?)`; skip the untyped variant until needed.

### Parity confirmed (core)

- `RetryDelaySeq` math is an exact port (fixed-vs-exp branch, `min·multiplier^(n−1)` capped by `max`, jitter, clamps) — `retry-delay-seq.ts:46-62` vs `RetryDelaySeq.cs:47-68` + `RandomTimeSpan.cs:43-50`; `cancelDelays()` swap-then-abort ordering matches C#.
- `withTimeout` (`withTimeout.ts:23-43`): timer cleared in `finally`, both race arms observed; mirrors `Task.WaitAsync(timeout)`. Only gap: no `AbortSignal` overload — minor.
- `throttle`/`debounce`: timers cleared or provably self-neutralizing; ActualChat-origin, no C# contract to violate.
- `AsyncLock` fairness/release-on-throw correct (FIFO queue, `finally` release).

Note: `AsyncContext` not flowing across `await` is a real deviation from C# `ExecutionContext`/`AsyncLocal`, but its user-facing impact is the kernel's dependency-capture loss — tracked as K3, not duplicated here.

### Out of scope (core)

- `serialize.ts` — an execution serializer from ActualChat, not a wire serializer; no C# counterpart in ActualLab.Core; currently unused by rpc/fusion.
- `timed-out.ts` (`TimedOut` sentinel) — no counterpart, zero consumers; consider deleting.
- `catchErrors`, `delayAsyncWith` — convenience helpers, unused by rpc/fusion.
- `logging*.ts` — TS-native design (console + localStorage/IndexedDB), intentionally not an `ILogger` port.
- `polyfills.ts`, `resolved-promise.ts`, `disposable.ts` — trivial, match their .NET analogs.
- C# `AsyncLockSet<TKey>` — not ported; `compute-function.ts` hand-rolls a per-key lock Map instead (the leak is K8).

---

## Test-coverage gaps worth closing alongside fixes

- Kernel: invalidation racing an in-flight computation (K2); nested compute calls after an `await` (K3); dependency capture on an invalidated dependency (K1).
- State: `set()` observed from a synchronous invalidation handler (S3); dispose mid-computation (S5); StrictMode double-mount for both hooks (S7).
- RPC: header-bearing V5/V5C frames (R1); `$sys.End` with an index gap (R4); tracker emptiness + `AckEnd` after stream completion (R3); duplicate inbound call id (R9).
- Fusion-over-RPC: server-side invalidation for a computed invalidated mid-computation (F1); `Invalidate`-before-result (F2).
- Core: unobserved `PromiseSource` rejection (C1); `retry-delayer` has no test file at all; mutation-during-dispatch in `events.test.ts`.

## Next step

Mirror the invalidation-audit flow:

1. **Adversarial verification pass** over the items above (each was produced by a single reviewer; the spot-checked sample and the five test-confirmed items held, but the rest deserves the same treatment before implementation). This pass may also surface additional findings — add them here with the same numbering scheme.
2. **Companion `ts-port-fixes.md`** recording the agreed course of action per item — the five test-confirmed items already carry Recommended/Alternative pairs above as input to that decision — including explicit won't-fix decisions for the deliberate deviations (F6's reconnect simplification, K3's AsyncContext limitation, S16's lazy renewal).
3. **Fix stage**, flipping the red reproduction tests green as each of the five confirmed items lands (commit the tests together with their fixes).
