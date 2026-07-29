# VERIFY: the backend-only (`IBackendCommand` / `IBackendService`) gate

Adversarial verification, ActualLab.Fusion @ `master` (v14.1.x), reproduced against the
**latest published NuGet packages, 14.1.78**.

Repro: `tmp/verify-backend-gate/` (`VerifyBackendGate.csproj`, `Program.cs` = Phase A,
`PhaseB.cs` = Phase B). Raw output: `tmp/review-r2/run-broken.txt` (as shipped) and
`tmp/review-r2/run-fixed.txt` (with the one-line constant repaired at process start via
`FIX_BACKEND_NAME=1`). Nothing outside `tmp/` was touched; the main working tree was not built.

---

## Overall verdict

**Reviewer C is correct and is the root cause. Reviewer A is correct about the *conclusion*
(the gate is bypassable) but wrong about the *mechanism* being primary — his mechanism is a
real, additional, second-order hole. Reviewer B is wrong.**

Three findings, all confirmed by execution:

| # | Finding | Status |
|---|---------|--------|
| 1 | `RpcMethodDef.BackendCommandInterfaceFullName` is `"ActualLab.CommandR.IBackendCommand"`, but the real `FullName` is `"ActualLab.CommandR.**Commands**.IBackendCommand"`. `isBackendCommand` is therefore **always `false` for every type**, and the method-level backend gate has **never** worked. | **CONFIRMED by execution** |
| 2 | Concrete, shipped, reachable target: `IKeyValueStore.Set` / `.Remove` (`IBackendCommand`s on a non-`IBackendService` service) can be invoked by an ordinary, non-backend remote peer against **any shard and any key**. | **CONFIRMED end-to-end** |
| 3 | Reviewer A's declared-vs-runtime-type hole at `RpcMethodDef.cs:105` is *additionally* real: it survives the fix for #1. | **CONFIRMED by execution, with #1 repaired** |

`Errors.BackendCommandRequiresBackendPeer()` is indeed **never thrown** anywhere, and
`CommandServiceInterceptor` indeed enforces **nothing** about `IBackendCommand` — both of
Reviewer A's supporting observations are true.

---

## 1. Proof of the `FullName` mismatch (executed, not read)

`tmp/review-r2/run-broken.txt`, Phase B:

```
[B0] RpcMethodDef.CommandInterfaceFullName        = ActualLab.CommandR.ICommand
[B0] RpcMethodDef.BackendCommandInterfaceFullName = ActualLab.CommandR.IBackendCommand
[B0] typeof(IBackendCommand).FullName             = ActualLab.CommandR.Commands.IBackendCommand
[B0] typeof(ICommand).FullName                    = ActualLab.CommandR.ICommand
[B0] typeof(KeyValueStore_Set).GetInterfaces():
        ActualLab.CommandR.Commands.IBackendCommand      <-- real name
        ActualLab.CommandR.ICommand
        ActualLab.CommandR.ICommand`1[[System.Reactive.Unit, ...]]
        ...
[B1] RpcMethodDef.IsCommandType(KeyValueStore_Set)  = True, isBackendCommand = False
[B1] RpcMethodDef.IsCommandType(EvilBackendCommand) = True, isBackendCommand = False
```

- `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:18` — `CommandInterfaceFullName = "ActualLab.CommandR.ICommand"` ✔ matches (`src/ActualLab.CommandR/ICommand.cs:1` → `namespace ActualLab.CommandR;`).
- `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:19` — `BackendCommandInterfaceFullName = "ActualLab.CommandR.IBackendCommand"` ✘ (`src/ActualLab.CommandR/Commands/IBackendCommand.cs:1` → `namespace ActualLab.CommandR.Commands;`).
- `src/ActualLab.Rpc/Configuration/RpcMethodDef.Static.cs:37-38` — ordinal `FullName` comparison ⇒ `isBackendCommand` can never be `true`.
- `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:105` — `IsBackend = service.IsBackend || isBackend` collapses to `service.IsBackend`.

**Age of the defect.** `git log -S 'BackendCommandInterfaceFullName'` returns only
`8667db1a8` ("wip: v10.3 with more robust RpcPeerRef implementation", 2025-08-05, which
introduced the constant already misspelled) and a later unrelated refactor. `git show
8667db1a8:src/ActualLab.CommandR/Commands/IBackendCommand.cs` shows the namespace was
`ActualLab.CommandR.Commands` at that commit too. **The method-level gate has been dead in
every release from v10.3 through 14.1.78.**

Cross-check with the constant repaired (`run-fixed.txt`):

```
[B1] RpcMethodDef.IsCommandType(KeyValueStore_Set)  = True, isBackendCommand = True
[B3] Set:2 IsBackend               = True
[B3] KeyValueStore_Set over non-backend peer -> RpcException: Endpoint not found: 'IKeyValueStore.Set:2'.
```

The one-character-class change flips the gate from "never fires" to "fires correctly".

---

## 2. Enforcement map — every check on the inbound path

Ordered as an inbound message traverses them.

| # | Where | file:line | What it rejects |
|---|-------|-----------|-----------------|
| 1 | Method resolution | `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:37` + `src/ActualLab.Rpc/Configuration/RpcMethodResolver.cs:59-61` (`serverOnly: true`) | Unknown methods and methods of services with no server ⇒ `NotFound` (`RpcInboundContext.cs:38-46`) |
| 2 | **THE backend gate** | `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47-54` — `if (MethodDef.IsBackend && !Peer.Ref.IsBackend)` | Backend methods on a non-backend peer ⇒ `NotFound`. **This is the only backend check on the whole inbound path.** |
| 3 | Call-type validation | `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:58-67` | Illegal call-type downgrades. Unrelated to backend. |
| 4 | `RpcRouteValidator` middleware | `src/ActualLab.Rpc/Middlewares/RpcRouteValidator.cs:22-27` | `RpcServiceMode.Client` ⇒ `PureClientCannotProcessInboundCalls`; `Distributed` ⇒ re-route. **No backend check.** |
| 5 | `RpcInboundCommandHandler` middleware | `src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:22` (Kind filter), `:28` (removes #4), `:31-32` (`Client` mode check), `:35-48` (hand-off to `ICommander`) | **No `IBackendCommand` check, no peer check.** It casts the deserialized argument and calls `CommandContext.Call` directly. |
| 6 | `ICommander` pipeline | `src/ActualLab.CommandR/Internal/Commander.cs`, `Configuration/CommandHandlerResolver.cs` | Dispatch is by **runtime** command type. No backend/peer awareness anywhere. |
| 7 | `CommandServiceInterceptor` | `src/ActualLab.CommandR/Interception/CommandServiceInterceptor.cs:28-45` | Only "handler must be called from inside a matching `CommandContext`". **Nothing about `IBackendCommand`.** |

Inputs to check #2:

| Input | file:line |
|---|---|
| `RpcServiceDef.IsBackend = typeof(IBackendService).IsAssignableFrom(Type)` | `src/ActualLab.Rpc/Configuration/RpcServiceDef.cs:59` |
| `RpcMethodDef.IsBackend = service.IsBackend \|\| isBackend` | `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:105` |
| `isBackend` comes from the **declared** parameter type | `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:173-181` (`GetMethodKind`) |
| the (broken) string match | `src/ActualLab.Rpc/Configuration/RpcMethodDef.Static.cs:28-42` |
| `Peer.Ref.IsBackend` is set by *which endpoint the client connected to* | `src/ActualLab.Rpc.Server/EndpointRouteBuilderExt.cs:22-24` (WS), `:41-43` (HTTP); `RpcWebSocketServerDefaultDelegates.cs:28-32`; `RpcHttpServerDefaultDelegates.cs:18-22`; gated by `options.ExposeBackend` |
| polymorphic-argument type bound (`expectedType.IsAssignableFrom(itemType)`) | `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83-94`; `TextTypeSerializer.cs:62-73` |
| orphan error, defined but never thrown | `src/ActualLab.CommandR/Internal/Errors.cs:16-18` |
| false doc claim ("`CommandServiceInterceptor` is responsible") | `src/ActualLab.CommandR/Commands/IBackendCommand.cs:10-12` |

### Service level vs. method level

The gate is a **single check over a value that is the OR of a service-level and a
method-level fact** (`RpcMethodDef.cs:105`).

- The **service-level** half (`IBackendService`) works. Verified in Phase A: a call to
  `IEvilBackend.Ping` over a non-backend peer is rejected with
  `RpcException: Endpoint not found: 'IEvilBackend.Ping:2'`. This is why Reviewer B's
  spot-check looked clean — he almost certainly tested an `IBackendService`.
- The **method-level** half (`IBackendCommand`) is dead (finding #1). Any service that is
  *not* `IBackendService` gets zero protection from `IBackendCommand`.

`grep -rn "BackendCommandRequiresBackendPeer"` over the whole repo returns exactly one hit,
its definition at `src/ActualLab.CommandR/Internal/Errors.cs:16`. **Never thrown.
Confirmed.**

---

## 3. The concrete reachable path (`IKeyValueStore`)

`src/ActualLab.Fusion.Ext.Services/Extensions/IKeyValueStore.cs:8`:

```csharp
public interface IKeyValueStore : IComputeService   // NOT IBackendService
{
    [CommandHandler] Task Set(KeyValueStore_Set command, CancellationToken ct = default);
    [CommandHandler] Task Remove(KeyValueStore_Remove command, CancellationToken ct = default);
    ...
}
public partial record KeyValueStore_Set(string Shard, (string Key, string Value, Moment? ExpiresAt)[] Items)
    : ICommand<Unit>, IBackendCommand;                       // :33-36
public partial record KeyValueStore_Remove(string Shard, string[] Keys)
    : ICommand<Unit>, IBackendCommand;                       // :43-46
```

Registered through the ordinary path — `fusion.AddService<IKeyValueStore, InMemoryKeyValueStore>()`
at `src/ActualLab.Fusion.Ext.Services/Extensions/FusionBuilderExt.cs:38`
(`AddInMemoryKeyValueStore`) and `:66` (`AddDbKeyValueStore`). On a server that does
`AddFusion(RpcServiceMode.Server)`, this service is published on the **public**
(non-backend) RPC endpoint.

The client-facing, safe wrapper is `ISandboxedKeyValueStore`
(`src/ActualLab.Fusion.Ext.Contracts/Extensions/ISandboxedKeyValueStore.cs:9`), whose commands
are `ISessionCommand<Unit>` (**not** `IBackendCommand`) and which prefixes every key with a
session/user-scoped prefix. `IBackendCommand` on `KeyValueStore_*` is precisely the control
that is supposed to stop clients from talking to the raw store directly. It does not.

### Exact call an attacker sends

Connect a plain WebSocket to the **public** endpoint
(`RpcWebSocketServerOptions.RequestPath`, default `/rpc/ws` — *not* `BackendRequestPath`),
complete the normal handshake, then send one regular inbound call message:

```
method  : "IKeyValueStore.Set:2"           (or "IKeyValueStore.Remove:2")
args    : [ KeyValueStore_Set { Shard = "<any shard>",
                                Items = [("<any key>", "<any value>", null)] },
            CancellationToken ]
```

No authentication token, no `Session`, no backend connection required — the argument type
carries no principal at all.

Phase B does exactly this: it builds the outbound call by hand
(`new RpcOutboundContext(frontPeer).PrepareCall(setDef, ArgumentList.New(command, ct))`),
pre-routed to a **non-backend** client peer, i.e. no generated proxy is involved — precisely
what a hostile client can do.

Result, as shipped (`run-broken.txt`):

```
[B3] IKeyValueStore.IsBackend      = False
[B3] Set:2 Kind                    = Command
[B3] Set:2 IsBackend               = False        <-- gate open
[B3] KeyValueStore_Set over non-backend peer -> call succeeded
[B3] server-side IKeyValueStore.Get("any-shard", "stolen-key") -> stolen-value
PHASE B VERDICT: a NON-backend peer executed KeyValueStore_Set (IBackendCommand) end-to-end.
```

With the constant repaired (`run-fixed.txt`):

```
[B3] Set:2 IsBackend               = True         <-- gate closed
[B3] KeyValueStore_Set over non-backend peer -> RpcException: Endpoint not found: 'IKeyValueStore.Set:2'.
[B3] server-side IKeyValueStore.Get("any-shard", "stolen-key") -> <null>
```

**Answer to Q3: yes — arbitrary shard, arbitrary key, write and delete.**
(Note the same is true for the `IKeyValueStore` *query* methods `Get` / `Count` /
`ListKeySuffixes` — but those were never `IBackendCommand`-protected, so that is a separate
design question, not this bug.)

`IAuthBackend` (`src/ActualLab.Fusion.Ext.Services/Authentication/IAuthBackend.cs:10`) is
`IBackendService` and is therefore **not** affected — the service-level half of the gate
still covers it. Same for `ITodoBackend` in the samples.

---

## 4. What else the mismatch disables

`isBackendCommand` has exactly one consumer: `RpcMethodDef.cs:175` → `RpcMethodDef.IsBackend`
(`:105`). Everything downstream of `RpcMethodDef.IsBackend` is therefore also wrong for
"backend command on a non-`IBackendService` service":

1. **Security** — `RpcInboundContext.cs:47` (the bug above).
2. **Timeouts** — `src/ActualLab.Rpc/Configuration/RpcCallTimeouts.Default.cs:27-33`: such a
   method gets `Command` timeouts (1.5 s connect / 10 s run) instead of `BackendCommand`
   (300 s / 300 s). Long-running backend commands invoked over a mesh connection can time out
   spuriously.
3. **Outbound routing** — the idiomatic router shape is
   `methodDef.IsBackend ? backendRef : apiRef` (exactly what the framework's own test base does:
   `tests/ActualLab.Tests/Rpc/RpcLocalTestBase.cs:56-60`). Apps using that shape send
   backend-command calls over the **API** connection instead of the backend one.

`RpcServiceDef.Scope` / `RpcDefaults.BackendScope` (`src/ActualLab.Rpc/Configuration/Options/RpcRegistryOptions.cs:31-34`)
is service-level only, so version negotiation is unaffected.

---

## 5. Is Reviewer A's declared-vs-runtime issue additionally real?

**Yes — it is real and it survives the fix for #1.** It is not moot; it is the *next* hole.

Phase A builds a client-facing, non-`IBackendService` service whose command method declares
the **base** type:

```csharp
public abstract record ThingCommand : ICommand<string>;                       // abstract, no [RpcSerializable]
public record EvilBackendCommand(string Name) : ThingCommand, IBackendCommand<string>;

public interface IPublicApi : IRpcService {                                    // NOT IBackendService
    Task<string> OnThing(ThingCommand command, CancellationToken ct = default); // declared as the BASE
}
```

Because `ThingCommand` is abstract and un-`[RpcSerializable]`,
`RpcArgumentSerializer.IsPolymorphic` (`src/ActualLab.Rpc/Serialization/RpcArgumentSerializer.cs:40-41`)
is `true`, so the wire carries the derived type name and
`ByteTypeSerializer.ReadDerivedItemType` (`:88`) accepts anything assignable to `ThingCommand`
— including `EvilBackendCommand`. `RpcMethodDef.IsBackend` was computed from the *declared*
type, so `RpcInboundContext.cs:47` lets it through, and
`RpcInboundCommandHandler.cs:35-48` hands the **runtime** command to `ICommander`.

Both runs (broken **and** fixed) show:

```
[control] backend service call over non-backend peer -> RpcException: Endpoint not found: 'IEvilBackend.Ping:2'.
[sanity ] public command over non-backend peer -> public:ok
[attack ] IBackendCommand over non-backend peer -> BACKEND-EXECUTED:pwned
[attack ] EvilBackend handler execution count   -> 1
VERDICT: BYPASS CONFIRMED
```

i.e. the backend command's handler on the `IBackendService` `IEvilBackend` executed for a
non-backend remote peer.

Qualifications, so this is rated honestly:

- It requires the **application** to declare a command method whose parameter type is
  abstract / an interface. **No framework or sample service in this repo does that** — I
  checked every command-shaped RPC declaration in `src/` and `samples/`; all use concrete
  records (`Auth_SignOut`, `KeyValueStore_Set`, `Todos_AddOrUpdate`, `EditCommand<T>`,
  `Chat_Post`, …).
- Type substitution is bounded: `expectedType.IsAssignableFrom(itemType)` — the attacker can
  only send subtypes of the declared base, not arbitrary gadget types.
- The `[RpcSerializable]`-with-unions variant (see `NonPolymorphicBase` in the test suite) is
  equally exploitable and does **not** set `HasPolymorphicArguments`, so any fix must test the
  runtime object, not `HasPolymorphicArguments`.
- Standalone severity: **Medium — a framework footgun**, not an unconditional bypass.

---

## True severity

**Overall: HIGH.**

- **Finding #1 (the string mismatch): High.** A documented, first-class security control
  (`IBackendCommand`, "will be rejected with an error") has been a complete no-op in every
  released version since v10.3 (Aug 2025), silently. Any application that relies on
  `IBackendCommand` to keep a command off the public RPC surface — while its service is not
  `IBackendService` — is unprotected, with no error, no warning and no log line. Blast radius
  is every downstream Fusion app that uses the tag, not just this repo.
- **Finding #2 (shipped `IKeyValueStore` exposure): High for any app that calls
  `AddInMemoryKeyValueStore()` / `AddDbKeyValueStore()` and publishes the public RPC
  endpoint** — unauthenticated arbitrary-shard/arbitrary-key writes and deletes on a store
  whose client-facing façade (`ISandboxedKeyValueStore`) exists solely to sandbox those keys.
  Opt-in feature, hence not "critical/unconditional".
- **Finding #3 (declared-vs-runtime): Medium.** Real, reachable, but needs an unusual
  application-side declaration.
- **Mitigating context (applies to all three):** the peer's backend-ness comes from *which
  endpoint* the client connected to; the backend endpoint is only mapped when
  `options.ExposeBackend` is set (`EndpointRouteBuilderExt.cs:23`, `:42`). Deployments that
  additionally protect the whole public endpoint with auth/network policy reduce exposure —
  but `IBackendCommand` is exactly the control meant to work *without* that assumption.

---

## Minimal fix

**(a) One-line root-cause fix** — `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:19`:

```csharp
public static string BackendCommandInterfaceFullName { get; set; } = "ActualLab.CommandR.Commands.IBackendCommand";
```

(`ActualLab.Rpc` deliberately does not reference `ActualLab.CommandR`, which is why these are
strings — so the constant must stay a string.)

**(b) Guard it with a test** in `tests/ActualLab.Tests` (which references both assemblies), so
this can never silently rot again:

```csharp
RpcMethodDef.CommandInterfaceFullName.Should().Be(typeof(ICommand).FullName);
RpcMethodDef.BackendCommandInterfaceFullName.Should().Be(typeof(IBackendCommand).FullName);
```

Plus one end-to-end test asserting `IKeyValueStore.Set:2` has `IsBackend == true` and is
rejected on a non-backend peer.

**(c) Close the declared-vs-runtime hole** in
`src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs` (this assembly *can* reference
`IBackendCommand` directly — no strings needed), which finally gives
`Errors.BackendCommandRequiresBackendPeer()` its intended use:

```csharp
return call => {
    commander ??= call.Hub.Services.Commander();
    var args = call.Arguments!;
    var command = (ICommand<T>?)args.Get0Untyped()!;
    if (command is null)
        throw new ArgumentNullException(nameof(command));
    if (command is IBackendCommand && !call.Context.Peer.Ref.IsBackend)
        throw Errors.BackendCommandRequiresBackendPeer();   // ActualLab.CommandR.Internal.Errors
    ...
};
```

This mirrors `RpcInboundContext.cs:47` exactly, so it cannot reject anything that check
already admits, and it works for both polymorphic and `[RpcSerializable]`-union arguments.

**(d) Fix the false documentation** at
`src/ActualLab.CommandR/Commands/IBackendCommand.cs:10-12` — the `<remarks>` names
`CommandServiceInterceptor` as the enforcer; after (c), the enforcers are
`RpcInboundContext` (method/service level) and `RpcInboundCommandHandler` (runtime level).

**(e) Optional defence in depth:** consider whether `IKeyValueStore` should be
`IBackendService` outright, given that its query methods (`Get` / `Count` /
`ListKeySuffixes`, taking a raw `shard`) are currently reachable by any client too.
