# TypeScript Fusion Client — Plan Update 1

Architectural deltas from design review. Base plan: `typescript-fusion-client.md`.
This document captures **changes only**.

---

## Delta A: Package Restructuring — Decouple Fusion from RPC

### Current (wrong)

```
@actuallab/core  ←  @actuallab/rpc  ←  @actuallab/fusion
```

Fusion depends on RPC. `ComputeServiceHost`, `FusionHub`, service definitions
with RPC type descriptors — all live inside `@actuallab/fusion`.

### New

```
@actuallab/core  ←  @actuallab/rpc
@actuallab/core  ←  @actuallab/fusion
@actuallab/fusion + @actuallab/rpc  ←  @actuallab/fusion-rpc  (NEW)
```

**Fusion must have ZERO dependencies on RPC.** Compute services and compute
methods are pure Fusion concepts — usable without RPC. The bridge between
Fusion and RPC lives in a new `@actuallab/fusion-rpc` package.

### What moves to `@actuallab/fusion-rpc`

| Current location | Module | Reason |
|-----------------|--------|--------|
| `fusion/fusion-hub.ts` + `fusion/compute-service-host.ts` | `FusionHub` (merged) | Coordinates RPC + Fusion; directly manages compute functions and invalidation→`$sys-c.Invalidate` wiring |
| `fusion/service-def.ts` | `defineComputeService` (RPC variant), `RpcType`, `ServiceDef`, `MethodDef` | RPC-coupled service metadata |
| `rpc/rpc-service-def.ts` | `defineComputeService` (RPC variant) | RPC compute service definition helper |
| `fusion/tests/e2e-rpc.test.ts` | E2E RPC tests | Tests the Fusion+RPC bridge |

`ComputeServiceHost` is merged into `FusionHub` — no need for a separate class.
`FusionHub` directly stores `ComputeFunction` instances per registered service
method and handles the compute call → invalidation → `$sys-c.Invalidate` flow.

### What stays in `@actuallab/fusion`

- `Computed<T>`, `ConsistencyState`
- `ComputeContext`, `computeContextKey`
- `ComputedInput` (redesigned — see Delta D)
- `ComputedRegistry`
- `ComputeFunction` (redesigned — see Delta B)
- `@computeMethod` decorator (see Delta B)
- `ComputedState<T>`, `MutableState<T>` (with shared `State<T>` — see Delta G)
- `UpdateDelayer`, `NoDelayer`, `FixedDelayer`

### What gets DELETED from `@actuallab/fusion`

| Module | Reason |
|--------|--------|
| `service-def.ts` | RPC-coupled; replaced by decorators |
| `interceptor.ts` | Generic interceptor chain is overkill for pure Fusion |
| `compute-service-interceptor.ts` | Part of interceptor chain; replaced by `@computeMethod` |
| `compute-service.ts` (`createLocalService`) | Replaced by `@computeMethod` |
| `invocation.ts` | Part of interceptor chain |
| `fusion-hub.ts` | Merged into `FusionHub` in `@actuallab/fusion-rpc` |
| `compute-service-host.ts` | Merged into `FusionHub` in `@actuallab/fusion-rpc` |

---

## Delta B: Decorator-Based API — `@computeMethod`, `@rpcService`, `@rpcMethod`

Replaces the previous `makeComputeService` / `defineComputeService` / `createLocalService`
approach with TC39 stage 3 decorators (TypeScript 5.0+) and decorator metadata (TS 5.2+).

### Why TC39 Stage 3 Decorators (not experimental + reflect-metadata)

- **Standard**: Aligns with ECMAScript proposal, future-proof.
- **No reflect-metadata needed**: We don't need `design:paramtypes` — arg count
  comes from `fn.length`, and we use JSON for serialization.
- **Decorator metadata**: `context.metadata` (shared across all decorators on a
  class) + `Class[Symbol.metadata]` to read metadata after decoration.
- **Polyfill**: Single line: `Symbol.metadata ??= Symbol("Symbol.metadata")`.

### tsconfig requirements

```json
{
  "compilerOptions": {
    "target": "es2022",
    "lib": ["es2022", "esnext.decorators", "dom"]
  }
}
```

No `experimentalDecorators`. No `emitDecoratorMetadata`.

### Metadata Storage

All decorator metadata is stored in `context.metadata` and read via
`Class[Symbol.metadata]`:

```typescript
// Shared metadata keys (symbols)
const METHODS_META = Symbol("actuallab.methods");
const SERVICE_META = Symbol("actuallab.service");

interface MethodMeta {
  argCount: number;
  compute?: boolean;
  stream?: boolean;
}

interface ServiceMeta {
  name: string;
}

// Read metadata from a class
function getMethodsMeta(cls: Function): Record<string, MethodMeta> | undefined {
  return (cls as any)[Symbol.metadata]?.[METHODS_META];
}

function getServiceMeta(cls: Function): ServiceMeta | undefined {
  return (cls as any)[Symbol.metadata]?.[SERVICE_META];
}
```

### `@computeMethod` — in `@actuallab/fusion`

Method decorator. Wraps the method to route through `ComputeFunction` for
caching and dependency tracking. Also stores `{ compute: true }` in metadata.

```typescript
class CounterService {
  private store = new Map<string, number>();

  @computeMethod
  async getValue(id: string): Promise<number> {
    return this.store.get(id) ?? 0;  // 'this' is the real instance
  }

  // NOT decorated — mutation, no caching
  async increment(id: string): Promise<void> {
    this.store.set(id, (this.store.get(id) ?? 0) + 1);
    // Invalidate the compute method for this id
    this.getValue.invalidate(id);
  }
}

const svc = new CounterService();
const val = await svc.getValue("abc");  // cached via ComputeFunction
```

**How `this` works**: The decorator replaces the method on the prototype.
When called via `instance.method()`, JS binds `this` to the instance naturally
— exactly like inheritance + override. No Proxy needed.

**`ComputeFunction` lifecycle** — ONE per (class declaration × method declaration):

The `ComputeFunction` is created once at decoration time (when the class is
defined) and captured in the closure. **No WeakMap lookup at call time.** The
instance is passed per-call via `this`.

```
Decoration time (@computeMethod runs):
  → Create ONE ComputeFunction(methodName, originalFn)
  → Captured in replacement closure — zero runtime lookups

Call time (svc.getValue("abc")):
  → replacement.call(svc, "abc")
  → cf.invoke(this, args)            // this = svc, no WeakMap
  → new ComputeMethodInput(this, "getValue", ["abc"])
  → registry.get(input) → hit? return cached (input discarded).
                          miss? compute, register, return.
```

**Implementation**:

```typescript
function computeMethod(
  target: Function,
  context: ClassMethodDecoratorContext,
) {
  const methodName = String(context.name);

  // Store metadata
  const methods = ((context.metadata as any)[METHODS_META] ??= {});
  methods[methodName] = { ...methods[methodName], compute: true, argCount: target.length };

  // ONE ComputeFunction per class×method — created at decoration time
  const cf = new ComputeFunction(methodName, target);

  // Prototype-level replacement (fallback, 'this' bound by call site)
  const replacement = function(this: any, ...allArgs: unknown[]) {
    return cf.invoke(this, allArgs);
  };

  // Per-instance setup: create bound method with .invalidate pre-bound
  // addInitializer runs at instance construction time
  context.addInitializer(function(this: any) {
    const instance = this;
    const boundMethod = (...allArgs: unknown[]) => {
      return cf.invoke(instance, allArgs);
    };
    boundMethod.invalidate = (...args: unknown[]) => {
      const input = new ComputeMethodInput(instance, methodName, args);
      computedRegistry.get(input)?.invalidate();
    };
    this[methodName] = boundMethod;  // own property shadows prototype
  });

  return replacement;
}
```

The `addInitializer` callback runs once per instance at construction time.
It creates an own property on the instance with `.invalidate` already bound
to the instance. Cost: one function + one invalidate function per instance per
method (same as class fields with arrow functions). This enables the clean API:

```typescript
this.getValue.invalidate(id);  // works — .invalidate is pre-bound to instance
```

**`ComputeFunction` class** — no longer holds instance ref:

```typescript
class ComputeFunction {
  readonly methodName: string;
  private _fn: Function;
  private _locks = new Map<string, AsyncLock>();

  constructor(methodName: string, fn: Function) {
    this.methodName = methodName;
    this._fn = fn;
  }

  async invoke(instance: object, allArgs: unknown[]): Promise<Computed<unknown>> {
    // 1. Resolve AsyncContext ONCE via DRY helper
    const asyncCtx = AsyncContext.fromArgs(allArgs);

    // 2. Pull values directly — no repeated resolution
    const callerComputeCtx = asyncCtx?.get(computeContextKey);
    const ct = asyncCtx?.get(cancellationTokenKey) ?? CancellationToken.none;

    // 3. Strip THIS context from args (reference equality — safe)
    const args = asyncCtx?.stripFromArgs(allArgs) ?? allArgs;

    // 4. Check cache — on hit, input is discarded (identical one in registry)
    const input = new ComputeMethodInput(instance, this.methodName, args);
    const existing = computedRegistry.get(input);
    if (existing?.isConsistent) {
      callerComputeCtx?.captureDependency(existing);
      return existing;
    }

    // 5. Cache miss — create new ComputeContext
    const newComputed = new Computed<unknown>(input);
    const childComputeCtx = new ComputeContext(newComputed);

    // 6. Create child AsyncContext — only swap computeContextKey,
    //    CancellationToken and everything else carries through.
    //    AsyncContext.empty avoids allocation (immutable singleton).
    const childAsyncCtx = (asyncCtx ?? AsyncContext.empty)
      .with(computeContextKey, childComputeCtx);

    // 7. Run with original .run() — args are already stripped of old context,
    //    so no stale AsyncContext can override what .run() exposes.
    let result: Result<unknown>;
    try {
      const value = childAsyncCtx.run(() => this._fn.call(instance, ...args));
      const resolved = value instanceof Promise ? await value : value;
      result = ok(resolved);
    } catch (e) {
      result = error(e);
    }

    if (result.ok) newComputed.setOutput(result.value);
    else newComputed.setError(result.error);

    computedRegistry.register(newComputed);
    callerComputeCtx?.captureDependency(newComputed);
    return newComputed;
  }
}
```

**Static methods**: Work identically. `context.static === true` and `this` is
the class itself, which serves as the instance reference for `ComputedInput`:

```typescript
class GlobalConfig {
  @computeMethod
  static async getTheme(): Promise<string> { return "dark"; }
}
// this = GlobalConfig (the class), used as instance ref
```

**Standalone functions**: `@computeMethod` can also be applied as a plain
function wrapper for non-class functions:

```typescript
// As a wrapper function (not a decorator)
const computeValue = computeMethod.wrap((id: string) => expensiveCalc(id));
await computeValue("abc");           // cached
computeValue.invalidate("abc");      // invalidate
```

A synthetic instance object is created internally for standalone functions.

### `@rpcService(name)` — in `@actuallab/rpc`

Class decorator. Stores the service name (used on the wire as
`ServiceName.MethodName`). Required for RPC registration.

```typescript
function rpcService(serviceName: string) {
  return function(target: any, context: ClassDecoratorContext) {
    const meta = ((context.metadata as any)[SERVICE_META] ??= {});
    meta.name = serviceName;
  };
}
```

### `@rpcMethod(options?)` — in `@actuallab/rpc`

Method decorator. Stores RPC metadata (arg count from `fn.length`, stream flag).
Does **not** wrap the method — just metadata.

```typescript
function rpcMethod(options?: { stream?: boolean }) {
  return function(target: Function, context: ClassMethodDecoratorContext) {
    const methodName = String(context.name);
    const methods = ((context.metadata as any)[METHODS_META] ??= {});
    methods[methodName] = {
      ...methods[methodName],
      argCount: target.length,
      stream: options?.stream ?? false,
    };
    return target; // no wrapping — RPC proxy handles invocation
  };
}
```

### Contract Classes — RPC Client Stubs

TypeScript interfaces are erased at runtime, so decorators can't go on them.
Instead, use a **contract class with empty/stub method bodies** — the bodies
are never called (the RPC proxy intercepts everything):

```typescript
@rpcService("ProductService")
class IProductService {
  @computeMethod @rpcMethod()
  async getProduct(id: string): Promise<Product> { return undefined!; }

  @computeMethod @rpcMethod({ stream: true })
  async getProducts(query: string): AsyncIterable<Product> { return undefined!; }

  @rpcMethod()
  async editProduct(cmd: EditProductCommand): Promise<void> {}
}
```

- `@rpcService("ProductService")` → wire name
- `@computeMethod @rpcMethod()` → compute method exposed via RPC
- `@rpcMethod()` alone → plain RPC method (no compute caching)
- Method bodies (`return undefined!`, empty `{}`) are stubs — never executed

### `rpcHub.addClient` / `rpcHub.addServer`

Registration reads decorator metadata from the contract class:

```typescript
// Client: creates a Proxy-based typed client
const productService = rpcHub.addClient(IProductService, peer);
const product = await productService.getProduct("abc"); // → RPC call

// Server: maps contract to implementation
rpcHub.addServer(IProductService, new ProductServiceImpl());
```

`addClient(ContractClass, peer)`:
1. Reads `ContractClass[Symbol.metadata]` → service name, methods, arg counts
2. Creates a `Proxy` that intercepts method calls
3. For compute methods (`compute: true`): routes through `ComputeFunction` + RPC
4. For plain methods: sends `RpcOutboundCall` directly

`addServer(ContractClass, impl)`:
1. Reads metadata from `ContractClass`
2. Registers `impl`'s methods as RPC handlers
3. For compute methods: routes through `ComputeFunction` for caching,
   watches resulting `Computed<T>` for invalidation, sends `$sys-c.Invalidate`
   (all handled directly by `FusionHub`)

### `@computeService` — NOT needed

`@computeMethod` handles method wrapping. `@rpcService` handles wire naming.
For local-only compute services, no class-level decorator is needed. Debug name
comes from `constructor.name`.

---

## Delta C: Compute Function Invalidation API

### Current

No way to explicitly invalidate a compute function's cached result from outside.

### New

Each `@computeMethod`-decorated method has an `.invalidate(...)` attached:

```typescript
class CounterService {
  @computeMethod
  async getValue(id: string): Promise<number> { /* ... */ }

  async increment(id: string): Promise<void> {
    this.store.set(id, (this.store.get(id) ?? 0) + 1);
    // Invalidate the cached Computed for getValue(id)
    this.getValue.invalidate(id);
  }
}
```

**Implementation**: `.invalidate(...)` constructs a `ComputedInput` from
`(instance, methodName, args)`, looks it up in `ComputedRegistry`, and calls
`.invalidate()` on the found `Computed`. Since `ComputedInput` is cheap to
construct and is discarded after lookup (same as the per-call pattern), this
is consistent with the overall design.

**`this` binding**: The `@computeMethod` decorator uses `context.addInitializer`
to create per-instance own properties at construction time. Each instance gets
its own method function with `.invalidate` pre-bound to the instance via closure.
No `.call(this, ...)` needed — `this.getValue.invalidate(id)` just works.

---

## Delta D: Instance Identity — Replace serviceId with Instance Reference

### Current

`ComputedInput` uses a string `serviceId`:

```typescript
this.key = `${serviceId}.${methodName}:${args}`;
```

### New

Use the **service instance itself** as identity. JavaScript `===` compares
objects by reference. `Map` and `WeakMap` use reference identity for keys.

For string-keyed `ComputedInput.key`, auto-assign numeric IDs via `WeakMap`:

```typescript
let _nextInstanceId = 0;
const _instanceIds = new WeakMap<object, number>();

function getInstanceId(instance: object): number {
  let id = _instanceIds.get(instance);
  if (id === undefined) {
    id = ++_nextInstanceId;
    _instanceIds.set(instance, id);
  }
  return id;
}
```

#### `ComputedInput` hierarchy — mirrors .NET

`ComputedInput` is the **base class**, kept minimal so that `State` types can
inherit from it directly. `ComputeMethodInput` extends it with the extra fields
needed for compute method cache lookup.

```typescript
/** Base — identity key for any computed value. States inherit from this directly. */
export class ComputedInput {
  readonly key: string;

  constructor(key: string) {
    this.key = key;
  }

  equals(other: ComputedInput): boolean {
    return this.key === other.key;
  }

  toString(): string {
    return this.key;
  }
}

/** Extended input for compute methods — adds instance ref, method name, args. */
export class ComputeMethodInput extends ComputedInput {
  readonly instance: object;    // Strong ref → keeps service alive
  readonly methodName: string;
  readonly args: unknown[];

  constructor(instance: object, methodName: string, args: unknown[]) {
    const instanceId = getInstanceId(instance);
    super(`${instanceId}.${methodName}:${args.map(a => JSON.stringify(a)).join(",")}`);
    this.instance = instance;
    this.methodName = methodName;
    this.args = args;
  }
}
```

- `ComputeMethodInput` is used by `ComputeFunction` — constructed per-call,
  discarded on cache hit (identical one already in `ComputedRegistry`).
- `State` types (`ComputedState`, `MutableState`) extend `ComputedInput` directly
  with their own key format. They are NOT stored in `ComputedRegistry` — they
  track their own `Computed<T>` instance directly.

**Why strong ref in `ComputeMethodInput`**: `Computed → ComputeMethodInput → instance`
keeps the service alive. When the `Computed` is GC'd, everything is released.

---

## Delta E: Proper AsyncContext Integration

### Current (broken)

`computeContextKey` defined but unused. `ComputeContext` uses its own
`static current` field, never stores itself in `AsyncContext`.

### New

`ComputeContext` stored in `AsyncContext` via `computeContextKey`. Remove
`ComputeContext.current` static entirely.

```typescript
export class ComputeContext {
  // NO static current field

  readonly computed: Computed<unknown>;
  private _isCapturing = true;

  constructor(computed: Computed<unknown>) {
    this.computed = computed;
  }

  captureDependency(dep: Computed<unknown>): void {
    if (!this._isCapturing) return;
    this.computed.addDependency(dep);
  }

  stopCapturing(): void { this._isCapturing = false; }

  static from(ctx: AsyncContext | undefined): ComputeContext | undefined {
    return AsyncContext.from(ctx)?.get(computeContextKey);
  }

  static run<T>(computeCtx: ComputeContext, fn: () => T): T {
    const asyncCtx = AsyncContext.getOrCreate().with(computeContextKey, computeCtx);
    return asyncCtx.run(fn);
  }
}
```

Circular-import accessor changes to:

```typescript
_setContextAccessor(() => AsyncContext.current?.get(computeContextKey));
```

---

## Delta F: DRY Context Resolution Helpers

Static helpers for resolving context values, plus instance helpers for
safe arg manipulation:

```typescript
// @actuallab/core — AsyncContext
class AsyncContext {
  /** Immutable empty singleton — avoids allocating new AsyncContext(). */
  static readonly empty = new AsyncContext();

  /** Resolve: return ctx if provided, otherwise AsyncContext.current. */
  static from(ctx: AsyncContext | undefined): AsyncContext | undefined {
    return ctx ?? AsyncContext.current;
  }

  /** Extract AsyncContext from last arg, or fall back to .current. */
  static fromArgs(args: unknown[]): AsyncContext | undefined {
    const last = args[args.length - 1];
    return last instanceof AsyncContext ? last : AsyncContext.current;
  }

  /** Strip THIS exact instance from args (reference equality).
   *  Returns args without the trailing AsyncContext if it matches this. */
  stripFromArgs(args: unknown[]): unknown[] {
    return args[args.length - 1] === this ? args.slice(0, -1) : args;
  }
}

// @actuallab/core — CancellationToken
class CancellationToken {
  static from(ctx: AsyncContext | undefined): CancellationToken {
    return AsyncContext.from(ctx)?.get(cancellationTokenKey) ?? CancellationToken.none;
  }
}

// @actuallab/fusion — ComputeContext
class ComputeContext {
  static from(ctx: AsyncContext | undefined): ComputeContext | undefined {
    return AsyncContext.from(ctx)?.get(computeContextKey);
  }
}
```

**`stripFromArgs` usage pattern** — inside `ComputeFunction.invoke()`:

```typescript
const asyncCtx = AsyncContext.fromArgs(allArgs);   // resolve once
const args = asyncCtx?.stripFromArgs(allArgs) ?? allArgs;  // strip THIS ctx
// args is now clean — no stale AsyncContext to override .run()
```

---

## Delta G: Shared `State<T>` Interface and Constructor Options

### Interface

```typescript
/** Common interface for all reactive state types. */
export interface State<T> {
  readonly value: T;          // throws if no value yet and no initialValue
  readonly error: unknown;    // the error, if output is ResultError
  readonly output: Result<T>; // the full Result<T>
  readonly computed: Computed<T>;
}
```

Both `ComputedState<T>` and `MutableState<T>` implement `State<T>`.

### Constructor Options

Following .NET's pattern where `ComputedState` accepts options:

```typescript
export interface StateOptions<T> {
  initialValue?: T;            // initial value before first computation
  initialOutput?: Result<T>;   // initial output (allows passing ResultError)
  delayer?: UpdateDelayer;      // update delay strategy (ComputedState only)
}
```

If both `initialValue` and `initialOutput` are provided, `initialOutput` wins.

### `State` extends `ComputedInput`

Both `ComputedState` and `MutableState` **extend `ComputedInput` directly**
(not `ComputeMethodInput`). They generate their own key format and are **NOT
stored in `ComputedRegistry`**. The state always tracks its own "method" and
the most recent `Computed<T>` instance directly. This mirrors .NET where
`State<T>` inherits from `ComputedInput`.

---

## Delta H: No Untyped Versions Needed

`Computed<unknown>` serves the role of .NET's untyped `Computed` base.
Already the case in our implementation.

---

## Delta I: AsyncContext Filtering from ComputedInput Key

The `AsyncContext` (if passed as the last argument) must be stripped before
constructing `ComputeMethodInput`. This is handled by `asyncCtx.stripFromArgs(args)`
which uses **reference equality** — it only removes the last arg if it's the
exact same `AsyncContext` instance that was resolved. This is safer than
`instanceof` checking, which could accidentally strip a user-provided
`AsyncContext` argument.

```typescript
// Inside ComputeFunction.invoke():
const asyncCtx = AsyncContext.fromArgs(allArgs);
const args = asyncCtx?.stripFromArgs(allArgs) ?? allArgs;
const input = new ComputeMethodInput(instance, this.methodName, args);
```

---

## Decorator Summary

| Decorator | Package | Target | Wraps? | Stores metadata? |
|-----------|---------|--------|--------|-----------------|
| `@computeMethod` | `fusion` | instance method, static method | Yes — routes through `ComputeFunction` | Yes — `{ compute: true, argCount }` |
| `@rpcService(name)` | `rpc` | class | No | Yes — `{ name }` |
| `@rpcMethod(opts?)` | `rpc` | method | No — just metadata | Yes — `{ argCount, stream? }` |

Decorators compose: `@computeMethod @rpcMethod()` on a method means it's both
a cached compute method AND exposed via RPC.

---

## Implementation Order

1. **Metadata infrastructure** — polyfill `Symbol.metadata`, shared metadata
   keys and reader functions

2. **Add DRY helpers to `@actuallab/core`** (Delta F)
   - `AsyncContext.from()`, `AsyncContext.fromArgs()`
   - `CancellationToken.from()`

3. **Redesign `@actuallab/fusion` internals** (Deltas B, C, D, E, G, I)
   - Rewrite `ComputedInput` with instance reference + auto-ID
   - Fix `ComputeContext` to use `AsyncContext` via `computeContextKey`
   - Rewrite `ComputeFunction` with new constructor signature
   - Implement `@computeMethod` decorator with `.invalidate()`
   - Add `State<T>` interface, implement in `ComputedState<T>` and `MutableState<T>`
   - Delete: `service-def.ts`, `interceptor.ts`, `compute-service-interceptor.ts`,
     `compute-service.ts`, `invocation.ts`

4. **Add `@rpcService` and `@rpcMethod` to `@actuallab/rpc`** (Delta B)
   - Implement decorators + metadata reader functions
   - Add `rpcHub.addClient(ContractClass, peer)` — reads metadata, creates Proxy
   - Add `rpcHub.addServer(ContractClass, impl)` — reads metadata, registers handlers

5. **Create `@actuallab/fusion-rpc` package** (Delta A)
   - Create `FusionHub` (merged from `FusionHub` + `ComputeServiceHost`)
   - `addClient(ContractClass, peer)` — reads decorator metadata, creates Proxy
   - `addServer(ContractClass, impl)` — reads metadata, registers handlers,
     routes compute methods through `ComputeFunction`, wires invalidation → `$sys-c.Invalidate`
   - Move e2e-rpc tests

6. **Update all tests**
   - Rewrite fusion tests for `@computeMethod` decorator API
   - Move and adapt `e2e-rpc.test.ts` to `fusion-rpc` package

---

## Q&A — Resolved

### Q1: `ComputeFunction` — bound at construction or per-call?

**Answer**: One `ComputeFunction` per (class declaration × method declaration).
Created once at decoration time, captured in closure. Instance passed per-call
via `this`. No WeakMap lookup at runtime. `ComputedInput` is constructed
per-call and discarded on cache hit.

### Q2: `State<T>` — value, error, output, constructor options

**Answer**: `State<T>` has `.value` (T, throws if not ready), `.error`,
`.output` (Result<T>). Constructor accepts options: `initialValue`,
`initialOutput` (Result<T>, allows passing ResultError), `delayer`.
`State` is a `ComputedInput` but NOT in `ComputedRegistry` — tracks its
own method and current `Computed<T>` directly.

### Q3: Standalone `@computeMethod` outside a class?

**Answer**: Yes. Provide `computeMethod.wrap(fn)` as a function wrapper for
standalone functions. Uses a synthetic instance object internally.

### Q4: Naming — `@actuallab/fusion-rpc` or `@actuallab/fusion.rpc`?

**Answer**: `@actuallab/fusion-rpc` (npm convention).

### Q5: `.invalidate()` `this`-binding

**Answer**: Resolved. Use `context.addInitializer` to create per-instance
bound methods with `.invalidate` pre-bound via closure. Cost: one function +
one invalidate function per instance per method. Clean API:
`this.getValue.invalidate(id)` — no `.call()` needed.
