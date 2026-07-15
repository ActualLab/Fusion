# TypeScript Port Gap Audit

This document records contract and robustness gaps found while reviewing the TypeScript port (`ts/packages/*`) against the canonical C# implementation. It is a discussion document — the items below describe deviations and defects as observed in code, but the course of action for each is decided separately (a companion doc, same pattern as the earlier invalidation audit/fixes pair).

The port is deliberately partial. The standard applied here is: **every feature the port does implement must be as robust as the C# version and follow the same contracts**, adjusted for the JS realm (single-threaded event loop, promises vs. tasks, `AbortSignal` vs. `CancellationToken`). Wholesale-missing subsystems are listed briefly per section under "Out of scope" and are not treated as defects.

**Audit status (2026-07-14):** produced by five parallel per-area reviews (Fusion kernel, State + React, RPC, Fusion-over-RPC glue, core utilities), each comparing TS and C# sources file-by-file. Every item carries a confidence label from its auditor (*confirmed* = the full failure chain was read in code on both sides; *plausible* = mechanism confirmed, trigger conditions not fully traced). A sample of the highest-severity items (K1, K2, K4, K5, K12, S3, R1, F1, F2, C2) was independently re-verified against current sources. A full adversarial verification pass — the step the invalidation audit got before its fixes — has not been done yet and is the natural next step.

**Test confirmation (2026-07-14):** the five highest-severity items — **K1, K2, K3, F1, R1** — are additionally confirmed by executable reproduction tests that assert the C# contract and fail against the current code (5/5 red): `ts/packages/{fusion,fusion-rpc,rpc}/tests/ts-port-audit-repro.test.ts`. The tests are deliberately left red (uncommitted until the fixes land) and become the regression tests for these items.

**Every item carries a Recommended / Alternative course-of-action pair** as input to the resolution stage; final decisions per item (including won't-fixes) will be recorded in a companion `ts-port-fixes.md`. The pairs for the five test-confirmed items were written first and are the most scrutinized; the rest follow the same pattern but haven't been through the verification pass yet.

**Second pass (2026-07-14):** a second review pass added five more findings — **R18** (regular dispatch loses the implementation receiver), **R19** (decorator metadata shared/mutated across base and derived classes), **R20** (decorator wire arity wrong with default/rest parameters), **R21** (tracker identity invariants violated on replace/unregister), **R22** (streams registered and kept alive before enumeration). All five were independently validated against TS and C# sources (and four reproduced with short executable probes, removed after verification); they are merged into the RPC section below with Recommended/Alternative pairs.

Item numbering: **K** = Fusion kernel, **S** = State layer + React, **R** = RPC layer, **F** = Fusion-over-RPC glue, **C** = core utilities.

## Decisions

Directives from Alex that override or refine the per-item proposals below; the impacted items were updated in place.

- **D5 (2026-07-14) — close all listed test-coverage gaps.** The "Test-coverage gaps worth closing alongside fixes" list below is approved as work: each gap gets a test asserting the C# contract. Gaps whose tests fail against current code join the red repro suite (`ts-port-audit-repro.test.ts` per package, committed with their fixes); gaps whose behavior is already correct (e.g. `RetryDelaySeq` math) get green regression tests committed immediately. S7's StrictMode test needs React render infrastructure (jsdom + `react-dom` test setup) that `fusion-react` doesn't have yet — it lands with the S6/S7 fix.
- **D4 (2026-07-14) — dev-relaxed keep-alive timeout: yes.** Add a static `RpcLimits.Debug` preset to `ts/packages/rpc/src/rpc-limits.ts` with `keepAliveTimeoutMs = 300_000` (matching .NET's debugger-attached `KeepAlivePeriod`, `RpcLimits.cs:46-54`), activated explicitly (`RpcLimits.Default = RpcLimits.Debug` or per-hub/per-peer via the existing override paths). The TS side cannot detect a debugger on the *server*, so activation stays an explicit app-level (dev-config) choice rather than an automatic `NODE_ENV` switch — auto-relaxing every dev session would delay dead-link detection to 5 min for everyone debugging nothing.
- **D3 (2026-07-14) — R2: TS `$sys.Error` sends `RemoteException`.** .NET-calls-into-JS is a rare direction, so keep it simple for now: the TS error sender uses the `TypeRef` of `ActualLab.Serialization.RemoteException` (resolvable on every ActualLab peer, `(string message)` ctor → reconstructs directly as a typed `RemoteException`), with the JS error name folded into the message (`"{name}: {message}"`) for provenance. Consequence accepted: `RemoteException` is `ITransientException`, so .NET reprocessors may briefly retry deterministic JS errors — the established ActualLab convention for unknown remote errors. Deferred, not rejected: sending `JavaScript.{error.name}` pseudo-TypeRefs (the `ToException()` fallback wraps them into `RemoteException` while preserving the name on `ExceptionInfo.TypeRef`, and `UnknownExceptionTypeResolver` could map them to real .NET types) and a dedicated `JavaScriptException` .NET type — revisit if .NET→JS calls become common.
- **D2 (2026-07-14) — K13 is a won't-fix: keep `JSON.stringify` keying.** Speed wins here — `JSON.stringify` is likely the fastest keying option available, so the default stays as-is; the compute method's author is responsible for using properly-keyable argument types (or supplying `argToString`) and documenting the method's behavior. No throw/warn on non-representable args. The one tweak worth taking: the `?? 'undefined'` fallback can become `?? ''` — the result is only a key component between RS delimiters, and the empty string is just as collision-safe (only `undefined`/functions/symbols hit the fallback, and they already collide with each other today) while being shorter.
- **D1 (2026-07-14) — introduce a `ComputedOptions` analog.** The TS port gets a real (initially minimal) `ComputedOptions` rather than point fixes like the global `Computed.errorAutoInvalidateDelay`: an options object carried per `ComputeFunction` / compute method (overridable at declaration — decorator argument / registration input), with static per-kind defaults mirroring C# (`ComputedOptions.Default` for compute methods, a `MutableStateDefault` analog for state-bound computeds). First field: **`errorAutoInvalidateDelay`** (the K5 fix; `Infinity` = disabled, the state-bound default). The structure is the designated home for later per-method knobs — `autoInvalidationDelay`, `minCacheDuration`, transiency-aware error delays, cancellation-reprocessing policy — so each lands as a field, not a new mechanism. Impacted items reworked: K5, K6, F3; the kernel and Fusion-over-RPC out-of-scope notes are narrowed accordingly.

## Severity overview

The items most likely to produce user-visible wrong behavior in an app that uses only what the port already ships:

- **Permanent staleness** — K1 (dead dependency edges), K2 (invalidation lost during computation), K3 (dependencies silently dropped after the first `await`), S1 (initial-value computeds never invalidated), F1 (server never sends `Invalidate` for computeds invalidated mid-computation).
- **Hangs** — F2 (pre-result `Invalidate` permanently poisons a compute key's lock), S5 (dispose mid-computation deadlocks `update()` callers), R2 (TypeRef-less `$sys.Error` hangs .NET callers), R12 (no outbound call timeouts).
- **Wire-protocol breaks vs .NET peers** — R1 (V5/V5C header position), R6 (`mempack6` registered as msgpack), R7 (no polymorphism markers), R5 (pre-handshake sends).
- **Process-level failures** — C1/C7/S14 (unhandled promise rejections; crash Node by default), K4 (a throwing invalidation handler halts the cascade and kills `ComputedState` update loops).
- **Resource leaks** — R3 (completed streams never unregister on either peer), R22 (never-enumerated streams leased forever on both peers), K7/K8/K10/S4/F4/F11 (graph edges, locks, listeners, calls, server peers).
- **Registration/dispatch correctness** — R18 (server dispatch loses the implementation receiver), R19 (decorator metadata contaminates base/derived contracts), R20 (wrong wire arity with default/rest parameters), R21 (tracker replace/unregister violate identity invariants).

---

## A. Fusion kernel

### K1. `addDependency` has no state checks — an edge added to an already-invalidated Computed is silently dead

Status: **closed** — fixed 2026-07-15 (batch kernel1).

Confidence: confirmed.

- TS: `ts/packages/fusion/src/computed.ts:218-224` — `addDependency` unconditionally adds the edge. No check that `this` is still Computing, and no check that `dependency` is not Invalidated. Since `invalidate()` early-returns for Invalidated (`computed.ts:170-171`) and its `_dependants` were already cleared (`computed.ts:188`), an edge added to an invalidated dependency never fires.
- C#: `Computed.cs:453-474` — `AddDependency` returns unless the dependant is `Computing`; `AddDependant` (`Computed.cs:477-492`) detects an Invalidated dependency and invalidates the dependant immediately. The comment at `Computed.cs:230-234` documents this as the load-bearing guarantee of `Use`.
- Failure: caller computed C awaits `cf.invoke(...)`; while awaiting, the produced computed D is invalidated. The capture (`compute-function.ts:116`, or `use()`'s `.then` at `computed.ts:126-129`) then adds an edge to the dead D. C completes Consistent with a value derived from stale data and is **never invalidated** afterward. Same hole via `useInconsistent` (`computed.ts:135-140`).
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (fusion) — `parent()` keeps returning the stale composite after the child's data changed.
- **Recommended:** port the C# guards into `Computed.addDependency`: return unless `this` is still `Computing`; if the dependency is `Invalidated`, call `this.invalidate()` (which lands in `_invalidatePending` for a Computing dependant) instead of adding the edge. One primitive, covers every capture site — `captureDependency`, `use()`, `useInconsistent`, `capture` — including future ones.
- **Alternative:** guard at each capture site individually (`ComputeContext.captureDependency`, `use()`'s `.then`, `invoke`'s post-lock capture). Rejected-leaning: several sites today, more tomorrow; the invariant belongs to the graph-mutation primitive, exactly where C# enforces it.

### K2. In-flight computations are invisible to key-based invalidation — `invalidate()` during computation is lost

Status: **closed** — fixed 2026-07-15 (batch kernel1; creation-time registration + "already registered" early exit).

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

Status: **closed** — fixed 2026-07-15 (batch kernel2).

Confidence: confirmed.

- TS: `computed.ts:184-193` — dependant propagation and `onInvalidated.trigger()` run with no try/catch (`events.ts:19-21` doesn't isolate handlers either). A throwing handler propagates up: remaining dependants are never invalidated, `_unregister` is skipped (an Invalidated computed stays registered), and the exception surfaces to whoever called `invalidate()` — e.g. out of `MutableState.set()`, or inside `ComputedState._updateCycle`, whose outer catch (`computed-state.ts:150-154`) logs "UpdateCycle failed and stopped" and **exits the loop permanently**.
- C#: "Invalidate doesn't throw — ever": the whole cascade is wrapped in try/finally/try-catch with logging (`Computed.cs:300-329`); dependants are invalidated in a `finally`. State event handler exceptions are similarly isolated (`State.cs:287-321`).
- Failure: one buggy `onInvalidated` subscriber throws once → half the dependency graph stays consistent-but-stale, and that `ComputedState` silently stops auto-updating for the app's lifetime; UI freezes on the last value.
- **Recommended:** make `invalidate()` never throw, mirroring `Computed.cs:300-329`: fire own handlers first (each wrapped in try/catch + error log), then propagate to dependants unconditionally (per-dependant try/catch), then unregister — all guaranteed to complete. Fixes K17's ordering in the same rework.
- **Alternative:** isolate exceptions inside `EventHandlerSet.trigger` for *all* events. Rejected-leaning: C# general events do propagate handler exceptions; only invalidation carries the never-throws contract, so the isolation belongs in `Computed.invalidate`.

### K5. Every error auto-invalidates after a global 1 s — states included → perpetual 1 Hz invalidate/recompute loops

Confidence: confirmed.

- TS: `computed.ts:24-25, 154-158` — `setOutput` schedules `setTimeout(() => this.invalidate(), 1000)` for **any** error on **any** Computed, including `StateBoundComputed` (states call `setOutput` at `state.ts:87, 92`). `MutableState`'s renewer recreates the computed with the same error output (`mutable-state.ts:7-11`), scheduling the next 1 s invalidation.
- C#: error auto-invalidation is per-`ComputedOptions` with transiency classification (`Computed.cs:374-405, 540-567`); `MutableState` uses `ComputedOptions.MutableStateDefault` — a MutableState holding an error **never** auto-invalidates (`ComputedOptions.cs:21-24`).
- Failure: `state.set(errorResult(e))` → every dependant is invalidated and recomputed every second, forever. Non-transient/terminal errors in compute methods retry at 1 s forever (C# differentiates via `NonTransientErrorInvalidationDelay` = 30 s and `Transiency.Terminal`). Bonus defects: the timer strongly retains the computed for 1 s (defeats WeakRef eviction) and an un-`unref`'d `setTimeout` keeps a Node process alive.
- **Recommended (per D1):** introduce the `ComputedOptions` analog and move error auto-invalidation onto `options.errorAutoInvalidateDelay`: each `Computed` takes its options from its `ComputeFunction` (declaration-time override) or its kind's static default — compute methods keep a finite default, `StateBoundComputed` uses the `MutableStateDefault` analog (`Infinity` — states never auto-invalidate errors; `ComputedState` retries via its update loop, see S8). Cancel the timer when the computed is invalidated by other means, and `unref()` it in Node. The global `Computed.errorAutoInvalidateDelay` becomes the default-options seed or is removed.
- **Alternative (superseded by D1):** a lone per-computed `errorAutoInvalidateDelay` field, or keeping the global delay with a `StateBoundComputed` exclusion — smaller, but every next per-method knob would need its own mechanism.

### K6. Cancellation errors are cached as values — C# contract: OCE must never be cached

Confidence: confirmed (absence verified).

- TS: `compute-function.ts:98-110` — every throw, including `AbortError` from an aborted fetch, becomes `errorResult(e)` and is cached via `setOutput` (until K5's 1 s timer). No AbortSignal exists anywhere in the compute pipeline.
- C#: `ComputedImpl.Helpers.cs:116-159` — an `OperationCanceledException` never becomes a cached consistent output: the computed is invalidated immediately and the error rethrown to the canceled caller only; internal cancellations are reprocessed with retries. `StartAutoInvalidation` instant-invalidates OCE outputs as a second line of defense (`Computed.cs:388-391`).
- Failure: caller A's fetch is aborted (user navigated away); for up to 1 s, callers B/C of the same compute key receive A's `AbortError` from cache — cancellation of one consumer poisons all consumers. See also F3 for the remote-call variant.
- **Recommended:** introduce a cancellation-detection helper in `@actuallab/core` (recognizes `DOMException`/`Error` named `AbortError` plus a dedicated `OperationCancelledError`); in `ComputeFunction.invoke`, when the caught error is cancellation-shaped, still `setOutput` (keeps the single-flight lock protocol intact) but invalidate the computed immediately — C#'s `StartAutoInvalidation` OCE path — so it is never served from cache. With D1's `ComputedOptions` in place, a future cancellation-reprocessing policy (C# `ComputedCancellationReprocessingOptions`) has its natural home there.
- **Alternative:** bypass caching entirely and rethrow to the cancelled caller. Closer to C#'s primary path but leaves the `Computed` stuck in `Computing` and complicates the lock/renewer flow; the invalidate-immediately variant gets the same observable behavior with less surgery.

### K7. Invalidated dependants never unlink from their dependencies; no graph pruner

Status: **closed** — eager unlink fixed 2026-07-15 (batch kernel1); a pruner remains a possible follow-up if profiling shows growth.

Confidence: confirmed.

- TS: `invalidate()` clears only its own `_dependencies` and `_dependants` (`computed.ts:180-188`); it never removes itself from its dependencies' `_dependants` maps. Entries are `version → WeakRef`; the WeakRef target can be GC'd but the Map entry lives until the *dependency* itself is invalidated.
- C#: on invalidation, each dependency gets `RemoveDependant(this)` (`Computed.cs:309`); plus `PruneDependants` (`Computed.cs:507-526`) driven by `ComputedGraphPruner`.
- Failure: a rarely-changing computed (config, session) used by a frequently recomputing `ComputedState` accumulates one dead `(number, WeakRef)` entry per recompute — tens of thousands over a long SPA session.
- **Recommended:** in `invalidate()`, delete `this._version` from each dependency's `_dependants` before clearing `_dependencies` (C# `RemoveDependant` parity; cheap in JS, no lock concerns).
- **Alternative:** a periodic pruner sweeping `_dependants` maps for dead WeakRefs (`ComputedGraphPruner` analog). Heavier and only complements — eager unlink is the correct fix; a pruner can follow if profiling still shows growth.

### K8. `ComputeFunction._locks` is never cleaned — one `AsyncLock` leaked per distinct key, forever

Status: **closed** — fixed 2026-07-15 (batch kernel1). Residual: the delete is skipped when `lock.run` rejects (only possible when an invalidation handler throws) — K4's root cause, resolved with K4.

Confidence: confirmed.

- TS: `compute-function.ts:32, 71-77` — locks are created on demand and never deleted.
- C#: shared `AsyncLockSet<ComputedInput>` (`ComputedRegistry.cs:77-81`) releases per-key entries when unused.
- Failure: a compute method keyed by user/entity id in a long-running app → unbounded Map of idle AsyncLocks.
- **Recommended:** after `lock.run` resolves, delete the map entry when the lock has no queued waiters — a few lines in `ComputeFunction.invoke`.
- **Alternative:** implement a small `AsyncLockSet` in `@actuallab/core` (C# analog, release-on-idle built in) and use it here. Cleaner shared abstraction — promote to this if a second per-key-lock consumer appears.

### K9. `update()` is not isolated — renewal registers a dependency in the ambient compute context

Status: **closed** — fixed 2026-07-15 (batch kernel2).

Confidence: confirmed.

- TS: `Computed.update()` → `_renewer()` (`computed.ts:106-114`) → `ComputeFunction.invoke`, which falls back to `AsyncContext.current` (`compute-function.ts:55-58`) and captures the produced computed into that ambient context (`compute-function.ts:116`).
- C#: `UpdateUntyped` wraps production in `BeginIsolation()` (`Computed.cs:207-215`) — updating a computed must not create a dependency edge; only `Use` does.
- Failure: inside a computing method, `someComputed.update()` (or `state.update()/recompute()`) intended as "refresh without depending" silently records a dependency — spurious recomputation storms. This inverts the documented Update/Use distinction.
- **Recommended:** in the renewer path, run `invoke` with a context explicitly cleared of `computeContextKey` (the TS analog of `BeginIsolation`) — one place, covers `update()`, `state.update()`, and `recompute()`.
- **Alternative:** strip the context at each caller of `update()`. Rejected-leaning: multiple sites, and direct `computed.update()` calls would still capture.

### K10. `whenInvalidated(abortSignal)` leaks one abort listener per call on long-lived signals; never settles on an already-aborted signal

Status: **closed** — fixed 2026-07-15 (batch kernel2).

Confidence: confirmed.

- TS: `computed.ts:196-216` — the `'abort'` listener (added `{ once: true }`) is never removed when the promise resolves via invalidation. `ComputedState._updateCycle` passes the same `disposeSignal` every iteration (`computed-state.ts:139`), so listeners accumulate one per update. Also: when the signal is *already aborted*, the listener is skipped and the returned promise may never settle (feeds S5); and `ps.reject(abortSignal.reason)` can reject with `undefined`.
- C#: `WhenInvalidatedClosure.OnInvalidated` disposes the CancellationTokenRegistration as soon as invalidation fires (`Internal/WhenInvalidatedClosure.cs:27-31`); already-cancelled tokens throw immediately (`ComputedExt.cs:120-122`).
- Failure: a `ComputedState` updating every few seconds in a day-long session accumulates tens of thousands of listeners on its dispose signal — memory growth plus O(n) abort dispatch.
- **Recommended:** store the abort listener in a variable so both sides clean each other up: the invalidation handler removes the abort listener, the abort listener removes the invalidation handler; pre-check `abortSignal.aborted` and reject immediately (with `signal.reason ?? OperationCancelledError`).
- **Alternative:** build the shared "await with signal" core helper first (see S4) and rewrite this on top of it — same end state, better if done together with the delayer-leak fixes.

### K11. `Computed.capture` deviates: returns the *first* captured computed (C#: last), throws on failed computations (C#: returns the errored computed), and pollutes real dependants with a stub

Confidence: confirmed.

- TS: `computed.ts:28-42` — returns `deps.values().next().value` (first inserted); if `fn` rejects, `capture` rejects; the fake Computing stub is registered in real computeds' `_dependants` (via K1's unchecked `addDependency`) and never cleaned.
- C#: `ComputeContext.TryCapture` deliberately overwrites → last capture wins (`ComputeContext.cs:70-84`); `Computed.Capture` catches non-cancellation exceptions and returns the captured computed if it `HasError` (`Computed.Static.cs:153-169`).
- Failure: `Computed.capture(() => svc.failingMethod())` throws instead of yielding a `Computed` with `hasError` — reactive wrappers (the primary use of capture) can't observe invalidation of the failed computation; with two top-level calls in `fn`, TS returns the wrong one relative to C# docs/samples.
- **Recommended:** track "last captured" explicitly on the capture context (a dedicated capture mode, not a fake Computing stub — so no dependant edges pollute real computeds); on `fn` rejection, return the captured computed if it has an error, else rethrow — full `Computed.Capture` parity.
- **Alternative:** document the deviations (first-wins, throws on error). Rejected-leaning: `capture` is the primary API for building reactive wrappers; C# samples and docs rely on the last-wins + errored-computed behavior.

### K12. `onInvalidated` is a public raw handler set — a handler added after invalidation never fires (C#: fires immediately)

Status: **closed** — fixed 2026-07-15 (batch kernel2; `onInvalidated(handler)` is now a method with immediate fire on already-invalidated computeds).

Confidence: confirmed.

- TS: `computed.ts:51, 190-191` — after `trigger()` the set is cleared; `EventHandlerSet.add` on an already-invalidated computed stores a handler that can never fire. Only `whenInvalidated` has the state pre-check.
- C#: the `Invalidated` event's `add` invokes the handler immediately when state is already Invalidated (`Computed.cs:105-117`) — exactly-once regardless of subscription timing.
- Failure: `computed.onInvalidated.add(h)` after an async gap → `h` never runs → a UI subscription never refreshes. This is the exact race the C# `add` accessor exists to close, and the root cause of F1.
- **Recommended:** replace the public field with an `onInvalidated(handler)` method that invokes the handler immediately when the computed is already invalidated (C# `add`-accessor parity); keep the handler set private.
- **Alternative:** keep the field but give `EventHandlerSet` an optional "completed with replay" mode (promise-like: once triggered, late `add`s fire immediately). More general, but changes a core primitive's semantics for one consumer; the method API is the closer C# mirror.

### K13. Default argument keying via `JSON.stringify` produces key collisions

Confidence: confirmed.

- TS: `compute-function.ts:19-22, 41-47` — `JSON.stringify(arg) ?? 'undefined'`: any function, `undefined`, or `Symbol` arg → `'undefined'`; objects without serializable props → `'{}'`; `NaN` → `null`; `Map`/`Set` → `'{}'`. Semantically different arguments collide onto the same cache key.
- C#: `ComputeMethodInput.Equals` compares proxy identity and actual argument values (`ComputeMethodInput.cs:25-27, 48-56`).
- Failure: `getWidget(selectorFn1)` and `getWidget(selectorFn2)` (or two different `Map` args) return each other's cached results — silent wrong answers, not just cache misses.
- **Resolved — won't-fix (per D2):** keep `JSON.stringify` keying as-is; it is the fastest option, and the compute method author owns the contract — use properly-keyable argument types or supply a custom `argToString`, and document the method accordingly. Add a doc comment on `defaultArgToString`/`argToString` stating the collision rules. Only code tweak: the `?? 'undefined'` fallback becomes `?? ''` (equally collision-safe for a delimiter-separated key component, shorter).
- **Rejected alternatives:** throwing/warning on non-representable args (runtime cost + breaks legitimate "I know what I'm doing" cases); a stable type-tagged stringifier (slower, still can't key functions meaningfully).

### K14. Same-key reentrant computation deadlocks silently (C#: fails fast)

Confidence: confirmed (mechanism; not runtime-tested).

- TS: `AsyncLock` is non-reentrant with no reentry detection (see C5); a compute function that (indirectly) awaits itself with the same key awaits its own lock forever (`compute-function.ts:72-78`).
- C#: `AsyncLockSet` is created with `LockReentryMode.CheckedFail` (`ComputedRegistry.cs:77-81`) — reentry throws immediately.
- Failure: accidental self-recursion hangs the app with no diagnostics instead of a clear error.
- **Recommended:** give `ComputeContext` a parent link (each nested `invoke` already knows its caller context); before awaiting the per-key lock, walk the parent chain and throw a clear "reentrant compute call" error when the same key is found — the C# `CheckedFail` analog for every ctx-threaded call.
- **Alternative:** dev-mode diagnostics only — log a warning when a lock acquisition waits longer than a few seconds. Doesn't prevent the deadlock, but catches cases the parent-chain walk can't see (calls made without a threaded context, see K3).

### K15. Renewer chain permanently retains the generation-1 computed and the original argument objects (low)

Confidence: confirmed. Every later generation reuses gen-1's `_renewer`, whose closure strongly captures the gen-1 computed and the first call's `argsWithoutCtx` (`compute-function.ts:85-89`). Small fixed leak per key; renewals also re-run the impl against the *first* call's argument object identities. C# reconstructs from `ComputeMethodInput` each time.

- **Recommended:** drop the `prevComputed?._renewer` reuse; build the renewer from `(instance, argsWithoutCtx)` only — one small closure per generation, no computed captured.
- **Alternative:** keep the shared renewer but capture the args in a per-key record instead of the gen-1 computed. Same effect, more state to manage.

### K16. `registry.register` overwrites without invalidating a displaced still-consistent computed (low)

Status: **closed** — fixed 2026-07-15 (batch kernel1).

Confidence: confirmed. `computed-registry.ts:25-29` vs C# `ComputedRegistry.cs:126-132` (invalidates the displaced target). Unreachable through the single-flight path today, but the invariant is unenforced — and the K2 fix will make this path live.

- **Recommended:** in `register`, invalidate a displaced computed that is not already invalidated (C# parity). Prerequisite for K2's recommended fix.
- **Alternative:** none meaningful — throwing on displacement would break K2's registration-while-computing flow.

### K17. Invalidation ordering differs (low)

Status: **closed** — fixed 2026-07-15 (batch kernel2, folded into the K4 rework as planned).

Confidence: confirmed. TS notifies dependants before the computed's own `onInvalidated` handlers (`computed.ts:184-191`); C# fires own handlers first, dependants in `finally` (`Computed.cs:303-318`). Observable to handlers that inspect dependants' state.

- **Recommended:** fold into the K4 rework (its recommended shape already fires own handlers first, dependants after, both exception-isolated).
- **Alternative:** document the TS ordering as a deviation. Rejected-leaning: K4 touches the same lines anyway, so parity is free.

### Out of scope (kernel)

- ~~Per-input `ComputedOptions`~~ — **in scope in minimal form per D1** (`errorAutoInvalidateDelay` first). Still out of scope: `MinCacheDuration`/keep-alive timeouts, `AutoInvalidationDelay`, `InvalidationDelay`, `ConsolidationDelay` — each becomes a future field on the D1 structure.
- Error transiency classification (`TransiencyResolver`, transient/super-transient/terminal).
- `CallOptions` context machinery (`GetExisting`, `Capture`, `Invalidate` via context; `Invalidation.Begin()` blocks) — TS substitutes bound `.invalidate()` on methods.
- `InvalidationSource` diagnostics.
- `ComputedGraphPruner` (partially covered by K7).
- Cancellation reprocessing (`ComputedCancellationReprocessingOptions`).
- `ComputedSynchronizer`, `ComputedSource`, `ComputedExt.When/Changes` streams, Session, DI/interceptor integration.
- Registry instrumentation (`OnRegister/OnUnregister/OnAccess`, metrics, `InvalidateEverything`).

Note: the TS registry's WeakRef + FinalizationRegistry approach (`computed-registry.ts`) is a sound equivalent of C#'s weak-ref + pruning for *key* storage; the gaps are in graph edges (K1, K7) and registration timing (K2), not key eviction.

Registry residuals surfaced during kernel1 verification (low, unassigned): the FinalizationRegistry callback deletes `_entries` by key without a WeakRef identity check — a GC'd predecessor's late callback can drop a live successor's entry (C# uses identity `TryRemove`); `ComputedRegistry.unregister` similarly lacks C#'s entry-identity check (currently unreachable while the per-key lock serializes same-key computation); `Computed.capture` of a just-invalidated computed now throws "No Computed was captured" — to be addressed by K11's capture rework.

---

## B. State layer + React

### S1. `State._update` publishes a new computed without invalidating the one it replaces — dependants of the initial-value computed go stale forever

Confidence: confirmed.

- TS: `state.ts:91-98` — `_update()` just assigns `this._computed = computed`. For `ComputedState` constructed with `initialValue`/`initialOutput`, `_initialize` (`computed-state.ts:47-56`) creates a *consistent* initial computed; the first `_updateCycle` iteration replaces it via `_update` (`computed-state.ts:133`) while the initial computed remains Consistent.
- C#: the initial computed is created **already invalidated** (`State.cs:246`), and `SetComputed` always invalidates `prevSnapshot.Computed` when publishing a successor (`State.cs:258`).
- Failure: a compute function calls `state.use()` before the first real computation finishes → captures a dependency on the consistent initial computed → gets `initialValue`. When the real value arrives, the initial computed is never invalidated, so the dependant is never invalidated — the parent shows the placeholder value forever.
- **Recommended:** both halves of C# parity: create the initial computed **pre-invalidated** (which also fixes S13 and removes the `_hasOutput` guards), and make `_update` invalidate the replaced computed when it is still consistent (defense in depth — `SetComputed` parity covers any other path that publishes a successor).
- **Alternative:** only the `_update`-invalidates half. Smaller, closes the staleness, but keeps the consistent-initial-computed semantics (an `initialValue` reads as authoritative until the first computation) and keeps S13's guards.

### S2. `ComputedState.value` silently masks errors by falling back to `lastNonErrorValue`

Confidence: confirmed.

- TS: `computed-state.ts:72-79` — when `_computed.hasError`, `value` returns `_lastNonErrorValue` instead of throwing; `valueOrUndefined` (`computed-state.ts:86-89`) likewise. Base `State.value` and `MutableState` *do* throw on error (`state.ts:33-35`) — the port is internally inconsistent too.
- C#: `State.Value` throws the stored error (`State.cs:99-102`); the stale-value fallback is a separate, explicit API — `LastNonErrorValue` (`State.cs:104-107`, `StateSnapshot.cs:15`).
- Failure: a computer starts failing (server down). C# callers of `state.Value` see the exception; TS callers keep receiving the last good value with no indication anything is wrong. `hasValue` is `false` while `value` returns a value — contradictory.
- **Recommended:** `value`/`output`/`valueOrUndefined` behave like the base class (rethrow / reflect the error); stale-value access stays on `lastNonErrorValue` only — C# `State.Value` vs `LastNonErrorValue` split.
- **Alternative:** keep the masking behavior under an honest name (`valueOrLastNonError`) and make `value` throw — preserves the convenience for UI code that wants it, without lying through the primary getter.

### S3. `MutableState.set` invalidates *before* staging the new output — synchronous readers during the cascade observe (and can permanently publish) the old value

Confidence: confirmed.

- TS: `mutable-state.ts:22-25` — `set()` first calls `this._computed.invalidate()` (synchronous cascade to dependants and handlers), then `_update(..., output)`. During the cascade, `this._computed` is still the old computed. If any handler synchronously calls `state.use()`/`update()`, the renewer (`mutable-state.ts:7-11`) publishes a new consistent computed **carrying the OLD output**; `set()`'s own `_update` then replaces that computed without invalidating it (S1), so anything that captured it is stale forever.
- C#: `MutableState.Set` stages `NextOutput = result` **before** invalidating, inside the lock (`MutableState.cs:112-131`); `OnInvalidated` synchronously renews from `NextOutput` (`MutableState.cs:152-162`) — any read during/after invalidation observes the new value.
- Failure: `computed.onInvalidated.add(() => render(state.use()))` — a legitimate C# pattern — renders the old value in TS and detaches the renderer from future updates.
- **Recommended:** introduce a `_nextOutput` field staged before `invalidate()`; `_renewer` builds the new computed from `_nextOutput`, so `set` = stage + invalidate with the renewal path shared — exact C# ordering (`MutableState.Set` / `OnInvalidated`).
- **Alternative:** publish-then-invalidate — call `_update` with the new computed first, then invalidate the old one. Simpler; readers during the cascade see the new value. Diverges from C#'s internal shape (no staged `NextOutput`), which matters if S16's synchronous renewal is adopted — the renewer needs a staged output to renew *from*.

### S4. Unbounded listener/handler accumulation in update-delayer waits

Confidence: confirmed. (The `whenInvalidated` half of this leak is K10.)

- TS: `ui-update-delayer.ts:20-28` — when the delay timer fires normally, `t.changed.remove(onChanged)` is never called; the handler is removed only the next time a UI action becomes active. In an app with frequent invalidations and rare UI actions, `uiActions.changed` grows without bound. Same for its abort listener (`ui-update-delayer.ts:37-45`) and the >10 s branch of `FixedDelayer` (`update-delayer.ts:27-44`).
- C#: `UpdateDelayer` uses a linked CTS cancelled/disposed in `finally` (`UpdateDelayer.cs:46-54`).
- Failure: a dashboard updating once per second leaks ~86k closures/day; long sessions degrade in memory and `changed.trigger()` cost.
- **Recommended:** add a shared "await with signal" helper to `@actuallab/core` (resolve/reject + guaranteed removal of the abort listener and any event handler on *every* exit path) and rewrite the delayer waits — and K10's `whenInvalidated` — on top of it.
- **Alternative:** per-site manual cleanup (remove `changed` handlers and abort listeners on the success path of each wait). Same end state, three copies of the same fiddly pattern.

### S5. Disposal mid-computation: one post-dispose update is published, then the update cycle hangs; `update()`/`use()` callers hang forever

Confidence: confirmed.

- TS: `computed-state.ts:111-149` — the dispose check happens only at the loop top. If `dispose()` fires while `await this._computer()` is in flight, the cycle still calls `_update` (an observable post-dispose update), then `computed.whenInvalidated(disposeSignal)` with an already-aborted signal — which never settles (K10) → the cycle stays pending indefinitely. `FixedDelayer` with `ms <= 10_000` ignores `abortSignal` entirely (`update-delayer.ts:45-48`), and `_renewer` (`computed-state.ts:39-44`) awaits `whenUpdated()`, which never resolves after disposal — so `state.update()`/`state.use()` on a disposed-mid-cycle state hangs the caller forever.
- C#: `DisposeToken` cancels `WhenInvalidated` and the delay promptly; update-after-dispose is explicitly managed via `GracefulDisposeToken` (`ComputedState.cs:167-193`), and the cycle always terminates.
- Failure: React unmount during an in-flight fetch: the state publishes one more update, and any code awaiting `state.update()` never resolves — silent deadlock of that dependency chain.
- **Recommended:** terminate promptly and fail waiters fast: check `disposeSignal.aborted` after the compute returns (skip `_update`, exit the loop); make `whenInvalidated` reject immediately on an already-aborted signal (K10); reject outstanding and future `whenUpdated()` waits on dispose (S11); make `FixedDelayer`'s ≤10 s branch honor the abort signal.
- **Alternative:** port C#'s `GracefulDisposeToken` semantics (allow one final graceful update within a configurable delay before hard-cancelling). Fuller parity, meaningfully more machinery; the prompt-termination shape is the correct floor and doesn't preclude it.

### S6. React hooks miss updates between render and effect subscription — classic external-store race

Confidence: confirmed.

- TS: both hooks are hand-rolled (no `useSyncExternalStore`). `use-computed-state.ts:42-74` and `use-mutable-state.ts:19-39` read the value during render, then subscribe inside `useEffect` via `state.whenUpdated()` — a fresh `PromiseSource` (`state.ts:74-76`) resolving only on the *next* `_update`. An update landing between render and effect execution is invisible.
- C#: waits are per-generation — capture `state.Snapshot`, await `snapshot.WhenUpdated()` (`StateSnapshot.cs:62`), already completed if the update happened in the meantime. No lost-wakeup window.
- Failure: `useMutableState` mounts; another component calls `state.set(v1)` in the same tick after render but before effects flush → the UI shows the initial value while `state.value === v1`, until some future `set` — potentially forever.
- **Recommended:** rewrite both hooks on `useSyncExternalStore` — React's purpose-built answer to exactly this race (and to tearing under concurrent rendering); `getSnapshot` reads `updateIndex`, `subscribe` builds on S11's versioned wait. Naturally absorbs S7's lifecycle fix.
- **Alternative:** keep the hand-rolled effect but compare `state.updateIndex` against the index captured at render and force a re-render before entering the await loop. Closes the missed-update window only; StrictMode/concurrent issues (S7) still need their own fix.

### S7. `useComputedState` is permanently broken under React StrictMode (and unsafe under concurrent rendering)

Confidence: confirmed.

- TS: `use-computed-state.ts:33-37` creates/disposes `ComputedState` **during render** (a side effect); effect cleanup disposes the state but leaves it in `stateRef`. StrictMode dev (mount → cleanup → re-mount): the same **disposed** state is re-subscribed — `subscribe()` bails on `state.isDisposed` (`use-computed-state.ts:52,57`) and the component never re-renders; the value freezes. Under concurrent React, a discarded render leaks a running `ComputedState`.
- C#: n/a mechanically; the Blazor counterpart ties state lifetime to component lifecycle callbacks, never render.
- Failure: any app running `<StrictMode>` in development shows `useComputedState` values stuck at the initial value; production behaves differently — hard to diagnose.
- **Recommended:** fold into S6's `useSyncExternalStore` rewrite: create/dispose the `ComputedState` exclusively inside `useEffect` keyed on deps (render never mutates), matching how the Blazor counterpart ties state lifetime to lifecycle callbacks.
- **Alternative (if S6's rewrite is deferred):** minimal patch — never dispose during render, and at effect time recreate the state when `stateRef.current.isDisposed`. Fixes StrictMode; concurrent-render leaks remain possible.

### S8. No retry backoff: `UpdateDelayer` has no `retryCount`, no `RetryDelays`, no transient-error tracking

Confidence: confirmed.

- TS: `UpdateDelayer` is `(abortSignal?) => Promise<void>` (`update-delayer.ts:2`) — the delay cannot depend on failure history. No `StateSnapshot`, so no `RetryCount`/`ErrorCount`. Error recovery relies solely on the global 1 s auto-invalidation (K5) — a constant ~1 s retry loop forever.
- C#: `UpdateDelayer.Delay(retryCount, ct)` uses `RetryDelays[retryCount]` growing 1 s → 1 min (`UpdateDelayer.cs:30-35, 67-68`); `RetryCount` is computed per snapshot from transient-error classification (`StateSnapshot.cs:40-53`).
- Failure: backend down → every `ComputedState` in the app hammers it once per second indefinitely; C# backs off to one attempt per minute.
- **Recommended:** extend the delayer signature to `(retryCount, abortSignal)`; `_updateCycle` tracks a consecutive-error counter (reset on success); the default delayers use `RetryDelaySeq` (1 s → 1 min, C# `RetryDelays` parity). Pairs with K5 (states stop relying on the blanket 1 s error auto-invalidation).
- **Alternative:** keep the delayer signature and implement backoff inside `ComputedState` only (multiply the delay by a factor per consecutive error). Smaller, but custom delayers can't participate and the backoff isn't configurable per state.

### S9. `UIActionTracker` has no post-action instant-update window; the 50 ms buffer is ineffective and adds latency

Confidence: confirmed.

- TS: `ui-action-tracker.ts:12-14` — `isActive` is true only while `_activeCount > 0`. In `run`/`call` (`ui-action-tracker.ts:27-31, 45-49`) the `finally` decrements first, then waits 50 ms, then triggers `changed`: (a) during the 50 ms "buffer", `isActive` is already false, so a `UIUpdateDelayer` consulted when the post-command invalidation arrives waits the full delay — the buffer buys nothing; (b) `await uiActions.call(fn)` returns 50 ms late.
- C#: `AreInstantUpdatesEnabled()` is true while actions run **or** within `InstantUpdatePeriod` (300 ms) after the last result (`UIActionTracker.cs:16, 82-91`); `UpdateDelayer` races `WhenInstantUpdatesEnabled()` against the delay (`UpdateDelayer.cs:42-55`).
- Failure: user clicks "Save"; the server invalidates the dependent query right after the command completes. C#: instant refresh. TS: full configured update delay (plus the 50 ms handler latency).
- **Recommended:** track `lastResultAt`; add `areInstantUpdatesEnabled()` = active **or** within a ~300 ms `instantUpdatePeriod` after the last completion (C# parity); in `run`/`call`, decrement + timestamp + trigger `changed` immediately — no artificial 50 ms sleep; `UIUpdateDelayer` consults the new predicate.
- **Alternative:** keep the `isActive`-only model but move the 50 ms wait *before* the decrement, restoring the buffer's intent. Inferior: still adds latency to every command caller and 50 ms is far tighter than the C# window.

### S10. `UIUpdateDelayer` skips the delay entirely (no `MinDelay` floor) while a UI action is active — hot-loop risk

Confidence: confirmed.

- TS: `ui-update-delayer.ts:15` — `if (t.isActive) return Promise.resolve();` and the `onChanged` cancel path also resolves with zero residual delay.
- C#: `UpdateDelayer.Delay` enforces `minDelay` (default 32 ms) even when instant updates kick in, with an explicit comment that otherwise updates can consume 100% CPU (`UpdateDelayer.cs:32-35, 58-64`).
- Failure: a long-running UI action while a dependency invalidates on every recompute → microtask-tight recompute loop for the action's duration; frozen tab.
- **Recommended:** enforce a small `minDelay` (~32 ms, C# default) in `UIUpdateDelayer`'s short-circuit paths, measured from delay start — `UpdateDelayer.Delay` parity.
- **Alternative:** enforce the floor inside `ComputedState._updateCycle` instead, guarding against *any* zero-delay delayer (including user-supplied ones). Both is belt-and-braces; delayer-side is where C# puts it.

### S11. `whenUpdated()` is state-level and lossy; never settles on dispose

Confidence: confirmed.

- TS: `state.ts:74-76, 96-97` — one shared `PromiseSource` resolved-and-nulled per update. A consumer looping `await state.whenUpdated()` misses updates landing between resolution and re-subscription. After `dispose()`, pending/future `whenUpdated()`/`whenFirstTimeUpdated()` promises never settle.
- C#: waits are per-snapshot (`StateSnapshot.cs:61-62`) — no missed generation.
- Failure: `await state.whenUpdated(); assert(...)` intermittently misses generations; teardown code awaiting `whenFirstTimeUpdated()` after dispose deadlocks (see also S5).
- **Recommended:** a monotonically-versioned wait — `whenUpdated(sinceIndex = this.updateIndex)` resolving immediately when `updateIndex > sinceIndex`; reject outstanding and future waits on dispose. Fixes the lost-wakeup class for React hooks (S6) and every other consumer.
- **Alternative:** port `StateSnapshot` (per-generation immutable snapshot with `WhenUpdated`/`WhenInvalidated`) — the full C# shape, which S12's events and counters would also need. Bigger; the index-based wait is the pragmatic subset and doesn't preclude it.

### S12. State events (`Invalidated`/`Updating`/`Updated`) and `StateSnapshot` counters are absent

Confidence: confirmed (absent in TS).

- TS: no equivalent of the three events or `StateEventKind`; no `UpdateCount`/`ErrorCount`/`RetryCount`/`IsInitial` (only `updateIndex`); `lastNonErrorValue` is stored as a raw value — ambiguous when `T` includes `undefined` (`state.ts:14, 21-23, 94`).
- C#: `State.cs:124-126` — events with exactly-once-per-generation semantics via `StateSnapshot.TryClaimInvalidatedRaise`; counters in `StateSnapshot.cs:16-19`.
- Failure: C# patterns like `state.Invalidated += ...` (used pervasively in Fusion UIs for eager recompute) have no counterpart; `whenInvalidated()` covers one generation with none of the exactly-once machinery.
- **Recommended:** keep the event trio out of scope for now (document it), but replace `lastNonErrorValue`'s raw-value storage with a reference to the last non-error *computed* — removes the `undefined` ambiguity cheaply.
- **Alternative:** port the events with exactly-once semantics — requires the `StateSnapshot` concept (S11's alternative) to carry the claim flags; do it only together with that port.

### S13. Base `State` members crash with `TypeError` on a `ComputedState` before its first computation

Confidence: confirmed.

- TS: `computed-state.ts:57` — `_computed` stays unset until the first `_update()`. `ComputedState` guards its own getters with `_hasOutput`, but inherited members dereference `this._computed` directly: `use` (`state.ts:53-55`), `useInconsistent`, `update`, `recompute`, `whenInvalidated` → `TypeError`.
- C#: a state always has a computed from construction — an invalidated initial one (`State.cs:240-246`) — so `Use`/`Update` before the first computation just await it.
- Failure: `parentComputer = () => childState.use()` on a freshly created async `ComputedState` → hard crash instead of awaiting the first value.
- **Recommended:** always create the initial computed in the constructor, pre-invalidated — same fix as S1's recommended path; `use`/`update` before the first computation then await it like C#.
- **Alternative:** null-guard every inherited member (`use`, `update`, `recompute`, `whenInvalidated`, …). Spreads special-casing through the base class; rejected-leaning.

### S14. `Promise.race` losers in `_updateCycle` produce unhandled promise rejections

Confidence: confirmed.

- TS: `computed-state.ts:145-148` races `updateDelayer(disposeSignal)` against `_cancelDelaySource`. If the cancel source wins and the state is later disposed, the still-pending delayer promise rejects on abort with nobody attached → `unhandledRejection` (crashes Node by default).
- C#: `UpdateDelayer` awaits with `SuppressCancellationAwait`/`SilentAwait` (`UpdateDelayer.cs:38, 48-50`).
- Failure: SSR/Node test process exits with `ERR_UNHANDLED_REJECTION` when a delayer-driven state is disposed shortly after a user action cancelled its delay.
- **Recommended:** make delayers **resolve** (not reject) on abort — the update loop re-checks `disposeSignal` right after, so rejection carries no information; this removes the whole unhandled-rejection class at the source (C#'s `SuppressCancellationAwait` spirit).
- **Alternative:** attach a no-op `.catch()` to the delayer promise before racing. Local and safe, but every future race site must remember the same trick.

### S15. `MutableState.set` lacks the equality short-circuit

Confidence: confirmed. TS always invalidates + republishes (`mutable-state.ts:22-25`); C# returns early on `NextOutput == result` (`MutableState.cs:114-116`). `set(sameValue)` per keystroke triggers full cascades and re-renders where C# is a no-op.

- **Recommended:** add `Result.equals` (C9) and early-return in `set` when the staged output equals the current one (value via `Object.is`, error by reference) — C# parity.
- **Alternative:** additionally accept a custom value comparer as a `MutableState` option for structural-equality use cases. Only if a real consumer needs it.

### S16. External invalidation of a `MutableState` is not eagerly renewed

Confidence: confirmed. TS leaves `_computed` invalidated until the next `update()`/`use()` (renewal is lazy, `mutable-state.ts:7-11`); C#'s `OnInvalidated` renews synchronously — a `MutableState` is *never* observed invalidated (`MutableState.cs:152-162`). Mostly benign, but code checking `state.computed.isConsistent` behaves differently, and TS tests codify the lazy behavior (`mutable-state.test.ts:65-81`).

- **Recommended:** renew synchronously on invalidation (cheap — the renewer is sync), restoring the "a MutableState is always consistent" invariant; update the test that codifies laziness. Builds naturally on S3's staged `_nextOutput`.
- **Alternative:** keep lazy renewal and document the divergence explicitly. Acceptable if the S3 fix lands without staging.

### S17. `useMutableState` render throws if the state holds an error result

Confidence: confirmed. `use-mutable-state.ts:41` returns `state.value`, whose getter throws on error output — and the setter accepts `Result<T>`, so `setter(errorResult(e))` is a supported call. Next render of every component using the state throws, unmounting the tree absent an error boundary.

- **Recommended:** return `{ value, error, set }` (the shape `useComputedState` already uses) so error results render instead of throwing.
- **Alternative:** narrow the setter to plain values (no `Result`) and document that errors can't be stored via this hook. Smaller, but loses parity with `MutableState.set`'s contract.

### S18. `UIActionTracker.errors` grows unbounded with no dedup

Confidence: confirmed. Every failure is pushed to a plain array; only manual `dismissError` removes entries (`ui-action-tracker.ts:10, 26, 43`). C# dedups same-type/same-message failures within `MaxDuplicateRecency` (1 s) (`UIActionFailureTracker.cs:64-98`). Combined with S8's fixed 1 s retry, an error-toast UI floods and memory grows for the session's lifetime.

- **Recommended:** port the recency-based dedup (same error name + message within ~1 s is dropped) plus a size cap as a backstop.
- **Alternative:** cap-only (bounded ring of recent errors). Stops the memory growth but a retry loop still floods the toast UI within the cap.

### Out of scope (state + UI)

- `StateFactory` / DI integration (`StateOptions`, `EventConfigurator`, `Category`) — TS constructs states directly.
- `UICommander` / `UIAction` / commander pipeline — TS reduces this to `uiActions.run/call(fn)`.
- `ComputedState` options: `TryComputeSynchronously`, `FlowExecutionContext`, `GracefulDisposeDelay` — TS has hard dispose only.
- `ComputedSynchronizer` / `IsSynchronized` / `WhenSynchronized` / `WhenNonInitial`.
- Updater-function `Set` overloads (`MutableState.cs:243-286`).

---

## C. RPC layer

### R1. V5/V5C binary format: header block parsed at the wrong position

Status: **closed** — fixed 2026-07-15 (batch rpcwire; byte-level parity re-verified against the .NET writer, incl. multi-header and multi-message frames).

Confidence: confirmed (byte-level; re-verified).

- TS: `rpc-serialization.ts:293-321` (`deserializeBinaryMessage`) reads the 4-byte argLen, then skips headers, then reads argData. Same in the compact variant at `:473-495`.
- C#: `Serialization/RpcByteMessageSerializerV5.cs:38-66` — the 4-byte prefix is *immediately followed by argument data*; headers are read **after** it. Writers (`WriteWithArgumentData` + `WriteHeaders`, lines 120-147) put headers last. `RpcByteMessageSerializerV5Compact.cs` is identical.
- Failure: any header-bearing binary message from .NET — e.g. a call with an active `Activity` (W3C header injected via `RpcOutboundCall.SendRegistered` → `RpcActivityInjector.Inject`) or middleware-set `ResultHeaders` on `$sys.Ok` — makes TS decode the first bytes of argument data as header lengths; `decodeMulti` throws and the **entire WebSocket frame is dropped** (`rpc-connection.ts:171-174`) — calls hang, stream items vanish. TS never sends headers itself, so it never round-trips its own bug; no TS test has `headerCount > 0`.
- **Test-confirmed:** `ts-port-audit-repro.test.ts` (rpc) — a synthetic frame built per the .NET writer layout (one header after argument data) decodes to `args: []` instead of the actual arguments.
- **Recommended:** fix `deserializeBinaryMessage` (and the compact variant) to read argument data immediately after the 4-byte length prefix and parse headers *after* it, per `RpcByteMessageSerializerV5`; skip them correctly and account for them in `bytesRead`. The TS writer needs no change — it never emits headers (`headerCount` is always 0). The repro test becomes the regression test.
- **Alternative:** additionally surface parsed headers on `RpcMessage` (needed eventually for tracing/activity propagation) — more scope, can follow later. Rejecting header-bearing frames loudly is *not* an option: .NET sends them legitimately (Activity injection), and dropping them must not break the call.

### R2. `$sys.Error` sent without `TypeRef` permanently hangs .NET callers

Status: **closed** — fixed 2026-07-15 (batch rpcwire, per D3).

Confidence: confirmed.

- TS: `rpc-system-call-sender.ts:152-165` — `error()` sends `[{ Message }]`, no `TypeRef`.
- C#: `Infrastructure/RpcSystemCalls.cs:95-104` — `Error` does `error.ToException()!`; `ExceptionInfo.ToException()` returns **null** when `TypeRef` is empty (`ExceptionInfo.cs:32, 57-68`), so `SetError(null!)` throws inside the handler and the `$sys.Error` is effectively swallowed.
- Failure: a .NET peer calls into a TS callee; the TS handler throws → the .NET caller's task hangs (queries: forever; commands: until the 10 s `RunTimeout`). TS's own `sendEnd` already knows this contract — it sends `TypeRef: 'System.Exception'` (`rpc-stream-sender.ts:214-216`).
- **Resolved (per D3):** send the `TypeRef` of `ActualLab.Serialization.RemoteException` (assembly-qualified, version-free — `'ActualLab.Serialization.RemoteException, ActualLab.Core'`) with `Message = "{error.name}: {error.message}"` from `RpcSystemCallSender.error`; align `sendEnd` (`rpc-stream-sender.ts:214-216`, currently `'System.Exception'`) to the same convention. .NET callers get a typed `RemoteException` with JS provenance in the message.
- **Deferred alternatives (see D3):** `JavaScript.{error.name}` pseudo-TypeRefs riding `ToException()`'s `RemoteException` fallback (preserves the JS type machine-readably on `ExceptionInfo.TypeRef`); a dedicated `JavaScriptException` .NET type. Revisit if .NET→JS calls become common.

### R3. Normally-completed remote streams never send `AckEnd` and never unregister → unbounded leaks on both peers

Status: **closed** — fixed 2026-07-15 (batch rpcstreams). Residual note: `disconnect()`/non-reconnectable completion still defers unregistration to the consumer's next `next()` and skips AckEnd, unlike C#'s immediate `CloseFromLock` — minor, tracked in RPC notes.

Confidence: confirmed.

- TS: `rpc-stream.ts:307-313` — `onEnd` only flips `_completed`; the iterator's done-path (`:373-376`) returns without `dispose()`, and JS `for await` does not invoke `return()` on natural exhaustion. The stream stays in `peer.remoteObjects` (a **strong** `Map`, `rpc-remote-object-tracker.ts:3-27`) and no `$sys.AckEnd` is ever sent. Same for the gap-error completion path (`:258-267`).
- C#: `RpcStream.cs:296-313` — `OnEnd` → `CloseFromLock` → sends `$sys.AckEnd` and unregisters (`:334-358`); the tracker uses weak refs (`Infrastructure/RpcObjectTrackers.cs:41-48`).
- Failure: every completed server→client stream (a) leaks an `RpcStream` in the TS client for the peer's lifetime and (b) keeps its id in the keep-alive list (`rpc-peer.ts:595-597`), making the .NET server's `RpcSharedStream` immortal — parked awaiting an ack after `End` (`RpcSharedStream.cs:130-152, 239-253`) and refreshed by `KeepAlive` every 10 s, so the 125 s `ObjectReleaseTimeout` reaper never fires. Memory grows on **both** ends.
- **Recommended:** mirror C#'s `CloseFromLock`: on `onEnd` (and the gap-error completion path) send `$sys.AckEnd` and unregister/dispose immediately; also dispose from the iterator's natural-exhaustion done-path.
- **Alternative (complementary):** make `remoteObjects` a weak tracker (WeakRef + FinalizationRegistry, C# parity) as a safety net for *abandoned* streams. Doesn't replace the eager close — the .NET-side `RpcSharedStream` still needs the `AckEnd`.

### R4. `$sys.End` index is ignored — silent loss of tail items

Status: **closed** — fixed 2026-07-15 (batch rpcstreams).

Confidence: confirmed.

- TS: `rpc-stream.ts:307-313` — `onEnd(index, error)` completes the stream regardless of `index` vs `_nextExpectedIndex`.
- C#: `RpcStream.cs:296-313` — `OnEnd` applies the same ordering rules as items: gap → `SendResetFromLock(_nextIndex)` (re-request the missing range); duplicate → duplicate-ack.
- Failure: after a reconnect, replayed traffic can deliver `End(N)` while items `k..N-1` were dropped with the old connection; TS completes the stream *successfully* with the tail missing — silent data loss where C# recovers via reset-ack.
- **Recommended:** apply the same index gate to `onEnd` as to items: gap → send reset-ack and keep waiting; duplicate → duplicate-ack/ignore — C# `OnEnd` parity.
- **Alternative:** treat a gapped `End` as an error completion. Fails loudly instead of silently, but loses C#'s recovery (the sender can replay the missing range); rejected-leaning.

### R5. System messages can be flushed onto the wire *before* the handshake

Confidence: confirmed (code path; failure against .NET verified by its handshake contract).

- TS: `rpc-connection.ts:265-286` — `_sendRaw` buffers writes while the WS is `CONNECTING` and flushes them in `onopen`, i.e. **before** the peer's run loop sends `$sys.Handshake` (`rpc-peer.ts:859-877`). The abort handler sends `$sys.Cancel` whenever `_connection !== undefined` (`rpc-peer.ts:377-386`), which is true throughout connect/handshake.
- C#: `RpcPeer.Transport` returns null until the handshake completes, with a comment explaining that early writes corrupt the remote handshake (`RpcPeer.cs:29-52`); the server treats the **first** inbound message as the handshake and fails otherwise (`RpcPeer.cs:306-324`).
- Failure: user aborts a deferred call while the socket is still connecting → the buffered `$sys.Cancel` is the first message the .NET server reads → handshake fails → connection drops → reconnect; repeatable under cancellation churn.
- **Recommended:** gate at the peer level, like C#'s null-`Transport`-until-handshake: hold non-handshake sends in the peer until the handshake completes (queue calls, drop pre-handshake system calls like `Cancel` — the call they'd cancel hasn't been sent either).
- **Alternative:** keep the connection-level CONNECTING buffer but guarantee the handshake is written first (front-insert / two-phase flush). Fragile ordering contract spread across two layers; rejected-leaning.

### R6. `mempack6` / `mempack6c` registered as MessagePack formats

Status: **closed** — fixed 2026-07-15 (batch rpcwire).

Confidence: confirmed.

- TS: `rpc-serialization-format.ts:239-242, 249-252` — `MemoryPackV6`/`MemoryPackV6C` are constructed as msgpack formats and resolvable via `f=` URL keys.
- C#: `Configuration/RpcSerializationFormat.cs:51-57` — `mempack6*` uses **MemoryPack** argument serialization, a completely different byte format.
- Failure: a peer configured with `f=mempack6` connects; the .NET server speaks MemoryPack, TS speaks msgpack — every payload is garbage, surfacing as confusing deserialization errors rather than a clear "unsupported format".
- **Recommended:** unregister the mempack keys and make the resolver throw a clear "MemoryPack formats are not supported by the TS client" error at connection setup.
- **Alternative:** implement MemoryPack. Not justified — msgpack covers the same wire role and the .NET side offers both.

### R7. Binary polymorphism markers (`msgpack6`) not implemented

Status: **closed (partial)** — 2026-07-15 (batch rpcwire). Polymorphic payloads now fail loudly ("polymorphic payloads are not supported") via an arity guard on `$sys.Ok`/`I`/`B` value counts, and `readPolymorphismMarker` ships as a verified building block. Actual decoding of polymorphic payloads stays unsupported until TS method metadata gains a polymorphism flag — there is no wire signal to key on (`needsPolymorphism` is .NET-side method metadata).

Confidence: confirmed in code; triggers only for polymorphic types.

- TS: args are encoded/decoded as bare concatenated msgpack values (`rpc-serialization.ts:209-257, 309-321`), no type-marker handling.
- C#: `msgpack6` prefixes every *polymorphic* argument/result with a derived-type marker (`Serialization/RpcByteArgumentSerializerV4.cs:54-98`, `ByteTypeSerializer.cs:65-112`); `$sys.Ok`/`I`/`B` are sent with `needsPolymorphism: true` when the return/item type is abstract/interface/object.
- Failure: any TS-facing method or stream whose declared result/item type is polymorphic in .NET terms → TS decodes marker bytes as msgpack → frame decode error or corrupt values. Works today only because ActualChat's TS-facing APIs avoid polymorphic types — nothing enforces that.
- **Recommended:** implement marker *parsing*: handle the `\0\0` non-polymorphic fast path (trivial), and on an actual derived-type marker fail with a clear "polymorphic payloads are not supported" error instead of corrupting the frame. Full type resolution can follow if ever needed.
- **Alternative:** document/enforce "no polymorphic types on TS-facing methods" only. Zero code, but the failure mode stays a confusing decode error the first time someone breaks the rule.

### R8. Peer-change detection keyed on `RemoteHubId` instead of `RemotePeerId`

Confidence: confirmed.

- TS: `rpc-peer.ts:905-920` compares successive `RemoteHubId`s.
- C#: `Infrastructure/RpcHandshake.cs:24-32` — `GetPeerChangeKind` compares `RemotePeerId`.
- Failure: the .NET server removes an idle `RpcServerPeer` while the client is offline; on reconnect a *new* server peer (new `Id`, same `HubId`) is created. C# clients treat this as `Changed` and reset shared/remote objects; TS treats it as same-peer — `peerChanged` doesn't fire and client-owned `RpcStreamSender`s wait forever for acks the new peer will never send.
- **Recommended:** compare `RemotePeerId` (already parsed into `RemoteHandshake`, `rpc-peer.ts:481-493`) — C# `GetPeerChangeKind` parity.
- **Alternative:** compare both ids (peerId decides, hubId mismatch logged as an extra signal). Marginal value over the recommended one-liner.

### R9. Inbound call tracker has no dedup (`GetOrRegister`/`TryReprocess`) — duplicate execution on a TS callee

Confidence: confirmed.

- TS: `rpc-peer.ts:509-521` — `inboundCalls.register(call)` unconditionally overwrites (`rpc-call-tracker.ts:192-194`) and dispatch always runs.
- C#: `Infrastructure/RpcCallTrackers.cs:48-56` + `RpcInboundCall.cs:114-158` — a resent call id reuses the existing call and just re-sends its result.
- Failure: reconnect resends are by design — and TS even double-sends locally: a call issued while disconnected is sent by its deferred `Connected` listener (`rpc-peer.ts:361-367`, triggered synchronously at `:926`) **and** again by the blind resend in `_reconnect` (`:1043-1058`). Against a .NET server this is absorbed; against a TS server the method body executes twice (double side effects for commands).
- **Recommended:** port `GetOrRegister` semantics to the inbound tracker: a resent call id attaches to the existing dispatch instead of re-invoking, and re-sends the result if already computed — protects against every resend source, local or remote.
- **Alternative:** only fix the local double-send (the deferred-`Connected` listener and `_reconnect`'s blind resend overlap). Narrower; a .NET client resending to a TS server still double-executes.

### R10. TS never sends `$sys.Disconnect`

Confidence: confirmed.

- TS: no `disconnect` sender exists in `rpc-system-call-sender.ts`; TS only *handles* it (`rpc-system-call-handler.ts:179-206`). The keep-alive handler also ignores the id list entirely (`rpc-system-call-handler.ts:109-115`), so per-object keep-alive/`ObjectReleaseTimeout` semantics are absent.
- C#: sends it in three places — unknown shared object acked (`RpcSystemCalls.cs:149-159`), unknown ids in keep-alive (`RpcObjectTrackers.cs:300-317`), and `RpcSharedStream.SendDisconnect` for host-mismatch/late/rejected acks (`RpcSharedStream.cs:78-111, 284-289`).
- Failure: a consumer acks a stream whose sender the TS callee no longer tracks → gets nothing back → the remote stream hangs instead of failing fast with `RpcStreamNotFoundException`.
- **Recommended:** add `disconnect()` to the system-call sender and reply with it from the `$sys.Ack` handler when the shared object is unknown — streams then fail fast (`RpcStreamNotFoundException` analog) instead of hanging. Process keep-alive id lists (unknown ids → disconnect) in the same change.
- **Alternative:** the Ack-handler path only, deferring keep-alive list processing. Covers the user-visible hang; per-object release timeouts remain absent.

### R11. `RpcStreamSender.onAck` skips C#'s validation

Confidence: confirmed.

- TS: `rpc-stream-sender.ts:159-167` — any ack starts the pump; `mustReset` is just "hostId non-empty"; no host equality check; no rejection of resets on `allowReconnect === false`.
- C#: `RpcSharedStream.OnAck` (`RpcSharedStream.cs:78-111`): host mismatch → `SendDisconnect`; a not-yet-started stream only accepts `mustReset && nextIndex == 0`; reset on a non-reconnectable stream → `SendDisconnect`.
- Failure: a consumer that reconnected through a different host keeps a TS sender streaming into the void; a stray/duplicate ack starts a sender C# would refuse to start.
- **Recommended:** port the three `OnAck` guards — host mismatch → disconnect; a not-yet-started stream accepts only `mustReset && nextIndex === 0`; reset on a non-reconnectable sender → disconnect. Depends on R10's `disconnect()`.
- **Alternative:** the host-equality check alone (the load-balancer case). Cheapest meaningful subset; the other two guards close smaller correctness holes.

### R12. No outbound call timeouts of any kind

Confidence: confirmed (documented omission, but callers of the implemented API assume liveness).

- TS: no equivalent of the Maintain loop; `rpc-call-tracker.ts:25-28` documents it away. Deferred (`AwaitForConnection`) calls also wait indefinitely (`rpc-peer.ts:355-373`).
- C#: `RpcCallTrackers.cs:108-207` — RunTimeout/DelayTimeout enforcement; commands default to connect 1.5 s / run 10 s (`RpcCallTimeouts.Default.cs:16`); plus `ConnectTimeout` (`RpcOutboundCall.cs:103-115`).
- Failure: connected-but-stuck server (handler deadlock, lost `Ok`) → TS promise pending forever; UI awaits hang. The keep-alive watchdog only covers dead links, not stuck calls.
- **Recommended:** a per-peer maintain interval scanning `outboundCalls` against `RpcCallTimeouts` with C#'s defaults — commands bounded (≈1.5 s connect / 10 s run), queries unbounded — overridable per call/method.
- **Alternative:** opt-in per-call timeout option only, no defaults. Smaller, but leaves C#'s "commands never hang forever" guarantee unmet by default.

### R13. No reconnect backoff and no premature-disconnect penalty

Confidence: confirmed (partly intentional).

- TS: fixed 100 ms delay (`rpc-client-peer-reconnect-delayer.ts:8-12`); `_tryIndex` reset on every successful handshake (`rpc-peer.ts:927`).
- C#: exponential backoff 1→60 s (`RpcClientPeerReconnectDelayer.cs:21-25`) plus `PrematureDisconnectTimeout` — a graceful close within 15 s still bumps the attempt index (`RpcPeer.cs:404-405`, `RpcLimits.cs:18`).
- Failure: a server crash-looping *after* handshake gets re-connected by every TS client ~5-10×/s indefinitely (thundering herd).
- **Recommended:** keep the deliberately-simple fixed base delay, but stop resetting `_tryIndex` when the connection lived less than a premature-disconnect threshold (~15 s, C# `PrematureDisconnectTimeout`) — crash-looping servers then see growing delays via the existing `RetryDelaySeq` plumbing.
- **Alternative:** full exponential-backoff port (1 → 60 s defaults). More C#-faithful, but changes reconnect UX for the common brief-blip case; the premature-disconnect guard alone fixes the herd problem.

### R14. `parseStreamRef` pattern-sniffs every string in every result

Confidence: confirmed code; failure plausible.

- TS: `rpc-stream.ts:58-72` accepts *any* 4-6 part comma string whose parts 2-4 `parseInt` (`hostId` unconstrained); `resolveStreamRefs` (`:465-499`) rewrites such strings into registered `RpcStream`s inside arbitrary result graphs (`rpc-hub.ts:325-332`).
- C#: stream refs are only materialized where the static type says so (`RpcStreamJsonConverter`).
- Failure: JSON transport; a legit user-data string like `"a,1,30,61"` in a DTO silently becomes a broken `RpcStream` (and registers a phantom remote object that keep-alive then advertises).
- **Recommended:** resolve stream refs only at positions the method def declares as streams (C#'s type-driven materialization); keep the pattern-sniff nowhere.
- **Alternative:** keep the sniff but require `hostId` to parse as a GUID — one line, kills virtually all false positives; acceptable stopgap if the type-driven plumbing is deferred.

### R15. `$sys.End` error detection keyed on `Message` truthiness, not `TypeRef`

Status: **closed** — fixed 2026-07-15 (batch rpcwire).

Confidence: confirmed. TS: `rpc-system-call-handler.ts:146-163` — `error = msg ? new Error(msg) : null`. C#: `error.IsNone` = empty `TypeRef` (`RpcSystemCalls.cs:180-187`, `ExceptionInfo.cs:32`). A stream terminated by an exception with an empty `Message` looks like clean completion.

- **Recommended:** key error detection on `TypeRef` presence (fall back to `Message`), and carry the `TypeRef` into the constructed `Error`'s name for fidelity.
- **Alternative:** none meaningful beyond the fidelity refinement — the discriminator must match the .NET contract.

### R16. `$sys.Reconnect` handler skips handshake-index validation

Confidence: confirmed (acknowledged in a comment, `rpc-system-call-handler.ts:65-69`). TS `_handleReconnect` ignores `args[0]`; C# throws `TooLateToReconnect` when `ownHandshake.Index != handshakeIndex` (`RpcSystemCalls.cs:57-70`). A TS callee can reconcile against the wrong connection generation after a rapid double-reconnect; bounded damage (worst case duplicate execution — R9).

- **Recommended:** track the peer's own handshake index and reject a stale `$sys.Reconnect` with the `TooLateToReconnect` equivalent — the caller then falls back to blind resend, which R9's dedup absorbs.
- **Alternative:** accept-but-log the mismatch. Keeps behavior, adds observability; rejected-leaning once R9 lands, since validation is a few lines.

### R17. `$sys.Cancel` doesn't abort the running handler and the result is still sent

Confidence: confirmed (documented omission). TS removes the tracker entry only (`rpc-system-call-handler.ts:101-107`); the dispatch continues and still sends the result (`rpc-peer.ts:554-570` — the send is not gated on the call still being registered). C# cancels the linked CTS and suppresses the result (`RpcInboundCall.cs:209-210, 246-247`).

- **Recommended:** thread an `AbortSignal` per inbound call through `RpcServiceHost.dispatch` (aborted by `$sys.Cancel`), and skip the response when the call is no longer registered. Also the transport half of F5.
- **Alternative:** response-suppression only (no handler abort). Cheap, stops the wasted send; server-side work still runs to completion.

### R18. Regular service dispatch loses the implementation receiver

Status: **closed** — fixed 2026-07-15 (batch rpcdeco).

Confidence: confirmed and executable-probe verified. (Second-pass finding.)

- TS stores only the extracted function (`rpc-service-host.ts:54-61`), then calls `entry.fn(...args)`
  (`rpc-service-host.ts:65-82`). The receiver is the internal `entry` record, not the registered implementation.
- C# invokes the method against `methodDef.Service.Server` (`RpcMethodDef.NestedTypes.cs:45-68`).
- A probe registered `{ prefix: 'v=', getValue(n) { return this.prefix + n; } }`. Dispatch returned `NaN` because
  `this.prefix` was read from the host's internal entry. A class instance has the same failure.
- Fusion compute methods escape this specific bug because `FusionHub._wrapServerMethod` passes `impl` into
  `ComputeFunction.invoke`; regular, no-wait, and stream methods do not.
- Failure: any idiomatic stateful service method reads the wrong receiver. Private fields fail more loudly with
  `TypeError: Cannot read private member ...`.
- **Recommended:** store the implementation receiver beside the function in the method entry and dispatch via
  `entry.fn.call(entry.impl, ...args)` (`Reflect.apply` equivalent). Keeping the unbound function composes with
  wrappers that re-target the receiver (`FusionHub._wrapServerMethod` already invokes with an explicit `impl`).
  Add end-to-end tests for regular, no-wait, and stream methods using both ordinary and `#private` instance state.
- **Alternative:** bind once at registration (`impl[name].bind(impl)`). Same observable behavior, marginally
  faster dispatch; loses the unbound function for any future wrapper/middleware that needs to control the receiver.

### R19. Decorator metadata is shared and mutated across base and derived classes

Status: **closed** — fixed 2026-07-15 (batch rpcdeco; shared `ownMetadata` helper in `@actuallab/core`).

Confidence: confirmed and executable-probe verified. (Second-pass finding.)

- Stage-3 decorator metadata for a derived class inherits from the base metadata object. Both `rpcService` and
  `rpcMethod` read through that prototype chain and use `??=` (`rpc-decorators.ts:63-71, 75-95`). If a metadata
  record is inherited, `??=` reuses it instead of creating an own copy.
- The same pattern exists in Fusion's `computeMethod` (`compute-method.ts:30-38`), so the defect crosses packages.
- A probe declared `Base.base()` and `Derived.derived()`. After decorating `Derived`, `getMethodsMeta(Base)` returned
  both `base` and `derived`. Decorating the derived service as `"Derived"` also changed the base service name from
  `"Base"` to `"Derived"`.
- C# builds service and method definitions from a concrete `Type`; derived declarations cannot mutate the metadata
  already associated with a base service type.
- Failure: merely loading a derived contract can change an already registered or later-built base contract's wire
  name and method set. Sibling derived contracts contaminate one another through their base.
- **Recommended:** write only to *own* metadata records: when the record is inherited
  (`!Object.hasOwn(context.metadata, KEY)`), create an own copy first — clone the inherited methods map (so base
  methods stay visible on the derived contract) and create a fresh service record before setting its name.
  Implement as one small shared helper in `@actuallab/core` (own-record-or-clone semantics) used by `rpcService`,
  `rpcMethod`, and `computeMethod` — one fix for both packages. Test base/derived/sibling contracts in both
  decorator packages.
- **Alternative:** duplicate the helper locally in each package (risks the two copies diverging — the exact bug
  shape being fixed), or forbid contract inheritance outright (throw on inherited metadata) — simpler, but removes
  a pattern the C# side supports.

### R20. Decorator wire arity is wrong after a default parameter and for rest parameters

Status: **closed** — fixed 2026-07-15 (batch rpcdeco; explicit `argCount` option, ambiguity throws at declaration time; `computeMethod` became dual-mode — bare or `@computeMethod({ argCount })`).

Confidence: confirmed and executable-probe verified. (Second-pass finding.)

- `rpcMethod` records `target.length` (`rpc-decorators.ts:90-95`); `computeMethod` does the same
  (`compute-method.ts:34-38`). JavaScript `Function.length` stops before the first parameter with a default and does
  not count a rest parameter.
- A three-parameter probe `method(a, b = 1, c?)` produced `argCount === 1`. The hub then derives a wire name using
  that count (`rpc-hub.ts:244-253`), while callers passing all three arguments select/send a different arity.
- C# uses the full reflected parameter list (`RpcMethodDef.cs:62-70`, `RpcServiceDef.cs:136-149`), including optional
  parameters.
- Failure: a valid decorated method may be published under the wrong `Service.Method:N` name; overload selection and
  interop with .NET fail even though direct local calls work.
- **Recommended:** add an explicit `argCount` option to `rpcMethod` and `computeMethod`; when absent, detect
  ambiguity by scanning the parameter list of `Function.prototype.toString()` for `=` / `...` and throw a clear
  declaration-time error demanding the explicit count (TypeScript cannot recover the erased count reliably at
  runtime). Document that rest parameters are unsuitable for fixed-arity RPC without an explicit wire contract.
- **Alternative:** fully parse `toString()` output to *infer* the true count — fragile against destructuring,
  comments, and minification; or document-only — leaves the wire-name mismatch silent until interop fails.

### R21. Remote-object replacement and unregister violate tracker identity invariants

Status: **closed** — fixed 2026-07-15 (batch rpcstreams).

Confidence: confirmed and executable-probe verified. (Second-pass finding.)

- TS `RpcRemoteObjectTracker.register` blindly overwrites by `localId`, while `unregister` blindly deletes by
  `localId` (`rpc-remote-object-tracker.ts:3-20`). The shared tracker has the same unregister shape
  (`rpc-shared-object-tracker.ts:11-24`). Neither verifies that the map still points to the supplied object.
- A probe registered object A and then B with the same ID. A was not disconnected. Calling `unregister(A)` afterward
  removed B, even though B was the current entry.
- C# remote registration disconnects a displaced live object before replacement, and unregister succeeds only if the
  stored target is the same object (`RpcObjectTrackers.cs:73-141`). Shared registration rejects duplicate IDs and
  shared unregister also compares the stored object (`RpcObjectTrackers.cs:255-269`).
- This is reachable without ID wraparound: a result containing the same serialized stream reference twice makes
  `resolveStreamRefs` construct and register two `RpcStream` instances for one remote ID (`rpc-stream.ts:465-473`).
- Failure: the displaced stream remains user-visible but is no longer reconnected or kept alive. Its later disposal
  can unregister the replacement, so frames for the replacement become unrouteable too.
- **Recommended:** port the C# invariants into both trackers: `unregister(obj)` removes the entry only when it
  still points to `obj`; remote `register` no-ops for the same object and disconnects a displaced *live* object
  before replacing it; shared `register` rejects duplicate ids. Add the ABA regression test and a
  same-stream-reference-in-two-fields result test.
- **Alternative (complementary, not substitute):** dedupe stream refs in `resolveStreamRefs` (same ref within one
  result graph → one `RpcStream` instance) — closes today's concrete trigger but leaves the tracker invariant
  unenforced. Weak-ref tracking (R3's alternative) can layer on afterwards; it presumes exactly these identity
  checks.

### R22. Remote streams are registered and kept alive before enumeration starts

Status: **closed** — fixed 2026-07-15 (batch rpcstreams; registration moved to the lazy-start boundary, disposal before enumeration is a local no-op).

Confidence: confirmed by full source trace. (Second-pass finding.)

- TS registers a returned stream immediately in both the declared-stream path (`rpc-hub.ts:300-315`) and nested-ref
  path (`rpc-stream.ts:465-473`). Registration happens before the first `next()`; the initial ACK is correctly deferred
  until then (`rpc-stream.ts:340-360`).
- The keep-alive sender includes every registered remote ID (`rpc-peer.ts:588-597`), so an application that receives
  but never enumerates a stream continually renews its server-side sender.
- C# waits until `GetAsyncEnumerator` to register the remote stream and send reset/ACK (`RpcStream.cs:166-193`). A
  never-enumerated stream therefore creates no remote-object lease.
- Combined with TS's strong tracker map and R10's missing per-object reaper, simply ignoring a stream-valued result
  leaks both ends for the lifetime of the connection. This is broader than R3, which requires natural completion after
  enumeration.
- **Recommended:** move remote-stream registration (both the declared-stream path in `rpc-hub.ts` and the
  nested-ref path in `resolveStreamRefs`) to the first-iteration lazy-start boundary where the initial ACK is
  already sent — `RpcStream.GetAsyncEnumerator` parity. Disposal before enumeration stays a local no-op; reconnect
  and keep-alive then see only started streams, so a never-enumerated stream creates no lease on either peer.
- **Alternative:** keep eager registration but exclude never-started streams from keep-alive/reconnect and rely on
  a per-object release timeout (R10) to reap them. Keeps client-side tracker growth and diverges from C#'s lease
  model; rejected-leaning.

### RPC notes (low)

- `readVarUint` truncates values to 32 bits (`rpc-serialization.ts:151-167`: max 5 bytes, `value >>> 0`) vs C#'s 64-bit VarUInt; an id ≥ 2^35 would desync the frame. Practically unreachable with per-peer counters — note only.
- `msgpack-map-patch.ts` is needed and byte-correct (JS `Map` → msgpack map with typed keys, matching .NET `Dictionary<int, byte[]>` for `$sys.Reconnect`), but it patches `Encoder.prototype` **globally** on module load — worth documenting for host apps.
- Debugger-attached .NET peers use `KeepAlivePeriod = 300 s` (`RpcLimits.cs:46-54`); TS's 25 s watchdog (`rpc-limits.ts:43`) force-closes every 25 s against such a server — dev-environment churn. **Resolved (per D4):** add a `RpcLimits.Debug` preset (`keepAliveTimeoutMs = 300_000`), opted into explicitly via the existing override paths.
- Second-pass candidates reviewed and excluded: stream-reference numeric range validation (C# parsing also accepts negative timing values — a hardening opportunity, not a TS/C# contract gap); hash-collision handling in `RpcMethodRegistry` (poor failure mode, but a 32-bit method-hash collision is too remote to prioritize without a concrete case).
- Post-R3 residual (found during verification): `RpcStream.disconnect()` / non-reconnectable gap completion completes the stream without AckEnd and defers unregistration to the consumer's next `next()`; C#'s `Disconnect` → `CloseFromLock` unregisters (and attempts AckEnd) immediately. Minor divergence, revisit if it shows up in practice.

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
- **Recommended:** use `get` + `instanceof` check before any removal (C# parity); on pre-result invalidation, reject `call.result` with a dedicated cancellation-shaped error and have the compute client path treat it as retryable — the `TrySetCanceled` + `TryReprocessServerSideCancellation` analog. Requires F3/K6 so the rejection isn't cached.
- **Alternative:** minimal — reject `call.result` without the retry semantics. Prevents the hang and the poisoned lock; the caller sees an error instead of a transparent retry.

### F3. Rejected remote calls (including cancellation/disconnect errors) are cached as error computeds

Confidence: confirmed. The remote-call variant of K6.

- TS: any rejection of `outboundCall.result` propagates out of `rpcImpl` (`fusion-hub.ts:224`) and is cached via `errorResult(e)` → `setOutput` — including abort-driven `'Call cancelled.'` (`rpc-peer.ts:377-391`) and `'Peer closed.'` from `rejectAll` (`rpc-call-tracker.ts:146-156`). On the error path the `whenInvalidated → computed.invalidate()` wiring is never established (only wired after a successful await, `fusion-hub.ts:226-230`), so error computeds rely solely on the 1 s timer (K5).
- C#: `ComputeRpc` rethrows OCE instead of producing a computed (`RemoteComputeMethodFunction.cs:227-231`); server-side cancellations are retried with backoff; genuine error computeds still track server invalidation (`RpcOutboundComputeCall.SetError`, `:119-146`).
- Failure: one caller cancels (or a peer closes mid-flight); for the next second every other consumer of the same compute key observes a cancellation error as if it were the value.
- **Recommended:** ride on K6's kernel fix (cancellation-shaped errors are never served from cache) and make peer-lifecycle rejections (`'Call cancelled.'`, `'Peer closed.'`) cancellation-shaped; additionally, wire `whenInvalidated → computed.invalidate()` on the *error* path too, so genuine error computeds still track server invalidation (C# `SetError` parity). Genuine remote errors then cache for the client method's `options.errorAutoInvalidateDelay` (D1) instead of the blanket 1 s — remote compute methods get their own per-declaration default, the C# client-options analog.
- **Alternative:** invalidate every error computed immediately in the RPC glue (skip classification). Simpler; loses the distinction between a genuine remote error (worth briefly caching) and a cancellation, effectively reintroducing K5's retry churn for remote errors.

### F4. Local invalidation of a client computed never releases the outbound call — no computed→call binding, no `$sys.Cancel`, strong-ref leak

Confidence: confirmed.

- TS: only the call→computed direction exists (`fusion-hub.ts:226-230`). Nothing observes the client computed's invalidation. A locally-invalidated computed's stage-3 `RpcOutboundComputeCall` stays registered until server invalidation or reconnect; the server is never told to stop tracking. The tracker's strong ref chain (call → `whenInvalidated.then` closure → `Computed`) also defeats `ComputedRegistry`'s WeakRef/GC design for the old computed.
- C#: `RemoteComputed.OnInvalidated` → `BindToCallFromOnInvalidated` → `CompleteAndUnregister(notifyCancelled: true)` which sends `$sys.Cancel`, letting the server stop waiting (`RemoteComputedExt.cs:26-44`, `RpcOutboundComputeCall.cs:179-186`, `RpcInboundComputeCall.cs:95-99`). Plus a `Dispose`/finalizer escape hatch.
- Failure: an app that locally invalidates (or rapidly re-keys) remote computeds accumulates tracker entries and pinned computeds per peer; the server keeps invalidation-notification state alive for clients that no longer care.
- **Recommended:** after binding, observe the local computed's invalidation (via `whenInvalidated()`, not the K12-broken `add`); if the server hasn't invalidated yet, unregister the call and send `$sys.Cancel` — `BindToCallFromOnInvalidated` parity, releasing both peers' state.
- **Alternative:** a periodic sweep of completed calls whose bound computed is invalidated/collected. GC-ish safety net, laggy and weaker; could complement but not replace.

### F5. Cancellation propagation is absent end-to-end in the compute-call glue

Confidence: confirmed (absent in TS).

- TS: `rpcImpl` builds `RpcCallOptions` without a `signal` (`fusion-hub.ts:214-223`) even though `RpcPeer.call` supports one; the compute client surface offers no cancellation input at all. Inbound `$sys.Cancel` also doesn't abort a running TS server handler (R17).
- C#: the caller's `CancellationToken` is threaded through `SendRpcCall` (`RemoteComputeMethodFunction.cs:405-451`); the server links `CallCancelToken` into its invalidation wait (`RpcInboundComputeCall.cs:86-88`).
- Failure: a client can't abandon a slow remote compute call; the call, its lock queue, and the server's work all run to completion regardless.
- **Recommended:** accept an `AbortSignal` in the compute client path (options arg or via `AsyncContext`) and pass it to `peer.call`; on abort, unregister the call and don't cache the computed (K6/F3 semantics). Server-side abort of running handlers is R17.
- **Alternative:** defer until cancellation is threaded kernel-wide (K6 + K3's ctx threading) and document the absence. Acceptable sequencing — F3 must land first anyway so aborts aren't cached.

### F6. Every reconnect (including transient same-peer) invalidates all stage-3 compute calls — refetch storm

Confidence: confirmed (deliberate, test-codified deviation — but with a wasteful protocol interaction).

- TS: `_reconnect` unconditionally self-invalidates any compute call with a completed result (`rpc-peer.ts:1043-1050`), after first *including those same calls* in the `$sys.Reconnect` reconciliation (`rpc-peer.ts:1035-1036`; `getReconnectStage` reports `ResultReady`, `rpc-call-tracker.ts:93-98`). Tests codify this (`fusion-rpc-reconnection.test.ts:121-156`).
- C#: same-peer reconnect reports the stage and the server resumes stage-2 tracking without re-execution (`RpcOutboundComputeCall.GetReconnectStage`, `RpcInboundComputeCall.TryReprocess`); only a *peer change* invalidates completed calls.
- Failure: a 2-second WiFi blip invalidates every remote computed on the client → full refetch burst (C# clients refetch nothing). Against a .NET server, the client also first claims "these calls are alive" via `$sys.Reconnect` and then drops them, leaving the server holding re-armed invalidation tracking for discarded computeds.
- **Recommended:** keep invalidate-on-reconnect as a documented simplification, but stop reporting the soon-to-be-dropped compute calls in `_reconcileReconnect` (report `null`, like C#'s invalidate-on-peer-change branch) — removes the wasteful server-side re-arm.
- **Alternative:** full stage-based same-peer keep-alive (C# parity — computeds survive brief blips, no refetch burst). The right long-term shape; substantially more machinery, fine as a follow-up item.

### F7. TS server ignores the message's `CallType` — regular calls to compute methods still get invalidation tracking

Confidence: confirmed.

- TS: `_wrapServerMethod` decides by the server-side `methodDef.callTypeId` only (`fusion-hub.ts:171`); `createRpcClient` produces exactly such regular calls (plain `RpcOutboundCall`, `removeOnOk = true` — `rpc-client.ts:72-90`).
- C#: `RpcInboundComputeCall.IsRegularCall` (`RpcInboundComputeCall.cs:19-25`) returns the result immediately and skips invalidation tracking when the caller didn't request a compute call.
- Failure: regular callers cause the server to send invalidation notifications to clients that dropped the call on `$sys.Ok` — wasted messages and bookkeeping per call.
- **Recommended:** pass the inbound message's `CallType` through the dispatch context; when it isn't `FUSION_CALL_TYPE_ID`, return the result directly and skip the invalidation wiring — `IsRegularCall` parity.
- **Alternative:** document that regular calls to compute methods carry tracking overhead. Zero code, leaves the waste; rejected-leaning given the fix is small.

### F8. No deduplication of remote computeds across proxies — each `addClient` mints a fresh key space

Confidence: confirmed.

- TS: `_createClientMethod` creates a new `ComputeFunction` (unique id — `compute-function.ts:36`) and a new `syntheticInstance` per `addClient` call (`fusion-hub.ts:233-234`); the key embeds both. Two proxies for the same service+peer maintain independent computeds, RPC calls, and invalidation streams for the same logical value.
- C#: client proxies are DI singletons; `ComputeMethodInput` keys by service/method/args so all consumers share one `RemoteComputed`.
- Failure: an app calling `hub.addClient` per component doubles/triples RPC traffic and can show inconsistent values across components between invalidation deliveries.
- **Recommended:** cache the proxy per `(peer, serviceDef)` inside `FusionHub.addClient` — all consumers share one computed/call/invalidation stream per logical value, matching C#'s singleton client proxies.
- **Alternative:** document `addClient` as a create-once API and warn at runtime on a duplicate registration. Leaves the footgun armed; rejected-leaning.

### F9. Server-side invalidation send bypasses the peer's serialization format and `systemCallSender` (low)

Confidence: confirmed code path; impact plausible-low. `_wrapServerMethod` hand-rolls `serializeMessage(...)` — JSON-only (`rpc-serialization.ts:31-41`) — and writes to the captured `context.connection` (`fusion-hub.ts:186-192`), while all other responses go through `hub.systemCallSender` with `peer.serializationFormat`. On a msgpack connection the invalidation goes out as a JSON text frame; TS clients tolerate mixed frames, a .NET client would not. Also uses a possibly-dead captured connection.

- **Recommended:** add an `invalidate()` to the (Fusion-extended) system-call sender, sending via the peer's *current* connection and serialization format — `RpcComputeSystemCallSender.Invalidate` parity. Natural companion to F1's fix, same code.
- **Alternative:** keep the inline send but resolve the connection at send time and use the peer's format. Same effect, less structure; fine if F1 is fixed the minimal way.

### F10. `FusionHub._buildServiceDef` drifted from the base implementation (low)

Confidence: confirmed. The override hardcodes `remoteExecutionMode: Default` and skips the base's `noWait → mode 0` and `meta.remoteExecutionMode` honoring (`fusion-hub.ts:146-160` vs `rpc-hub.ts:244-259`). A decorator-declared custom `remoteExecutionMode` is silently ignored when registered through a `FusionHub`.

- **Recommended:** delegate to `super._buildServiceDef` and only patch `callTypeId` for compute methods — removes the duplicated logic and the drift with it.
- **Alternative:** re-duplicate the `noWait`/`meta` handling in the override. Fixes today's drift, invites tomorrow's; rejected-leaning.

### F11. Server peers created by `acceptConnection` are never removed from `hub.peers` (low)

Confidence: confirmed. Each accepted connection creates a UUID-ref `RpcServerPeer` (`fusion-hub.ts:118-132`) that stays in `hub.peers` after the connection closes; cleanup only happens on hub `close()`. A long-running TS server leaks one peer (plus trackers) per client connection. C# server peers auto-dispose after a close timeout.

- **Recommended:** remove the server peer from `hub.peers` (and stop it) when its connection closes and isn't re-accepted within a close timeout — C#'s server-peer auto-dispose shape, which still allows same-peer reconnects within the window.
- **Alternative:** immediate removal on close. Simplest, but breaks `$sys.Reconnect` reconciliation against TS servers (the peer's trackers vanish with it).

### Parity confirmed (Fusion-over-RPC)

- Invalidation arriving just *after* the result is handled correctly: `whenInvalidated.then(...)` replays on an already-resolved promise, and `invalidate()` during Computing defers via `_invalidatePending` — equivalent to C#'s invalidated-before-bound handling.
- Call keep-alive: `removeOnOk = false` keeps compute calls registered past the result (`rpc-outbound-compute-call.ts:6`), with unregistration on invalidate/reconnect/close — matches C#'s `CompleteKeepRegistered`/`CompleteAndUnregister` shape (modulo F2 and F4).
- Disconnect alone does not invalidate stage-3 calls or captured computeds; reconnect/peer-stop does — matches C#'s peer-change contract, well covered by tests.
- Dependency capture for local-compute-method → remote-compute-method works; server invalidation cascades to dependants.

### Out of scope (Fusion-over-RPC)

- `RemoteComputedCache` / cache-and-swap + `$sys.M` hash validation.
- Serve-stale-on-disconnect + `InvalidateWhenReconnected`.
- Per-method `ComputedOptions` beyond D1's minimal form — min cache duration, auto-invalidation, `CancellationReprocessing` retry policy (the D1 structure is where they'd land; client methods do get `errorAutoInvalidateDelay`, see F3).
- `WhenSynchronized` / `ComputedSynchronizer`.
- Rerouting and Distributed/local-execution mode.
- Server-side stage-based inbound reprocessing (`RpcInboundComputeCall.TryReprocess`) — the TS server reconnect handler is membership-only.

---

## E. Core utilities

### C1. `PromiseSource` rejection with no attached consumer fires `unhandledrejection` (Node: process crash by default)

Status: **closed** — fixed 2026-07-15 (batch core1).

Confidence: confirmed.

- TS: `promise-source.ts:27-32` — `reject()` rejects `this.promise` with no pre-attached rejection observer. Same for the timer-driven reject in `promise-source-with-timeout.ts:38-42`.
- C#: an unobserved faulted `Task` is benign in .NET; C# code freely does `TrySetException` on sources nobody may await.
- Failure: `rpc-peer.ts:1007/1015` and `rpc-call-tracker.ts:152` reject `call.result` for every tracked call on disconnect/error. Any tracked call whose caller isn't (yet or anymore) awaiting `result` raises `unhandledrejection`; in Node ≥15 that terminates the process by default. The port pattern assumes "reject is always safe".
- **Recommended:** attach a no-op observer in the constructor (`this.promise.catch(() => {})`) of both `PromiseSource` variants; consumer chains still see the rejection normally. Exactly reproduces the .NET "unobserved faulted task is benign" contract for every current and future use site.
- **Alternative:** attach the observer inside `reject()` just before rejecting. Equivalent semantics, marginally lazier; no reason to prefer it over the one-line constructor fix.

### C2. `Result` misclassifies `undefined` errors as success

Status: **closed** — fixed 2026-07-15 (batch core1).

Confidence: confirmed (re-verified).

- TS: `result.ts:17-22` — `hasValue = error === undefined`. So `errorResult(undefined)` yields `hasValue === true`; `resultFrom`/`resultFromAsync` (`result.ts:54-70`) pass a caught `undefined` straight through — `throw undefined` or `reject()` with no reason becomes a *successful* result with value `undefined`. Asymmetric, too: `error: null` IS treated as an error.
- C#: `Result.cs:329-335` — `Error is null` is the discriminator, and the type system guarantees a caught exception is never null; the state is unrepresentable.
- Failure: `compute-function.ts:106-107` wraps the user's compute body in `errorResult(e)`. A dependency that does `throw undefined` (legal JS) gets cached as a valid computed value `undefined` — consumers see a wrong value instead of an error.
- **Recommended:** store an explicit `_hasError` flag set by the constructing path (`ok`/`errorResult`/`resultFrom*` decide, not the error value) — makes "error result with a nullish error" representable-but-normalized and the discriminator unforgeable, matching C#'s type-level guarantee. Normalize nullish errors to `new Error('Unspecified error')` at the error-constructing entry points.
- **Alternative:** normalization only (`error ?? new Error(...)` in `errorResult`/`resultFrom*`). Two lines, closes the `throw undefined` hole; the two-arg constructor remains a footgun for direct callers.

### C3. `RetryDelayer.getDelay` ignores an already-aborted `cancellationSignal`

Status: **closed** — fixed 2026-07-15 (batch core2). The optional stop-signal threading into `rpc-peer.ts` was deferred (RpcPeer has no stop `AbortSignal` yet) — folded into the R10/R12 peer-lifecycle batch.

Confidence: confirmed.

- TS: `retry-delayer.ts:43-70` — no `signal.aborted` pre-check; `addEventListener('abort', ...)` on an already-aborted signal never fires, so the delay runs to completion and *resolves normally* despite cancellation. On a live abort it also rejects with a generic `Error` rather than `signal.reason`.
- C#: `RetryDelayer.cs:46-56` — linked CTS; an already-canceled token throws OCE immediately; only the `cancelDelaysToken` branch completes normally (that half TS gets right).
- Failure: currently latent (`rpc-peer.ts:772` passes no signal), but any future caller passing its stop signal will "successfully" wait out the delay after being stopped, then reconnect a stopped peer.
- **Recommended:** rebuild `getDelay`'s wait on C6's abortable `delayAsync(ms, signal)` — the pre-check and `signal.reason` rejection come for free — and thread the peer's stop signal into `rpc-peer.ts:772`.
- **Alternative:** patch in place: pre-check `signal.aborted` → reject with `signal.reason`; reject with `signal.reason` on live abort. Fine if C6 is deferred.

### C4. `EventHandlerSet.trigger` live-iterates the `Set` — handlers added during dispatch run in the *same* dispatch

Status: **closed** — fixed 2026-07-15 (batch core1).

Confidence: confirmed.

- TS: `events.ts:19-21` — `for (const handler of this._handlers) handler(arg);` — no snapshot. Consequences: (a) `whenNext()` (`events.ts:27-35`) called from inside a handler of the same set resolves *immediately* with the current event instead of the next; (b) a handler that re-adds handlers can extend the loop; (c) a handler removed by an earlier handler is silently skipped. Also: `Set` dedupes — adding the same function twice invokes once; a C# delegate invokes twice.
- C#: multicast delegates invoke an immutable snapshot — subscribers added during raise are not invoked, removed ones still are. (The TS doc comment at `events.ts:3` itself claims delegate parity.)
- Failure: `EventHandlerSet` is the backbone of RPC state plumbing (`rpc-peer.ts`, `rpc-connection.ts`, `rpc-peer-state-monitor.ts`) and Fusion (`computed.ts`). "In my `stateChanged` handler, await the next state via `whenNext()`" self-resolves with the event being dispatched — a subtle off-by-one in reconnect/state logic.
- **Recommended:** iterate a snapshot (`[...this._handlers]`) in `trigger` — one line; add-during-dispatch no longer fires in the same dispatch (fixes `whenNext()` self-resolution) and remove-during-dispatch matches C#. Document the remaining Set-dedup deviation (same handler added twice fires once).
- **Alternative:** full C# multicast semantics (array-backed, duplicates allowed). Changes `add`/`remove` contracts for no known consumer need.

### C5. `AsyncLock` has no reentry detection (`LockReentryMode`) and no abort-while-queued

Status: **closed** — abort-while-queued fixed 2026-07-15 (batch core2); reentry detection stays with K14 as planned.

Confidence: confirmed absent.

- TS: `async-lock.ts:10-18` — `acquire()` takes no `AbortSignal`; a queued waiter cannot be cancelled; no reentry tracking. (Fairness and release-on-throw are correct.)
- C#: `Locking/AsyncLock.cs:8-43` — `Lock(CancellationToken)` plus `LockReentryMode.CheckedFail`. Fusion C# uses `CheckedFail` in the exact places the TS port mirrors (`State.cs:59`, `ComputedRegistry.cs:78`).
- Failure: same-key compute recursion deadlocks silently (K14); nothing can time out or abort a queued waiter.
- **Recommended:** add an optional `AbortSignal` to `acquire()`/`run()` that removes the queued resolver on abort (rejecting with `signal.reason`). Reentry *detection* stays at the fusion layer (K14's parent-chain walk) — without an ambient execution context, the lock itself can't know the caller's chain.
- **Alternative:** an owner-token reentry API (callers pass an explicit token the lock compares). C#-shaped, but plumbing-heavy in JS and redundant once K14 lands.

### C6. No cancellable delay: `delayAsync` takes no `AbortSignal`, so `retry()` can't be aborted

Status: **closed** — fixed 2026-07-15 (batch core2).

Confidence: confirmed absent.

- TS: `delay.ts:2-6` — bare `setTimeout`; `retry.ts:22-43` accepts no signal.
- C#: every delay in a retry path is cancellable (`Task.Delay(delay, ct)` / `Clock.Delay`).
- Failure: teardown during a `retry()` inter-attempt delay: the timer keeps the Node event loop alive and the next attempt executes against disposed state. `RetryDelayer` had to reimplement abortable delay inline instead of reusing a shared primitive.
- **Recommended:** `delayAsync(ms, signal?)` that pre-checks `signal.aborted`, clears the timer on abort, and rejects with `signal.reason`; thread an optional signal through `retry()`; rebuild `RetryDelayer.getDelay` on it (C3).
- **Alternative:** leave `retry()` as-is and only add the primitive. Half measure — the known non-cancellable retry path stays.

### C7. `abortPromise` fast path for already-aborted signals contradicts its own caching/observation contract

Status: **closed** — fixed 2026-07-15 (batch core1).

Confidence: confirmed. `abort-promise.ts:26` returns a *fresh, unobserved* rejected promise per call for an already-aborted signal, while the pending path caches per signal and pre-attaches `.catch()` precisely to avoid unhandled rejections — and the doc promises "same promise per signal". Code that grabs the promise and only races it on a later iteration gets an `unhandledrejection` (Node crash).

- **Recommended:** route the already-aborted case through the same WeakMap cache (create once, pre-attach the no-op `.catch()`, store) — 3 lines, restores the documented contract.
- **Alternative:** pre-attach a no-op `.catch()` to the fresh per-call rejection. Fixes the crash but still allocates per call and violates the same-promise doc; rejected-leaning.

### C8. `RingBuffer` — head-side/tail-side API half missing; constructor semantics differ (low)

Confidence: confirmed absent. TS has `pushTail`/`pushTailAndMoveHeadIfFull`/`pullHead`/`moveHead`/`get` only; C# also has `TryPullHead`, `PullTail`/`TryPullTail`, `PushHead`, indexer setter, `GetSpans` (`RingBuffer.cs:98-146`), and `RingBuffer(minCapacity)` rounds capacity *up* while TS treats it as exact. No caller needs the missing half today (`rpc-stream-sender.ts:260` uses the implemented subset).

- **Recommended:** document "exact capacity" as a deliberate deviation in the header comment; add the missing operations only when a caller appears.
- **Alternative:** port the full C# API now. No consumer; speculative surface area.

### C9. `Result` — no equality, no untyped variant (low)

Status: **closed** — `equals` added 2026-07-15 (batch core1); untyped variant stays deferred.

Confidence: confirmed absent. No `equals` (C# `Result<T>.Equals` + operators, `Result.cs:380-388`), no untyped `Result`. C# paths that skip work when `oldResult == newResult` can't be ported faithfully — this is what blocks S15.

- **Recommended:** add `equals(other, valueComparer?)` (default `Object.is` for values, reference equality for errors) together with S15, its first consumer.
- **Alternative:** also port the untyped `Result` variant. Defer until something (likely RPC serialization) actually needs it.

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
