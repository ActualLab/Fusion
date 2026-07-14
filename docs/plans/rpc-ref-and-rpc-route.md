# RpcRef + RpcRoute: Stable Refs with Resettable Routes

## Goal

Make peer refs **stable**: one ref per logical target (shard, host, "default"), cached forever,
safe to return from routers with no factory ceremony. Shard-map changes no longer mint new refs —
instead, the ref's **route** (a new per-generation object, `RpcRoute`) is reset in place.
Peers stay 1:1 with route generations, so the entire battle-tested reroute pipeline
(`RpcRerouteException`, `TryReroute`, `PrepareReroutedCall`, peer disposal + drain) is preserved.

Two renames anchor the change:

| Old | New |
|-----|-----|
| `RpcPeerRef` | `RpcRef` |
| `RpcRouteState` | `RpcRoute` (absorbed + extended) |

## Why not a resettable RpcPeer

Considered and rejected. The route-generation token must be capturable by *everything* that
participates in a routed call — including **local calls, which never touch a peer**
(`RpcMethodDef.CreateOutboundCall` returns `null` for `ConnectionKind.Local`; the interceptor
invokes the local target directly, and the "peer" is an inert placeholder). A reset API on
`RpcPeer` cannot govern local execution gating (`LocalExecutionAwaiter`, shard-ownership
`Computed` dependencies) or the OCE→reroute conversion for in-flight local calls.

Additionally, reset-in-place breaks the ABA-freedom of every staleness check:
`RpcOutboundCall.IsPeerChanged()` compares peer *identity* against a fresh router invocation,
and ~15 call sites (`WhenConnectedOrReroute`, `InvalidateWhenReconnected`, outbound contexts,
remote computeds) are correct today only because capturing a peer transitively pins its
generation. A mutable peer would force explicit epoch capture at each of those sites.

So the design keeps "new generation → new peer" and moves the generation *inside* the ref.

## Prior art

An older plan (`rpc-peer-ref-refactoring.md`, now removed) proposed replacing the ref with a
`Symbol` identity plus lifecycle/resolver services. From it, this plan adopts one idea: the
route/lifecycle token becomes directly reachable from `RpcPeer` (here: `RpcPeer.Route`,
captured at construction). The `Symbol` direction is rejected: refs must remain subclassable
stateful handles — with stable refs they gain a second responsibility, owning route resolution
(`CreateRoute()`), which a string identity cannot carry. Dual equality also stays: server refs
need address equality for WebSocket reconnect takeover; mesh refs stay referential.

## Design

### RpcRoute — one route generation of a ref

Replaces `RpcRouteState` and absorbs the per-generation *resolved target* data that currently
lives on ref subclasses (`RpcShardPeerRef.HostId`, ActualChat's `ResolvedMeshRef`):

```csharp
public class RpcRoute
{
    public RpcRef Ref { get; }                    // Owner
    public int Version { get; }                   // Per-ref counter; logging/debugging only
    public RpcPeerConnectionKind ConnectionKind { get; init; } // Resolved per generation!
    public CancellationToken ChangedToken { get; }
    public Task WhenChanged { get; }
    public Func<bool, CancellationToken, ValueTask>? LocalExecutionAwaiter { get; set; }

    public void MarkChanged();                    // Same semantics as RpcRouteState.MarkChanged
    public void ThrowIfChanged();
}
```

- Not sealed: mesh implementations subclass it to carry resolved-target data
  (host id, node, endpoint), which the URL resolver and logging read per generation.
- `RpcRouteStateExt` becomes `RpcRouteExt` with the same members
  (`IsChanged`, `RerouteIfChanged`, `WhenChanged(ct)`, `PrepareLocalExecution`,
  `MustConvertToRpcRerouteException`).
- `ConnectionKind` moves here from the resolution path: a shard's locality is a property of
  *where it currently maps*, i.e. of the generation. `RpcPeerOptions.ConnectionKindDetector`
  remains only for routeless refs (see below).

### RpcRef — stable identity + route owner

`RpcPeerRef` renamed to `RpcRef`, with these changes:

```csharp
public partial class RpcRef : IEquatable<RpcRef>
{
    // Identity/config — unchanged: Address, IsServer, IsBackend, SerializationFormat,
    // HostInfo, Versions, UseReferentialEquality, equality rules, Initialize()

    public RpcRoute? Route { get; }   // Current generation; null = fixed route (never reroutes)
    public RpcRoute? Reset();         // MarkChanged current route + mint the next one eagerly

    // Protected methods

    protected virtual RpcRoute? CreateRoute();  // Base returns null; mesh subclasses override
}
```

- `Initialize()` calls `CreateRoute()` once; `null` means the ref is permanently routeless —
  this preserves the interceptor's `Route is null` fast path (no rerouting logic engaged).
- The `Route` getter re-mints **lazily**: if the current route `IsChanged()`, it locks and calls
  `CreateRoute()` for the next generation. Lazy minting coalesces a burst of mesh-state churn
  into a single re-resolution — something the current eager ref-per-version model can't do.
- Watcher tasks that call `MarkChanged()` (today spawned in `RpcShardPeerRef` /
  `MeshRpcPeerRef` constructors) move into `CreateRoute()` overrides — each generation gets
  its own watchers, which complete when they fire.
- `ToString()` stays stable (the Address); route version/target info surfaces via
  `RpcRoute.ToString()` and `RpcPeer.ToString()` so overlapping generations remain
  distinguishable in logs (replaces the `"<rerouted>"` prefix and `HostInfo` version suffixes).

### RpcPeer — captures its generation

- New property `RpcRoute? Route`, assigned in the constructor from the value `RpcHub.GetPeer`
  resolved. **Every** `peer.Ref.RouteState` read in the pipeline becomes `peer.Route`:
  `WhenConnectedOrReroute`, `OnRun`'s dispose-on-change registration, `RpcInterceptor`,
  `RpcOutboundCall.IsPeerChanged`/`SetMustRerouteError` path, `RpcCallTrackers.TryReroute`,
  `RpcOutboundContext`, `RemoteComputeMethodFunction`, `RpcCommandHandler`.
  This is the correctness-critical rule: with stable refs, `ref.Route` is the *current*
  generation, while a draining peer must keep observing *its own*.
- `ConnectionKind = Route?.ConnectionKind ?? Options.ConnectionKindDetector.Invoke(peerRef)`.
- `RpcPeerOptions.PeerFactory` gains the route:
  `Func<RpcHub, RpcRef, RpcRoute?, RpcPeer>` — passing it explicitly avoids a race between
  `GetPeer`'s route read and the constructor re-reading `ref.Route`.

### RpcHub.GetPeer — generation check + entry replacement

```csharp
public RpcPeer GetPeer(RpcRef rpcRef)
{
    var route = rpcRef.Route; // Re-mints if changed
    if (Peers.TryGetValue(rpcRef, out var peer) && ReferenceEquals(peer.Route, route))
        return peer;

    lock (Lock) {
        // Double-check, then:
        peer = PeerOptions.PeerFactory.Invoke(this, rpcRef, route);
        Peers[rpcRef] = peer; // Replaces a stale entry, if any
        peer.Start(isolate: true);
        // Dispose-on-route-change registration — same as today, but on peer.Route
        return peer;
    }
}
```

- `Peers` stays `ConcurrentDictionary<RpcRef, RpcPeer>`. A stale peer is *replaced* in the
  dictionary rather than coexisting under its own key; it stays alive off-dictionary while
  draining (disposal, `TryReroute`, `PeerRemoveDelay`) — nothing looks it up by ref anymore,
  which matches today's behavior where nothing looks up an old-generation ref.
- `RemovePeer` switches to pair-removal semantics
  (`Peers.TryRemove(KeyValuePair.Create(peer.Ref, peer))`) so a drained old peer never
  evicts its replacement.

### What app code looks like after

The sample's two-class cache ceremony collapses to:

```csharp
public sealed class RpcShardPeerRef : RpcRef
{
    private static readonly ConcurrentDictionary<ShardRef, RpcShardPeerRef> Cache = new();

    public static RpcShardPeerRef Get(ShardRef shardRef)
        => Cache.GetOrAdd(shardRef, static key => new RpcShardPeerRef(key));

    protected override RpcRoute? CreateRoute()
    {
        var meshState = MeshState.State.Value;
        var hostId = meshState.GetShardHost(ShardRef)?.Id;
        var route = new MeshRoute(this, hostId) {
            ConnectionKind = hostId == OwnHostId
                ? RpcPeerConnectionKind.Local
                : RpcPeerConnectionKind.Remote,
        };
        // Spawn the watcher that calls route.MarkChanged() when hostId's mapping changes
        return route;
    }
}
```

No versions, no `IsChanged` double-checks, no re-minting races in app code — the class of bug
recently fixed in actual-chat#4049 lives exactly in that removed layer. ActualChat's
`MeshRpcPeerRefs.Get` similarly becomes a plain `GetOrAdd`, and `ResolvedMeshRef` becomes the
`MeshRoute` payload.

## Reuse

**Existing abstractions to reuse:**

- `RpcRouteState` / `RpcRouteStateExt` — absorbed into `RpcRoute` / `RpcRouteExt` nearly
  verbatim (latch, `LocalExecutionAwaiter`, `PrepareLocalExecution`, OCE conversion).
- The whole reroute pipeline stays untouched: `RpcRerouteException`,
  `RpcOutboundCallTracker.TryReroute`, `RpcOutboundContext.PrepareReroutedCall`,
  `RpcInterceptor.InvokeWithRerouting`, `RemoteComputeMethodFunction.ProduceComputedImpl`
  reroute loop, peer disposal + `PeerRemoveDelayProvider` drain.
- `TaskCompletionSourceExt.New<T>()` for the `WhenChanged` source (as today).
- `LazySlim` — no longer needed in the sample cache (plain `GetOrAdd` suffices since refs
  are stable), but remains available where construction races matter.
- No fitting existing abstraction was found for the "lazily re-minted generation" holder
  itself — hence `RpcRoute`.

**Reusability of new components:**

- `RpcRoute` / `RpcRouteExt` — inherently RPC-specific (peer connection kinds, reroute
  exceptions, outbound-call semantics). They belong in `ActualLab.Rpc` next to `RpcRef`;
  promoting to `ActualLab.Core` would drag RPC types into Core. Recommendation: `ActualLab.Rpc`.
- No other new components are introduced.

## Phases

Each phase should build green and pass `ActualLab.Fusion.sln` tests before the next starts.

### Phase 1 — Mechanical rename: RpcPeerRef → RpcRef

Pure rename, no behavior change:

- Type + files: `RpcPeerRef.cs` → `RpcRef.cs`, `RpcPeerRef.Static.cs` → `RpcRef.Static.cs`,
  `RpcPeerRefExt` → `RpcRefExt`, `Internal/RpcPeerRefAddress` → `Internal/RpcRefAddress`.
- Identifiers: `peerRef` parameters/locals → `rpcRef` (or `@ref` where unambiguous),
  `Errors.ClientRpcPeerRefExpected` → `ClientRpcRefExpected` (and Server/Backend variants),
  `RpcWebSocketServerPeerRefFactory`-style delegate names in Server/Server.NetFx projects.
- `RpcPeer.Ref` property name stays.
- Update `docs/api-index.md` / `docs/api-index-full.md` and doc pages referencing `RpcPeerRef`.

### Phase 2 — RpcRoute replaces RpcRouteState; RpcPeer.Route

Still no semantic change to routing:

- Rename/extend `RpcRouteState` → `RpcRoute` (add `Ref`, `Version`, `ConnectionKind`);
  `RpcRouteStateExt` → `RpcRouteExt`.
- `RpcRef.RouteState` → `RpcRef.Route` (still assigned by subclass constructors at this phase).
- Add `RpcPeer.Route`, captured at construction; convert all `peer.Ref.RouteState` /
  `Ref.RouteState` readers to `peer.Route` (full list in Design → RpcPeer above).
- `RpcPeerOptions.PeerFactory` gains the `RpcRoute?` parameter.

### Phase 3 — Stable refs: CreateRoute + lazy re-mint + GetPeer replacement

The semantic core:

- `protected virtual RpcRoute? CreateRoute()`; `Initialize()` mints the first route;
  `Route` getter re-mints under a lock when the current route is changed; add `Reset()`.
- `RpcHub.GetPeer` generation check + stale-entry replacement; `RemovePeer` pair-removal.
- `RpcPeer.ConnectionKind` prefers `Route.ConnectionKind`.
- Migrate in-repo mesh test infrastructure
  (`tests/ActualLab.Fusion.Tests/MeshRpc/Infrastructure/ShardPeerRef.cs` and its users)
  from ref-per-version to `CreateRoute()`.

### Phase 4 — Tests

Beyond keeping the existing `MeshRpc` and Rpc test suites green:

- Route re-mint: `MarkChanged` → next `GetPeer` returns a new peer for a new route; old peer
  reroutes in-flight calls (exists partially; extend for stable-ref semantics).
- Churn coalescing: N rapid mesh changes with no interleaved calls yield one new generation.
- Local↔remote flip on a *stable* ref: shard moves to/from the own host across a reset;
  compute-method invalidation and `LocalExecutionAwaiter` gating still fire.
- `GetPeer` race: concurrent `GetPeer` + `Reset` never returns a peer whose route is already
  changed-and-replaced (may return the draining peer, as today — calls get rerouted).
- Versions ignored in identity: two generations of one ref are never equal-by-address confused
  in `Peers` (pair-removal test).

### Phase 5 — Follow-ups (separate repos)

- `ActualLab.Fusion.Samples`: rewrite `MeshRpc` sample's `RpcShardPeerRef`/`RpcHostPeerRef`
  per the sketch above.
- `ActualChat`: see the dedicated section below — it's the production consumer of this API,
  and its constraints shaped the design, so the agent executing Phases 1–4 must keep it in
  view even though the ActualChat changes land in a separate repo/PR.

## ActualChat migration (`/proj/ActualChat`, follow-up PR)

All code is under `src/dotnet/Core.Server/Rpc` unless noted. Current model: `MeshRpcPeerRefs`
mints a new versioned `MeshRpcPeerRef` per route change (double-checked lock on
`RouteState.IsChanged()`); `ResolvedMeshRef` snapshots the shard→node resolution;
per-ref watcher tasks call `RouteState.MarkChanged()`.

### MeshRpcPeerRef → stable `RpcRef` subclass

- Drop the `Version` field and the `Owner`-mediated re-minting; one instance per normalized
  `MeshRef`, forever.
- `CreateRoute()` override:
  - `ShardRef` refs: return a `MeshRoute` (see below) — these are reroutable, as today.
  - `NodeRef`-only refs: return `null` — routeless, matching today's "no `RouteState`" rule
    (a node ref never remaps; node death is terminal, handled by the URI resolver).
- The three constructor-spawned behaviors move into `MeshRoute`/`CreateRoute()`:
  `MarkChangedWhenResolvedChanged`, `MarkChangedWhenShardOwnershipEnds`,
  `GetLocalExecutionAwaiter` (shard-ownership gating + `Computed` dependency). Watchers bind
  to the route's `ChangedToken` so each generation cleans up after itself.

### ResolvedMeshRef → `MeshRoute : RpcRoute`

`ResolvedMeshRef` is absorbed: `Node`, `NodeRef`, `ShardState`, and the `ConnectionKind`
resolution (including the `Node.Endpoint == ThisNode.Endpoint` equality branch) become
`MeshRoute` members, resolved once at mint time from `MeshState.LastNonErrorValue`.
Its `Latest`/`IsChanged`/`WhenChanged` trio becomes the mint step plus the watcher predicate.

### MeshRpcPeerRefs → plain cache

Collapses to a `ConcurrentDictionary<MeshRef, MeshRpcPeerRef>.GetOrAdd` keeping only the
`MeshRef` normalization logic. The `_lock`, version counter, and `IsChanged` double-check
disappear — that re-minting layer is exactly where the actual-chat#4049 class of races lived.
`RpcPeerRefs` enumeration property stays for diagnostics.

### RpcBackendHelpers (`Internal/RpcBackendHelpers.cs`)

- `GetConnectionUri(RpcClientPeer peer)`: read `peer.Route as MeshRoute` instead of
  `peer.Ref is MeshRpcPeerRef` + `peerRef.RouteState`:
  - Routed (shard) peers: dead/missing node → `null` URI (peer waits; route change reconnects
    it) — unchanged semantics, now keyed off the peer's own generation.
  - Routeless (node) peers: still re-check the *latest* node state via `MeshState` and throw
    `RpcReconnectFailedException` when the node is dead (terminal) — this logic stays on the
    ref/resolver since there's no route to carry it.
- `RouterFactory`: unchanged except type names; it keeps returning the cached stable ref.

### Smaller call sites

- `RpcHostBuilder.cs:271`: registration unchanged besides type names.
- `Sharding/MeshRefResolvers.cs`: comment/type references only.
- `Chat.Service/ShardRoutingMonitor.cs`: resolves `MeshWatcher` via `MeshRpcPeerRefs` only —
  switch to resolving `MeshWatcher` directly. The probe logic itself is untouched and serves
  as the production canary for exactly the invariants Phase 4 tests cover (routing correctness
  + no frozen computeds on former shard owners).
- `Chat.Service/DiagnosticsBackendLocal.cs`: same mechanical type-name updates.

### Tests to re-baseline

- `tests/Core.Server.IntegrationTests/Mesh/MeshWatcherTest.cs`
- `tests/Core.Server.IntegrationTests/Sharding/ShardMigrationComputedTest.cs` — the key
  regression suite for shard migration + computed invalidation; must pass unmodified in
  behavior (only type/property renames), since the new model claims semantic equivalence.

## Files affected (this repo)

- `src/ActualLab.Rpc`: `RpcPeerRef.cs`, `RpcPeerRef.Static.cs`, `RpcPeerRefExt.cs`,
  `Internal/RpcPeerRefAddress.cs`, `RpcRouteState.cs`, `RpcRouteStateExt.cs`, `RpcPeer.cs`,
  `RpcClientPeer.cs`, `RpcServerPeer.cs`, `RpcHub.cs`, `Internal/Errors.cs`,
  `Configuration/Options/RpcPeerOptions.cs`, `Configuration/Options/RpcOutboundCallOptions.cs`,
  `Configuration/RpcMethodDef.Outbound.cs`, `Configuration/RpcLocalExecutionMode.cs`,
  `Infrastructure/RpcInterceptor.cs`, `Infrastructure/RpcOutboundCall.cs`,
  `Infrastructure/RpcOutboundContext.cs`, `Infrastructure/RpcCallTrackers.cs`,
  `Infrastructure/RpcSystemCalls.cs`, `Middlewares/RpcRouteValidator.cs`,
  `Testing/RpcTestClient.cs`, `Testing/RpcTestConnection.cs`
- `src/ActualLab.Rpc.Server` + `src/ActualLab.Rpc.Server.NetFx`:
  `RpcWebSocketServerDefaultDelegates.cs`, `RpcHttpServerDefaultDelegates.cs`
- `src/ActualLab.Fusion`: `Client/Interception/RemoteComputeMethodFunction.cs`,
  `Client/Internal/RpcOutboundComputeCall.cs`
- `src/ActualLab.CommandR`: `Rpc/RpcCommandHandler.cs`
- `tests/ActualLab.Fusion.Tests/MeshRpc/**`, `tests/**/RpcTestBase.cs` and other
  `RpcPeerRef` users under `tests/`
- `docs/`: api indexes + pages mentioning `RpcPeerRef`/`RpcRouteState`

## Risks & open questions

1. **Missed `Ref.RouteState` → `peer.Route` conversion** is the main correctness risk: a
   draining peer reading the ref's *current* route would consider itself healthy and never
   reroute its calls. Mitigation: after Phase 2, `RpcRef.Route` should be unreadable from
   pipeline code paths that hold a peer (grep-audit; consider making the getter
   `EditorBrowsable(Never)` off-limits by convention in `src/ActualLab.Rpc` internals).
2. **`CreateRoute()` is a virtual call during construction** (from `Initialize()`), same
   pattern the current subclasses already rely on (heavy work in constructors). Subclasses
   must set their fields before calling `Initialize()` — document on the member.
3. **Watcher lifetime**: per-generation watchers must exit once their route is changed
   (bind them to `ChangedToken`), or stale watchers accumulate across generations.
4. **Server refs**: routeless and address-equal — must be entirely unaffected. Phase 3's
   `GetPeer` changes are no-ops for `Route is null` refs; verify with WebSocket reconnect
   takeover tests.
5. **Public API break** is accepted (pre-1.0 semantics). Optional: keep
   `[Obsolete] RpcPeerRef : RpcRef` and `[Obsolete] RpcRouteState : RpcRoute` shims for one
   release; default is a clean break — decide before Phase 1.
6. **Eager vs lazy first mint**: `Initialize()` mints eagerly (needed to know routeless vs
   routed); *re*-mints are lazy. If a ref sits unused across many mesh changes, its first
   post-idle `Route` read resolves from fresh state — desired behavior.
