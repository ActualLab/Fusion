# Adversarial verification — single-source HIGH claims (round 2)

Method: each claim was attacked, not defended. Where an experiment was cheap it was run
against **the actual HEAD source** (`ee263d814`) via a dedicated git worktree
(`D:\Projects\ActualLab.Fusion-verify-r2`, branch `review-r2-verify`), not against NuGet.
Repro projects: `D:\Projects\ActualLab.Fusion\tmp\verify-r2\{V1,V2,V3}\`.

| Claim | Verdict | Claimed | Corrected |
|-------|---------|---------|-----------|
| V1 auth identity secrets on the wire | CONFIRMED (mechanics) | HIGH | **MEDIUM** |
| V2 MessagePack `TrustedData` | PARTIALLY CONFIRMED | HIGH | **MEDIUM** |
| V3 type-cache keys alias the transport buffer | CONFIRMED (both halves) | HIGH | **HIGH** |
| V4 inbound compute calls accumulate | CONFIRMED (facts) | HIGH | **MEDIUM** |
| V5 shared remote computed cache / key has no peer | PARTIALLY CONFIRMED | HIGH | **LOW–MEDIUM** |

---

## V1 — `IAuth.GetUser(Session)` ships identity secrets to the client

**Verdict: CONFIRMED (mechanics), severity HIGH → MEDIUM.**

### What is actually stored, and does it reach the wire

`UserIdentity` → value pairs: the **key** is the identity (`UserIdentity.Id`, i.e.
`"<schema>/<schema-bound-id>"`, e.g. `Google/1234567890`); the **value** is the stored
secret. `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbUserConverter.cs:55-57`
maps `ui.Secret` straight into `User.Identities`, and
`src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbUserIdentity.cs:19` is the
`Secret` column it comes from.

The reviewer was right to be suspicious about the "JSON-compatible property" hedge, and
right on the outcome. `User.Identities` is `[JsonIgnore, Newtonsoft.Json.JsonIgnore,
IgnoreDataMember, MemoryPackIgnore, IgnoreMember]`
(`src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs:36-37`) — but the surrogate
`JsonCompatibleIdentities` carries the **full member attribute set for every serializer**:
`[DataMember(Name = nameof(Identities)), MemoryPackOrder(4), Key(4)]` +
`[JsonPropertyName]` + `[Newtonsoft.Json.JsonProperty]`
(`src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs:41-46`). So the value is
transmitted by all four serializers.

Measured (`tmp/verify-r2/V1`, HEAD source, secret `SUPER-SECRET-REFRESH-TOKEN-42`):

```
MemoryPack       bytes= 107 secretInWire=True identityIdInWire=True roundTrip: [Google/1234567890]='SUPER-SECRET-REFRESH-TOKEN-42'
MessagePack      bytes= 109 secretInWire=True identityIdInWire=True roundTrip: [Google/1234567890]='SUPER-SECRET-REFRESH-TOKEN-42'
System.Text.Json len= 117 ... {"id":"u1","name":"Alice","version":0,"claims":{},"Identities":{"Google/1234567890":"SUPER-SECRET-REFRESH-TOKEN-42"}}
Newtonsoft.Json  len= 117 ... {"Id":"u1","Name":"Alice","Version":0,"Claims":{},"Identities":{"Google/1234567890":"SUPER-SECRET-REFRESH-TOKEN-42"}}
```

`ToClientSideUser()` has **zero call sites in `src/`** (only `docs/` mentions it and a test
type re-declares it) — confirmed by repo-wide grep. `DbAuthService.GetUser` returns the
unmasked model at `DbAuthService.cs:184-185`; `InMemoryAuthService.GetUser` at
`InMemoryAuthService.cs:172-173`.

**Client-callable:** yes in the normal server topology.
`FusionBuilderExt.AddAuthService` registers `IAuth` via
`fusion.AddService(typeof(IAuth), implementationType, hasCommandHandlers: false)`
(`src/ActualLab.Fusion.Ext.Services/Authentication/FusionBuilderExt.cs:68`), i.e. with
`RpcServiceMode.Default` → `DefaultServiceMode` (`FusionBuilder.cs:216`), which server apps
set to `Server`/`Distributed`. `IAuth.GetUser` is `[ComputeMethod(MinCacheDuration = 10)]`
(`IAuth.cs:25-26`). `IAuthBackend.GetUser(shard, userId)` — the one that can fetch *other*
users — is **not** RPC-exposed (registered as a plain `AddSingleton` alias,
`FusionBuilderExt.cs:69`), so there is no cross-user read path.

### Why the severity drops

1. **Self-disclosure, not cross-user.** `GetUser(Session)` returns the caller's own user, so
   the recipient of the secret is the account owner. There is no path to another user's
   identity secrets.
2. **Stock Fusion never writes a non-empty secret.** The only in-box producer,
   `ServerAuthHelper.CreateOrUpdateUser`, stores `""`
   (`src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:228-230`).
   An out-of-the-box app leaks nothing but the raw identity id.
3. The leak materialises only for apps that use `DbUserIdentity.Secret` for what it is
   named for (e.g. OAuth access/refresh tokens) — which is a documented-looking use, hence
   still a real trap, not a non-issue.

**Exactly what a client receives today:** `{ Id, Name, Version, Claims,
Identities: { "<schema>/<provider-bound-user-id>": "<stored secret>" } }` — including the
raw provider-bound account id (always) and the secret (whenever the app stores one).

**Minimal fix.** In `User`, stop serializing the secret at all: keep
`JsonCompatibleIdentities`'s *keys* and emit `""` for the values, adding a separate
backend-only DTO for the secret-bearing form. Cheaper interim fix: return
`user.ToClientSideUser()` from `DbAuthService.GetUser(Session)` (`DbAuthService.cs:185`)
and `InMemoryAuthService.GetUser(Session)` (`InMemoryAuthService.cs:173`), plus a
serialization test asserting a stored secret never appears in the `IAuth.GetUser` payload.
Note: the existing `ToClientSideUser()` has its **own** bug reported separately
(it mutates `ApiMap<UserIdentity,string>.Empty`) — fix that first or the interim fix is worse
than the disease.

---

## V2 — MessagePack RPC deserializes in `TrustedData` mode

**Verdict: PARTIALLY CONFIRMED. Severity HIGH → MEDIUM.**

### Confirmed
`MessagePackByteSerializer.DefaultOptions` is built as `new(DefaultResolver)` with no
`.WithSecurity(...)` (`src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:29-41`),
and repo-wide there is no later `WithSecurity` call. Measured against the pinned package
(`MessagePack 3.1.8`, `Directory.Packages.props:71`):

```
DefaultOptions.Security == TrustedData   : True
Security.HashCollisionResistant          : False
```

`msgpack5/5c/6/6c` are in the default accepted format set
(`RpcSerializationFormat.All`, `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:67-73`;
`RpcSerializationFormatResolver.DefaultFormats` returns it verbatim), so a hostile client can
negotiate MessagePack even though `mempack6` is the default.

### Refuted: the depth half
`MaximumObjectGraphDepth` is **500 in both modes** and is enforced regardless:

```
TrustedData:   HCR=False Depth=500
UntrustedData: HCR=True  Depth=500
depth=   400: OK
depth=   600: MessagePackSerializationException / InsufficientExecutionStackException:
              "...object graph that exceeds the maximum depth allowed of 500."
depth=200000: same
```

`WithSecurity(UntrustedData)` would change **nothing** about depth. That half of the claim
is wrong.

### Confirmed but narrower: the hash-collision half
`GetEqualityComparer<string>()` returns .NET's `StringEqualityComparer` in **both** modes
(string hashing is already randomized per process), so string-keyed dictionaries — the
overwhelmingly common case in this codebase (`ApiMap<string,string>` in `User.Claims`,
`PropertyBag`, …) — are unaffected. The difference is confined to unmanaged key types:

```
TrustedData.GetEqualityComparer<int>()   : GenericEqualityComparer`1
UntrustedData.GetEqualityComparer<int>() : CollisionResistantHasherUnmanaged`1
```

With real bucket collisions the amplification is large and easily reached:

```
n=  20000 payload= 119993B: trusted-evil=  250 ms, trusted-random= 0 ms, untrusted-evil= 2 ms
n=  60000 payload= 359997B: trusted-evil= 2127 ms, trusted-random= 1 ms, untrusted-evil= 4 ms
n= 120000 payload= 719997B: trusted-evil= 2160 ms, trusted-random= 3 ms, untrusted-evil= 3 ms
```

≈700× CPU amplification from a 720 KB frame. `ApiMap<K,V> : Dictionary<K,V>`
(`src/ActualLab.Core/Api/ApiMap.cs:9-12`) inherits the same behaviour — the round trip
through the default RPC path installs `GenericEqualityComparer` for `ApiMap<int,int>`.

**Reachability caveat that caps severity:** it needs an RPC contract that actually accepts a
dictionary with an unmanaged (int/long/enum/struct) key. No such contract exists in
`src/`; this is a latent hazard for consuming apps, not an in-box exploitable path.

**Minimal fix.** One line:
```csharp
return _defaultOptions ??= new MessagePackSerializerOptions(DefaultResolver)
    .WithSecurity(MessagePackSecurity.UntrustedData);
```
(`MessagePackByteSerializer.cs:35`). Cost is a slower hash for unmanaged dictionary keys only.
If the perf regression matters, apply `UntrustedData` on the RPC read path
(`RpcByteArgumentSerializerV4`) and keep `TrustedData` for local persistence.

---

## V3 — `ByteTypeSerializer` / `TextTypeSerializer` cache keys alias the transport buffer

**Verdict: CONFIRMED — both halves. Severity HIGH, stands. This is the strongest of the five.**

### Buffer lifetime — the decisive question, settled

The competing reviewer's rebuttal ("`RpcInboundCall` copies/clears `ArgumentData`") is
**wrong**: `RpcInboundCall.cs:102`, `:110`, `:141` assign `context.Message.ArgumentData =
default`, which drops *the message's* reference. It does not copy anything, and it happens
*after* deserialization has already inserted the aliasing key into the static cache.
The chain is zero-copy end to end:
`RpcWebSocketTransport` reads into a pooled `ArrayPoolBuffer` → `RpcFrameCodec` slices it
(`RpcFrameCodec.cs:115-116`, `:140-141`, its own comment says *"ArgumentData is a projection
into our buffer (zero-copy)"*) → `RpcByteMessageSerializerV5.cs:37` `reader.ReadL4Memory(...)`
slices again → `ByteTypeSerializer.ReadItemType` does
`FromBytes(data[..fullLength].AsByteString())` (`ByteTypeSerializer.cs:108`) and
`ByteStringExt.AsByteString(ReadOnlyMemory<byte>)` wraps without copying
(`src/ActualLab.Core/Text/ByteStringExt.cs:17`; `ByteString(ReadOnlyMemory<byte>)`,
`ByteString.cs:55-56`). `RpcWebSocketTransport.cs:187` then calls `buffer.Renew(...)`, which
keeps the *same array* and resets `_position = 0` unless the array is oversized
(`src/ActualLab.Core/Collections/ArrayPoolBuffer.cs:200-209`) — so the next frame overwrites
the exact bytes the dictionary key points at.

Measured directly against HEAD (`tmp/verify-r2/V3`), reflecting on the private static
`ByteTypeSerializer.FromBytesCache`:

```
Resolved type: System.String
Cached key's backing array IS the frame buffer: True (offset=100)

-- transport reuses the frame buffer (overwrite in place) --
Lookup by the ORIGINAL correct bytes still finds the key: False
A key whose content is now 0xAB.. exists in the dictionary: True
  -> TryGetValue(mutatedKeyContent) = False (null)      <-- entry is permanently unreachable
Re-read of the same type marker: cache 1 -> 2 (MISS - a second entry was added)
```

So the entry is not merely stale: it becomes **unreachable garbage that is never evicted**,
and the *correct* marker permanently misses and inserts another aliasing key that will itself
be corrupted by the next frame. Under ordinary (non-malicious) polymorphic traffic —
which includes every CommandR command over RPC — the process-global cache grows monotonically
and never serves a hit for network-sourced markers. This is a plain memory leak in normal
operation, not only an attack.

### Second half: the 2-byte hash is written but never validated

`ToBytes` writes `unchecked((ushort)nameSpan.GetXxHash3L())` at offset 2
(`ByteTypeSerializer.cs:38`); `FromBytes` reads the length at `[0..2]`, **skips `[2..4]`
entirely**, and decodes the name from `[4..length+4]` (`ByteTypeSerializer.cs:52-62`).
Confirmed empirically:

```
5000 forged-hash variants of the SAME type name all resolved to System.String.
Cache count: 2 -> 5002 (delta = 5000)
```

65,536 distinct, permanently-retained cache keys per type name.

### What does *not* work (credit where due)
Unresolvable names are not cached — `TypeRef.Resolve()` throws
(`src/ActualLab.Core/Reflection/TypeRef.cs:61-62`) and `GetOrAdd`'s factory therefore adds
nothing:

```
Unresolvable name threw KeyNotFoundException -> not cached. Count 5002 -> 5002
```

But attacker-chosen **resolvable** names are effectively unbounded — arbitrarily nested
generic instantiations all resolve and all get cached (and each also materialises a permanent,
unloadable runtime `Type`):

```
Attacker-chosen generic instantiations resolved: 200; cache 5002 -> 5202
```

`TextTypeSerializer` (`FromBytes`/`ReadItemType`, `TextTypeSerializer.cs:34-42`, `:75-95`)
has the same aliasing defect; it has no hash field, so only the ×65536 multiplier is absent.

**Minimal fix.** Two independent changes, both small:
1. Never let a network-owned buffer become a dictionary key. In both `FromBytes`
   implementations, look up first and copy only on insert:
   ```csharp
   if (FromBytesCache.TryGetValue(bytes, out var type)) return type;
   return FromBytesCache.GetOrAdd(new ByteString(bytes.Bytes.ToArray()), Resolve);
   ```
   (The `RpcCacheKey` doc comment at `src/ActualLab.Rpc/Caching/RpcCacheKey.cs:12-14`
   already states this ownership contract — `ByteTypeSerializer` simply violates it.)
2. Validate the 2-byte hash in `FromBytes` against `GetXxHash3L()` of the decoded name and
   reject on mismatch; additionally cap `FromBytesCache`/`ToBytesCache` size (or key
   `FromBytesCache` by the decoded name string, which collapses the 65536 variants into one).

---

## V4 — completed inbound **compute** calls stay registered until invalidation

**Verdict: CONFIRMED on every factual sub-question. Severity HIGH → MEDIUM.**

Answers to the four decisive questions:

1. **Does a duplicate `(method, args)` call reuse one registration?** No.
   `RpcInboundCallTracker.GetOrRegister` keys purely on `call.Id`
   (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:59-67`), and `call.Id` is the
   peer-supplied `context.Message.RelatedId` (`RpcInboundCall.cs:52`). N distinct call ids →
   N `RpcInboundComputeCall` objects, N `CancellationTokenSource`s (each registering a
   callback on the shared `peerChangedToken`, so that registration list also grows O(N)),
   N dictionary entries — all pointing at the *same* `Computed`, which they hold via a
   **strong** field (`RpcInboundComputeCall<TResult>.Computed`,
   `src/ActualLab.Fusion/Client/Internal/RpcInboundComputeCall.cs:121`), defeating the
   computed registry's weak-reference eviction.
2. **Any keep-alive/expiry on inbound calls?** None. `RpcCallTracker` has no eviction path,
   and `RpcOutboundCallTracker.Maintain` (`RpcCallTrackers.cs:123-247`) times out **outbound**
   calls only. `RpcLimits` (`src/ActualLab.Rpc/Configuration/RpcLimits.cs`) has no
   inbound-call count or lifetime cap. Unregistration happens only in `ProcessStage2` after
   `await computed.WhenInvalidated(...)` (`RpcInboundComputeCall.cs:94-107`), and
   `ComputedOptions.AutoInvalidationDelay` is `TimeSpan.MaxValue` by default
   (`src/ActualLab.Fusion/Configuration/ComputedOptions.cs:32-33`), so a stable compute method
   never invalidates on its own. (Regular calls are fine — `RpcInboundCall.ProcessStage1Plus`
   unregisters immediately, `RpcInboundCall.cs:201-203`.)
3. **Does the client's disconnect release them?** Not promptly, which is the part that makes
   this more than a design note. `peerChangedCts` fires only on a genuine *peer change*
   (`RpcPeer.cs:369-376`) or on peer stop (`RpcPeer.cs:495-496` → `Reset` →
   `InboundCalls.Clear()`, `RpcPeer.cs:517`). A server peer whose socket simply drops is
   `KeepInboundCallsIncomplete` (`RpcPeer.cs:571-574`) and waits for a reconnect for
   `ServerPeerShutdownTimeoutProvider` = `clamp(peerLifetime × 0.33, 3 min, 15 min)`
   (`src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54-58`, consumed at
   `src/ActualLab.Rpc/RpcServerPeer.cs:78-86`). So connect → register N compute calls →
   abandon → repeat accumulates N × (connections per 3–15 min window).
4. **Anything else bounding it?** No.

### Why the severity drops
This is the intended shape of Fusion's invalidation push: the server *must* retain one
inbound call per live client subscription in order to send the invalidation later. A normal
client legitimately holds thousands. Per-entry cost is small (call object + CTS + dictionary
slot + a shared computed reference). Calling it a "HIGH DoS" over-reads a designed
resource-per-subscription model. The genuine defects are the two missing guardrails.

**Minimal fix.** Add an `RpcLimits.MaxInboundCallCount` (per peer, default generous, e.g.
100k) enforced in `RpcInboundCallTracker.GetOrRegister` — reject with an error result once
exceeded, and log/meter it (the `RpcInstruments.OpenInboundCallGauge` plumbing already exists,
`src/ActualLab.Rpc/Diagnostics/RpcInstruments.cs:78`). Separately, drop retained inbound
compute calls on socket loss after a short grace period rather than holding them for the full
3–15 min reconnect window.

---

## V5 — `SharedRemoteComputedCache.Instance` static + peer-less `RpcCacheKey`

**Verdict: PARTIALLY CONFIRMED. Severity HIGH → LOW–MEDIUM. The "info-leak" framing is not
supported for shipped code.**

### Confirmed facts
- `SharedRemoteComputedCache.Instance` is a mutable `public static` with a public setter,
  and the constructor is `Instance ??= instanceFactory.Invoke()` — non-atomic, unsynchronized,
  first-writer-wins (`src/ActualLab.Fusion/Client/Caching/SharedRemoteComputedCache.cs:12-18`).
  A second DI container's cache instance is silently discarded, and its `RemoteComputedCache`
  is a `RpcServiceBase` bound to the *first* container's hub — so the first container's
  `ArgumentSerializer` and `AnyMethodResolver` are used to decode entries for the second
  (`src/ActualLab.Fusion/Client/Caching/RemoteComputedCache.cs:30-31`, `:98-99`, `:117`).
  That is a cross-container correctness hazard in its own right.
- **No part of the key incorporates the peer or the route.** The only producer is
  `Key ??= new RpcCacheKey(context.MethodDef!.FullName, message.ArgumentData)`
  (`src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:72`); equality compares only `Name` +
  `ArgumentData` (`src/ActualLab.Rpc/Caching/RpcCacheKey.cs:41-45`). The lookup likewise
  ignores `input` — `RemoteComputedCache.Get(input, key, ct)` forwards only `key` to
  `GetImpl` (`RemoteComputedCache.cs:66-76`, `:94-99`).
- **A cache hit is returned to the caller before any server validation.** `ComputeCachedOrRpc`
  builds `cachedComputed` from the cache entry and `return cachedComputed;` at
  `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:288-303`, firing
  `ApplyRpcUpdate` on a detached execution context. The entry path is
  `RemoteComputeMethodFunction.cs:138-142` (`existing is null && cache is not null`).

### Why the conclusion does not follow
The claim's payoff — *"one server's cached value can be served to the other provider"* —
requires the router to map the same `(service, method, args)` to **different** peers with
**different** data. Fusion's default routing is a pure function of the call, so the key
already determines the peer; the only in-repo consumer of the shared cache, the `MeshRpc`
sample (`ActualLab.Fusion.Samples/src/MeshRpc/Host.cs:33-42`, `:91`), deliberately shares one
cache across many in-process hosts precisely because routing is deterministic — no leak there.
Reaching the claimed outcome needs an app to write a peer-dependent (e.g. tenant- or
session-routed) `RouterFactory` **and** opt into a cache. It is a real design gap, not a
shipped information disclosure.

Also note the static is not the load-bearing part: the plain (non-shared)
`IRemoteComputedCache` is a per-container singleton, and a single container can already hold
several client peers — so the missing peer namespace exists on the default path too. The
static only widens the blast radius across containers.

**Minimal fix (in priority order).**
1. Namespace the key by route: extend `RpcCacheKey` with the peer/route key (or hash
   `peer.Ref` into `Name` at `RpcCacheInfoCapture.cs:72`), keeping the Nerdbank converter
   (`src/ActualLab.Serialization.NerdbankMessagePack/Internal/RpcCacheKeyNerdbankConverter.cs`)
   and the TS client in lockstep — the wire-format note at `RpcCacheKey.cs:9-11` calls this out.
2. Make `SharedRemoteComputedCache.Instance` set-once-and-throw (or `Interlocked.CompareExchange`
   + log) instead of silent first-writer-wins, and document that all sharers must target the
   same server topology.
3. Optionally gate serve-before-validate behind an opt-in for peer-dependent routers.

---

### Artefacts
- Worktree: `D:\Projects\ActualLab.Fusion-verify-r2` (branch `review-r2-verify`, at `ee263d814`).
- Repros: `tmp/verify-r2/V1` (serializer wire dump), `tmp/verify-r2/V2` (MessagePack security /
  depth / collision timings), `tmp/verify-r2/V3` (type-cache aliasing + forged-hash growth).
- Nothing outside `tmp/review-r2/` and `tmp/verify-r2/` was modified; the main working tree was
  never built.
