# Proxy Method Slots and Array-Based Interceptor Dispatch

Date: 2026-07-16

Status: implemented and measured (2026-07-16, `feat/proxy-method-slots`). Cached compute
calls got 8-25% faster (Long 36.5 -> 28.3 ns, String 46.5 -> 34.7 ns, SessionAndString
44.7 -> 41.0 ns, recompute -12%), matching the estimate below; raw proxy dispatch got
1.3-3.7x faster with unchanged per-call allocations. Legacy `MethodInfo`-keyed caches
remain as the compatibility/cold path; slot-indexed `MethodDef?[]` on the binding and
old-cache removal are still deferred.

## Conclusion

The proposal is viable and is a better architectural replacement for the
per-method proxy-field idea. Generated proxies can assign a small integer slot to
every intercepted non-generic method, publish one static method table, and bind an
interceptor to that table once. Warm calls can then resolve their handler with an
array access instead of `ConcurrentDictionary<MethodInfo, ...>`.

The important qualification is that the integer is **proxy-table-local**, not a
globally meaningful method identity. A slot must always be paired with its
`ProxyMethodTable`, either explicitly or through a proxy's interceptor binding.
This matters for different proxy types, closed generic proxy types, class
overrides, and interface multiple inheritance.

This task belongs in interception infrastructure and is intentionally excluded
from the general [Fusion Hot-Path Optimization Plan](fusion-hot-path-optimization.md).

## Evidence from the Current Implementation

- `ProxyTypeGenerator.AddProxyMethods` already increments `methodIndex` while
  emitting intercepted methods. The index currently only names generated fields.
- `ProxyTypeGenerator.GetDeclaredProxyMethods` skips generic methods, as well as
  static and sealed methods. Non-generic methods therefore have a fixed slot in a
  generated proxy layout.
- A generated proxy currently has one static `MethodInfo` field per method, one
  original-call delegate field per method, and one cached `Interceptor.Intercept`
  delegate per distinct return type.
- Every warm call constructs `Invocation` with `MethodInfo`, enters
  `SelectHandler`, and probes `_handlerCache`, a
  `ConcurrentDictionary<MethodInfo, Func<Invocation, object?>?>`.
- `MethodDef` creation is normally cold, but it uses another
  `ConcurrentDictionary<MethodInfo, MethodDef?>`. RPC service lookup has additional
  `MethodInfo` dictionaries and signature fallback.
- Generated proxy classes are sealed and directly inherit the original service
  type. A derived service's proxy does not inherit the base service's generated
  proxy, so slot inheritance must be generator metadata rather than physical CLR
  field layout.
- Generic service types are supported even though generic methods are not. Each
  constructed generic proxy type naturally gets its own closed static method
  table.

## Scope

Replace method-identity lookups on the generated-proxy outbound interception path:

- generated proxy to interceptor handler selection;
- proxy-local `MethodDef` resolution;
- built-in interceptor composition for compute, command, remote-compute, and RPC
  handlers.

The following lookups are not part of this optimization:

- the computed-value registry keyed by compute arguments;
- inbound RPC resolution from a wire method reference or name;
- reflection-oriented configuration APIs that begin with an arbitrary
  `MethodInfo`;
- other dictionaries unrelated to generated proxy method dispatch.

Those cold or externally keyed APIs may use the method table's reverse map, but
they cannot all be replaced by a bare array access.

## Reuse

### Existing abstractions to reuse

- Reuse the generator's existing effective-method discovery, callable-signature
  compatibility rules, `methodIndex`, and `ProxyHelper.GetMethodInfo` expressions.
- Reuse the existing `IProxy.Interceptor` assignment lifecycle. Binding already
  occurs before normal calls and supports interceptor reassignment.
- Reuse `MethodDef`, its handler factories, and the existing built-in interceptor
  fallback semantics rather than introducing a second method-description model.
- Reuse `Invocation` and preserve its original-call delegate and argument-list
  behavior. `Invocation.With` must preserve the same slot.
- Reuse the current validation and trimming preservation paths; the generated
  method table replaces the individual static `MethodInfo` fields but uses the
  same statically generated reflection references.

### Reusability and placement of new components

- `ProxyMethodTable`, a proxy-method reference type, and interceptor bindings are
  general interception concepts. They belong in `ActualLab.Interception`, not in
  Fusion or `ActualLab.Core`.
- Slot emission and table initialization belong in `ActualLab.Generators`.
- Fusion, CommandR, and RPC should only adapt their interceptor-specific method-def
  and handler factories to the shared binding API. They should not define their
  own slot tables.
- Benchmarks should remain in the BenchmarkDotNet project, with a small direct
  interceptor-dispatch benchmark in addition to the end-to-end cached compute and
  RPC outbound-call benchmarks.

## Proposed Model

### ProxyMethodTable

Each generated proxy type publishes one static immutable table containing:

- the generated proxy type and non-proxy service type;
- `MethodInfo[]`, indexed by local slot;
- a reverse `MethodInfo -> slot` map for cold compatibility APIs;
- alias mappings for overridden base methods and equivalent interface declarations;
- enough metadata to create or locate the corresponding method definition.

The reverse map does not participate in a normal generated proxy call. It exists
for reflection-driven callers and compatibility with existing public APIs.

### Proxy method reference

Use a table-qualified reference conceptually equivalent to:

```text
(ProxyMethodTable Table, int Index)
```

A bare `int` should not escape APIs that already know the table. Naming it
`MethodIndex` without table qualification invites accidental cross-proxy use.

### Interceptor binding

Create one binding for each `(Interceptor instance, ProxyMethodTable)` pair. The
binding owns slot-indexed arrays for resolved method definitions and handlers. It
is shared by all proxy instances using that interceptor and proxy type.

The generated proxy's existing interceptor storage can be replaced by a binding
reference:

- `IProxy.Interceptor.get` returns `binding.Interceptor`;
- `IProxy.Interceptor.set` obtains the binding for the generated type's static
  method table;
- reassigning the interceptor replaces the binding;
- no per-method instance handler fields are added.

This may also allow removal of the current per-return-type cached intercept
delegate fields. The object-layout goal is one binding reference plus the existing
per-method original-call delegates, potentially fewer proxy fields than today.

### Warm dispatch

A generated method uses its compile-time local slot:

```text
binding.Intercept<TResult>(slot, invocation)
```

The warm stable path becomes:

1. load the proxy's binding;
2. load the handler from the binding array at `slot`;
3. invoke the handler, or invoke the original implementation for a no-handler
   slot.

There is no `MethodInfo` hash, dictionary probe, virtual `SelectHandler`, or
proxy-type lookup on this path.

### Lazy initialization

Current handlers and some `MethodDef` properties are lazy. Eagerly creating every
handler at proxy binding time would trade call speed for startup work and allocate
handlers for methods that may never run.

Preserve lazy behavior with an explicitly published slot state:

- unresolved;
- resolved with a handler;
- resolved with no handler;
- dynamic compatibility fallback.

Use `Volatile.Read` and `Interlocked.CompareExchange`, or an equivalent one-time
publication mechanism, so concurrent first calls may create at most a harmless
duplicate candidate and all warm calls see immutable state. Benchmark the slot
representation: one object array with sentinels may be faster than separate state
and handler arrays, but this must be measured.

## MethodDef Integration

The slot-indexed binding should contain `MethodDef?[]`, making the array position
the primary fast association. Adding a raw `MethodIndex` to every `MethodDef` is
not sufficient:

- `RpcMethodDef` instances are created from service reflection before a generated
  proxy binding is involved;
- one logical or inherited method can appear in more than one proxy table;
- the same local index names unrelated methods in different tables;
- consolidation can create additional compute method definitions for one method.

If a method definition needs to navigate back to its proxy slot, store a
table-qualified proxy-method reference, or keep that association in the binding.
The recommended starting point is to keep it in the binding and avoid enlarging
every `MethodDef` until a concrete consumer requires the reverse link.

Existing APIs can be layered as follows:

- `GetMethodDef(proxyMethodReference)` uses the binding array and is the hot API;
- `GetMethodDef(MethodInfo, proxyType)` remains as a compatibility/cold API and
  resolves through the proxy method table's reverse map;
- `Invocation.Method` remains available as a compatibility property resolving
  `table.Methods[index]`, while built-in hot paths use the index or resolved
  `MethodDef` directly.

## Slot Assignment

### Classes

For single-inheritance class hierarchies, assign slots base-first:

1. preserve the base class's effective proxyable slot range;
2. map an override to its base slot;
3. append newly introduced proxyable methods in the derived type's range.

The `MethodInfo` stored for an overridden slot should represent the effective
method for that generated proxy, while the reverse map should include base-method
aliases where necessary.

### Interfaces

Interfaces do not have a single base-to-derived slot layout because they support
multiple inheritance and diamonds. Use a deterministic proxy-local linearization:

1. traverse base interfaces in a deterministic order;
2. collapse methods using the generator's existing compatible-callable-signature
   rule;
3. assign each effective method one local slot;
4. map all equivalent interface declarations to that slot in the reverse map.

An inherited interface is not guaranteed to occupy the same numeric range in
every composite interface. That is acceptable because slots are qualified by the
proxy table.

### Stability

Slots are an internal runtime ABI between generated code and its matching method
table. They must not be serialized, persisted, or exposed as RPC method indexes.
Use deterministic signature ordering within each declared range if reproducible
generated output is desired; long-term numeric stability across service-version
changes is not required.

## Dynamic and Composite Interceptors

`SelectHandler` is virtual and can theoretically depend on the complete
invocation. The slot architecture must not silently assume all external
interceptors are stable.

- Built-in interceptors should opt into slot binding and resolve their final
  compute, command, RPC, or original-call choice once per slot.
- Composite interceptors such as `ComputeServiceInterceptor` and
  `RemoteComputeServiceInterceptor` should compose bindings at slot-resolution
  time rather than repeat fallback selection on every call.
- Interceptors that do not support stable slot resolution use a dynamic slot state
  that calls the existing `SelectHandler` behavior. They gain little performance
  but remain correct.

This compatibility fallback can remain during migration and for third-party
interceptors. The old `MethodInfo` cache should be removable from built-in warm
paths even if the public compatibility API remains.

## Expected Impact

For the current cached compute benchmark, this removes one warm
`ConcurrentDictionary<MethodInfo, handler>` probe plus virtual/composite handler
selection. Source-only estimate: **10-25% faster for the whole cached compute
call**, medium confidence. From the current 29.6 ns baseline, that suggests roughly
22-27 ns before combining it with the separate computed-input allocation change.

RPC outbound proxy calls and command-service calls should gain a similar absolute
dispatch saving, but a smaller percentage when serialization or call setup
dominates. The direct-dispatch benchmark is needed to measure the infrastructure
gain independently.

Memory impact should be neutral or favorable per proxy instance if the binding
replaces the interceptor field and per-return-type intercept delegates. Shared
memory increases by one handler/state array per `(interceptor, proxy table)` pair.

## Risks and Required Tests

- Class overrides must keep one slot and invoke the effective original method.
- Interface diamonds, signature collisions, overloads, and explicit compatible
  declarations must resolve to the intended slot.
- Closed generic proxy types must not share tables containing open or differently
  closed `MethodInfo` values.
- Interceptor reassignment must immediately switch bindings without retaining a
  stale handler.
- Concurrent first calls must publish exactly one effective slot state.
- A resolved no-handler slot must be distinct from an unresolved slot.
- Dynamic third-party `SelectHandler` implementations must retain their current
  behavior.
- Reflection, trimming, NativeAOT preservation, and existing `Invocation.Method`
  consumers must continue to work.
- RPC method aliases and base/interface `MethodInfo` identities must map to the
  same service method as before.

## Suggested Implementation Stages

1. Add generator tests for deterministic slots across class inheritance,
   overrides, interface diamonds, overloads, and generic declaring types.
2. Introduce `ProxyMethodTable` and table-qualified method references while
   retaining the current dispatch path.
3. Change generated `Invocation` construction to carry a local slot and preserve
   `Method` as a compatibility property.
4. Introduce interceptor bindings and lazy slot resolution with a dynamic fallback.
5. Migrate base, compute, command, RPC, and remote-compute interceptors to stable
   slot resolution.
6. Remove warm-path `MethodInfo` handler lookups and obsolete generated intercept
   delegate fields.
7. Benchmark direct dispatch, cached compute calls, recomputation, and RPC outbound
   calls; inspect allocations and generated/JIT code.
8. Remove old caches only after all compatibility and concurrency tests pass.

Keep generator/table infrastructure, built-in interceptor migration, and cache
removal in separate revertible commits.
