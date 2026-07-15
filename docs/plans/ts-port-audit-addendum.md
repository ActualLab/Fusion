# TypeScript Port Gap Audit — Second Pass

This document adds gaps found in a second review of the TypeScript RPC port. It is an addendum to
[`ts-port-audit.md`](ts-port-audit.md), not a replacement. The earlier report's findings are not repeated here.

The same standard applies: a feature may be partial, but an implemented feature must preserve the C# contract,
adjusted for JavaScript. Findings below were checked against the current TypeScript and C# sources. Four were also
reproduced with short executable probes; the probes were removed after verification and did not touch the existing
uncommitted audit tests.

**Validation (2026-07-14):** all five findings were independently re-verified against the TS and C# sources
(`rpc-service-host.ts` receiver-less dispatch; `??=` writes through inherited stage-3 decorator metadata in both
`rpc-decorators.ts` and `compute-method.ts`; `argCount: target.length`; blind set/delete in both trackers vs.
C# `RpcObjectTrackers.Register/Unregister` identity invariants; eager registration at `rpc-hub.ts:313-314` vs. C#
`RpcStream.GetAsyncEnumerator` lazy registration). All five are **confirmed**. Each item below now carries a
**Recommended / Alternative** course-of-action pair in the main audit's format.

## Summary

| ID | Severity | Finding | Verification |
|---|---:|---|---|
| R18 | High | Regular service dispatch loses the implementation receiver | Executable probe + source trace |
| R19 | High | Decorator metadata is shared and mutated across base and derived classes | Executable probe + source trace |
| R20 | Medium | Decorator wire arity is wrong after a default parameter and for rest parameters | Executable probe + source trace |
| R21 | High | Remote-object replacement and unregister violate tracker identity invariants | Executable probe + C# comparison |
| R22 | High | Remote streams are registered and kept alive before enumeration starts | Source trace |

## Findings

### R18. Regular service dispatch loses the implementation receiver

Confidence: confirmed and executable-probe verified.

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

Confidence: confirmed and executable-probe verified.

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
  `rpcMethod`, and `computeMethod` — one fix for both packages, per this addendum's Reuse note. Test
  base/derived/sibling contracts in both decorator packages.
- **Alternative:** duplicate the helper locally in each package (risks the two copies diverging — the exact bug
  shape being fixed), or forbid contract inheritance outright (throw on inherited metadata) — simpler, but removes
  a pattern the C# side supports.

### R20. Decorator wire arity is wrong after a default parameter and for rest parameters

Confidence: confirmed and executable-probe verified.

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

Confidence: confirmed and executable-probe verified.

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

Confidence: confirmed by full source trace.

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

## Reuse

### Existing abstractions to reuse

- Reuse `Reflect.apply`, already used in `@actuallab/core`'s serializer, to preserve the RPC implementation receiver
  for R18.
- Reuse the `WeakRef` + `FinalizationRegistry` approach in `fusion/src/computed-registry.ts` when making remote-object
  tracking weak, while preserving the stronger replacement/unregister invariants from C# `RpcRemoteObjectTracker`.
- Reuse `RpcSystemCallSender`, the existing peer timers, and `RpcLimits` for keep-alive/disconnect/release work rather
  than introducing a second wire sender or free-standing timer service.
- Reuse the existing `RpcStream` lazy-start boundary for R22; registration can move to the same first-iteration path
  that already emits the initial ACK.

### Reusability of new components

- An "own decorator metadata record" helper is needed by both `rpcMethod` and `computeMethod`. A local helper in each
  package is small but risks the same inheritance bug diverging twice. Prefer a shared helper in `ts/actuallab-core`
  (`@actuallab/core`) with explicit clone-vs-new semantics.
- Shared-object lease bookkeeping is RPC-specific. Keep it in `@actuallab/rpc`; placing it in core would expose wire
  protocol and peer-lifetime concepts that have no general consumer.
- No other new reusable component is implied. Receiver preservation and identity-checked map updates fit the
  existing classes directly.

## Verification notes and excluded candidates

- The executable probes used the current source through the repository's `tsx` dependency. Observed outputs included
  `dispatch NaN`, base metadata containing the derived method, the base service name changing to the derived name,
  `argCount === 1` for a three-parameter defaulted signature, and stale unregister removing the replacement object.
- Incoming keep-alive ID handling and shared-object lease expiry were independently confirmed, but are not repeated:
  the concurrent expansion of R10 in the original audit now covers them.
- Stream-reference numeric range validation was reviewed but not listed: C# parsing also accepts negative timing
  values, so it is a hardening opportunity rather than a TS/C# contract gap.
- Hash-collision handling in `RpcMethodRegistry` has a poor failure mode, but a 32-bit method-hash collision is too
  remote to prioritize without a concrete collision case. The findings above have ordinary application triggers.
