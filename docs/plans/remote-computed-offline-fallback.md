# Remote computed: offline fallback primitives

Two additions to the client side of remote compute calls, so that an app can keep rendering from
its `IRemoteComputedCache` while the RPC peer is disconnected:

1. `RemoteComputedCacheMode.ReturnDefault` - a cache mode that behaves like `Cache`, except that
   the "cached" value is always `default(T)` and nothing is ever read from or written to the cache.
2. A reconnect timeout for in-flight remote compute calls - when the peer disconnects mid-call,
   wait up to a configurable time for it to come back, then abandon the call and restart the
   computation from the cache path.

The consumer is Voxt (ActualChat). Its read path is inventoried in
`D:\Projects\ActualChat\docs\architecture\offline-render-path.md`, and the plan these two items
unblock is `D:\Projects\ActualChat\docs\plans\offline-mode.md`. Read both for the "why"; this
document is the "what", written against `master` at `2aa76ab3d` (v14.3.36). Line numbers below
are from that commit.

Follow the repo's `CLAUDE.md` / `AGENTS.md`. Deliver as a PR against `master`, not a commit to it.

## How the client works today

All in `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs` unless noted.

- `ProduceComputedImpl` (:136-143) consults the cache only when there is **no existing computed**
  for the input and `GetCache` (:487-490) returns non-null, i.e. the method's
  `RemoteComputedCacheMode == Cache` **and** an `IRemoteComputedCache` is registered:
  ```csharp
  return existing is null && cache is not null
      ? await ComputeCachedOrRpc(typedInput, cache, peer, cancellationToken)
      : await ComputeRpc(typedInput, cache, existing, peer, cancellationToken);
  ```
- `ComputeCachedOrRpc` (:263-304): a cache hit returns a consistent computed immediately, with the
  `RpcCacheEntry` attached and no call bound, and forks `ApplyRpcUpdate` (:306-410), which waits for
  the connection (with the method's `ConnectTimeout`, infinite for queries) and then validates the
  entry by hash; a differing value produces a new computed that displaces the cached one.
- `ComputeRpc` (:163-262) has two serve-stale paths, both requiring
  `existing?.CacheEntry` to be non-null:
  - not connected at call time (:171-192) - serve the entry immediately, chain the
    `SynchronizedSource`, `InvalidateWhenReconnected` (:541-548);
  - disconnected while the sent call is pending (:205-234) - `Task.WhenAny(sendTask, WhenDisconnected)`,
    serve the entry on disconnect, same follow-ups. The abandoned outbound call is left to the
    tracker, which resends it on reconnect.
  Without an entry, the call parks on `WhenConnectedCheckedAsync` (:526-537) or on `sendTask`, with
  no timeout, until the peer reconnects.
- `UpdateCache` (:478-484) is the only writer: `Set` on a value, `Remove` on an error result.
- `RemoteComputed.OnInvalidated` (`src/ActualLab.Fusion/Client/RemoteComputed.cs:77-95`) keeps
  `Cache`-mode computeds pseudo-registered so the next call finds `existing` and its entry.
- Mode plumbing: `src/ActualLab.Fusion/RemoteComputedCacheMode.cs` (`Default | Cache | NoCache`),
  `RemoteComputeMethodAttribute.CacheMode`, `ComputedOptions.ClientDefault` (`Cache`,
  `src/ActualLab.Fusion/Configuration/ComputedOptions.cs:16-20`) and the attribute parsing at
  `ComputedOptions.cs:58-91`.
- Timeouts: `RpcCallTimeouts` (`src/ActualLab.Rpc/Configuration/RpcCallTimeouts.cs:11-19`) carries
  `ConnectTimeout / RunTimeout / DelayTimeout`; defaults per method kind in
  `RpcCallTimeouts.Default.cs:12-33` (queries: `None`, i.e. infinite); per-method overrides via
  `[RpcMethod(ConnectTimeout = ..)]` (`src/ActualLab.Rpc/Attributes/RpcMethodAttribute.cs:18-30`)
  merged in `RpcOutboundCallOptions.DefaultTimeoutsProvider`
  (`src/ActualLab.Rpc/Configuration/Options/RpcOutboundCallOptions.cs:39-49`) into
  `RpcMethodDef.OutboundCallTimeouts` (`RpcMethodDef.cs:132`).
- `RpcPeer.WhenConnectedOrReroute(timeout, ct)` (`src/ActualLab.Rpc/RpcPeer.cs:184-214`) throws
  `Errors.ConnectTimeout` (a `TimeoutException`) when the timeout elapses;
  `RpcClientPeer.ReconnectsAt` (`RpcClientPeer.cs:17`, set in `GetConnection`) publishes when the
  next reconnect attempt is due.

## 1. `RemoteComputedCacheMode.ReturnDefault`

### Semantics

Add `ReturnDefault` to the enum. A method in this mode behaves exactly like `Cache` on every path
above, with one substitution: **the cache always "contains" `default(T)`** for every key, and it is
never read or written.

| Path | `Cache` today | `ReturnDefault` |
|---|---|---|
| First call, no existing computed | cache lookup; hit → cached value now + background validation; miss → RPC | always a "hit": `default(T)` now + background validation via `ApplyRpcUpdate` |
| Background validation lands with a value | new computed displaces the cached one, cache written | same displacement; nothing written |
| RPC result is an error | `cache.Remove` | nothing |
| Invalidated, peer not connected | serve the attached entry, invalidate on reconnect | serve `default(T)`, invalidate on reconnect |
| Disconnected mid-call | serve the attached entry | serve `default(T)` |
| `IRemoteComputedCache` not registered | falls back to plain RPC (`cache is null`) | **still works** - the mode needs no store |

The entry attached to a `ReturnDefault` computed is always the default entry, never the last real
value: after the RPC lands, the computed holds the real value, but a later stale serve must yield
`default(T)` again. That is the whole point of the mode - it is for methods whose stale value is
misleading (a live session that ended while offline, who is typing), where "nothing" is the right
thing to show until the peer is back.

`ComputedSynchronizer` semantics are unchanged: a default-served computed is unsynchronized until
its validation lands, exactly like a cache hit.

### Suggested implementation

The least invasive shape is a synthetic entry rather than new branches:

- An internal `IRemoteComputedCache` implementation (e.g. `DefaultValueRemoteComputedCache`,
  singleton) whose `Get(input, key, ct)` returns
  `new RpcCacheEntry(key, <sentinel value>, <default of the method's unwrapped result type>)`,
  and whose `Set` / `Remove` / `Clear` are no-ops. `RpcMethodDef.ResultListType.Factory.Invoke()`
  followed by `Get0Untyped()` yields the typed default without reflection, the same way
  `AppRemoteComputedCache` builds its result list before deserializing.
- `GetCache` returns it for `ReturnDefault` regardless of whether a real cache is registered.
- `UpdateCache` must return the default entry (not a real one) when the cache is the synthetic
  one, so the "always default on stale" rule holds - see the table.
- The sentinel `RpcCacheValue` must never compare equal to a real value in
  `HashOrDataEquals` (`ApplyRpcUpdate` step 7, :391-395), so validation always displaces the
  default. Sending `RpcCacheEntry.RequestHash` semantics for its hash is fine.
- Every `== RemoteComputedCacheMode.Cache` check becomes "is caching" (`Cache` or `ReturnDefault`):
  at least `GetCache` and `RemoteComputed.OnInvalidated`. Grep for the enum to find the rest,
  including the metrics in `FusionInstruments`.
- Attribute and options plumbing: `RemoteComputeMethodAttribute.CacheMode = ReturnDefault` must
  survive `ComputedOptions` resolution; `Default` still resolves to `ClientDefault`.

### Tests

Extend the existing harness (`tests/ActualLab.Fusion.Tests/Rpc/FusionRpcServeStaleTest.cs`,
`Services/ServeStaleTester.cs`, plus `KeyValueServiceWithCacheTest.cs` /
`RemoteComputedCacheMetricsTest.cs` for cache interaction) with a `ReturnDefault` method:

- First call returns `default` synchronously-fast and is not `IsSynchronized`; the real value
  displaces it once the peer answers; the computed is then synchronized.
- With the peer disconnected, an invalidated computed re-serves `default`, and is invalidated on
  reconnect.
- A registered `IRemoteComputedCache` never sees `Get` / `Set` / `Remove` for the method (assert
  on a counting cache).
- Works with no `IRemoteComputedCache` registered at all.
- `MinCacheDuration` and pseudo-registration behave as for `Cache`.

## 2. Reconnect timeout with restart-from-cache

### Semantics

A remote compute call whose peer is not connected - at call time, or because the connection
dropped while the call was pending - **waits up to `ReconnectTimeout` for the peer to reconnect**.
If it reconnects in time, the call proceeds as today (the tracker resends it, the fresh value is
returned). If it doesn't, the call is abandoned and the computation **restarts on the cache path**,
as if there were no existing computed:

- cache entry (or `ReturnDefault`) → the cached value is returned now, chained to the abandoned
  computed's `SynchronizedSource`, and `InvalidateWhenReconnected` refreshes it on reconnect;
- cache miss → nothing to fall back to: keep waiting for the connection (today's behaviour). No
  exception reaches the caller from this feature. (A finite `ConnectTimeout` for cold misses is a
  separate, later change on the consumer side.)

Default value **0**, which reproduces today's behaviour on every path: serve stale immediately
when there is an entry, keep waiting when there isn't. Voxt will set a few seconds so that a
reconnect blip yields the fresh value instead of a stale render followed by a second compute.

One refinement that belongs with it: if the peer *itself* says it won't try to reconnect within
the timeout (`RpcClientPeer.ReconnectsAt - now > ReconnectTimeout`, which is how Voxt parks
reconnects while the OS reports offline), don't wait the timeout out - fall back at once. The same
check makes sense inside `WhenConnectedOrReroute(timeout)` for `ConnectTimeout`, so consider
putting it there (client peers only) rather than in the compute layer.

### Where to put the option

Recommended: **`RpcCallTimeouts.ReconnectTimeout`**, next to `ConnectTimeout / RunTimeout /
DelayTimeout`, with `[RpcMethod(ReconnectTimeout = ..)]` for per-method overrides and the usual
merge in `DefaultTimeoutsProvider`, so a consumer can set it once via
`RpcCallTimeouts.Default.Query`. It is a property of the peer connection, like `ConnectTimeout`,
and the timeouts record is where every other connection-related deadline already lives.
**Enforcement stays in `RemoteComputeMethodFunction`** for now - only remote compute calls know
how to "restart from cache"; plain RPC calls keep their current behaviour and simply ignore the
field until someone gives them a meaning for it.

Alternative, if keeping `ActualLab.Rpc` untouched matters more: `RemoteComputeMethodAttribute.ReconnectTimeout`
→ `ComputedOptions`, resolved like `CacheMode`. Either is fine; pick one and say why in the PR.

### Mechanics in `ComputeRpc`

The disconnect race already exists for the entry case (:205-234); generalize it:

1. Race `sendTask` against `peer.ConnectionState.Value.WhenDisconnected` **whether or not** there is
   an entry.
2. On disconnect, `await peer.WhenConnectedOrReroute(ReconnectTimeout, ct)` (the `ReconnectsAt`
   refinement above applies). Reconnected → keep awaiting `sendTask`; the tracker resends. A
   `RpcRerouteException` propagates as today.
3. Timed out → abandon the call and restart: the cleanest form is an internal signal caught in
   `ProduceComputedImpl`, which then runs `ComputeCachedOrRpc(input, cache, peer, ct)` (or, when
   `cache is null` - `NoCache` and no store - `ComputeRpc` with `existing: null`, which parks on
   the connection as today). Don't recurse from inside `ComputeRpc`; keep the restart at the top
   of the loop that already handles reroutes (:145-161).
4. The not-connected-at-call-time branch (:171-192) gets the same wait before serving the entry,
   so both branches obey one rule.
5. Chain the abandoned computed's `SynchronizedSource` to whatever the restart produces, and
   `InvalidateWhenReconnected` the fallback computed - both as the current stale paths do.
6. **Abandon the outbound call properly.** Today's mid-call stale path leaves the sent call in the
   tracker, which resends it on reconnect; its result is then ignored and the server-side
   `RpcInboundComputeCall` stays subscribed until the client-side call object is collected. Check
   what that costs and, if it leaks, complete/unregister the abandoned call instead of letting it
   resend - the restart issues its own call. Whatever the answer, the same treatment should apply
   to the existing stale path.
7. `ApplyRpcUpdate` is unchanged: it already has a value to show and must keep parking until
   connected, however long that takes. Make that explicit (`TimeSpan.MaxValue` rather than the
   method's `ConnectTimeout`) so a consumer can later make query `ConnectTimeout` finite without
   invalidating cached values.

### Tests

In `FusionRpcServeStaleTest` (the existing `MidCallDisconnectStaleComputedMustSynchronizeTest`
is the template) and `ComputedSynchronizerTest` where synchronization is asserted:

- `ReconnectTimeout = 0`: the existing tests pass unchanged.
- Disconnect mid-call, reconnect within the timeout: the caller gets the **fresh** value, no stale
  computed is produced, no `RemoteComputedCacheStaleValueCount` increment.
- Disconnect mid-call, no reconnect: after about the timeout the stale entry is served, the
  computed is unsynchronized, and it is invalidated when the peer finally reconnects.
- Same two cases for a `ReturnDefault` method (default served) and for a cold miss (still pending
  after the timeout, no exception, completes on reconnect).
- Not connected at call time, with an entry: served after the timeout, not before; with the
  `ReconnectsAt` refinement, served immediately when the delayer parks the reconnect.
- The abandoned call is not resent after reconnect (or, if resend is kept, the server-side inbound
  call completes rather than lingering) - assert on the peer's outbound/inbound call counts.

## Consumer notes (not part of this PR)

Voxt will use the two primitives as follows, which is what the semantics above are tuned for:

- `ReturnDefault` on `ILiveSessions.*`, `ILiveVideoStreams.*`, `IChatTypingActivities.ListTypingAuthorIds`,
  `ISystemProperties.GetServerApiInfo` and the other methods currently marked `NoCache` - every
  one of them today parks the chat view or a list row while offline.
- `RpcCallTimeouts.Default.Query = new RpcCallTimeouts(...) { ReconnectTimeout = TimeSpan.FromSeconds(3..5) }`
  in `ClientStartup`, and `Peer.Disconnect()` on OS-offline so the timeout starts counting at once.
- A Fusion version bump on the Voxt side needs the AOT helper regeneration and a clean rebuild of
  the WASM/MAUI apps, so the release should be a NuGet one with a changelog entry, as usual.

## Checklist

- [ ] `RemoteComputedCacheMode.ReturnDefault` with the semantics table above, no store required.
- [ ] `ReconnectTimeout` option (placement stated in the PR), enforced in `RemoteComputeMethodFunction`,
      default 0, `ReconnectsAt`-aware.
- [ ] `ApplyRpcUpdate` waits without a timeout.
- [ ] Abandoned-call handling decided and applied to the existing stale path too.
- [ ] Tests listed under both items; the whole `ActualLab.Fusion.Tests` suite green.
- [ ] Docs: update whatever page documents `RemoteComputedCacheMode` and `RpcCallTimeouts`
      (grep `docs/` for both), and add a changelog entry for the next NuGet version.
