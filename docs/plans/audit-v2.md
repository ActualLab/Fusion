# Audit v2 — ActualLab.Fusion security & severe-bug review

Branch: `feat/audit-v2`. Predecessor: [`audit-v1.md`](audit-v1.md) (the library-wide
correctness audit; this one is scoped to security and severe bugs, and was reviewed
twice independently — see **Method**).

## Fix status (updated 2026-07-28)

**Both CRITICAL findings and all 35 HIGH findings are closed** — fixed, deliberately
rejected, or recorded as known-open with the reasoning. The MEDIUM (60) and LOW (23)
tiers are untouched apart from the handful that fell out of the HIGH work.

### Fixed

| Tier | Findings |
|---|---|
| CRITICAL | H1, F1 |
| HIGH | A1, A2, A5, B1, B2, B3, B4, B5, B6, C1, C3, C4, C5, D1, D3, D4, F2, F3, F6, G1, H2, H3, I1, I6, I7 |
| MEDIUM | A6, A8, B12, C6, D2, F8, H4, I8–I14, I16 |
| LOW | B16, I15 |

Partially fixed:
- **I3** — the fusion-rpc half only. Inbound compute arguments are truncated to the
  declared arity, which closes the cache-poisoning impact. The general arity check in
  `RpcServiceHost.dispatch` is still absent; TS servers were out of scope.

### Closed by decision

Rejected outright — see **Maintainer decisions** at the end:
**A4** (the `CancellationToken.None` claim was simply wrong — `Abort()` faults pending
operations).

Known-open on purpose: **A3**, **B4**'s `int.MaxValue` default, **C2**, **C7**, **E2**,
**F5**, **I2**, **I4**, **I5**, **I15**.

**B3 is now fixed** (see below). The earlier rejection answered B3's *fallback* — "bound
the receive channel" — and not its primary lever, which the rejection itself named:
enforce the `AckAdvance` window and fail the stream. The bounded channel stays rejected
for the reason recorded then; the window check is not that, since it neither blocks nor
fails a write. Both **B3** and **I6** now enforce the same rule.

### Carried forward

1. **The testhost crash.** Pre-existing and unexplained. Not orchestrator-only and not
   any test's logic — `--blame-crash` shows every test `Completed="True"` before the
   abort, so it is teardown/process-exit instability. Nondeterministic, and enabling
   dump capture perturbs timing enough that it stops reproducing. Needs a debugger or
   `procdump -e -ma`, not more re-runs.
2. **A2's residual.** Version churn within a *known* scope still costs an
   O(services × methods) resolver build per cache miss. A CPU cost, not a leak.
3. The MEDIUM and LOW tiers.

**B1's residual is closed.** An earlier fix bounded the `$sys.B` batch slot by
`IReadOnlyList<T>`, which still admitted any covariant read-only collection. That was
the original mistake in miniature — bending the *bound* to satisfy the polymorphism
*trigger*. The trigger is now what changed: `RpcArgumentSerializer.IsPolymorphic` sees
through arrays, so the slot stays `T[]`, which is simultaneously the deserialization
target and the exact bound. Since `RpcSharedStream.Batcher` only ever emits `U[]` for
one runtime `U` per batch, the accepted set now equals the producible set.

### Breaking changes for the changelog

- `?f=njson5` / `njson5np` are denied to clients by default (**B2**). Restore with
  `RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys = ImmutableHashSet<string>.Empty`.
  This is the one place a secure default was chosen over a strictly non-breaking one.
- Session options moved `ImmutableOptionSet` → `PropertyBag` (**C1**); both option types
  are now `[Obsolete]`. `mempack6` tolerates an empty set in both directions, `msgpack6`
  does not even when empty, and populated options are incompatible in every format.
  Legacy `_Sessions.OptionsJson` rows read back as empty — accepted silent data loss.
- TypeScript: `resolveStreamRefs` → `toRpcStream` (**I9**). Auto-conversion is gone;
  callers convert explicitly. Warrants an npm minor bump, not a patch.
- Size ceilings tightened (**A6**, Q5): header and method-ref 64 KiB → 1 KiB, frame
  32 MiB → 16 MiB, argument data 16 MiB → 15.5 MiB, text envelope ~12.2M → 244,297.
- `ApiMap.Empty` / `ApiSet.Empty` removed — they are mutable, so the name could never
  mean what it means elsewhere. Callers use `new()`.
- `Session.ToString()` is redacted to `{4-char prefix}:{Hash}`; `Session.Hash` keeps its
  legacy XxHash3 value because it is on the wire, and a new `Sha256Hash` exposes the
  strong digest.
- An `IBackendCommand` reaching a non-backend peer is now rejected (**H1**) — that gate
  had been decorative since v10.3.

### Two findings that only surfaced because verification was insisted on

- **`HMACSHA256` is a `PlatformNotSupportedException` stub on net5.0 and net6.0
  browser-wasm.** The spec predicted "expected result: fine" and required verification
  anyway. Established by decompiling four runtime packs: those runtimes ship a managed
  hash provider but no managed MAC provider until .NET 7. Had it been assumed, A5 would
  have broken every Blazor WASM 5/6 client's ability to connect — an outage worse than
  the vulnerability. `RpcReconnectProof` spells RFC 2104 out over `SHA256` instead.
- **`RandomNumberGenerator.GetBytes(int)` is net6.0+, not netcoreapp3.0** as the spec
  claimed. It broke the netcoreapp3.1 and net5.0 builds and was caught only by the
  multitargeting build, which CI does not run.

## Headline

**120 findings: 2 CRITICAL, 35 HIGH, 60 MEDIUM, 23 LOW.**
By attribution: **33 B** (both models, independently) · **53 O** (Opus only) ·
**34 C** (Codex only). 14 carry a runnable repro; 7 more went through a dedicated
adversarial verification pass that changed five severities and corrected two root causes.

**Fix these first** — each is confirmed, cheap to fix, and either breaks an
authorization boundary or is reachable pre-authentication:

| | Finding | Why first |
|---|---|---|
| 1 | **H1** — misspelled `IBackendCommand` constant disables the backend-command gate | One-line fix. Exploited end-to-end; dead in every release since v10.3 |
| 2 | **F1** — `IKeyValueStore` exposed as a frontend RPC service | One-word fix (`IBackendService`). Cross-user data read *and*, via H1, write |
| 3 | **C1** — Json.NET `TypeNameHandling.Auto` reachable from the wire | Arbitrary type instantiation; reproduced through the *default* binary formats |
| 4 | **D1** — no `Origin` check on the WebSocket upgrade | Cross-site WebSocket hijacking of the cookie-bound session |
| 5 | **D2** — session ids and client ids logged unredacted on every connection | Bearer credentials into every log sink; enables A5 |
| 6 | **G1** — `RetryPolicy.Apply` uncancellable 100%-CPU spin | Two-line fix; reproduced at 1.9 M iterations in 5 s |
| 7 | **C3** — type-cache keys alias the pooled receive buffer | Grows under *ordinary* traffic, not just attack; survived a direct rebuttal |
| 8 | **A1–A4, B4, B5** — no size, count or concurrency limits anywhere pre-auth | `RpcLimits` has only time-based limits today |

## Method

The codebase was split into 9 partitions. Each partition was reviewed **twice,
independently**, by two different models that could not see each other's work:

| Tag | Reviewer |
|-----|----------|
| **O** | Opus 5, high effort |
| **C** | ChatGPT 5.6 Sol, high effort (via Codex CLI) |
| **B** | **Both**, found independently — treat as high-signal |

A finding tagged **B** was reached twice from different directions; those are the
ones to fix first. A finding tagged **O** or **C** is not weaker evidence *per se*
— many are single-reviewer simply because only one reviewer read that file
closely — but it has had one less pair of eyes.

Reviewers were instructed to verify by tracing a call path from an
attacker-reachable entry point, to mark each finding CONFIRMED or PLAUSIBLE
honestly, and to run experiments only in a git worktree or a `tmp/` repro project
against published NuGet/npm packages. The main working tree was never built or
modified.

Per-reviewer raw reports, the verification write-ups and the reconnect-proof spec live in [`audit-v2/`](audit-v2/).

### Threat model

Untrusted input: everything a remote peer sends over an RPC connection
(handshake, service/method names, call ids, argument buffers, headers, stream
ids, frames); HTTP requests to the server endpoints including the WebSocket
upgrade (query, headers, cookies, `Origin`); session ids and auth tokens; rows
read back from the DB/Redis that originated from client input; and — for both the
.NET and TypeScript clients — everything the server sends.

### Severity

**CRITICAL** RCE, authz bypass, cross-user data exposure, remote crash of the
server process, silent data corruption · **HIGH** pre-auth resource exhaustion,
data reaching the wrong peer, shared-state corruption, reachable deadlock ·
**MEDIUM** broken feature, resource leak over time, weakness needing unusual
preconditions · **LOW** real but minor.

---

## A. RPC transport, peers & connection lifecycle

### A1 · HIGH · **B** — A pre-handshake WebSocket message can pin ~136 MB (allocated as 256 MiB) per connection

`RpcWebSocketTransport.Options.MaxMessageSize` is derived from
`MaxArgumentDataSize` (130 MB) and works out to 142,261,962 bytes. The receive
loop grows a single `ArrayPoolBuffer<byte>` *up to that limit* before it detects
overflow, and `ArrayPoolBufferCapacity.Round` rounds every allocation to the next
power of two — so the array actually reaches **2^28 = 256 MiB**, with a transient
~384 MiB peak during the final `Pool.Resize` (rent-new + copy + return-old).

The endpoint is anonymous (`MapRpcWebSocketServer` attaches no authorization
metadata), and the limit applies *before* the first message is known to be the
tiny handshake. An attacker sends one logical message as an endless series of
continuation frames (`endOfMessage: false`), one byte per second, and holds
hundreds of MB per connection indefinitely. Arrays >1 MiB are not pooled, so each
is a fresh LOH allocation.

- `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:25`, `:110`, `:125`, `:167`
- `src/ActualLab.Core/Collections/ArrayPoolBufferCapacity.cs:27`
- `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`
- Server reuses the client defaults verbatim: `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:90`

**Fix:** enforce a small dedicated first-message/handshake limit before switching
to the negotiated limit; lower the general default by an order of magnitude
(4–16 MiB) and set it explicitly rather than deriving it from
`MaxArgumentDataSize`; close the connection as soon as
`WrittenCount + count` *would* exceed the limit instead of after the limit has
been fully buffered; clamp the power-of-two rounding to the configured maximum.

### A2 · HIGH · **B** — Unbounded, never-evicted method-resolver cache keyed by the remote peer's handshake `VersionSet`

*(Found four times: O-P1, C-P1, O-P2, C-P2.)*

`RpcServiceRegistry._legacyServerMethodResolvers` is a process-wide
`ConcurrentDictionary<VersionSet, RpcMethodResolver>` with no bound, no TTL and no
eviction anywhere in `src/`. Its key is `RpcHandshake.RemoteApiVersionSet` — taken
verbatim from the first message a remote peer sends, pre-auth. Every distinct
value permanently adds the key plus a freshly built `RpcMethodResolver`, and
building each resolver walks every registered service × method.

`VersionSet` is deserialized from a single string bounded only by
`MaxArgumentDataSize` (130 MB), so a handshake can pin tens of MB; alternatively a
stream of cheap connections with distinct tiny version sets leaks steadily and
irreversibly. Entries survive peer disposal because the registry is a singleton.

Aggravating factor (O): `VersionSet.GetHashCode` is a plain **XOR** fold
(`src/ActualLab.Core/Collections/VersionSet.cs:41`). XOR combiners are trivially
collidable, so an attacker can additionally force thousands of distinct keys into
one bucket and degrade every subsequent handshake lookup to an O(n) chain of full
`Equals` comparisons.

- `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:15`, `:110`
- `src/ActualLab.Rpc/RpcPeer.cs:350`, `:563`, `:614`
- `src/ActualLab.Core/Collections/VersionSet.cs:37`, `:41`, `:109`

**Fix:** normalize the incoming set to the scopes the registry actually knows
(unknown scopes cannot affect resolution, so drop them from the key entirely —
all garbage then collapses to one entry); reject sets exceeding a small
count/length bound; bound the cache with an LRU; replace the XOR fold with an
order-independent but non-linear combiner, or seed it per process.

### A3 · HIGH · **B** — An unauthenticated upgrade with a fresh `clientId` pins a server peer for ≥3 minutes

*(Found four times: O-P1, C-P1, O-P4, C-P4.)*

`RpcHub.Peers` is an unbounded `ConcurrentDictionary<RpcRoute, RpcPeer>`. A
server peer — plus a background `WorkerBase` task and four
`ConcurrentDictionary(ProcessorCountPo2, 131)` trackers, order 10 KB of live
state — is created as soon as an anonymous upgrade request arrives, keyed by the
raw `?clientId=` query value. Creation happens at
`RpcWebSocketServer.cs:62`, i.e. **before** `AcceptWebSocketAsync`, before the
handshake, and before any authentication.

When the connection never arrives or drops, `RpcServerPeer.GetConnection` parks on
`ServerPeerShutdownTimeoutProvider`, whose default clamps to a **3-minute
minimum** for a brand-new peer. Repeat with a fresh random `clientId`: at ~1000
req/s the steady state is ~180,000 live peers ⇒ multiple GB ⇒ OOM. There is no
`Peers.Count` cap, no per-IP limit, and no rate limit. Same pattern on the HTTP/2
endpoint and on OWIN.

Secondary amplification (O): each peer emits ~5 log records including a
`LogWarning` per removal, so the same requests also flood the log pipeline.

- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62`; `RpcHttpServer.cs:57`; `.NetFx/RpcWebSocketServer.cs:45`
- `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`
- `src/ActualLab.Rpc/RpcHub.cs:124`, `:138`; `src/ActualLab.Rpc/RpcServerPeer.cs:78`
- `src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54`

**Fix:** create the peer only after the transport exists (move `GetServerPeer`
after `AcceptWebSocketAsync`); give a never-handshaken peer a seconds-scale grace
period rather than the 3–15 minute reconnect window; add a configurable cap on
live server peers and on peers per remote IP, rejecting with 503; validate
`clientId` shape/length before using it as a key.

### A4 · HIGH · **B** — Unbounded outbound write channel with a non-cancellable send — slow-reader memory exhaustion

Every frame-based transport enqueues outbound messages into an **unbounded**
channel and flushes with `WebSocket.SendAsync(..., CancellationToken.None)`. A
peer that completes the handshake and then stops reading (TCP zero-window) parks
the writer loop forever while the channel keeps growing. Nothing applies
backpressure, times the send out, or drops the peer — and the keep-alive watchdog
only checks *inbound* keep-alives, so a peer that keeps sending never trips it.

Because serialization is lazy (`CreateOutboundMessage` stores the `ArgumentList`,
not bytes), each queued message also pins the entire result object graph.
`Ok`/`Error`/stream-item responses are `NoWait` and never registered, so the
outbound-call timeout maintenance does not cover them either. Even a `$sys.Ack`
for a missing object produces a response, giving a cheap response-generating
request.

- `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:34`, `:90`
- `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:73`, `:77`, `:176`
- `src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:28`; `RpcStreamTransport.cs:26`
- `src/ActualLab.Rpc/RpcPeer.cs:419` (read loop never awaits dispatch)

**Fix:** give each transport a bounded message *and byte* budget; on exhaustion
pause inbound dispatch or close the connection with a resource-limit error
(do not spawn one pending `WriteAsync` per overflowed message). Pass a
cancellable token to `WebSocket.SendAsync`. Make the keep-alive watchdog
bidirectional — drop the peer when no frame has been successfully *flushed*
within `KeepAliveTimeout`.

### A5 · HIGH · **B** — The handshake authenticates nothing: `clientId` alone selects (and evicts, and inherits) a server peer

*(Found by O-P1, C-P1, O-P4, O-P2. Severity ranged MEDIUM→HIGH across reviewers;
rated HIGH here on the strength of the takeover path.)*

Which `RpcServerPeer` a connection binds to is decided entirely by the
`clientId` query parameter — `RpcRef` equality is by `Address`, and the address
ends with the raw value. The value is not signed, not bound to the session, and
not checked against anything. `ProcessHandshake` validates only the *shape* of
the handshake; `RemotePeerId`/`RemoteHubId` are accepted verbatim and used only to
decide *whether the remote changed*.

Two consequences:

1. **Unconditional DoS.** Connect with a victim's `clientId`; lines 72–76 call
   `peer.Disconnect(...)` on the victim's live connection. Loop, and the victim
   can never hold a connection. No credential required.
2. **Peer takeover.** `RpcClientPeer.ClientId` is *literally*
   `Id.ToBase64Url()` — the same GUID the client sends as
   `RpcHandshake.RemotePeerId`. So the attacker can reverse the URL-visible
   `clientId` into the victim's `RemotePeerId` and replay it.
   `GetPeerChangeKind` then returns `Unchanged`, which **skips**
   `Reset(Errors.PeerChanged())`. The peer's server-side state survives onto the
   attacker's socket: still-running inbound calls send their results through the
   peer's *current* transport, and `SharedObjects.Maintain` keeps pumping the
   victim's server→client streams to the attacker. Results computed under the
   victim's session are delivered to the attacker.

Degenerate case: an absent `clientId` defaults to `""`, collapsing all such
clients onto one shared server peer.

The `clientId` is CSPRNG-generated and unguessable — the exposure is entirely
"this capability travels in a URL and is logged", which is **D3**.

- `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`
- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:72`
- `src/ActualLab.Rpc/RpcRef.cs:134`; `src/ActualLab.Rpc/RpcClientPeer.cs:20`
- `src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:25`, `:30`; `src/ActualLab.Rpc/RpcPeer.cs:369`, `:541`
- `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:279`, `:286` (resend-on-changed-peer)

**Fix:** make the peer key a function of the `clientId` **and** the connection's
authenticated identity, so a `clientId` from one identity can never select
another's peer — `RpcWebSocketServerRefFactory` is already the designed extension
point, but the *default* must be the safe one. Stronger: mint a random reconnect
token at first handshake, return it in the handshake, require it in a header on
reconnect, and stop deriving `ClientId` from the peer `Id`. Reject empty
`clientId` and bound its length/charset.

### A6 · MEDIUM · **B** — HTTP transports accept outbound payloads their receivers always reject

Pipe and stream transports reject *inbound* frames above 16,000,000 bytes but
apply no `MaxFrameSize` check on the outbound path, while the default serializers
accept 130,000,000 bytes of argument data. A locally-issued call with a 16–130 MB
payload is serialized, written, rejected by the peer with `InvalidItemSize`, and
the connection drops — then reconnect/resend can repeat the same impossible call
until its timeout. `RpcHttpClient` uses pipes by default. (O reported the same
8.5× discrepancy as a note under A1; C reported it as a finding.)

- `src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:26`, `:134`; `RpcStreamTransport.cs:24`, `:145`
- `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:168`, `:176`
- `src/ActualLab.Rpc/Clients/RpcHttpClientOptions.cs:18`

**Fix:** define one effective per-transport limit and enforce it symmetrically
before enqueueing; fail the outbound call locally with a size-limit exception so
it is not retried.

### A7 · MEDIUM · **C** — Disposing a hub schedules uncancellable 5-minute peer-retention tasks

Every client peer schedules an uncancellable delayed removal in its `finally`
(`Task.Run` + `Task.Delay(..., CancellationToken.None)`), including when it stops
*because the hub is being disposed*. The default delay is five minutes, and the
task captures the peer and hub, keeping the disposed RPC graph rooted. A process
that churns service providers/hubs (tests, tenant scopes, app reloads) reports
disposal complete while the graphs remain live.

- `src/ActualLab.Rpc/RpcPeer.cs:498`, `:502`; `src/ActualLab.Rpc/RpcHub.cs:63`
- `src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:66`, `:70`

**Fix:** remove peers immediately when the hub or peer is explicitly disposing;
use a hub-owned cancellable eviction mechanism drained during disposal instead of
one detached `Task.Run` per peer.

### A8 · MEDIUM · **O** — `MaxArgumentDataSize` defaults to 130 MB per message

A single inbound message may carry 130 MB of argument data, which the transport
must buffer before dispatch. Combined with the absence of any concurrent-call
limit (**B4**) this is an easy pre-auth OOM on its own, and it is the multiplier
behind A1, A2 and B9.

- `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`; `RpcTextMessageSerializer.cs:13`

**Fix:** lower the default to a few MB; make large payloads opt-in per
method/service.

### A9 · LOW · **O** — Unbalanced `Lock.Exit()` in `RpcPeer.SetConnectionState`

The `TrySetNext`-failed early return sits inside the `try` and releases the lock
itself, but the `finally` releases it a second time — and also runs the
state-transition side effects (`_transport = …`, `MarkConnected`/
`MarkDisconnected`, reader-token cancel) against a state that was never
installed. Latent: every current call site guards with `RequireNonFinal()`/
`IsFinal`, so no reachable path was found. Any future call site turns it into a
`SynchronizationLockException` thrown from a `finally` in the peer's shutdown
path.

- `src/ActualLab.Rpc/RpcPeer.cs:604`, `:621`, `:653`

**Fix:** move the early return above the `try`, or use a `mustRunEffects` flag
checked at the top of the `finally` with a single `Exit`.

---

## B. RPC call pipeline, routing, streams & access control

### B1 · HIGH · **O** — Stream-batch arguments are deserialized with the expected type widened to `object`, disabling the polymorphic type allow-check

> ✅ **Verified end-to-end** against a live server with a hostile client. The bypass is
> confirmed exactly as described: the widened-`object` path constructed an arbitrary type
> (ctor + setters ran), while the non-widened `<long, Fruit>` control **rejected the
> identical payload**. Note the sink is broader than streams — *any* RPC method with an
> `object` or abstract parameter is a direct sink.

For `$sys.B` (stream batch) with a polymorphic item type, `RpcSystemCalls.IsValidCall`
deliberately replaces the expected argument type `T[]` with `object` so the
argument serializer takes the polymorphic path. The polymorphic readers accept a
wire-supplied type only if `expectedType.IsAssignableFrom(itemType)` — and
`typeof(object)` is assignable from everything. The batch payload therefore has
**no type restriction at all**, unlike every other RPC argument. The
`(T[])items!` cast in `RpcStream<T>.OnBatch` throws only *after* the object has
been constructed.

Reachable in both directions: a hostile client via a stream argument the server
enumerates, a hostile server via a returned stream. With MemoryPack/MessagePack
the effect is arbitrary type resolution + formatter construction driven by a
64 KB attacker string (a `Type.GetType`/assembly-probe primitive). With the
Newtonsoft-backed formats it would be deserialization into an arbitrary type —
see **B2**.

- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:235`
- guard being bypassed: `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:88`, `TextTypeSerializer.cs:67`
- `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:234`; `src/ActualLab.Rpc/RpcStream.cs:304`

**Fix:** don't widen the type. Add a per-slot expected-type override to
`RpcArgumentSerializer` (force polymorphism for slot N *against* `T[]`), or keep
`stream.CreateStreamBatchArguments()` and set the polymorphism flag separately so
`ReadDerivedItemType` runs with `typeof(T[])`. Defence in depth: validate the
runtime type in `OnBatch` before the payload body is deserialized.

### B2 · HIGH (CRITICAL if a gadget exists) · **O** — A client can unilaterally select the Newtonsoft format, which runs `TypeNameHandling.Auto` with no binder

> ✅ **Verified.** The server has **no format allow-list** — its only gate is "is this key
> registered" — and a live server accepted a client pinning `?f=njson5`. Two corrections:
> the same unrestricted resolution also fires on the default-registered **System.Text.Json**
> format (`json5`), via ActualLab's own `TextTypeSerializer` + `TypeRef.Resolve`, so this is
> not Newtonsoft-specific; and the binary formats are safe on *this* path (closed formatter
> registry). A `?f=` allow-list is therefore defence-in-depth, not the fix — see **C1**/**C4**.

`NewtonsoftJsonSerializer.DefaultSettings` sets
`TypeNameHandling = TypeNameHandling.Auto` with the stock
`DefaultSerializationBinder`. On read, Json.NET honours `$type` for any member
whose declared type is `object` (or a compatible abstract/interface). The
`njson5`/`njson5np` formats are in `RpcSerializationFormat.All`, which is the
default set for `RpcSerializationFormatResolver.Default`, and the **client**
picks the format via the `f=`/`serializationFormat=` query parameter on the
upgrade. So an application that only ever uses `mempack6` still exposes an
attacker-selectable Newtonsoft path — and combined with B1's unconstrained
`object` slot, that is classic gadget territory.

- `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`
- `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:27`, `:67`
- `src/ActualLab.Rpc/Configuration/RpcSerializationFormatResolver.cs:11`
- `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:31`

**Fix:** remove `TypeNameHandling.Auto` from the default settings, or install an
allow-list `SerializationBinder`; do not register all formats by default — make
the accepted set explicit per deployment and default it to the binary formats.

### B3 · HIGH · **B** — The stream receive path has no flow control: unbounded items into an unbounded channel

`RpcStream<T>` (consumer side) buffers into `Channel.CreateUnbounded<T>()` and
accepts every in-order item without ever checking how far the sender has run
ahead of the last acknowledged index. The entire `AckAdvance`/`AckPeriod`
protocol is enforced **only on the sending side** — i.e. it depends on the remote
peer being well-behaved. Because the channel is unbounded, `TryWrite` never fails
and there is no natural backpressure point.

Both directions: a client passing an `RpcStream<T>` argument makes the server the
consumer and can flood `$sys.I`/`$sys.B` at line rate; a compromised server OOMs
the .NET/Blazor client symmetrically. The TypeScript client has the identical
weakness (**I6**).

- `src/ActualLab.Rpc/RpcStream.cs:183`, `:194`, `:285`, `:304`
- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:164`, `:172`
- sender-side-only enforcement: `src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:221`

**Fix (applied):** the receiver enforces the credit window. `OnItem`/`OnBatch` fail the
stream via the existing `CloseFromLock` (which already sends `$sys.AckEnd` and
unregisters) when the incoming index would run past the window; `OnBatch` also caps
`T[]` at `RpcStream.MaxBatchSize`. `_remoteChannel` stays **unbounded** — the check is a
compare inside the lock `OnItem` already holds, so no write ever blocks or fails and the
peer's inbound path is untouched.

The window is credited by **consumption** (`_consumedIndex`, advanced only by the
enumerator), never by the last ack sent. A reset ack re-bases to what we've *received*,
so crediting from it would let a flooding peer ratchet its own limit up by alternating
"fill the window" with a forced gap — `RpcStreamAckWindowTest.ForcedResetsDoNotWidenTheAckWindow`
covers exactly that. The bound is `2 * AckAdvance + AckPeriod`: `AckAdvance` in steady
state, another `AckAdvance` because a reset legitimately re-bases the sender, plus
`AckPeriod` for the lag of acks behind consumption.

TypeScript (**I6**) enforced the window already but had the same reset ratchet and no
batch cap; both are fixed, and `_isBeyondAckWindow` now measures from
`_nextConsumedIndex`.

Residual: this bounds buffered *items*, not bytes — `2 * AckAdvance` items of up to
`MaxArgumentDataSize` each is still large. `RpcLimits.ObjectCountLimit` caps how many
streams a peer can hold, so the product is bounded, but a byte budget is the stronger
lever if this ever matters.

### B4 · HIGH · **B** — No cap on concurrent inbound calls or on the inbound-call table

The peer read loop dispatches every inbound message with `_ = ProcessMessage(...)`
and never awaits or throttles. Inbound calls are registered in an unbounded
`ConcurrentDictionary<long, RpcInboundCall>` keyed by the **attacker-chosen**
`RelatedId`; each registered call also allocates a linked
`CancellationTokenSource` chained to the peer's `peerChangedCts`. `RpcLimits`
contains only time-based limits — there is no `MaxInboundCalls` or concurrency
limit anywhere in `src/ActualLab.Rpc`.

A single unauthenticated WebSocket can hold millions of concurrent calls to any
slow method. `NoWait` calls bypass even the dictionary accounting while still
launching unbounded async work, so a call-count limit alone would not cover them.
This is also the multiplier that makes **A4** practical.

- `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`, `:59`
- `src/ActualLab.Rpc/RpcPeer.cs:419`
- `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:89`, `:136`
- `src/ActualLab.Rpc/Configuration/RpcLimits.cs:9`; `Options/RpcInboundCallOptions.cs:10`

**Fix:** add configurable per-peer (and optionally per-method) in-flight limits
covering regular *and* `NoWait` calls; reserve capacity before invoking, release
on every completion path, reject or disconnect on excess. Optionally gate the
read loop on a semaphore so TCP backpressure does the work.

### B5 · HIGH · **B** — No per-peer limit on shared objects / streams

`RpcSharedObjectTracker` strongly retains every shared object in an unbounded
dictionary, and serializing each locally returned `RpcStream<T>` registers a new
`RpcSharedStream<T>` (with a background worker, a `RingBuffer<Result<T>>` of
`AckAdvance+1` entries, and an unbounded ack channel). An unresponsive consumer
leaves them retained until the 125 s `ObjectReleaseTimeout`. A caller that
repeatedly invokes a stream-returning method and never acknowledges, enumerates or
disconnects creates objects faster than expiry removes them — even though the RPC
calls themselves finish immediately.

- `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:193`, `:214`, `:231`
- `src/ActualLab.Rpc/RpcStream.cs:116`

**Fix:** configurable max shared-object/stream count and retained-byte budget per
peer; reserve a slot before response serialization and roll back on send failure;
reject or close the peer at the limit.

### B6 · HIGH · **C** — Duplicate call IDs leak linked cancellation sources

Every non-`NoWait` inbound call creates a linked `CancellationTokenSource`
*before* registration. When a duplicate call ID resolves to an existing call,
`Process` returns the existing call's task and never disposes the losing
instance's linked source. A peer that starts one long call with ID X and then
floods duplicates of ID X retains one CTS + one registration on the peer-change
token per duplicate, while the inbound dictionary stays at a single entry — so a
call-count limit alone would not stop this.

- `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:48`, `:114`–`:119`, `:294`–`:300`

**Fix:** dispose the losing instance's linked source on the duplicate branch;
also validate that a duplicate's method reference and call type match the
registered call, and reject it as a protocol violation otherwise.

### B7 · MEDIUM · **O** — `$sys.Reconnect` re-triggers `ProcessStage1Plus` on the same in-flight call without limit

`RpcInboundCall.TryReprocess(0, …)` starts a **new** `ProcessStage1Plus`
continuation on an already-running call and overwrites `WhenProcessed`. The only
guards are "still registered" and "`ResultTask` is not null" — nothing detects
that the call is already being processed, and nothing rate-limits it. The
attacker issues one long-running call, then loops
`$sys.Reconnect(<known server handshake index>, {0: seq([1])})`. Each iteration
appends another async state machine and continuation to the same `Task`, all
retained until the original completes, and each will call `SendResult()`, emitting
N duplicate `$sys.Ok` responses for one request. The same path is reachable by
simply re-sending a call message with a duplicate `RelatedId`.

- `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:146`, `:201`–`:217`
- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:74`–`:82`

**Fix:** make `TryReprocess` idempotent (return the existing `WhenProcessed` if
it is set and incomplete); de-duplicate/rate-limit `Reconnect` per connection;
make `Complete()` a no-op when `UnregisterFromLock()` returns `false`.

### B8 · MEDIUM · **O** (PLAUSIBLE) — `$sys.Reconnect` accepts an attacker-shaped `Dictionary<int, byte[]>` (hash-flooding)

`IRpcSystemCalls.Reconnect(int, Dictionary<int, byte[]>, CancellationToken)` is
callable by any remote peer (system services are not `IBackendService`).
`Dictionary<int, V>` uses the identity hash and never falls back to a randomized
comparer; bucket counts come from the deterministic `HashHelpers` prime table, so
an attacker who controls the entry count can pick all-colliding keys. ~10⁵ keys
(a few hundred KB with MessagePack small-int encoding) degenerates insertion to
O(n²). The `ownHandshake.Index != handshakeIndex` guard does **not** help — the
dictionary is fully materialized during argument deserialization, before the
method body runs.

Not confirmed: depends on the concrete dictionary formatter pre-sizing from the
wire-declared count, which was not executed.

- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:15`, `:51`, `:61`

**Fix:** change the wire type to a bounded shape — the valid stage set is tiny and
known, so `(int Stage, byte[] Data)[]` with a length cap is right. Audit any other
`Dictionary<int,…>`/`Dictionary<long,…>` used as an RPC argument.

### B9 · MEDIUM · **O** — `$sys.KeepAlive` amplifies an unbounded `long[]` into several large allocations plus an equally large reply

`RpcSharedObjectTracker.KeepAlive(long[] localIds)` sizes a pooled buffer from the
wire-controlled `localIds.Length`, collects every *unknown* id into it, and sends
the whole collection straight back as `$sys.Disconnect(long[])`. No length cap, no
plausibility check. With `MaxArgumentDataSize` = 130 MB that is up to ~16 M longs,
producing (a) the deserialized array, (b) an equal-size pooled array, (c)
`buffer.ToArray()`, and (d) an outbound message that *retains* (c) in the
**unbounded** write channel (A4). A peer that stops reading makes (d) permanent.
`$sys.Disconnect` has the same unbounded-array shape minus the echo.

- `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:259`–`:276`
- entry point `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:133`, `:141`

**Fix:** cap `localIds.Length` against the peer's actual `SharedObjects.Count`
plus slack and drop the connection on violation; cap the echoed disconnect list at
a small constant.

### B10 · MEDIUM · **B** — Server exception type and message are forwarded verbatim to the remote peer

Every non-cancellation exception escaping an RPC method is converted to
`ExceptionInfo` — assembly-qualified type reference + raw `Message` — and sent to
the caller. There is no filter, allow-list, mapping hook, or error-mapping
middleware; the only knob is app-level try/catch in every method. In practice this
leaks internal assembly/namespace names (useful for fingerprinting and for picking
deserialization gadgets, cf. B1/B2), EF Core/Npgsql messages embedding table,
column and constraint names, `FileNotFoundException` messages with absolute server
paths, and `ArgumentException` messages containing internal parameter values.

- `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:116`, `:132`, `:136`, `:146`
- `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:312`
- `src/ActualLab.Core/Serialization/ExceptionInfo.cs:41`, `:44`

**Fix:** add `RpcInboundCallOptions.ErrorTransformer`
(`Func<Exception, RpcPeer, ExceptionInfo>`) applied in `RpcSystemCallSender.Error`;
default on non-backend peers to "pass through only client-safe exception types,
otherwise an opaque error with a correlation id". Log full detail server-side only.

### B11 · MEDIUM · **O** (disputed) — `IsBackend` for command methods comes from the *declared* parameter type, while dispatch uses the *runtime* type

> ✅ **Verified — and both reviewers had the mechanism wrong.** The real root cause is a
> misspelled string constant that disables the gate entirely (**H1**, CRITICAL). *This*
> declared-vs-runtime gap is **additionally** real and reproduces both before and after
> the constant is repaired — but it needs an application to declare an RPC command method
> with an abstract/interface parameter, which no framework or sample service does. MEDIUM,
> a footgun. The `RpcInboundCommandHandler` check in **H1(c)** closes it.

The only access-control decision in the inbound pipeline is
`if (MethodDef.IsBackend && !Peer.Ref.IsBackend) → NotFound`. For a command-shaped
method, `IsBackend` is computed once at registration from
`IsCommandType(parameterTypes[0], out isBackend)` — the *static* parameter type.
If that type is abstract or an interface, `HasPolymorphicArguments` is true and
the client picks the concrete command type on the wire, where the only check is
`expectedType.IsAssignableFrom(itemType)`. A concrete `IBackendCommand` subtype of
a non-backend declared type would therefore pass the gate and be dispatched by
CommandR on its runtime type.

Supporting evidence from the P8 reviewer: `Errors.BackendCommandRequiresBackendPeer()`
exists in the codebase but is **never thrown anywhere**, and the `IBackendCommand`
doc comment claims `CommandServiceInterceptor` enforces it, which it does not.

- `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:103`–`:105`; `RpcMethodDef.Static.cs:28`–`:42`
- `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`
- `src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:37`

**Fix (if confirmed):** re-check backend-ness after argument deserialization in a
middleware — for `RpcMethodKind.Command`, reject when the *deserialized* command's
runtime type is an `IBackendCommand` and the peer is not backend, throwing the
already-written `BackendCommandRequiresBackendPeer`. Alternatively refuse to
register a non-backend command method whose declared type is polymorphic and has
backend descendants.

### B12 · MEDIUM · **C** — An empty `$sys.Error` payload leaves the outbound call pending forever

`RpcSystemCalls.Error` null-forgives `ExceptionInfo.ToException()`, which returns
`null` for `ExceptionInfo.None`. Passing that null to `SetError` throws instead of
completing the referenced call. A server replying with a default/empty
`ExceptionInfo` faults the no-wait system handler (logged only), and the original
call stays registered until its run timeout or connection teardown.

- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:95`, `:100`
- `src/ActualLab.Core/Serialization/ExceptionInfo.cs:57`; `RpcOutboundCall.cs:297`

**Fix:** treat `ExceptionInfo.None` as an invalid error response and complete the
call with a non-null protocol exception. Never pass a null exception to `SetError`.

### B13 · MEDIUM · **C** — A pre-cancelled outbound call is still transmitted

`Invoke` registers the call and its cancellation callback, then unconditionally
calls `SendRegistered` when the peer is connected. If the token is already
cancelled, registration synchronously cancels and unregisters the call — yet the
request is sent anyway. A `$sys.Cancel` may precede the request, find no inbound
call, and the side-effecting method then executes remotely despite the caller
having cancelled before dispatch.

- `src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:81`, `:94`, `:116`–`:123`, `:316`, `:348`

**Fix:** fail fast on an already-cancelled token before registration/routing, and
synchronize the post-registration send with cancellation. Repeat the check after
awaiting a connection.

### B14 · LOW · **O** — `RpcCallTypes.Get` indexes an 8-element array with an unvalidated wire byte

The byte serializers pack the call type into 3 bits and are safe, but the JSON
envelope carries `CallType` as a full `byte` and `ValidateInboundEnvelope` does
not check it. `{"CallType":200,…}` reaches `Registry[200]` →
`IndexOutOfRangeException`. Caught in `RpcInboundCall.Process` and returned to the
caller, so not a crash — but the client gets a nonsense error instead of
`InvalidCallTypeId`.

- `src/ActualLab.Rpc/Configuration/RpcCallTypes.cs:22`, `:32`
- `src/ActualLab.Rpc/Serialization/Internal/JsonRpcMessage.cs:12`; `RpcTextMessageSerializerV3.cs:102`

**Fix:** bounds-check `Get`, and validate `CallType <= 7` in `ValidateInboundEnvelope`.

### B15 · LOW · **B** — Invalid-call-type errors report the *replacement* method's expected type

`RpcInboundContext` assigns `MethodDef = NotFoundMethodDef` *before* reading
`MethodDef.CallType.Id`, so the rejection names the `NotFound` method's call type
rather than the addressed method's — obscuring genuine protocol incompatibilities.

- `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:60`–`:62`

**Fix:** capture the expected call type id into a local before reassigning `MethodDef`.

### B16 · LOW · **O** — W3C trace context accepted from any peer, unvalidated, with a 64 KB `tracestate`

Every inbound call adopts the `~p`/`~s` headers as its parent `ActivityContext`
with no size or trust check; header values are allowed up to
`MaxHeaderSize` = 65536 bytes, while the W3C spec caps `tracestate` at 512
characters. An anonymous client can splice its requests into another tenant's
traces (poisoning trace-based correlation and alerting) and attach 64 KB of
`tracestate` that is propagated verbatim into every downstream span and log record.

- `src/ActualLab.Rpc/Diagnostics/RpcActivityInjector.cs:21`–`:31`; `RpcDefaultCallTracer.cs:54`–`:59`
- `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:17`

**Fix:** honour inbound trace context only on backend peers or when explicitly
opted in; enforce the W3C 512-char `tracestate` / 55-char `traceparent` limits
before parsing.

---

## C. Serialization (RPC + Core + Nerdbank MessagePack), text & IO buffers

This is the area with the most severe findings. Several were reproduced against the
published **14.1.78** NuGet packages in a throwaway project outside the repo.

### C1 · HIGH (CRITICAL where a gadget assembly is loaded) · **B** (reproduced end-to-end) — Unrestricted `$type` deserialization is reachable from the wire under *every* RPC format

`NewtonsoftJsonSerializer.DefaultSettings` sets `TypeNameHandling = TypeNameHandling.Auto`
with the stock `DefaultSerializationBinder` — no allow-list, no `SerializationBinder`
override. Json.NET honours `$type` on read for any `TypeNameHandling` other than
`None`, and its `objectType.IsAssignableFrom(specifiedType)` guard is vacuous when the
declared target is `object`. This is the textbook CA2326 gadget pattern.

There are two independent routes to it, and the first is the serious one:

1. **Format-independent (worst).** `ImmutableOptionSet` / `OptionSet` — ordinary Fusion
   wire contract types — store every value as `NewtonsoftJsonSerialized<object>`, i.e.
   exactly that vacuous case. `ImmutableOptionSet.JsonCompatibleItems` is the wire member
   for **all** formats (`[Key(0)]`, `[MemoryPackOrder(0)]`, `[JsonPropertyName("Items")]`),
   and its values are plain strings handed to
   `NewtonsoftJsonSerializer.Default.ToTyped<object>()` at deserialization time. So even
   under the *default* `mempack6` format, an attacker-controlled `ImmutableOptionSet`
   yields arbitrary type instantiation with attacker-controlled member values.
   `SessionInfo.Options` and `AuthBackend_SetSessionOptions.Options` are both
   `ImmutableOptionSet`; `SessionInfo` flows **server → client** via `IAuth.GetSessionInfo`
   / `GetUserSessions`, so a hostile or MITM'd server owns every .NET client, and any
   application contract carrying an `OptionSet` gives the same primitive **client →
   server**.
2. **Format selection.** The client picks the RPC serialization format via the `?f=`
   query parameter, validated only against `Hub.SerializationFormats`, which defaults to
   `RpcSerializationFormat.All` — including `njson5` / `njson5np`. An unauthenticated
   client can therefore force the server to deserialize *all* RPC arguments with
   Newtonsoft, extending `$type` handling to every `object`- or interface-typed member of
   every contract DTO. (Note `njson5np` advertises "no polymorphism" but delegates to the
   same singleton, so the label is wrong.)

Reproduced against published 14.1.78 — a payload of
`{"$type":"System.Text.StringBuilder, System.Private.CoreLib","Capacity":4242}` inside an
`ImmutableOptionSet` materialises the type and drives its setters through **both** the
`mempack6` and `msgpack6` binary round-trips:

```
[A] in-memory value type: System.Text.StringBuilder
[B] mempack bytes contain $type: True
[C] after mempack round-trip: System.Text.StringBuilder cap=4242
[D] after msgpack round-trip: System.Text.StringBuilder
```

Full RCE additionally requires a usable gadget in the target process's assembly set —
the usual Json.NET caveat, and neither reviewer enumerated one. Arbitrary type
construction with attacker-controlled member values is confirmed.

- `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:27`–`:34` (esp. `:29`)
- `src/ActualLab.Core/Serialization/Serialized/NewtonsoftJsonSerialized.cs:37`
- `src/ActualLab.Core/Collections/Legacy/ImmutableOptionSet.cs:34`; `Legacy/OptionSet.cs:33`; `Internal/OptionSetHelper.cs:22`
- `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:27`, `:31`, `:67`; `RpcSerializationFormatResolver.cs:11`
- `src/ActualLab.Rpc/Serialization/RpcTextArgumentSerializerV4NP.cs:52`–`:65`
- `src/ActualLab.Fusion.Ext.Contracts/Authentication/SessionInfo.cs:23`; `IAuth.cs:38`

**Fix:** (1) set `TypeNameHandling = None` in `DefaultSettings`, or install a deny-by-default
`SerializationBinder` — Fusion does not need `$type`, since polymorphism is already carried
out-of-band by `TextTypeSerializer`/`ByteTypeSerializer`. (2) Stop routing `object` values
through `NewtonsoftJsonSerialized<object>`: migrate `OptionSet`/`ImmutableOptionSet` onto
the `TypeDecoratingUniSerialized<TSchema, object>` mechanism with a restrictive default
schema (see **C7**). (3) Remove `njson5`/`njson5np` from the default format set.

### C2 · HIGH · **B** (crash reproduced) — Nested deserialization kills the process with an uncatchable `StackOverflowException` from ~72 KB

> **Verification corrected the mechanism.** `MaximumObjectGraphDepth` is 500 and *is*
> enforced in both security modes, so `TrustedData` is **not** why the crash happens.
> The load-bearing defect is that the type-decorating hop **resets MessagePack's depth
> counter on each nested `Deserialize` call**, so the limit never accumulates across
> levels. `TrustedData` is separately real but MEDIUM and scoped to unmanaged dictionary
> keys (strings already use .NET's randomized comparer in both modes).

`MessagePackByteSerializer.DefaultOptions` is built as `new(DefaultResolver)` with no
`.WithSecurity(...)`, so it inherits MessagePack-CSharp's `MessagePackSecurity.TrustedData`
default. Under `TrustedData`, `DepthStep(ref reader)` is a **no-op** and
`GetEqualityComparer<T>()` returns the ordinary collision-prone comparers. Fusion's
hand-written formatters *do* call `DepthStep`, but those calls do nothing under the shipped
default. `TypeDecoratingByteSerializer.Read` has no recursion budget of its own, and because
`PropertyBag` is aliased project-wide to `PropertyBag<TypeSchema.Any>`, a `PropertyBag`
value is allowed to be another `PropertyBag`.

Nesting costs ~120 bytes of payload per level and one CLR stack frame per level. On a
default 1 MB stack, ~600 levels — **≈72 KB of wire data** — overflow the stack.
`StackOverflowException` cannot be caught in .NET: **the entire server process dies**,
taking every other connection with it. Reproduced:

```
[E] MessagePack DefaultOptions.Security == TrustedData
depth=300  payloadBytes=35931  -> Deserialized OK
depth=600  payloadBytes=72039  -> Stack overflow.  (process terminated)
```

Reachable wherever a `PropertyBag`/`OptionSet`/`object`-typed value arrives from an
untrusted source: an application contract member, the `$sys.B` batch path whose expected
type is widened to `object` (**B1**), or a poisoned operation-log row (`Operation.Items` is
a `MutablePropertyBag`). Second impact of the same setting: dictionary-typed contract
members get the default comparer, so `$sys.Reconnect`'s `Dictionary<int, byte[]>` (**B8**)
is open to hash flooding.

For contrast, both JSON serializers *are* depth-capped at 64 — this is specific to the
MessagePack/MemoryPack + type-decorating chain.

- `src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:27`–`:36` (esp. `:35`)
- `src/ActualLab.Core/Serialization/Internal/Formatters/PropertyBagMessagePackFormatter.cs:30`
- `src/ActualLab.Core/Serialization/Internal/Formatters/PropertyBagItemMessagePackFormatter.cs:31`
- `src/ActualLab.Core/Serialization/Internal/Formatters/TypeDecoratingUniSerializedMessagePackFormatter.cs:29`
- `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:39`; `src/Directory.Build.props:89`

**Fix:** build the options as
`new MessagePackSerializerOptions(DefaultResolver).WithSecurity(MessagePackSecurity.UntrustedData)`
and lower `MaximumObjectGraphDepth` from 500 to something the stack can actually take
(~64). Add a serializer-independent nesting budget to
`TypeDecoratingByteSerializer.Read`/`TypeDecoratingTextSerializer.Read` — the
type-decorating hop crosses serializer boundaries, and MessagePack's own depth counter is
reset on each nested `Deserialize`. Keep any trusted-storage fast path as a separately
named opt-in serializer.

### C3 · HIGH · **B** (reproduced) — Type-cache keys alias the pooled receive buffer: unbounded growth, 100% miss rate, and 65,536 keys per type name

`ByteTypeSerializer.ReadItemType` builds a `ByteString` **directly over the inbound
`ArgumentData` memory** and uses it as the key of a process-wide, never-evicted
`ConcurrentDictionary<ByteString, Type?>`. `ArgumentData` is documented as a "zero-copy
projection into the buffer", and the transports explicitly recycle that buffer
(`buffer.Renew(...)`) once the frame is parsed. The stored key therefore mutates into
unrelated bytes right after insertion, so every inbound polymorphic message inserts a
*new* entry — the dictionary grows by one per message forever and degenerates to a 100%
miss rate, re-running `TypeRef.Resolve` every time (feeding **C4**). Reproduced:

```
iter 0: resolved=String, cacheCount=1
iter 4: resolved=String, cacheCount=5
keys now hold garbage: EEEEEEEEEEEE | EEEEEEEEEEEE | ...
```

Secondary: `ByteString`'s hash is a 32-bit `GetPartialXxHash3`, so a stale entry whose
bytes were overwritten can be hit by a colliding probe and return the *wrong* `Type` —
type confusion within an assignable set, since `IsAssignableFrom` still applies.

Independently, the binary marker writes a 2-byte "hash" that `FromBytes` **never reads or
validates** (it resolves only `memory[4..]`), so the same type name has 65,536 immediately
available distinct cache keys — an unbounded-growth vector that does not even depend on
the aliasing bug. The text variant has the same borrowed-key pattern.

That the surrounding code copies in the analogous situations —
`RpcByteMessageSerializerV4.cs:34` (`new RpcMethodRef(blob.ToArray(), …)`) and
`RpcHeaderKey.Utf8Name = utf8Name.ToArray()` — indicates this is an oversight, not a
deliberate trade-off.

- `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:16`, `:37`–`:39`, `:49`–`:63`, `:96`–`:109`
- `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:13`, `:34`–`:42`, `:91`–`:93`
- `src/ActualLab.Core/Text/ByteString.cs:50`; `src/ActualLab.Core/Text/ByteStringExt.cs:17`
- `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializerV4.cs:37`; `RpcFrameCodec.cs:112`–`:119`
- `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:186`–`:187`; `Infrastructure/RpcStreamTransport.cs:158`–`:165`

**Fix:** look the key up first and materialise an owned copy only on a miss; better still,
cache by a canonical owned value (the normalized type-name string or a registered stable
type id). Validate the full marker — including the 2-byte hash — before caching, and bound
both caches with an LRU.

> One reviewer's buffer-lifetime claim rests on the transport recycling the array before
> the cached key is next compared; another reviewer independently asserted that
> `RpcInboundCall` clears `Message.ArgumentData` synchronously. This is under verification
> (**V3**) — if the bytes turn out to be copied, the mutation half collapses and only the
> unbounded-growth half (which is confirmed either way) survives.

### C4 · HIGH · **B** — Wire type names are resolved *before* the assignability check, and successful resolutions are cached forever

Every polymorphic entry point resolves the attacker-supplied assembly-qualified name first
and only then checks `expectedType.IsAssignableFrom(itemType)` / `TypeFilter`. There is no
allow-list on what may be *resolved*, and `TypeRef.Resolve` memoizes every **successful**
resolution in a static unbounded `ConcurrentDictionary` (it evicts only `null` results —
the in-source comment `// Potential memory lead / attack vector` shows the null case was
noticed and the successful case was not).

`Type.GetType(aqn)` on a *generic* name materialises a new runtime type instantiation
(`MethodTable`, EEClass, …) in the loader heap, which is **never** reclaimed. A peer can
emit an unbounded stream of distinct resolvable names such as
``List`1[[List`1[[…Int32…]]]]``, each permanently growing native memory *and* adding a
permanent cache entry. Measured in an out-of-repo BCL-only harness: **≈1.4 KB permanently
leaked per tiny message**, linear and unbounded (`n=20000 → +27 MB`); deep nested generics
cost ~140 KB each. The leak lives in the loader heap, so it is invisible to GC heuristics
and reclaimable only by restarting the process.

The cheapest unauthenticated trigger needs no polymorphic contract at all:
`RpcSystemCalls.Error` calls `error.ToException()` **before** it looks up the related
outbound call, so a `$sys.Error` frame with any `RelatedId` reaches `TypeRef.Resolve`.
For *unresolvable* names, `Type.GetType` re-triggers assembly-name probing on every call
(nulls are deliberately not cached), turning each 100-byte message into a file-system
probe — and probing loads assemblies by attacker-chosen name, running their module
initializers. Types that survive the `Exception` check are then passed to
`ActivatorExt.CreateInstance`, which emits and permanently caches a `DynamicMethod` ctor
delegate per distinct type.

- `src/ActualLab.Core/Reflection/TypeRef.cs:31`, `:97`–`:106`
- `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83`–`:94`, `:60`–`:61`
- `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:62`–`:73`
- `src/ActualLab.Core/Serialization/ExceptionInfo.cs:99`
- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:95`–`:103`
- `src/ActualLab.Core/Reflection/ActivatorExt.cs:131`

**Fix:** resolve wire type names only through a per-`RpcServiceDef`/per-`expectedType`
allow-list built at startup from the declared contract types and their registered subtypes;
reject anything not in it *before* calling `Type.GetType`. At minimum: cap the AQN length,
reject generic-argument syntax unless the expected type is generic, size-cap `ResolveCache`,
and route wire-originated exception types through a bounded whitelist
(`ExceptionInfo.UnknownExceptionTypeResolver` is the existing hook). Independently, make
`RpcSystemCalls.Error` look up the outbound call **first** and skip `ToException()` when
`RelatedId` matches nothing — that removes the cheapest route at essentially zero cost.

### C5 · HIGH · **C** — Nerdbank converters preallocate arrays straight from an untrusted count

`ApiArrayNerdbankConverter.Read` executes `new T[len]` immediately after `ReadArrayHeader`,
and `PropertyBagNerdbankConverter` does the same — ignoring Nerdbank.MessagePack's
`context.Security.MaxCollectionPreallocation`. A peer sends a large declared count, one
malformed first item, and enough filler to satisfy the reader's coarse "≥1 byte per item"
check; a relatively small payload allocates a reference array roughly eight times its wire
size and then fails. Against the 130 MB argument allowance (**A8**), concurrent requests
drive the process into LOH pressure or OOM without ever producing a valid value. The
Nerdbank formats are opt-in (`Register()` must be called), which caps the blast radius.

- `src/ActualLab.Serialization.NerdbankMessagePack/Internal/ApiArrayNerdbankConverter.cs:10`–`:19`
- `src/ActualLab.Serialization.NerdbankMessagePack/Internal/PropertyBagNerdbankConverter.cs:25`–`:31`
- `src/ActualLab.Serialization.NerdbankMessagePack/NerdbankMessagePackByteSerializer.cs:138`–`:150`

**Fix:** honour `MaxCollectionPreallocation` — deserialize into a capped, incrementally
growing buffer and materialise the final array only after the elements validate. Add a
configurable maximum logical element count for RPC-facing collections.

### C6 · MEDIUM · **O** — `ExceptionInfo` round-trip: arbitrary exception-type construction inbound

`ExceptionInfo.ToException()` resolves an arbitrary wire-supplied type, accepts it if it
derives from `Exception`, and constructs it via `type.CreateInstance(message, null)` — so it
runs the named type's **static constructor** and an instance constructor of the attacker's
choosing with an attacker-controlled `string`. This is the cheapest "instantiate a type I
named" primitive in the codebase and the driver for **C4**. (The outbound half of the same
type — unfiltered exception type + message shipped to the peer — is **B10** / **D7**, where
both models found it.)

- `src/ActualLab.Core/Serialization/ExceptionInfo.cs:94`–`:128`, `:41`–`:50`
- `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:98`

**Fix:** restrict `ToException()` to a registered allow-list (Fusion's own exceptions plus
a handful of BCL types; everything else → `RemoteException`).

### C7 · MEDIUM · **O** — The `TypeSchema` allow-list mechanism is disabled everywhere it ships

`TypeDecoratingByteSerializer` / `TypeDecoratingTextSerializer` default their `TypeFilter`
to `_ => true`, and every shipped alias (`PropertyBag`, `MutablePropertyBag`,
`PropertyBagItem` in `Directory.Build.props`) binds to `TypeSchema.Any`, whose `IsAllowed`
is `true`. So the guardrail exists, is plumbed through, and is off by default in every
instantiation the framework provides. That is what leaves **C1**, **C2** and **C4** with no
second line of defence — and it is specifically what makes **C2**'s stack overflow
constructible, since a `PropertyBag` value is permitted to be another `PropertyBag`. The
Nerdbank converter goes further and hands the attacker-named type to
`ReflectionTypeShapeProvider.Default`, emitting a fresh reflection shape (and dynamic code)
per newly named type.

- `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:37`; `TypeDecoratingTextSerializer.cs:55`
- `src/ActualLab.Core/Serialization/TypeSchema.cs:13`–`:16`; `src/Directory.Build.props:89`–`:91`
- `src/ActualLab.Serialization.NerdbankMessagePack/Internal/TypeDecoratingUniSerializedNerdbankConverter.cs:38`–`:45`

**Fix:** bind the shipped aliases to a restrictive schema (`PrimitiveOnly` plus an
app-extensible registry) and flip the default `typeFilter` from allow-all to
deny-unless-registered. At minimum forbid `PropertyBag`/`OptionSet`/
`TypeDecoratingUniSerialized` as *value* types inside a `PropertyBag`.

### C8 · MEDIUM · **C** — Restricted type schemas are enforced on read but not on write

The type-decorating byte, text and Nerdbank serializers check `TypeFilter`/`TypeSchema`
during deserialization but not during serialization, so a restricted serializer happily
emits a payload containing a prohibited concrete type that the *same* serializer then
refuses to read. A caller can therefore persist unreadable cache/database values and only
discover it on round-trip.

- `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:47`–`:51` (read) vs `:59`–`:69` (write)
- `src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:73`–`:77` vs `:82`–`:97`
- `src/ActualLab.Serialization.NerdbankMessagePack/Internal/TypeDecoratingUniSerializedNerdbankConverter.cs:37`–`:66`

**Fix:** verify both `declaredType.IsAssignableFrom(actualType)` and the configured filter
before writing any type marker, in all three implementations.

### C9 · MEDIUM · **O** (measured) — MemoryPack serialization of a nested `PropertyBag` is exponential in depth

Each nesting level re-invokes the child's computed `MemoryPack` property, and the generated
version-tolerant formatter evaluates it more than once per level, so cost roughly doubles
per level:

```
mempack  depth=8  bytes=1126 ms=30
mempack  depth=16 bytes=2318 ms=135
mempack  depth=20 bytes=2914 ms=1338      (~2x per level)
msgpack  depth=500 bytes=59931 ms=3       (linear)
```

A 30-level bag (~4 KB) would take on the order of ~20 minutes of CPU under `mempack6`, the
default format. Reachable anywhere an untrusted `PropertyBag` is later re-serialized —
echoed back to a client, written to the operation log, or stored in the client-side cache.

- `src/ActualLab.Core/Serialization/Serialized/TypeDecoratingUniSerialized.cs:47`–`:57`, `:83`–`:102`
- `src/ActualLab.Core/Collections/Internal/PropertyBagItem.cs:20`

**Fix:** cache the serialized form per instance rather than recomputing it per property
read, and add the **C2** depth guard so deep bags cannot be constructed at all.

### C10 · MEDIUM · **C** — `StringAsSymbolMemoryPackFormatter` passes unvalidated signed deltas to `MemoryPackReader.Advance`

On a version-tolerant object header with a count other than 1, the formatter reads signed
varint deltas with `ReadVarIntInt32()` and passes each straight to `reader.Advance`.
Negative deltas move the reader's unsafe cursor **before** the supplied span; cumulative
advances past the remaining input are not rejected either. The feature is off by default
(`StringAsSymbolMemoryPackFormatterAttribute.IsEnabled`), which is why this is MEDIUM
rather than a default-path memory-safety finding.

- `src/ActualLab.Core/Text/Internal/StringAsSymbolMemoryPackFormatter.cs:28`–`:30`, `:37`–`:40`
- `src/ActualLab.Core/Text/StringAsSymbolMemoryPackFormatterAttribute.cs:21`–`:33`

**Fix:** reject negative deltas, checked-overflow the cumulative skip, and verify each skip
against the reader's remaining length — or use MemoryPack's own version-tolerant skip
primitive instead of reproducing its cursor arithmetic.

> **Open question flagged by the P3 reviewer:** whether `MemoryPackReader.Advance(int)`
> itself validates its argument could not be determined offline. It is called with
> attacker-controlled `int` values here *and in every MemoryPack-generated version-tolerant
> formatter*. If `Advance` is unchecked, that is a memory-safety issue reachable from any
> MemoryPack RPC payload. This is the single highest-value follow-up in the area.

### C11 · MEDIUM · **C** — `ByteString` is mutable through its source buffer despite content-based hashing

`ByteString` documents itself as an immutable value/key, but its constructors and
`AsByteString` helpers retain caller-owned arrays and mutable `Memory<byte>` without
copying, while equality and `GetHashCode` are recomputed from current contents. Insert one
into a dictionary and later reuse the array, and the key sits in its original bucket while
describing different bytes — failed lookups and removals, duplicate logical keys, unbounded
stale entries. **C3** is the concrete network-reachable instance of this contract violation.

- `src/ActualLab.Core/Text/ByteString.cs:9`–`:18`, `:49`–`:56`, `:108`–`:117`; `ByteStringExt.cs:8`–`:18`

**Fix:** have the normal constructors take an owned copy and expose any zero-copy form under
an explicitly borrowed/unsafe API that cannot be used as a hashed key.

### C12 · MEDIUM · **C** — `EncoderExt.Convert` sizes the output span by input *char* count

Both overloads call `GetSpan(source.Length)` even though UTF-8 needs up to 3 bytes per code
unit (4 per surrogate pair). `IBufferWriter<byte>` may legitimately return exactly the
requested size, so encoding a single `€` can throw "output byte buffer is too small"; an
encoder that instead reports zero progress makes the loop repeat the same source forever
(there is no zero-progress guard). Writers that over-allocate mask the defect entirely.

- `src/ActualLab.Core/Text/EncoderExt.cs:33`, `:42`–`:46`, `:55`, `:64`–`:68`

**Fix:** request a checked encoder-specific upper bound (chunked `GetMaxByteCount`), and
grow the span when no progress is made.

### C13 · LOW · **O** — `TryDeserializeBinaryWithSize` reads the 4-byte size prefix without checking 4 bytes remain

`size = array.AsSpan(offset).ReadLittleEndian()` reads past `totalLength` into the pooled
buffer, so when fewer than 4 bytes of the frame remain the size is composed from stale bytes
left by a previous message — which an attacker can influence. A residual `size` of 1..3
passes `isSizeValid` and produces a negative-length slice. The exception is caught and
`offset` still advances, so there is no hang or crash; but the one place that is supposed to
validate the frame is making decisions from uninitialised pool data.

- `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:135`–`:136`

**Fix:** `if (totalLength - offset < Int32Size) throw Errors.InvalidItemSize();` and require
`size >= Int32Size`.

### C14 · LOW · **O** — `RpcTextMessageSerializerV3.Read` indexes `tail[0]` on a possibly empty tail

A syntactically valid JSON envelope with no trailing delimiter yields an empty tail and an
`IndexOutOfRangeException`. `RpcFrameCodec` catches it, logs at Error and drops the message
— a remote log-flood/message-drop rather than a crash.

- `src/ActualLab.Rpc/Serialization/RpcTextMessageSerializerV3.cs:41`

**Fix:** `if (!tail.IsEmpty && tail[0] == Delimiter) tail = tail[1..];`

### C15 · LOW · **O** — The malformed-frame handler logs raw attacker bytes at `Error`, unthrottled

Every message that fails to deserialize produces
`LogError(e, "Couldn't deserialize: {Data}", …)` with no rate limiting. Per-message volume
is bounded (truncated at 64 bytes) but the *rate* is not, and in the text case the logged
content is attacker-chosen **characters** — a structured-log-injection vector for downstream
consumers.

- `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:123`, `:151`, `:176`

**Fix:** log at Debug/Warning with a per-peer rate limiter, and hex-encode rather than
passing raw text through.

### C16 · LOW · **O** — `ListFormat.Parse(string, …)` leaks the pooled `StringBuilder`

The `string` overload creates the `ListParser` without `using`, so `Dispose()` →
`ItemBuilder.Release()` never runs. The `ReadOnlySpan<char>` overload immediately below does
use `using`.

- `src/ActualLab.Core/Text/ListFormat.cs:47`–`:50`

### Notes carried out of this area

- **Compact message serializers identify methods by a 32-bit hash only**
  (`RpcByteMessageSerializerV4Compact.cs:26`–`:28`, `V5Compact.cs:26`–`:28`;
  `ServerMethodResolver[hashCode]`). Not exploitable *provided* the resolver is built strictly
  from the peer-visible method set — collisions would then only reach methods the peer may
  already call. Worth a startup assertion that no two methods in a resolver share a hash.
- **Negative results**, recorded so a later pass skips them: both JSON serializers are
  depth-capped at 64; `ByteString` *does* copy on MessagePack/MemoryPack read;
  `RpcHeaderKey` and `RpcMethodRef` correctly copy out of the pooled buffer — which is
  exactly what makes **C3** look like an oversight rather than a trade-off.

---

## D. Server hosting endpoints (ASP.NET Core + .NET Framework + RestEase)

### D1 · HIGH (CRITICAL under `SameSite=None`) · **B** — No `Origin` check on the WebSocket upgrade: cross-site WebSocket hijacking of the cookie-bound session

The RPC WebSocket endpoint accepts an upgrade from any origin. Nothing in
`ActualLab.Rpc.Server` or `ActualLab.Fusion.Server` inspects `Origin`, sets
`WebSocketOptions.AllowedOrigins`, or documents that the host must — `grep -ni
origin` over all four server projects returns nothing, and no sample or doc sets
it. Meanwhile `RpcPeerOptionsExt.ServerConnectionFactory` binds the browser's
`FusionAuth.SessionId` **cookie** to the resulting connection, and
`RpcDefaultSessionReplacer` substitutes that session into every inbound call
carrying `Session.Default`. The WebSocket handshake is exempt from CORS and from
preflight, so the same-origin policy does not stand in for the missing check.

Attack: victim signed in to `https://app.example.com` visits an attacker page,
which runs `new WebSocket("wss://app.example.com/rpc/ws?clientId=…&f=mempack3")`.
The browser attaches the cookie whenever cookie policy permits — always under the
documented cross-origin recipe (`docs/PartAA-Server.md:498`, `SameSite=None`), and
under the default `SameSite=Lax` whenever the attacker controls *any* origin on
the same registrable domain (sibling subdomain, subdomain takeover,
customer-controlled subdomain — `Lax` gates on *site*, not *origin*). The attacker
page then speaks RPC as the victim, bidirectionally, for as long as the tab is
open. The HTTP/2 transport is not equally exposed: it is a POST with request
streaming, which forces a preflight.

- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:33`, `:79`
- `src/ActualLab.Rpc.Server/EndpointRouteBuilderExt.cs:22`
- `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:36`
- `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:31`
- `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:34`

**Fix:** own the check rather than delegating it to the host by omission. Add
`OriginValidator`/`AllowedOrigins` to `RpcWebSocketServerOptions`, evaluate it in
`Invoke` before `GetServerPeer`, reject with 403, and default to *same-origin
only* (`Origin` absent ⇒ non-browser client ⇒ allow; `Origin` present and ≠
`Request.Host` ⇒ reject). Mirror on OWIN. Amend the `SameSite=None` recipe in the
docs to require an explicit allow-list.

### D2 · HIGH · **B** — Session ids and client ids are written to the server log on every connection

`requestDescription` is built from the full request URI **including the query
string** and logged at `Information` on the success path of every accepted
connection; error paths log it again. Fusion's own convention puts the session id
in that query string (`?session=`), and the `clientId` — a peer-identity
capability (**A5**) — is always there. A Fusion session id is a full bearer
credential: possession is equivalent to being the user, and
`ServerConnectionFactory` accepts `?session=<stolen>` verbatim.

`ActualLab.*` logs at `Information` under the default filter, so this is on by
default. Anyone with log access (aggregation, on-call, a leaked bundle, an SIEM
export, a crash dump) obtains live session ids for every connected user. The
project already treats this as a real risk on the *client* side — `sanitizeUrl`
was added to `@actuallab/rpc` precisely so the connect log line stops leaking
bearer-style query parameters — but the server never got the equivalent.

- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:25`, `:31`, `:61` (also `:45`, `:104`, `:110`, `:115`)
- `src/ActualLab.Rpc.Server/RpcHttpServer.cs:25`, `:31`, `:56`
- `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:108`
- `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:95` (`Session.ToString() == Id`)
- client-side counterpart that *does* redact: `ts/packages/rpc/src/rpc-peer.ts:147`

**Fix:** build the description from `Path` plus an allow-listed, redacted query in
all three servers; log `session.Hash` rather than `session` in `SessionMiddleware`.
Longer term deprecate `?session=` in favour of a header — a query parameter is
logged by every reverse proxy, load balancer and APM agent in the path, none of
which Fusion controls.

### D3 · HIGH · **C** — RestEase buffers an unbounded hostile 500 response body

Every HTTP 500 handled by `RestEaseHttpMessageHandler` is read completely into a
string with no byte limit before its content type or error shape is validated, and
the read does not receive the caller's cancellation token. A compromised or
attacker-controlled server returns 500 with a huge or indefinitely streamed body;
the client accumulates the whole thing and then may parse it again as JSON.

- `src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandler.cs:31`, `:33`, `:34`

**Fix:** read at most a small configurable limit (e.g. 64 KiB) from the response
stream using the request token, abort/dispose on overflow, return a generic
truncated `RemoteException`, and apply JSON depth/size limits to the bounded
payload.

*(Related, from the P4 Opus reviewer as an out-of-partition pointer:
`DeserializeError` hands that hostile body to `TypeDecoratingTextSerializer`,
which resolves an arbitrary assembly-qualified type name **before** its
`IsAssignableFrom` check — `src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:73`.
Because the target is the sealed struct `ExceptionInfo`, the reachable effect is
arbitrary type/assembly *load* (probing-path load, static-ctor execution), not
arbitrary object construction. Resolution policy is area C's.)*

### D4 · HIGH · **B** — The default invalid-session recovery can permanently redirect-loop a client

The default invalid-session handler signs out ASP.NET authentication and redirects
to the same URL, then short-circuits the middleware — but it neither deletes nor
replaces the invalid `FusionAuth.SessionId` cookie, because `GetOrCreateSession`
returns *before* the only cookie-writing block. Every redirected request presents
the same rejected cookie and gets another redirect; the user cannot recover
without manually clearing cookies. O reached this from the auth side and notes the
default of `ClientAuthHelper.SignOutEverywhere` is a *forced* sign-out, i.e. the
common path. Additionally, the unconditional `SignOutAsync()` throws when no
default sign-out scheme is configured, turning the same invalid cookie into a
persistent 500.

- `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:32`, `:36`, `:64`, `:98`, `:102`, `:108`–`:112`

**Fix:** expire or rotate the Fusion cookie before redirecting and issue a fresh
session on the next request; don't call the default sign-out unconditionally when
no scheme is configured; add a two-request regression test for recovery.

### D5 · MEDIUM · **B** — A session bound to an RPC connection bypasses `ISessionValidator`

`SessionMiddleware` runs the registered `ISessionValidator` and refuses a session
it rejects. The RPC connection factory does neither: the `?session=` value is
accepted after only a syntactic `IsValid()` check, and the cookie fallback calls
the raw `GetSession` rather than the validating `GetOrCreateSession`. So the
validator gates HTTP requests but not RPC connections — which is where
essentially all Fusion traffic flows. An app that plugs in its own validator for
revocation, expiry, IP pinning or tenant checks sees it honoured on HTTP and
silently skipped on the WebSocket.

With the built-in `IAuth` the practical impact is limited (a forced sign-out also
clears `UserId`, so the session de-authenticates anyway) — the defect is "a
security extension point that does not cover the main entry point".

- `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:28`–`:38`
- contrast `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:89`–`:92`
- `src/ActualLab.Fusion.Server/Rpc/RpcDefaultSessionReplacer.cs:35`

**Fix:** await `ISessionValidator` in `ServerConnectionFactory` (it already returns
`Task<RpcConnection>` and receives a token) for both branches; on rejection fall
through to an unbound connection. Re-validation happens naturally per reconnect.

### D6 · MEDIUM · **O** — Session fixation: any cookie value is adopted as the session identity and is never rotated at sign-in

`GetSession` turns whatever is in the `FusionAuth.SessionId` cookie into a
`Session` with no server-side check that the id was ever issued — the only
constraint is length ≥ 8. The middleware then re-issues that same id as the
response cookie, and authentication attaches the user to *that* id. Nothing mints
a fresh session id at the authentication boundary.

An attacker who can plant a cookie (a sibling subdomain setting
`Domain=example.com`, any cookie-injection primitive, an active network position
on a non-HSTS host) fixes the victim's session id to a known value; after the
victim signs in, the attacker replays it as a cookie or as `?session=` on an RPC
connection (**D5**) and is the victim. The standard mitigation — regenerate the
identifier on privilege change — is absent by design.

- `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:72`–`:78`, `:106`–`:112`
- `src/ActualLab.Fusion/Session/Session.cs:36`–`:42`
- `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:131`, `:179`

**Fix:** rotate at the privilege boundary — after a successful `SignIn`, mint
`Session.New()`, migrate the auth state, overwrite the cookie (the RPC
connection's bound session picks it up on the next reconnect). Optionally have
`SessionMiddleware` refuse cookie values unknown to the session store.

### D7 · MEDIUM · **B** — `JsonifyErrorsAttribute` returns unfiltered exception type and message

Both filters serialize `exception.ToExceptionInfo()` — assembly-qualified type
name plus raw `Message` — into every 500 response, with no allow-list, no
rewriting hook and no environment gate. For `SqlException`, `IOException`,
`SocketException` or EF's `InvalidOperationException` that routinely includes
table/column names, file paths, hostnames or connection details. The tests still
refer to a "RewriteErrorsIfSet" behaviour the implementation no longer has.

- `src/ActualLab.Fusion.Server/JsonifyErrorsAttribute.cs:21`–`:27`
- `src/ActualLab.Fusion.Server.NetFx/JsonifyErrorsAttribute.cs:25`–`:32`

**Fix:** reinstate a rewrite/allow-list hook defaulting to "generic
`RemoteException` + correlation id"; RestEase's client already degrades gracefully
to `Errors.UnknownServerSideError()`, so a redacted default does not break the
round-trip.

### D8 · MEDIUM · **O** — The .NET Framework Fusion server never binds a session to RPC connections

On ASP.NET Core, `FusionWebServerBuilder` registers `RpcDefaultSessionReplacer`
and a module initializer installs the session-binding `ServerConnectionFactory`.
The NetFx builder does neither, and the assembly contains no equivalent of either
type. Every session-taking compute method/command therefore receives
`Session.Default`, which `RequireValid()` rejects. It fails closed, so it is a
functionality gap rather than a hole — but a silent one: nothing says session-bound
RPC is unsupported on .NET Framework.

- `src/ActualLab.Fusion.Server.NetFx/FusionWebServerBuilder.cs:17`–`:33`
- contrast `src/ActualLab.Fusion.Server/FusionWebServerBuilder.cs:38`–`:47`

**Fix:** port `RpcPeerOptionsExt`/`SessionBoundRpcConnection`/`RpcDefaultSessionReplacer`
to the OWIN project, or document the limitation and fail fast at startup.

### D9 · MEDIUM · **C** — NetFx `UseDefaultSessionAttribute` resolves `ISessionResolver` from the root provider

The filter resolves from `Configuration.DependencyResolver` — the root Web API
provider — rather than the request's dependency scope, while Fusion registers
`ISessionResolver` as **scoped** and initializes the request instance. A POST
carrying an `ISessionCommand` with `Session.Default` therefore hits an
uninitialized resolver and `command.UseDefaultSession` throws. With a custom
mutable root resolver the alternative failure is worse: requests can share or
reuse another request's session.

- `src/ActualLab.Fusion.Server.NetFx/HttpContextExt.cs:26`; `UseDefaultSessionAttribute.cs:20`
- `src/ActualLab.Fusion/FusionBuilder.cs:114`; `src/ActualLab.Fusion/Session/SessionResolver.cs:32`

**Fix:** resolve from `actionContext.Request.GetDependencyScope()`, fail closed if
no request session was initialized, and add a concurrent two-request test.

### D10 · MEDIUM · **C** — `AddRestEase` changes 500 handling for *every* `IHttpClientFactory` client

`AddRestEase` registers an `IHttpMessageHandlerBuilderFilter`, which runs for
every named and typed client in the application — not only those added through
`RestEaseBuilder.AddClient`. `Configure` has no name/type condition and inserts the
Fusion-specific 500 translator at index zero for every builder, so an unrelated
client whose contract requires inspecting a 500 response has it consumed, disposed
and replaced with a `RemoteException`, breaking retry and status-code logic
application-wide.

- `src/ActualLab.RestEase/RestEaseBuilder.cs:35`, `:36`
- `src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandlerBuilderFilter.cs:12`

**Fix:** attach the handler only to the named clients created by
`RestEaseBuilder.AddClient` via per-client `IHttpClientBuilder` configuration.

### D11 · MEDIUM · **C** (PLAUSIBLE) — NetFx `TextMediaTypeFormatter` buffers and duplicates the whole request body

`ReadFromStreamAsync` copies the complete request stream into an unbounded
`MemoryStream` and then calls `ToArray()`, temporarily duplicating the payload,
with no size limit and no cancellation path. Severity depends on IIS/proxy
request-size limits, which live outside the repo — hence PLAUSIBLE.

- `src/ActualLab.Fusion.Server.NetFx/TextMediaTypeFormatter.cs:21`, `:23`, `:25`

**Fix:** configurable maximum text-body size, 413 on oversize, bounded/cancellable
decode without the extra copy; document the required matching proxy limit.

### D12 · LOW · **O** — An over-long `f` query value makes the rejection path throw instead of closing the socket

The unsupported-format rejection echoes the attacker-supplied format key into the
WebSocket close description. `CloseAsync` rejects a description longer than 123
UTF-8 bytes; the fixed prefix is 38 characters, so any `f` over ~85 characters
throws. The outer catch swallows it, so the close frame is never sent and the
socket is aborted — while producing a `Warning` record with a stack trace per
unauthenticated request (cheap log amplification).

- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:53`–`:57`

**Fix:** truncate the format key before interpolating, or reject unsupported
formats with a 400 before the upgrade, as the OWIN server already does.

### D13 · LOW · **O** — `MapFusionRenderModeEndpoints` emits a literal `Location: ~/`

`redirectTo` defaults to the MVC-style app-relative `"~/"`. The MVC path expands
it via `IUrlHelper.Content`; the minimal-API path does not — `Results.Redirect("~/")`
writes the literal string, so the browser resolves it relative to
`/fusion/renderMode/…` and lands on a non-existent path.

- `src/ActualLab.Fusion.Server/Endpoints/RenderModeEndpoint.cs:42`–`:45`, `:61`–`:65`

**Fix:** use `"/"`, or resolve `~/` against `Request.PathBase`.

### D14 · LOW · **O** — Duplicate `clientId`/`f` parameters throw; OWIN echoes an unvalidated sub-protocol

(a) `query[...].SingleOrDefault()` throws on two entries, so
`?clientId=a&clientId=b` yields a 500 plus a `Warning` with a stack trace rather
than a 400 — while the `session` parameter in `RpcPeerOptionsExt.cs:30` already
handles the duplicate case explicitly, so the inconsistency is unintended.
(b) The OWIN accept-context factory reflects the client's first offered
sub-protocol into `Sec-WebSocket-Protocol` without checking it against anything the
server supports; RFC 6455 requires choosing one the server implements, and the RPC
layer implements none.

- `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`–`:31`
- `src/ActualLab.Rpc.Server/RpcHttpServerDefaultDelegates.cs:20`–`:21`
- `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServerDefaultDelegates.cs:36`–`:38`

**Fix:** `Count == 1 ? values[0] : ""` plus a 400 for ambiguous values; drop the
sub-protocol echo or match it against a supported list.

---

## E. Fusion core: Computed, State, invalidation, operations, client cache

### E1 · MEDIUM (verified down from HIGH) · **C** — Completed compute calls create unbounded, indefinite server-side subscriptions

After returning a compute result, the server keeps the inbound call registered until its
captured `Computed` invalidates — and `AutoInvalidationDelay` defaults to `TimeSpan.MaxValue`,
i.e. no automatic invalidation. Regular calls `Unregister()` immediately; compute calls
instead `await ProcessStage2(...)`, which awaits `computed.WhenInvalidated(...)`. The
per-peer inbound-call dictionary has no count or age limit (**B4**). A peer that repeatedly
invokes any exposed compute method with fresh call ids and never cancels retains, per
request, an inbound call, its cancellation state, a continuation/event handler, and a strong
reference into the computed graph.

- `src/ActualLab.Fusion/Client/Internal/RpcInboundComputeCall.cs:83`, `:90`–`:107`
- `src/ActualLab.Fusion/Configuration/ComputedOptions.cs:32`
- `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`, `:59`

**Fix:** enforce a configurable per-peer limit on result-ready compute subscriptions; add a
finite post-result lease/idle timeout and unregister on expiry even when the computed is
still valid. *(Under verification — **V4** — specifically whether duplicate (method, args)
calls collapse onto one computed and one registration.)*

### E2 · HIGH · **C** — Invalidation replay failures are acknowledged, permanently losing cache invalidations

Exceptions thrown while replaying a committed command in invalidation mode are logged but
never propagated: `TryInvalidate` catches every handler exception and returns the next index
without throwing, and the outer handler only labels the telemetry outcome as an error.
`CompletionProducer` rethrows only for *external* completion errors and merely logs local
ones. `NotifyCompleted` inserts the operation UUID into the recently-seen set **before**
invoking listeners and does not remove it on failure, and the EF log reader deliberately
skips rows from the local host. Net effect: a command commits, its invalidation replay
fails, everything downstream treats the completion as successful, and — with the default
infinite auto-invalidation horizon — authorization or application data cached from before
the write can stay stale indefinitely.

- `src/ActualLab.Fusion/Operations/Internal/InvalidatingCommandCompletionHandler.cs:151`, `:164`, `:87`–`:100`
- `src/ActualLab.Fusion/Operations/Internal/CompletionProducer.cs:34`–`:54`
- `src/ActualLab.Fusion/Operations/OperationCompletionNotifier.cs:55`, `:93`–`:104`
- `src/ActualLab.Fusion.EntityFramework/Operations/LogProcessing/DbOperationLogReader.cs:29`–`:34`

**Fix:** attempt all replay handlers, then throw an aggregate so external readers redeliver.
For local operations keep the client-visible commit successful but durably enqueue the failed
completion for retry, drop its recently-seen marker, and let the local log reader retry until
invalidation succeeds. (See also **G6**, which can independently suppress a legitimate replay.)

### E3 · MEDIUM · **B** — `SharedRemoteComputedCache` is a process-global static and `RpcCacheKey` carries no peer/endpoint namespace

`SharedRemoteComputedCache.Instance` is a mutable static assigned `??=`, unsynchronized, so
the *first* service provider in the process wins and every later provider silently reuses
that instance regardless of its own configuration. `RpcCacheKey` consists only of the RPC
method name and serialized arguments — no peer, endpoint, hub, provider, tenant or auth
namespace. In a process hosting two Fusion client containers against different backends (a
desktop app with multiple accounts, tenant-scoped clients, an integration-test process), a
value cached from one server can be served to the other, and a cache hit is returned
immediately, before background RPC validation.

The two reviewers rated this differently — **C** called it HIGH on the mechanism, **O** called
it LOW because no in-repo configuration coexists two differently-scoped containers. MEDIUM is
the honest midpoint. *(Under verification — **V5**.)*

- `src/ActualLab.Fusion/Client/Caching/SharedRemoteComputedCache.cs:12`–`:32`
- `src/ActualLab.Fusion/FusionBuilder.cs:454`–`:469`
- `src/ActualLab.Rpc/Caching/RpcCacheKey.cs:22`–`:33`
- `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:283`–`:303`, `:462`–`:484`

**Fix:** replace the mutable process-global with an explicitly supplied shared-cache object
whose scope the application controls, and namespace every key by a stable server/route
identity plus any tenant/session context the result depends on.

### E4 · MEDIUM · **O** (reproduced) — `Computed.CopyDependenciesTo` never advances `ArrayBuffer.Count`, so `ComputedSynchronizer` always reports "synchronized"

`CopyDependenciesTo` copies the dependency set into the buffer's backing array but never
increases `buffer.Count` (and `EnsureCapacity` does not touch `_count` either). Every
consumer iterates `0 .. buffer.Count` — i.e. iterates nothing. So
`ComputedSynchronizer.IsSynchronized(Computed)` returns `true` and `WhenSynchronized`
returns `Task.CompletedTask` for **any** computed that is not itself an `IRemoteComputed` /
`IStateBoundComputed` / `IHasSynchronizationTarget`, regardless of its dependencies.
`ComputedImpl.CopyAllDependenciesTo` is a complete no-op.

This matters because `ComputedSynchronizer` is the mechanism that lets a client distinguish
"this value came from the local `RemoteComputedCache` and is unconfirmed" from "this value is
confirmed". The typical client pattern — a local compute method aggregating several remote
calls — produces a plain `ComputeMethodComputed<T>`, which takes exactly the dead recursive
branch. `ComputedSynchronizer.Precise` is therefore behaviourally identical to `.None` for
aggregate computeds, and combined with the serve-stale-on-disconnect path the application has
no working way to detect it is looking at stale data. Reproduced:

```
buffer.Count after CopyDependenciesTo = 0
GetDependencies().Length = 1
TestSynchronizer.IsSynchronized(outer) = True (expected False), visits = 1
WhenSynchronized(outer).IsCompleted = True (expected False)
```

Broken since at least `c508487ef` (2024-06-29); the predecessor `CopyUsedTo` had the same
defect, so this has never worked.

- `src/ActualLab.Fusion/Computed.cs:556`
- `src/ActualLab.Fusion/ComputedSynchronizer.cs:44`–`:53`, `:74`
- `src/ActualLab.Fusion/Internal/ComputedImpl.Helpers.cs:7`

**Fix:** set `buffer.Count = count + depCount` after the copy. **This fix must be paired with
hardening `ComputedSynchronizer`'s DFS**, which is currently dormant: it has no visited set
(so a diamond graph is traversed exponentially), no depth bound (deep chains overflow the
stack), and leases one pooled `ArrayBuffer` per recursion level. Convert it to an explicit
worklist with a visited set and a node budget in the same change.

### E5 · MEDIUM · **C** — An invalidated-in-flight computation is briefly published as `Consistent`

Invalidation during computation only ORs an `InvalidateOnSetOutput` flag and returns.
`TrySetOutput` then publishes the output with state `Consistent`, releases the lock, and only
*afterwards* checks the flag and invalidates. In that gap, another request for the same input
takes the lock-free `TryUseExisting` fast path and receives a value computed across a
known-invalid dependency epoch. The later invalidation makes it self-correcting, but the stale
value has already escaped and may have driven an authorization or data decision.

- `src/ActualLab.Fusion/Computed.cs:274`–`:278`, `:371`–`:387`
- `src/ActualLab.Fusion/Internal/ComputedImpl.Helpers.cs:19`–`:32`

**Fix:** never expose `Consistent` when `InvalidateOnSetOutput` is set — publish the output and
transition directly to `Invalidated` (or a non-consumable transitional state) under the same
lock, doing propagation and cleanup outside it.

### E6 · MEDIUM · **C** — Faulted cache lifecycle tasks are treated as success and can resurrect stale entries

`WhenInitializedUnlessVersionKey` checks `Task.IsCompleted` rather than *successful*
completion, and the read path silently suppresses initialization faults before consulting the
backing store. So if persistent storage fails while clearing the cache after a version change,
subsequent requests keep reading entries the version barrier was meant to invalidate. The
flushing cache has the matching defect: it treats a faulted prior flush as complete, replaces
the failed queue, and never retries — once a failed removal is no longer visible in
`FlushingQueue`, `Get` falls through to the backing store and resurrects the stale value.

- `src/ActualLab.Fusion/Client/Caching/RemoteComputedCache.cs:49`, `:87`–`:90`, `:103`–`:107`
- `src/ActualLab.Fusion/Client/Caching/FlushingRemoteComputedCache.cs:46`–`:54`, `:90`–`:93`, `:112`–`:138`

**Fix:** gate reads and writes on `IsCompletedSuccessfully`; fail the cache closed on
initialization failure (surface the error or return a miss without consulting old storage).
Preserve a failed flushing dictionary, merge later mutations into it with correct
last-write-wins ordering, and retry with backoff.

### E7 · MEDIUM · **C** — `WhenInvalidated` can leak cancellation registrations and whole computed graphs

`WhenInvalidatedClosure` subscribes to invalidation at line 22 but assigns
`_cancellationTokenRegistration` only at line 24. If invalidation wins that constructor race,
the handler disposes the field's *default* value; the constructor then creates a real
registration that nothing disposes until the token itself is cancelled. `ComputedState`'s
update loop calls `WhenInvalidated` with the long-lived disposal token, so under concurrent
invalidation/recomputation each race leaves a completed wait registered on that token — and
the token source retains the closure, which retains the old `Computed`, its input, and
potentially its dependency graph.

- `src/ActualLab.Fusion/Internal/WhenInvalidatedClosure.cs:17`–`:25`, `:27`–`:31`
- `src/ActualLab.Fusion/ComputedExt.cs:114`–`:123`; `src/ActualLab.Fusion/State/ComputedState.cs:167`–`:176`

**Fix:** coordinate subscription and registration with a small atomic state machine — after
storing the registration, re-check whether completion already won and dispose it if so; ensure
cancellation and invalidation each remove the handler and dispose the registration exactly once.

### E8 · MEDIUM · **C** — Disposed `RpcPeerStateMonitor` instances stay rooted by an undisposed token registration

Each `ComputeLastReconnectDelayCancelledAtState` computation registers its `Computed` on the
reconnect delayer's cancellation token and **discards** the returned
`CancellationTokenRegistration`. `DisposeAsyncCore` disposes only the two states, and
`ComputedState.Dispose` does not invalidate the current computed or unregister callbacks held
by unrelated tokens. The hub-owned retry delayer's still-live token therefore roots the
computed, which roots the monitor and its scoped service graph — so every disposed scope can
remain live for the lifetime of the hub unless something calls `CancelDelays`.

- `src/ActualLab.Fusion/Extensions/RpcPeerStateMonitor.cs:72`–`:78`, `:147`–`:157`
- `src/ActualLab.Fusion/State/ComputedState.cs:114`–`:131`; `src/ActualLab.Core/Net/RetryDelayer.cs:27`–`:31`

**Fix:** store the registration on the monitor, atomically replace/dispose it on
recomputation, and dispose it in `DisposeAsyncCore`.

### E9 · MEDIUM · **O** — `ApplyRpcUpdate` is fire-and-forget with no catch-all

`ComputeCachedOrRpc` starts the "confirm the cached value against the server" task with
`_ = ExecutionContextExt.Start(...)`. `ApplyRpcUpdate` has `try`/`catch` only around step 1;
steps 5–8 (`RequireKeyAndValue`, `InputLocks.Lock`, `UpdateCache` → `IRemoteComputedCache.Set/Remove`,
`NewRemoteComputed`) run unguarded. Any exception there — the **E10** NRE, or a user-supplied
cache implementation throwing on a full disk or locked DB — faults a task nobody awaits:
`SynchronizedSource` is never completed, so `ComputedSynchronizer.Precise` waits forever on
that computed, and the failure is invisible except via `TaskScheduler.UnobservedTaskException`.
In DEBUG builds the `Debug.Assert(!call.IsHandOffPending, …)` is another unguarded throw site
on the same path.

- `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:300`–`:303`, `:376`–`:409`

**Fix:** wrap the whole body in `try/catch` that logs, invalidates `cachedComputed`, and
completes `SynchronizedSource` so waiters are released.

### E10 · MEDIUM · **O** — `ComputeRpc` dereferences `RpcCacheInfoCapture.Call` without a null check

`ComputeRpc` calls `cacheInfoCapture.HasKeyAndValue(...)` for every non-cancellation outcome,
and that method starts with `lock (Call!.Lock)`. `Call` is assigned only inside `CaptureKey`,
which runs only from `SendRegistered()`/`RegisterCacheKeyOnly()` — so if the call fails before
it reaches the wire, `Call` is still `null` and the caller gets an `NullReferenceException`
instead of the real error. Concrete path: the peer looks connected, drops in the window before
`RpcOutboundCall.Invoke` re-checks, `WhenConnectedOrReroute` throws `TimeoutException` (not an
`OperationCanceledException`, so the guard above does not fire), `SetError` records the error
without setting `Call`, and the deref throws. Connection flapping and server restarts make this
reachable in normal operation. The sibling background path already guards for exactly this
shape (`ApplyRpcUpdate:337`, `if (call is null) … return;`); `ComputeRpc` has no equivalent.

- `src/ActualLab.Fusion/Client/Interception/RemoteComputeMethodFunction.cs:244`
- `src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs:49`, `:60`

**Fix:** make `HasKeyAndValue`/`RequireKeyAndValue` null-safe and add a `call is null` early-out
in `ComputeRpc` mirroring `ApplyRpcUpdate:337`.

### E11 · MEDIUM · **O** — `InMemoryRemoteComputedCache` has no eviction of any kind

The cache registered by `AddInMemoryRemoteComputedCache()` is a bare `ConcurrentDictionary`
with no size cap, TTL or LRU. Entries are added on flush and removed only when the
corresponding call errored, or by an explicit `Clear()`. The key is
`(methodFullName, serializedArguments)`, so every distinct argument tuple — a document id, a
search string, a paging cursor — becomes a permanent entry holding the full serialized
response. `ComputedRegistry` self-prunes via weak references, so the `Computed` instances
disappear but their cached payloads do not. Note the contrast with `ComputedOptions.MinCacheDuration`
and `ComputedGraphPruner`, which bound every *other* Fusion cache.

- `src/ActualLab.Fusion/Client/Caching/InMemoryRemoteComputedCache.cs:21`, `:35`

**Fix:** back it with a bounded/evicting store — `RecentlySeenMap<TKey,TValue>` already ships in
`ActualLab.Core` — or add `MaxEntryCount`/`MaxEntryAge` options and evict on `Flush`.

### E12 · MEDIUM · **C** (PLAUSIBLE) — Whole-chain invalidation tracking can create a self-cycle and spin forever

If a dependency invalidates during the `AddDependency`/`AddDependant` race, `AddDependant`
invalidates the dependant with **the dependant itself** as its invalidation source. Under
`InvalidationTrackingMode.WholeChain` that stores a self-referential source, and both origin
walkers (`InvalidationSource.Origin` and `GetInvalidationOrigin`) follow links in unbounded
loops with no visited set. `FusionMonitor` invokes the latter synchronously during
unregistration, so it can wedge the invalidating thread and the monitor's statistics lock.

- `src/ActualLab.Fusion/Computed.cs:496`–`:510`; `src/ActualLab.Fusion/InvalidationSource.cs:35`–`:46`, `:68`–`:71`
- `src/ActualLab.Fusion/ComputedExt.cs:105`–`:109`; `src/ActualLab.Fusion/Diagnostics/FusionMonitor.cs:211`–`:218`

**Fix:** use the invalidated dependency (`this`) as the source at `Computed.cs:509`, not the
dependant; and add identity-cycle detection or a bounded traversal to both origin walkers.

### E13 · LOW · **O** — `TrySetOutput` publishes the `Consistent` flag before `_output`

Inside the lock, `_state` is flipped to `Consistent` *before* `_output` is assigned, and
readers of `ConsistencyState`/`Output`/`Value` do not take the lock. A reader observing
`Consistent` between the two stores reads the initial output — `default(T)`, i.e. `null`/`0`/
`false`. Every in-framework consumer funnels through `GetValuePromise()`, which takes the same
monitor, so the residual risk is application code reading `computed.Value` directly after
`TryUseExisting` returns `true`. Silent `default(T)` in a cached-authorization method (e.g.
`Task<bool> IsBanned(...)` observing `false`) is the worst case.

- `src/ActualLab.Fusion/Computed.cs:374`–`:380`, `:84`–`:90`

**Fix:** assign `_output` first, then flip the flag; if lock-free reads of `ConsistencyState`
are a supported contract, make the pair `Volatile.Write`/`Volatile.Read`.

### E14 · LOW · **O** — `IFusionTime.Now(TimeSpan)` clamps only the upper bound of a client-supplied period

`TrimInvalidationDelay` applies only `Min(delay, MaxInvalidationDelay)`. A zero or negative
`updatePeriod` invalidates the computed the instant it is produced; a sub-second positive value
allocates a dedicated `CancellationTokenSource(delay)` + timer per computed. `AddFusionTime()`
registers `IFusionTime` with `RpcServiceMode.Default`, so in a server-mode app it is exposed to
every client. `GetMomentsAgo` degenerates the same way through a negative `(int)` cast.

- `src/ActualLab.Fusion/Extensions/Internal/FusionTime.cs:35`–`:38`, `:48`, `:81`–`:82`

**Fix:** add a `MinUpdatePeriod` option and clamp both ends; clamp `delta` before the `int` cast.

### E15 · LOW · **O** — `InvalidationTrackingMode.WholeChain` retains an unbounded strong-reference chain

In `WholeChain` mode `new InvalidationSource(Computed)` stores the invalidating `Computed`
itself and `_invalidationSource` is never cleared, so one live computed at the tail pins the
entire historical chain of already-invalidated computeds and, through them, their inputs and
argument objects. `ComputedGraphPruner`/`ComputedRegistry.PruneUnsafe` cannot help — these are
strong references. The default is `OriginOnly`, so this only bites operators who turn on
whole-chain diagnostics — typically while already debugging a production incident.

- `src/ActualLab.Fusion/InvalidationSource.cs:68`–`:71`; `src/ActualLab.Fusion/Computed.cs:266`–`:269`, `:332`

**Fix:** store a `WeakReference<Computed>`/`ComputedRef` in `WholeChain` mode and/or bound the
retained depth; document it as a short-lived diagnostic mode.

### Note carried out of this area

`RemoteComputed` has a finalizer that performs RPC bookkeeping —
`~RemoteComputed() => Dispose()` calls `RpcOutboundCall.CompleteAndUnregister(...)`, which
touches the peer's outbound-call registry, calls `CancellationTokenRegistration.Dispose()`
(which *blocks* if the callback is concurrently running on another thread) and can send a
`Cancel` system call, all on the finalizer thread. `Dispose()` never calls
`GC.SuppressFinalize(this)`, so the finalizer runs even after explicit disposal. Transport
`Send` is non-blocking, which keeps this low — but a stalled finalizer thread halts *all*
finalization process-wide. (`src/ActualLab.Fusion/Client/RemoteComputed.cs:63`–`:73`)

---

## F. Sessions, auth, extension services, EF Core & Redis

### F1 · CRITICAL · **B** — `IKeyValueStore` is exposed as a frontend RPC service, defeating `SandboxedKeyValueStore` entirely

`IKeyValueStore` is declared `IComputeService` only — **not** `IBackendService`. Its two write
commands are marked `IBackendCommand` (which, per **H1**, does nothing), but its three read
methods `Get(shard, key)`, `Count(shard, prefix)` and `ListKeySuffixes(shard, prefix, pageRef, …)`
carry no marker at all. Registered the documented way — `fusion.AddDbKeyValueStore<TDbContext>()`
→ `fusion.AddService<IKeyValueStore, …>()` with `RpcServiceMode.Default` — inside a server-mode
container, those three become ordinary client-callable RPC methods with **no session argument
and no authorization check**. Fusion's own test harness uses exactly this configuration.

The RPC wire protocol is name-based, so a client does not need the contract assembly. Any
connected peer can call
`IKeyValueStore.ListKeySuffixes("", "@user/", new PageRef<string>(10000))` to enumerate every
user's keys and then `Get("", "@user/<victimId>/<key>")` to read them — plus everything else the
application stores. `SandboxedKeyValueStore`, whose entire purpose is to constrain a client to
`@session/{id}` or `@user/{id}` prefixes, delegates to the very same service and is simply
bypassed. Contrast `IAuthBackend : IComputeService, IBackendService`, where the service-level
gate makes `GetUser(shard, userId)` unreachable.

Combined with **H1**, the *write* commands are reachable too — the backend-gate verifier
demonstrated `IKeyValueStore.Set` succeeding from a non-backend peer end-to-end.

- `src/ActualLab.Fusion.Ext.Services/Extensions/IKeyValueStore.cs:8`, `:15`–`:20`
- `src/ActualLab.Fusion.Ext.Services/Extensions/FusionBuilderExt.cs:38`, `:66`
- `src/ActualLab.Fusion.Ext.Services/Extensions/Services/SandboxedKeyValueStore.cs:74`, `:113`
- gate: `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`; `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:105`

**Fix:** make `IKeyValueStore : IComputeService, IBackendService`. That keeps the intended usage
(server-side code and `SandboxedKeyValueStore` resolve it locally from DI; backend peers can
still call it) while making every method — reads included — unreachable from client peers.
`AddDbKeyValueStore`/`AddInMemoryKeyValueStore` should also pass `RpcServiceMode.Local`
explicitly rather than inheriting the container default. Add a service-registry regression test
asserting the raw service and all its methods are absent from a frontend peer.

### F2 · HIGH · **B** — Unauthenticated `IAuth.SignOut` inserts a DB session row + operation-log row for any attacker-chosen session id

`IAuth.SignOut` is a plain, non-backend, client-callable command, and a `Session` is accepted
as long as its id is ≥ 8 characters. Its DB implementation calls
`Sessions.GetOrCreate(dbContext, session.Id, …)`, which **inserts** a `_Sessions` row for an
unknown id, then `Upsert`s it. The command runs inside a `DbOperationScope`, so each call also
adds a `DbOperation` row and, on completion, fires `NotifyChanged` to every host via
LISTEN/NOTIFY or Redis pub/sub — causing every host in the cluster to read and replay the
operation. Rows are trimmed only after `MaxSessionAge` = **60 days**.

One cheap RPC frame → 3 DB writes + N-host invalidation replay, with no authentication.
`InMemoryAuthService.SignOut` correctly uses `GetSessionInfo` and returns when it is null; only
the DB implementation creates.

- `src/ActualLab.Fusion.Ext.Contracts/Authentication/IAuth.cs:12`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.cs:39`, `:84`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbSessionInfoRepo.cs:61`–`:74`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbSessionInfoTrimmer.cs:30`

**Fix:** query with an update lock and return immediately when the row is absent — signing out a
session that was never set up is a no-op. Optionally set `MustStoreOperation = false` on that
path, and rate-limit session-creating commands per peer.

### F3 · HIGH · **B** — Unknown session shard tags permanently grow per-shard caches

In a multi-shard deployment the target shard comes from a tag inside the **client-supplied
session id** (`session.GetTag("s")`). The only validation before it is used as a dictionary key
is `DbShard.Validate`, which merely rejects special names — registry membership is checked
*later*. Two long-lived caches are populated with `GetOrAdd` before that check, and neither entry
is removed when it fails: `DbEntityResolver._batchProcessors` (each entry is a live
`BatchProcessor` with an unbounded channel, a CTS and at least one long-running worker task —
drained only in `DisposeAsync`) and `ShardDbContextFactory._factories` (whose `CacheEntry` is
removed only when the factory is already disposed).

An unauthenticated peer calls `IAuth.GetSessionInfo(new Session("aaaaaaaa&s=" + random))`
repeatedly; each distinct string permanently costs one worker task, one unbounded channel, one
CTS and two dictionary entries. Single-shard deployments are unaffected (`Resolve` short-circuits
to `DbShard.Single`) — so this is specific to sharded/multi-tenant hosts, exactly where it matters
most.

- `src/ActualLab.Fusion.EntityFramework/Sharding/DbShardResolver.cs:51`, `:58`; `Sharding/DbShard.cs:11`
- `src/ActualLab.Fusion.EntityFramework/DbEntityResolver.cs:151`, `:287`
- `src/ActualLab.Fusion.EntityFramework/Sharding/ShardDbContextFactory.cs:149`–`:170`, `:347`

**Fix:** reject values for which `ShardRegistry.CanUse(shard)` is false inside
`DbShardResolver.Resolve`, before the value is ever used as a cache key. Independently, remove
the `CacheEntry` when initialization fails and only add a batch processor for an accepted shard.

### F4 · MEDIUM (verified down from HIGH) · **C** — Public auth responses serialize stored identity secrets

`DbUserConverter.UpdateModel` maps `ui.Secret` into `User.Identities`, and
`User.JsonCompatibleIdentities` serializes those values. The type already provides
`ToClientSideUser()`, which replaces identities with hidden placeholders — but neither
`DbAuthService.GetUser(Session)` nor `InMemoryAuthService` applies it on the public return path.
Applications are explicitly allowed to use this field for password hashes, so exposing it to a
browser turns a short-lived authenticated session into offline credential disclosure.

- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbUserConverter.cs:49`
- `src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs:41`, `:100`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.cs:185`; `InMemoryAuthService.cs:164`

**Fix:** apply `ToClientSideUser()` to every result of the frontend `IAuth.GetUser(Session)`
contract, keeping the unmasked model behind `IAuthBackend`. Better: a separate client DTO with no
secret-valued property, plus a serializer test asserting a non-empty stored secret never appears
in the client response. *(Under verification — **V1** — including whether the binary wire members
actually carry it.)*

### F5 · HIGH · **C** — Anonymous sandboxed writes have no quota or size bound

`ISandboxedKeyValueStore.Set` is reachable before authentication, and the sandbox limits only the
key prefix and expiration. `GetKeyChecker` calls `Auth.GetUser` but still constructs a
session-prefix checker when that result is null, so guests get a writable namespace. There is no
per-session key count, aggregate byte quota, per-call item limit or value-size limit; `Set`
allocates an array sized directly from the request and forwards every accepted item, and the
`DbKeyValue` entity has no model-level maximum on either column. The default 30-day expiry bounds
lifetime but not write rate or total live data.

- `src/ActualLab.Fusion.Ext.Contracts/Extensions/ISandboxedKeyValueStore.cs:11`, `:34`
- `src/ActualLab.Fusion.Ext.Services/Extensions/Services/SandboxedKeyValueStore.cs:24`, `:39`, `:46`, `:103`, `:110`
- `src/ActualLab.Fusion.Ext.Services/Extensions/Services/DbKeyValueStore.cs:21`, `:48`; `DbKeyValue.cs:14`

**Fix:** enforce per-call item/byte limits and per-session live-key and aggregate-byte quotas; cap
key/value lengths at both validation and schema level; rate-limit anonymous writes and make guest
storage opt-in or much shorter-lived. Quota checks and writes must be atomic.

### F6 · HIGH · **O** (reproduced) — `User.ToClientSideUser()` mutates the process-global `ApiMap.Empty`

The helper whose job is to *mask* a user's identities starts from the shared static
`ApiMap<UserIdentity, string>.Empty` and calls `TryAdd` on it. `ApiMap` derives from
`Dictionary<,>` and `Empty` is a single process-wide instance, so this mutates global state: the
"empty" map stops being empty, every `User` constructed afterwards silently inherits the
accumulated identities, and every "masked" user object returned is literally the *same* map
object shared with every other caller — so user A's masked user also lists user B's identity
schemas, which are then serialized to the client. It is also an unsynchronized `Dictionary`
mutation from concurrent request threads, which can corrupt the buckets and hang or crash any
thread that later enumerates it (and it *is* enumerated on every `User` serialization).

Reproduced against the published `ActualLab.Fusion.Ext.Contracts` package:

```
Before: ApiMap<UserIdentity,string>.Empty.Count = 0
After ToClientSideUser: Empty.Count = 1
Fresh guest Identities is the SAME object as Empty: True
After Bob: Empty.Count = 2
Alice's 'masked' map now also lists Bob: Github/<hidden>, Google/<hidden>
```

- `src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs:100`–`:109`
- `src/ActualLab.Core/Api/ApiMap.cs:10`, `:14`

**Fix:** build a fresh `new ApiMap<UserIdentity, string>()`. Separately, `ApiMap.Empty` being a
mutable `Dictionary` handed out as a default value is a latent hazard for every other `ApiMap`
user — make it frozen/immutable, or at minimum document that it must never be mutated. Note **F4**
and **F6** interact: the correct fix for F4 is to call the very method that F6 makes unsafe, so
**F6 must be fixed first**.

### F7 · MEDIUM · **O** — Operation/event log payloads are deserialized with `TypeNameHandling.Auto` and executed as commands

`DbOperation` and `DbEvent` persist with `NewtonsoftJsonSerializer.Default` — `TypeNameHandling.Auto`,
no `SerializationBinder` (**C1**). `DbEvent.ToModel()` deserializes `ValueJson` with declared type
`typeof(object)`, so Json.NET's assignability check is vacuous and any `$type` in the row is
instantiated; if the result implements `ICommand`, `DbEventProcessor.Process` hands it straight to
`Commander.Call(command, isOutermost: true, …)` — a fully privileged local command that bypasses the
RPC backend gate entirely. `_Operations` rows are similarly deserialized on every host during
invalidation replay, and `PropertyBag` values are `object`-typed.

Anyone who can write a `_Events` row — a lower-privileged component sharing the database, a leaked
credential, an SQL injection elsewhere, a restored attacker-supplied backup — gets arbitrary type
instantiation on every host running the log reader, plus arbitrary privileged command execution
(e.g. an `AuthBackend_SignIn` row authenticating an attacker-chosen session as any user). This is
defence-in-depth rather than directly remote-reachable, but stored operation-log payloads are
explicitly in the threat model.

- `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`
- `src/ActualLab.Fusion.EntityFramework/Operations/DbEvent.cs:19`, `:63`–`:72`; `DbOperation.cs:18`, `:53`
- `src/ActualLab.Fusion.EntityFramework/Operations/DbEventProcessor.cs:29`–`:32`

**Fix:** give `DbOperation.Serializer`/`DbEvent.Serializer` a dedicated serializer with a strict
allow-list binder, and deserialize `ValueJson` with a declared type of `ICommand` (or a dedicated
marker) rather than `object`, so the assignability check actually constrains the result.

### F8 · MEDIUM · **O** — `InMemoryAuthService.GetUserSessions` is missing `[ComputeMethod]`, so `IAuth.GetUserSessions` is never invalidated

The DB implementation declares the intermediate helper `[ComputeMethod]`; the in-memory one does
not, and the overload is on no interface, so `ComputedOptions.Get` returns `null` and it is not
intercepted. `IAuth.GetUserSessions(Session)` calls it, so no dependency is recorded, and the
`_ = GetUserSessions(shard, userId, default)` calls inside every `Invalidation.IsActive` block
invalidate nothing. A client subscribed to `IAuth.GetUserSessions` serves a stale session list
indefinitely — sessions signed in or kicked on other devices never appear or disappear, so a
"manage my sessions / sign out everywhere" UI silently diverges from reality, including still
listing sessions the user believes they revoked.

- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/InMemoryAuthService.Backend.cs:150`
- contrast `.../DbAuthService.Backend.cs:165`

**Fix:** add `[ComputeMethod]`. Add a test asserting `IAuth.GetUserSessions` is invalidated when
another session of the same user signs out.

### F9 · MEDIUM · **O** — `Session` ids have no maximum length or charset, while `DbSessionInfo.Id` is `varchar(256)`

The constructor validates only a *minimum* length of 8. A remote peer can pass a multi-megabyte
session id to any session-taking compute method — each distinct value becomes a distinct `Computed`
registry key, a distinct `DbEntityResolver` input and a distinct SQL parameter: cheap to send,
expensive to hold. Ids over 256 characters make the `_Sessions` insert fail on providers that
enforce the length, turning `AuthBackend_SetupSession`/`IAuth.SignOut` into a guaranteed
server-side error path rather than a clean rejection. `SandboxedKeyValueStore` also builds key
prefixes by `string.Format("@session/{0}", session.Id)`, so an attacker-chosen id controls the
shape of stored keys (including embedded `/` delimiters, which `MatchesPrefix` treats specially —
no cross-session read was constructible, but the prefix machinery is being fed unvalidated input).

- `src/ActualLab.Fusion/Session/Session.cs:36`–`:43`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbSessionInfo.cs:21`
- `src/ActualLab.Fusion.Ext.Services/Extensions/Services/SandboxedKeyValueStore.cs:113`

**Fix:** enforce an upper bound and a conservative charset — length ≤ 256, characters restricted to
`RandomStringGenerator.DefaultAlphabet` plus the tag separators — exposed as a
`public static int MaxIdLength` so apps with custom formats can widen it deliberately.

### F10 · MEDIUM · **O** — Session id is not rotated on sign-in (session fixation)

`ServerAuthHelper.UpdateAuthState` binds the *existing* session id to the newly authenticated user
and nothing anywhere mints a new session id or cookie at that moment. The session id is a
long-lived bearer credential, so an id known to a third party *before* authentication remains valid
and fully authenticated *after* it. The default `AllowSignIn = AllowAnywhere` makes the binding
happen on any request, widening the window. Mitigating: the cookie is `HttpOnly` + `SameSite=Lax`,
so this needs a same-site injection primitive — hence MEDIUM. (Server-side view of the same defect
is **D6**.)

- `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:131`, `:173`–`:181`
- `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.Backend.cs:65`–`:70`

**Fix:** rotate at the privilege boundary — mint `Session.New()` in `SignIn`, run
`AuthBackend_SignIn` against it, force-sign-out the old one, and let the middleware re-issue the
cookie (it already does whenever `session != originalSession`).

### F11 · MEDIUM · **C** — One transient Redis subscription failure permanently wedges a subscriber

`RedisSubBase.Subscribe` caches its first subscription task forever, including when that task
faults or times out, and returns it whenever non-null. If Redis is unavailable for the first five
seconds of a `RedisQueue` dequeue, `EnqueueSub.Subscribe()` times out and **all** future dequeues on
that instance fail immediately at the same await, even after connectivity returns — permanently
breaking queue consumption until the owning service is recreated. The same failure mode affects any
long-lived `RedisSubBase` user that retries `Subscribe` on the same instance.

- `src/ActualLab.Redis/RedisSubBase.cs:22`, `:65`–`:82`
- `src/ActualLab.Redis/RedisQueue.cs:52`–`:54`, `:126`–`:128`

**Fix:** a retry-safe subscription state machine — on failure, atomically clear the cached task if it
is still the current attempt, undo any partial subscription, and let a later call create a new one;
coordinate with disposal so it cannot resubscribe after stop.

### F12 · LOW · **O** — `signIn/{scheme}` passes an unvalidated scheme to `ChallengeAsync`

The `scheme` comes straight from the route or query and is handed to `ChallengeAsync`/`SignOutAsync`
with no check that it exists, so `GET /signIn/whatever` yields an unhandled 500 (with a stack trace
under `DetailedErrors`) for a trivially craftable URL — a cheap error-log amplifier. The `returnUrl`
parameter *is* correctly validated with `RedirectUrlChecker`, so there is no open redirect here.

- `src/ActualLab.Fusion.Ext.Services/Authentication/Endpoints/AuthEndpoints.cs:42`, `:48`, `:59`

**Fix:** call `httpContext.IsAuthenticationSchemeSupported(scheme)` (already available at
`Authentication/HttpContextExt.cs:28`) and fall back to the default scheme or return 400.

### Ruled out in this area (recorded so a later pass skips it)

- **Session id entropy** is sound: `RandomStringGenerator(20)` over a 64-symbol alphabet backed by
  `RandomNumberGenerator.Create()` — ~120 bits, unbiased for power-of-two alphabets.
- **`Session.Hash`** is a truncated non-cryptographic XxHash3, but it only identifies one of a single
  user's own sessions and the kick loop is already restricted to that user's list.
- **SQL injection:** the only raw SQL in the area is `NpgsqlDbLogWatcher`'s `LISTEN`/`NOTIFY`, whose
  channel goes through `NpgsqlCommandBuilder.QuoteIdentifier` and whose payload is the host id with
  `'` doubled. Everything else is LINQ/parameterised; `RedisSequenceSet`'s Lua script passes the key
  via `ARGV`.
- **`SandboxedKeyValueStore` prefix checking** survives delimiter-boundary, `/`-in-session-id and
  `@user/1` vs `@user/12` attacks — the sandbox is bypassed via **F1**, not via the checker.
- **`IAuthBackend`** is correctly `IBackendService`, so the service-level gate covers it even where a
  command lacks the `IBackendCommand` marker.
- **Blazor:** `AuthStateProvider` is scoped per circuit and disposes its `ComputedState`; `CircuitHub`
  is scoped; `ComponentInfo`/`ParameterComparerProvider` caches are keyed by `Type`. No cross-circuit
  state sharing found.
- **Redis** key prefixes come from DI configuration and type names, not user input.

---

## G. ActualLab.Core: async, locking, concurrency, collections, time

### G1 · HIGH · **O** (reproduced) — `RetryPolicy.Apply` degenerates into a cancellation-immune 100%-CPU spin on `SuperTransient` errors

When `MustRetry` classifies an error as `Transiency.SuperTransient` it deliberately does **not**
increment `failedTryCount`. `Apply` then computes the backoff as `GetDelay(failedTryCount)` —
`GetDelay(0)`, which returns `TimeSpan.Zero` by contract — and the zero branch is
`await Task.Yield()`, a delay that neither backs off **nor observes `cancellationToken`**. The
result is an unbounded, un-cancellable hot loop that also ignores `TryCount` (since the count
never increments, the try-count check never trips).

`SuperTransient` is a documented first-class extension point, and `ActualLab.Core` ships
`RetryRequiredException : TransientException, ISuperTransientException` plus publicly settable
`TransiencyResolvers`. In-repo users are `DbOperationLogReader`/`DbEventLogReader` reprocess
policies, `DbOperationCompletionListener.NotifyRetryPolicy` and
`DbOperationScope.CommitVerificationPolicy` — all server-side background loops. So the impact is a
server-wide CPU DoS plus a process that can never shut down gracefully. A second trigger needs no
`SuperTransient` classification at all: any policy with a zero-delay sequence and `TryCount == null`
spins identically.

The three *other* retry loops in the codebase all guard with `retryDelays[Math.Max(1, tryIndex)]`,
so this is the outlier. Reproduced (published 14.1.78, token cancelled after 1 s):

```
WATCHDOG: still running after 5s; attempts=1914799; ct.IsCancellationRequested=True
```

- `src/ActualLab.Core/Resilience/RetryPolicy.cs:58`, `:104`, `:106`
- `src/ActualLab.Core/Time/RetryDelaySeq.cs:53`
- same pattern out of partition: `src/ActualLab.Fusion/Operations/Reprocessing/OperationReprocessor.cs:171`, `:177` (milder — its delay *does* observe the token)

**Fix (two lines):** compute the delay as `GetDelay(Math.Max(1, failedTryCount))`, and replace the
`await Task.Yield()` branch with a `cancellationToken.ThrowIfCancellationRequested()` at the top of
the `while (true)` body. Fix `OperationReprocessor` in the same change.

### G2 · MEDIUM · **B** (reproduced) — `UnbufferedPushSequence.Complete()`/`DisposeAsync()` throws `SemaphoreFullException` on the normal path

`_pushAllowed` is a `SemaphoreSlim(0, 1)`. The enumerator releases one permit at the top of every
iteration, so while the sequence idles waiting for the next push the count is already at its
maximum. The completion path then calls `Release()` unconditionally and guards only against
`ObjectDisposedException`, so the release overflows. Symmetrically, if `Complete()` runs before
enumeration, the first `MoveNextAsync` throws. This is the ordinary shutdown path of the type:
start enumerating, push an item, complete/dispose — and the exception escapes
`IAsyncDisposable.DisposeAsync`, so `await using` throws during unwinding and can mask the real
exception of the enclosing block. The behaviour is also non-deterministic (the enumerator's
`finally { _pushAllowed.Dispose(); }` sometimes wins the race, taking the tolerated ODE path
instead), which is itself a defect. Public API in `ActualLab.Core`, unused inside the repo — hence
MEDIUM.

```
T1: consumed=1
T1: Complete() THREW SemaphoreFullException: Adding the specified count to the semaphore
    would cause it to exceed its maximum count.
```

- `src/ActualLab.Core/Channels/UnbufferedPushSequence.cs:10`, `:41`–`:48`, `:96`, `:107`

**Fix:** construct as `new SemaphoreSlim(0)` (no max) keeping the existing ODE catch, or make the
release saturating (`if (_pushAllowed.CurrentCount == 0) Release();` — the existing
`SemaphoreSlimExt.ReleaseSilently` can be reused).

### G3 · MEDIUM · **C** — `AsyncTaskMethodBuilderExt.(Try)SetFromTask` corrupts cancellation

All four helpers distinguish only *faulted* tasks from everything else. For a **canceled** source
the generic overload evaluates `task.GetAwaiter().GetResult()` as the argument to `SetResult`, so it
throws before the target is ever completed — the discarded continuation faults with
`TaskCanceledException` and the returned `target.Task` **stays pending forever**. The untyped
overload does the opposite: it calls `target.SetResult()`, reporting cancellation as *success*. The
analogous `TaskCompletionSourceExt` helper gets this right (it checks `task.IsCanceled` first), which
shows the intent.

- `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.cs:113`, `:115`, `:118`, `:131`–`:136`
- `src/ActualLab.Core/Async/AsyncTaskMethodBuilderExt.Untyped.cs:111`, `:113`, `:116`, `:129`
- contrast `src/ActualLab.Core/Async/TaskCompletionSourceExt.cs:68`

**Fix:** check `task.IsCanceled` before `task.Exception` in all four and transition with
`SetCanceled`/`TrySetCanceled`; ensure the async wrappers cannot leave the target pending.

### G4 · MEDIUM · **C** — Disposing a custom-clock timer does not cancel its delay

For every clock other than `SystemClock`, `ClockExt.Timer` uses the *non*-cancellation-aware
`Observable.Create<long>(async observer => …)` overload and awaits `clock.Delay(dueIn)` with no
token. Disposing the subscription detaches the observer but cannot cancel the delay or release the
async state machine early. The adjacent custom-clock `Interval` implementation uses the
`(observer, ct)` overload and passes `ct` to `clock.Delay` — demonstrating the missing lifetime link.
Components that repeatedly create and replace `CpuClock`/`TestClock` timers accumulate retained
tasks, timer registrations and observer state until each original due time.

- `src/ActualLab.Core/Time/ClockExt.cs:32`, `:35`; contrast `:56`, `:63`

**Fix:** use the cancellation-aware overload, pass `ct` to `clock.Delay`, and treat
disposal-cancellation as normal termination rather than `OnError`.

### G5 · MEDIUM · **C** — `FenwickTree.Increment(-1, …)` spins forever

`Increment` does not validate its index. For the specific value `-1`, `index++` produces `0`, the
loop condition holds for any non-empty tree, and the Fenwick step `index += index & -index` leaves
`index` at `0` — a synchronous, non-cancellable infinite loop that permanently occupies the calling
thread. Other negative values generally throw on array access, making `-1` an easy-to-miss boundary.
No in-repo production caller forwards an external index, so this is a DoS primitive for library
consumers rather than a Fusion-server issue.

- `src/ActualLab.Core/Collections/FenwickTree.cs:43`, `:45`–`:48`

**Fix:** validate with `(uint)index >= (uint)Count` and throw `ArgumentOutOfRangeException` before
modifying the index.

### G6 · MEDIUM · **C** — Expired `RecentlySeenMap` entries can remain visible indefinitely

Time eviction runs only *after* a new key has been added successfully: `TryAdd` returns immediately
for an existing key (before its sole `Prune()` call) and `TryGet` never prunes. So retrying the same
key can never discover that its prior entry expired. `OperationCompletionNotifier` uses this map as
the operation-UUID deduplicator: in a long-lived but quiescent process, a UUID received again after
`MaxKnownOperationAge` is still rejected and its listeners skipped — and `DbOperationLogReader`
upcasts the returned `Task<bool>` to `Task`, so the `false` is ignored and the replay is treated as
successfully processed. The entry becomes eligible again only when some unrelated unique key
triggers pruning. This compounds **E2**.

- `src/ActualLab.Core/Collections/RecentlySeenMap.cs:22`, `:29`, `:33`, `:47`, `:58`
- `src/ActualLab.Fusion/Operations/OperationCompletionNotifier.cs:59`
- `src/ActualLab.Fusion.EntityFramework/Operations/LogProcessing/DbOperationLogReader.cs:34`

**Fix:** prune *before* the duplicate lookup in `TryAdd` (keeping the post-add capacity pruning), and
before `TryGet` if that method is meant to promise time-bounded visibility. Document or add the
internal synchronization the compound operations need.

### G7 · MEDIUM · **O** (PLAUSIBLE) — `BatchProcessor` never replaces a worker that exits, and can wedge permanently

Worker scaling is driven purely from `PlannedWorkerCount`; the live `Workers` set is never
reconciled against it. A worker that leaves `RunWorker` for any reason other than a `WorkerKiller`
item — `WaitToReadAsync` throwing because the channel completed with an error, or anything caught by
the blanket handler — is removed by the `ContinueWith` and never re-created, because
`AddOrRemoveWorkers(0)` returns immediately. If the last worker dies this way the processor is
permanently wedged: the unbounded queue keeps accepting items and every `Process()` returns a task
that never completes.

`BatchProcessor` backs `DbEntityResolver` (one instance per shard), which sits on the read path of
Fusion compute services — so a wedged processor turns every entity lookup for that shard into an
indefinite hang rather than an error. Two secondary symptoms of the same gap: `RunWorkerCollector`
awaits a probe item with **no timeout and no token**, so the auto-scaler itself blocks forever if no
worker is alive; and `Reset()` spins with 50 ms delays forever waiting for a worker count that will
never be reached.

- `src/ActualLab.Core/Async/BatchProcessor.cs:70`, `:100`–`:114`, `:141`–`:158`, `:169`, `:177`–`:183`, `:205`–`:207`, `:256`–`:259`

**Fix:** reconcile `Workers.Count` against `PlannedWorkerCount` in `AddOrRemoveWorkers` (or in the
worker-completion continuation) and respawn the difference; bound the collector's probe with
`WaitAsync(probeTimeout, StopToken)`.

### G8 · LOW · **O** — `CancellationTokenExt.FromTask` disposes a CTS inside its own callback, then unconditionally `Cancel()`s it

In the `CanBeCanceled` branch a callback is registered on the linked CTS's *own* token that disposes
that same CTS while its callbacks are still executing; the `ContinueWith` on the next line then calls
`cts.Cancel()` with no guard, so when the outer token wins the race `Cancel()` throws
`ObjectDisposedException` into a discarded continuation — an unobserved task exception (fatal in
hosts that opt into `ThrowUnobservedTaskExceptions`). In the `else` branch the CTS is disposed only
when `task` completes, so a task that never completes leaks both the CTS and its continuation. The
only in-repo caller is `RpcPeerStateMonitor`, once per connection-state transition.

- `src/ActualLab.Core/Async/CancellationTokenExt.cs:71`, `:79`

**Fix:** use `CancelSilently()`/`CancelAndDisposeSilently()` in the continuation and dispose the CTS
from exactly one place — not from inside its own cancellation callback.

### G9 · LOW · **O** — `HashSetSlim*.Add` returns `true` for a duplicate while inline, `false` after spilling

On the inline (≤ N items) path `Add` returns `true` when the item is already present; once the
collection spills into the backing `HashSet<T>`/`ImmutableHashSet<T>` the same call returns `false`.
The return value silently changes meaning at the spill boundary. No in-repo caller reads it, so this
is latent — but it is public `ActualLab.Core` API implementing `IHashSetSlim<T>`, and a consumer using
`Add`'s result to mean "newly added" gets the wrong answer for the first N items, which is exactly the
class of bug that is hard to spot.

- `src/ActualLab.Core/Collections/Slim/HashSetSlim2.cs:44`, `:52`; `SafeHashSetSlim2.cs:49`, `:57` (identical in the 1/3/4 and `Ref*` variants)

**Fix:** return `false` from the inline duplicate branches to match `ISet<T>.Add`, or change the
signature to `void Add(T)` so no caller can rely on it.

### G10 · LOW · **O** (PLAUSIBLE) — `TimerSet` allocates `RadixHeapSet(45)` while `GetBucketIndex` can return up to 64

`GetBucketIndex` computes `64 - LeadingZeroCount(priority ^ MinPriority)` — a value in `[0, 64]` —
and indexes `_buckets` without a bounds check, but `TimerSet` constructs the heap with 45 buckets. Not
reachable with the quanta the repo currently feeds it (`Timeouts` uses `2^21` ticks and clamps the
keep-alive slot), but `TimerSetOptions` lets a caller choose periods down to `MinQuanta = 10 ms`, at
which point a `Moment` near `DateTime.MaxValue` yields index ~46 and throws
`IndexOutOfRangeException` from inside `lock (_lock)` in a public API.

- `src/ActualLab.Core/Time/TimerSet.cs:35`; `src/ActualLab.Core/Collections/RadixHeapSet.cs:267`, `:272`–`:277`

**Fix:** construct with 65 buckets (the `RadixHeapSet` default), or clamp/validate the priority in
`TimerSet.FixPriorityFromLock` against a documented maximum.

### Ruled out in this area (recorded so a later pass skips it)

`AsyncLockSet` entry lifecycle (use-count state machine traced against add/remove/cancel/reentry
interleavings — no lost/double release, no stuck entry); `ConcurrentPool`/`StochasticCounter`
overshoot; `TaskCompletionHandler`'s thread-static pool; `RadixHeapSet` `MinPriority` bookkeeping;
`MaglevShardMapBuilder` termination and the coprime-skip search; `HashRing`/`ShardMap` index math;
`MathExt.Format*`/`GuidExt.Format` stack-buffer sizing; `SpanExt.ReadVarUInt32/64` bounds and
5th/10th-byte overflow rejection (including the BMI2 path); `ArrayPoolBufferCapacity` overflow;
`TimerSet` catch-up loop and the `ConcurrentTimerSet` sharding hash.

Two invariants were flagged but not chased, and are worth confirming from the owning side:
`SafeHashSetSlim*` documents itself as thread-safe but writes `_count` and a 2/3/4-field tuple
non-atomically (safe today only because `Computed._dependants`/`_dependencies` mutate strictly under
`Computed.Lock`); and `PropertyBag`'s deserializing constructor sorts the caller-supplied array
**in place** (`PropertyBag.cs:71`), which would be a shared-array mutation if a deserializer ever
reuses the array.

---

## H. ActualLab.Core (rest), Interception, Generators, CommandR, Plugins

### H1 · CRITICAL · **C** (verified end-to-end) — A namespace/string mismatch has silently disabled the backend-command gate since v10.3

`RpcMethodDef` classifies command types by comparing interface `FullName`s against two string
constants:

```csharp
public static string CommandInterfaceFullName        { get; set; } = "ActualLab.CommandR.ICommand";
public static string BackendCommandInterfaceFullName { get; set; } = "ActualLab.CommandR.IBackendCommand";
```

`ICommand` really is `ActualLab.CommandR.ICommand` — that one matches. But `IBackendCommand` lives in
`namespace ActualLab.CommandR.Commands`, so its `FullName` is
`ActualLab.CommandR.Commands.IBackendCommand` and the **ordinal** comparison never matches. Therefore
`isBackendCommand` is **always false for every type**, and
`IsBackend = service.IsBackend || isBackend` collapses to the service-level flag alone. Since
`RpcInboundContext.cs:47` is the *only* backend check on the inbound path, the `IBackendCommand`
marker has been decorative in every release from **v10.3 (Aug 2025) through 14.1.78** — `git log -S`
shows the constant was misspelled the day it was introduced.

Verified by execution against the published packages, not by reading:

```
RpcMethodDef.BackendCommandInterfaceFullName  = ActualLab.CommandR.IBackendCommand
typeof(IBackendCommand).FullName              = ActualLab.CommandR.Commands.IBackendCommand
RpcMethodDef.IsCommandType(KeyValueStore_Set) = True, isBackendCommand = False
```

and exploited end-to-end: a non-backend peer sending a hand-built `IKeyValueStore.Set:2` call with
`KeyValueStore_Set{Shard, Items}` → `call succeeded`, then
`Get("any-shard","stolen-key") -> stolen-value`. With the constant repaired, `Set:2 IsBackend = True`
and the same call is rejected with `Endpoint not found`.

Nothing else catches it. `RpcRouteValidator` only checks `RpcServiceMode`; `RpcInboundCommandHandler`
has no peer or `IBackendCommand` check and hands the runtime command straight to `ICommander`;
`CommandServiceInterceptor` only enforces `CommandContext` identity. `Errors.BackendCommandRequiresBackendPeer`
has exactly one hit repo-wide — its own definition — so it is **never thrown**, and the
`IBackendCommand` XML remark naming `CommandServiceInterceptor` as the enforcer is false.

Two more consequences of the same mismatch, unrelated to security: command methods get the wrong
call timeouts (1.5 s/10 s instead of 300 s/300 s), and the idiomatic
`methodDef.IsBackend ? backendRef : apiRef` outbound router picks the wrong route.

- `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:19`, `:105`; `RpcMethodDef.Static.cs:33`–`:38`
- `src/ActualLab.CommandR/Commands/IBackendCommand.cs:1`, `:13`
- `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`–`:54`
- `src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:22`, `:28`, `:35`–`:48`
- `src/ActualLab.CommandR/Internal/Errors.cs:16`; `src/ActualLab.Rpc/Configuration/RpcCallTimeouts.Default.cs:27`–`:33`

**Fix:** (a) correct the constant to `"ActualLab.CommandR.Commands.IBackendCommand"`; (b) add a test
asserting both constants equal `typeof(ICommand).FullName` / `typeof(IBackendCommand).FullName` so a
namespace move can never silently disable authorization again; (c) in `RpcInboundCommandHandler`, add
`if (command is IBackendCommand && !call.Context.Peer.Ref.IsBackend) throw Errors.BackendCommandRequiresBackendPeer();`
— finally using the orphan error, and covering **B11** at the same time; (d) fix the false XML remark.

### H2 · HIGH · **B** — Unbounded `TypeRef.ResolveCache` + unrestricted `Type.GetType` on wire-supplied names

Same defect as **C4**, reached from the P8 side. See C4 for the full write-up, the `$sys.Error`
trigger, and the measured ≈1.4 KB-per-message loader-heap leak.

- `src/ActualLab.Core/Reflection/TypeRef.cs:31`, `:97`

### H3 · HIGH · **O** — `ActualLab.Plugins` deserializes its cache from a shared temp directory with `TypeNameHandling.Auto`, then instantiates what it finds

`FileSystemPluginFinder` caches discovered `PluginSetInfo` as JSON in
`FilePath.GetApplicationTempDirectory()` — `Path.GetTempPath() & GetHashedName($"{appId}_{appDir}")`,
a deterministic function of the entry-assembly name and directory, both readable by any local user —
and deserializes it with `NewtonsoftJsonSerializer.Default`, i.e. `TypeNameHandling.Auto` with no
binder (**C1**). `Directory.CreateDirectory` is a no-op if the directory already exists, so a
lower-privileged local user can pre-create it (on Linux `/tmp` is world-writable; on Windows a service
running as `LocalSystem`/`NETWORK SERVICE` can share a temp root), compute the cache key from the
readable plugin directory and its file timestamps, and plant the file.

Even without a Json.NET gadget, the plugin path alone is code execution: `PluginHandle.GetInstances`
takes the attacker-supplied type map and does `PluginCache.GetOrCreate(pi.Type.Resolve()).Instance` —
`Type.GetType` on an attacker string, then `ActivatorUtilities.CreateInstance(services, type)`,
running an arbitrary constructor of an arbitrary loadable type inside the host process with
DI-resolved arguments; `PluginInstanceHandle.Dispose` then also calls `Dispose()` on it. The
deserialization failure path is swallowed and merely logged, so a gadget that throws after firing is
invisible. `PluginHostBuilder` registers `FileSystemPluginFinder` as the default `IPluginFinder`, so
this is the out-of-the-box configuration.

- `src/ActualLab.Plugins/Internal/CachingPluginFinderBase.cs:58`–`:60`, `:73`
- `src/ActualLab.Plugins/FileSystemPluginFinder.cs:31`–`:33`, `:61`–`:70`
- `src/ActualLab.Plugins/Internal/PluginHandle.cs:86`; `PluginCache.cs:23`; `PluginFactory.cs:21`
- `src/ActualLab.Core/IO/FilePath.Extras.cs:59`; `src/ActualLab.Core/Caching/FileSystemCache.cs:154`

**Fix:** serialize the cache with `TypeNameHandling.None` (the model is a closed concrete graph and
does not need `$type`) or an explicit restrictive binder; re-validate every cached `TypeRef` against
the types actually discovered by scanning before instantiating anything — the cache should be an
optimization, not a trust boundary; and default `CacheDir` to an app-owned location, refusing to read
a cache file whose directory is group/other-writable.

### H4 · MEDIUM · **O** — Command payloads, including raw `Session` ids, are logged on failure, ignoring `INotLogged`

Three CommandR log statements format the whole command object into the message, and none checks the
`INotLogged` marker that exists precisely for this and *is* honoured elsewhere
(`CompletionProducer.cs:42`). `Session.ToString()` returns the raw id, and most Fusion commands are
`ISessionCommand`s. Session ids are bearer credentials, so any client that can make a command fail —
bad arguments, a version conflict, an authorization error inside the handler — writes the victim's
session id into the application log at `Error`/`Warning`, which is typically shipped to an aggregator
or APM with a much wider audience than the session itself. `AuthBackend_SetupSession` is explicitly
marked `INotLogged`, so the intent is clear and only the enforcement is missing.

- `src/ActualLab.CommandR/Diagnostics/CommandTracer.cs:49`–`:54` (registered by default at `CommanderBuilder.cs:62`)
- `src/ActualLab.CommandR/Rpc/RpcCommandHandler.cs:91`; `src/ActualLab.CommandR/Internal/Commander.cs:113`
- `src/ActualLab.Fusion/Session/Session.cs:110`

**Fix:** gate all three on `command is not INotLogged`, and prefer logging
`command.GetType().GetName()` plus `Session.Hash` over the full payload. Consider making `Session`
redact itself in `ToString()` so the marker is a second line of defence rather than the only one.

### H5 · MEDIUM · **C** — `ScopedServiceInterceptor` never performs asynchronous scope disposal

For an async intercepted method the interceptor creates an `IServiceScope` and attaches a
fire-and-forget continuation that casts it only to `IDisposable` — it never calls or awaits
`IAsyncDisposable.DisposeAsync`. The interceptor is specifically usable as an RPC service resolver, so
every remotely invoked async method can create such a scope. If the scoped service or any dependency
is async-disposable-only, Microsoft DI's synchronous scope disposal throws instead of running cleanup,
and that exception belongs to the discarded continuation while the RPC task has already completed.
Repeated calls leave sockets, streams and pooled connections unclosed.

- `src/ActualLab.Interception/Interceptors/ScopedServiceInterceptor.cs:38`–`:46`

**Fix:** return an async wrapper that awaits the operation and then awaits scope disposal in
`finally` (using `CreateAsyncScope`/`IAsyncDisposable` with an `IDisposable` fallback), so the task
the caller sees includes cleanup completion and cleanup failures.

### H6 · MEDIUM · **B** — `RandomInt32Generator`/`RandomInt64Generator` read the shared buffer outside the lock

Both are documented `// Thread-safe!` and advertise cryptographically random values, but only the
*fill* of the shared instance field `_buffer` is inside the lock — the read happens after release. Two
threads interleaving `fill(T1) → fill(T2) → read(T1) → read(T2)` both return T2's value, so the
generator hands out duplicates with ordinary scheduling probability rather than the advertised
collision probability; a read can also observe a buffer mid-fill. In this repo the only shipped
consumer is a clock seed, so present impact is limited — but these are public API with an explicit
thread-safety and CSPRNG contract, and downstream code minting identifiers, nonces or tokens under
concurrency silently gets colliding values. `RandomStringGenerator.Next` correctly rents a **per-call**
buffer, which shows the intended shape.

- `src/ActualLab.Core/Generators/RandomInt32Generator.cs:11`, `:16`, `:18`; `RandomInt64Generator.cs:11`, `:16`, `:19`

**Fix:** use a stack buffer per call (`Span<byte> buffer = stackalloc byte[sizeof(long)]`) —
`RandomNumberGenerator.GetBytes` is itself thread-safe, so the lock can then be dropped entirely.

### H7 · MEDIUM · **B** (PLAUSIBLE — weak-memory architectures) — `LazySlim` double-checked locking with no volatile read or barrier

All three variants publish two independent non-volatile fields from inside a lock
(`field = f.Invoke(); _factory = null;`) and then read them on the fast path without the lock and
without an acquire barrier (`if (_factory is null) return field;`). `field` is not reached *through*
`_factory`, so there is no address dependency to order the two loads: on a weakly-ordered CPU a reader
can observe `_factory == null` while still seeing the pre-initialization value of `field`.

`LazySlim` sits on hot, widely-shared paths — `MethodDef._defaultResultLazy`,
`RuntimeCodegen.DefaultModeLazy`, `ArgumentList.InvokerCache` values,
`PluginInfoProvider._pluginCache`. A stale read returns `default(TValue)`, i.e. `null` for the
reference cases, surfacing as a sporadic `NullReferenceException` or a silently wrong default deep
inside interception/argument-list machinery — on exactly the kind of machine (ARM64 cloud instance)
that is now common. The rest of the codebase is aware of this hazard class:
`Samplers.ToConcurrent`/`EveryNth`/`Random` all end with an explicit `Thread.MemoryBarrier()`.

- `src/ActualLab.Core/LazySlim.cs:53`, `:57`–`:77`, `:107`–`:129`, `:166`–`:189`
- consumers: `src/ActualLab.Core/Collections/ConcurrentDictionaryExt.cs:12`; `src/ActualLab.Interception/ArgumentList.cs:17`, `:95`

**Fix:** read and write `_factory` through `Volatile.Read`/`Volatile.Write` (a volatile read on the
fast path is an acquire and orders the subsequent `field` read). Apply the same release/acquire
protocol to every variant and add an ARM64 stress test.

### H8 · MEDIUM · **C** — Plugin handles leak async-disposable plugin instances

`IPluginInstanceHandle` and its implementation support only synchronous disposal, and cleanup casts
the plugin instance solely to `IDisposable` — so a plugin implementing only `IAsyncDisposable` is
never cleaned up. Disposal also evaluates `lazyInstance.Value`, which can instantiate an otherwise
unused directly-resolved handle just to dispose it. Plugin instances are owned only through the
singleton handle in DI, so an async-only plugin's streams, sockets and timers leak across host
lifecycles.

- `src/ActualLab.Plugins/Internal/PluginInstanceHandle.cs:8`, `:55`–`:58`; `PluginHostBuilder.cs:27`

**Fix:** make handles implement `IAsyncDisposable`, skip the lazy when `IsValueCreated` is false, and
prefer `IAsyncDisposable.DisposeAsync` with an `IDisposable` fallback.

### H9 · MEDIUM · **C** — `PluginHost.DisposeAsync` silently skips sync-only custom providers

`DisposeAsync` returns a completed `ValueTask` when the wrapped provider does not implement
`IAsyncDisposable`, even if it implements `IDisposable` — while the builder's own failure-cleanup path
correctly falls back to synchronous disposal. `PluginHostBuilder` explicitly supports an arbitrary
custom `IServiceProvider` factory, so a caller using the advertised `await using` contract with a
sync-only provider gets apparent successful cleanup while the provider and all its plugin singletons
remain undisposed.

- `src/ActualLab.Plugins/PluginHost.cs:24`, `:27`; contrast `PluginHostBuilder.cs:56`–`:58`

**Fix:** give `DisposeAsync` the same async-first, synchronous-fallback logic as `BuildAsync`.

### H10 · MEDIUM · **O** — Runtime-vs-declared command type (see **B11**)

The declared-parameter-type gap is real and **independent of H1** — it reproduces both before and
after the constant is repaired. It requires an application to declare an RPC command method whose
parameter type is abstract or an interface with `IBackendCommand` descendants; no framework or sample
service does this, so it is a footgun rather than a live hole. The `RpcInboundCommandHandler` fix in
**H1(c)** closes it.

### H11 · LOW · **O** — Session-id entropy is returned to the shared `ArrayPool` unscrubbed

`RandomStringGenerator.Next` rents its randomness buffer with `mustClear: false` and returns it
unscrubbed. For the default power-of-two alphabet, buffer byte *i* maps deterministically to output
character *i*, so the residual bytes are a direct pre-image of the generated string. This generator is
the default session-id factory and also derives user ids, so the freed array goes back into a
process-wide pool carrying recently minted session ids — recoverable by an unrelated component that
rents a same-sized array and inspects uninitialized content, or from a crash dump or heap snapshot.
Not remotely exploitable alone; it widens the blast radius of any other memory-disclosure issue.

- `src/ActualLab.Core/Generators/RandomStringGenerator.cs:116`
- consumers: `src/ActualLab.Fusion/Session/DefaultSessionFactory.cs:12`; `.../DbUserIdHandler.cs:43`

**Fix:** pass `mustClear: true` (or `CryptographicOperations.ZeroMemory` before `Release()`) — the cost
is negligible for 16–32 byte buffers.

### H12 · LOW · **O** — `StaticLog` mixes `ILogger` and `ILogger<T>` under the same key

`For<T>()` and `For(Type)` share one `ConcurrentDictionary<object, ILogger>` and use the same key for
the same type, but store incompatible values: `For<T>()` stores a `Logger<T>` and unconditionally casts
the retrieved value to `ILogger<T>`, while `For(Type)` stores whatever `CreateLogger(Type)` returns. If
any code calls `StaticLog.For(typeof(Foo))` before `StaticLog.For<Foo>()`, the generic call throws
`InvalidCastException`. `StaticLog` is used for static-context logging in framework internals, so such a
crash would surface with no obvious cause. The two forms happen never to overlap today — a trap rather
than a live bug, but cheap to close.

- `src/ActualLab.Core/StaticLog.cs:31`–`:41`

**Fix:** use distinct key spaces for the generic and non-generic overloads, or route both through one
factory.

---

## I. TypeScript client (`ts/packages/*`)

Nine of the Opus findings in this area carry **runnable repros** executed against
the prebuilt `dist` bundles from a scratchpad outside the repo.

### I1 · HIGH · **O** (repro) — Server-side Fusion compute results are garbage-collectible, silently dropping their invalidation subscription

On a TS-hosted Fusion server the only thing linking an inbound compute call to its
`Computed` is the `computed.whenInvalidated()` subscription created inside
`_wrapServerMethod` — and that subscription is stored *on the computed itself*,
while `ComputedRegistry` holds only a `WeakRef`. Nothing else references the
computed once the wrapper returns, so V8 collects it, taking the subscription with
it. The client's `RpcOutboundComputeCall` stays registered with the old value and
never receives `$sys-c.Invalidate`.

No attacker needed — ordinary GC pressure is the trigger, and the failure is
silent and non-deterministic. This is precisely the guarantee Fusion exists to
provide. .NET does not have the hole: `RpcInboundComputeCall` keeps a **strong**
`Computed` reference for the lifetime of the inbound call.

Repro (two isolated Node processes, real `FusionHub` + `RpcClientPeer`, the only
difference being a `global.gc()` between the call and the mutation):

```
RESULT forceGc=false clientCachedValue=0 serverValueNow=42 clientGotInvalidate=true
RESULT forceGc=true  clientCachedValue=0 serverValueNow=42 clientGotInvalidate=false
```

- `ts/packages/fusion-rpc/src/fusion-hub.ts:205`, `:218`
- `ts/packages/fusion/src/computed-registry.ts:5`
- `ts/packages/rpc/src/rpc-call-tracker.ts:184` (no `computed` field on `RpcInboundCall`)

**Fix:** store the `Computed` on the `RpcInboundCall` created in
`RpcPeer._handleInbound` (thread it back through `RpcDispatchContext`) and release
it only once the invalidation has been sent or the call is cancelled — mirroring
.NET's `RpcInboundComputeCall.Computed`.

### I2 · HIGH · **B** (repro) — Every accepted connection leaks a peer plus a 1 Hz timer for 180 s, and peer refs are never reusable

Two defects compound. (a) The per-peer maintenance `setInterval` is created in the
`RpcPeer` constructor and cleared only in `close()`, so it keeps ticking through
the whole post-disconnect grace window — unlike .NET, where maintenance is scoped
to the connection. (b) `acceptConnection`/`acceptRpcConnection` mint a brand-new
`server://${crypto.randomUUID()}` ref per socket, so the "re-accept within the
close window keeps this peer alive" optimisation that `serverPeerCloseTimeoutMs`
exists for is **never** exercised — the 180 s retention is pure cost. C adds that
`accept` never arms a handshake deadline at all, so a client that opens a socket
and never handshakes retains a peer indefinitely.

Repro — 200 immediate connect/disconnect cycles against a real `FusionHub`:

```
after 200 connect/disconnect cycles: serverHub.peers.size = 200
                                     peers with a live 1Hz maintain timer = 200
```

- `ts/packages/rpc/src/rpc-peer.ts:232`, `:238`, `:488`, `:1283`, `:1314`
- `ts/packages/rpc/src/rpc-limits.ts:71`–`:77` (`serverPeerCloseTimeoutMs = 180_000`)
- `ts/packages/fusion-rpc/src/fusion-hub.ts:152`–`:165`
- `ts/packages/rpc/src/rpc-hub.ts:74`, `:158`

**Fix:** move the maintenance interval into the connected phase; arm the close
timer only for peers whose ref can actually be re-accepted and close
UUID-per-connection peers immediately on `conn.closed`; add a server-side
handshake deadline; cap `hub.peers` and the number of "disconnected, awaiting
close" peers.

### I3 · HIGH · **O** (repro) — No wire-argument arity validation: a hostile client bypasses the server-side compute cache entirely

`RpcServiceHost.dispatch` never checks the inbound argument count against the
method definition. For compute methods the over-long list flows into
`ComputeFunction.buildKey`, which folds **all** arguments into the cache key — so a
request carrying one extra attacker-chosen argument produces a *different cache
key for the same logical call* while still invoking the real handler with the
correct leading arguments. The compute cache is the primary protection between an
RPC endpoint and the backing store; appending `"nonce-<random>"` to every call
forces a full handler execution (DB/query/IO) on every request, each also
registering a new `Computed` and subscription. Impossible in .NET, where arity is
fixed by `RpcMethodDef` and a mismatch is a deserialization error.

Repro (`Svc.getValue:2`, handler counts its own executions):

```
20 identical calls        -> handler executions: 1
20 calls + junk extra arg -> handler executions: 20  (ComputedRegistry.size = 21)
```

- `ts/packages/rpc/src/rpc-service-host.ts:72`, `:86`–`:89`
- `ts/packages/fusion-rpc/src/fusion-hub.ts:207`
- `ts/packages/fusion/src/compute-function.ts:53`

**Fix:** reject (or at minimum truncate to `entry.def.argCount`) inbound argument
lists whose length does not match the definition, before invoking the handler.

### I4 · HIGH · **B** — RPC framing and deserialization have no inbound resource limits

The WebSocket receive path places no limit on frame bytes, messages per frame,
arguments per message, decoded collection sizes, or nesting. Both the text and
binary splitters fully materialize attacker-controlled collections before
dispatch; the MessagePack `Decoder` is constructed with defaults. None of the C#
ceilings (`MaxArgumentDataSize`, `MaxMethodRefSize`, `MaxHeaderSize`) exist in
`RpcLimits`, which carries only timing and completed-call settings.

- `ts/packages/rpc/src/rpc-connection.ts:94`, `:140`, `:199`
- `ts/packages/rpc/src/rpc-serialization.ts:48`, `:53`, `:307`, `:327`, `:380`
- `ts/packages/rpc/src/rpc-limits.ts:32`

**Fix:** add configurable hard limits for frame bytes, envelopes per frame,
arguments per envelope, method/header lengths, nesting depth and decoded
string/binary/array/map sizes; configure the MessagePack decoder with them;
validate every length as a non-negative in-bounds safe integer before slicing;
close the connection on the first violation.

### I5 · HIGH · **C** — Remote peers can create unbounded concurrent inbound calls

Every inbound call is dispatched immediately with no concurrency or in-flight cap.
Calls expecting results sit in an unbounded `Map` until their handler completes;
`noWait` calls bypass tracking entirely but still launch unlimited async work. The
only existing cap applies to *completed* calls, so it does not bound pending ones.
Reachable before handshake completion, because regular inbound messages are
dispatched without a connection-state gate.

- `ts/packages/rpc/src/rpc-peer.ts:573`, `:577`, `:647`, `:653`
- `ts/packages/rpc/src/rpc-call-tracker.ts:232`, `:250`, `:259`

**Fix:** per-peer and global in-flight caps plus a bounded concurrency
semaphore/queue; account for `noWait` under the same budget; gate ordinary calls
until the handshake completes.

### I6 · HIGH · **B** — Remote `RpcStream` receive buffer is unbounded; `ackAdvance` is advisory only

The receiver accepts and buffers every in-order item or batch into a `Denque` with
no capacity, never enforcing the advertised acknowledgement window. A compromised
server answers a stream request and floods `$sys.I`/`$sys.B` with strictly
increasing indices, ignoring the client's acks; if the consumer is slower than the
wire (or never iterates after the lazy start), the heap grows until the tab or
process dies. The sender-side `_pendingSendTimes` array is likewise unbounded
while disconnected. Note this mirrors **B3** in .NET — a shared design weakness,
not a port regression.

- `ts/packages/rpc/src/rpc-stream.ts:138`, `:252`, `:271`, `:276`, `:294`, `:355`
- `ts/packages/rpc/src/rpc-system-call-handler.ts:136`–`:167`

**Fix:** bound the receive buffer at `ackAdvance` (or an explicit
`maxBufferSize`); on overflow complete the stream with a protocol-violation error
and send `$sys.AckEnd`, as the gap path already does when `allowReconnect` is
false. Validate indices and batch lengths as non-negative safe integers.

### I7 · HIGH · **C** — Stream ACK flooding creates an unbounded, quadratically drained queue

Every accepted `$sys.Ack` is appended to a plain array with no
finite/safe-integer, monotonicity or size validation. Draining calls `shift()`
repeatedly, making a backlog quadratic to process — even though `_tryProcessAcks`
explicitly reduces the whole queue to the latest `nextIndex` plus the OR of
`mustReset`. A peer that owns a locally hosted stream can flood ACKs while the
source iterator is slow; the array grows without bound and, when the pump resumes,
repeated front-removal monopolizes the event loop.

- `ts/packages/rpc/src/rpc-system-call-handler.ts:196`–`:206`
- `ts/packages/rpc/src/rpc-stream-sender.ts:135`, `:182`–`:215`, `:501`–`:520`

**Fix:** coalesce ACK state at receipt into one latest index and one accumulated
reset bit instead of queueing; validate indices; keep a small hard cap if a queue
is retained at all.

### I8 · MEDIUM · **B** (repro) — `$sys.Disconnect` tears down *shared* objects using *remote* object ids

`$sys.Disconnect` carries ids from the sender's shared-object namespace, i.e. the
receiver's *remote*-object namespace. The TS handler looks each id up in
`peer.remoteObjects` (correct) **and then also** in `peer.sharedObjects` (wrong
namespace) and disconnects whatever it finds. Both counters start at 1, so
collisions are the norm. C# does only the first half.

No hostility required: a client that both consumes a server stream and pushes one
of its own (audio/video/upload — the documented `RpcStreamSender` use case) has its
**outgoing** stream aborted the moment the server tears down an incoming stream
sharing the id. A hostile server can kill all of a client's outgoing streams with
one `$sys.Disconnect [1..K]`.

Repro:
```
shared(outgoing) localId = 1  remote(incoming) localId = 1
before: outgoing sender aborted = false
after : outgoing sender aborted = true   (should be false)
```

- `ts/packages/rpc/src/rpc-system-call-handler.ts:219`–`:246`
- `ts/packages/rpc/src/rpc-shared-object-tracker.ts:4`; `rpc-remote-object-tracker.ts:4`
- contrast `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:139`

**Fix:** delete the `sharedObjects` branch. If a server must be able to terminate a
client-to-server sender, that needs its own message or namespace-tagged ids —
overloading the id space cannot be made correct.

### I9 · MEDIUM · **B** (repro) — `resolveStreamRefs` misclassifies ordinary strings as stream references

`RpcHub._createClientMethod` runs `resolveStreamRefs` over the whole deserialized
result of **every** regular RPC call, recursively replacing any string that
`parseStreamRef` accepts with a live `RpcStream`. The acceptance test is a pure
shape heuristic — 4–6 comma-separated parts where parts 1..3 are `parseInt`-able —
and ordinary data matches it constantly. Conversely, nested *MessagePack*
stream-reference objects are **not** recognized, because the recursive walker calls
`parseStreamRef` only in the string branch, so the same API behaves differently
per negotiated format.

Repro: `"1,2,3,4"`, `"a,1,2,3"`, `"10,20,30,40"`, `"John,1,2,3,4,5"`,
`"x,1,2,3xyz"`, `"2024,01,02,03"` all parse as stream refs; a method returning
`"10,20,30,40"` hands the caller an `RpcStream`.

- `ts/packages/rpc/src/rpc-stream.ts:58`–`:72`, `:505`–`:540`
- `ts/packages/rpc/src/rpc-hub.ts:325`, `:337`

**Fix:** stop inferring stream-ness from value shape — resolve only in positions
the `RpcMethodDef` declares as streams (mirroring .NET's type-driven
deserialization), or require an unambiguous tagged representation with a validated
GUID `hostId`, and test object-shaped refs before recursing.

### I10 · MEDIUM · **O** (repro) — Client-side outbound compute calls pin their computeds, defeating `WeakRef` eviction

An `RpcOutboundComputeCall` stays registered in `peer.outboundCalls` after
`$sys.Ok` (`removeOnOk = false`), and `bindComputeCall` attaches
`call.whenInvalidated.then(() => computed.invalidate())`. That reaction is retained
by the promise, which is retained by the call, which is retained by the peer's
`Map` — so the closure **strongly pins the `Computed`** and its result payload from
a GC root, and the registry's `WeakRef`/`FinalizationRegistry` can never fire. A
long-lived SPA retains one live call + computed + full result for every distinct
(method, args) tuple it has ever queried, and each retained call keeps the
server-side inbound call alive too. Combined with **I1** (which prevents the
invalidation that would release them) the set is effectively monotonic.

Repro — 300 distinct compute calls, all results discarded, three full `global.gc()`
passes: `peer.outboundCalls.size = 300`, `ComputedRegistry.size = 300`.

- `ts/packages/fusion-rpc/src/fusion-hub.ts:316`–`:336` (esp. `:325`)
- `ts/packages/fusion-rpc/src/rpc-outbound-compute-call.ts:6`

**Fix:** hold the computed in a `WeakRef` inside the reaction, and register it with
a `FinalizationRegistry` that removes the call from `peer.outboundCalls` and sends
`$sys.Cancel` — the TS analogue of .NET's `~RemoteComputed() => Dispose()`.

### I11 · MEDIUM · **O** (repro) — A hostile payload can throw out of the receive path, dropping the rest of the frame (and crashing a Node client)

`resolveStreamRefs` recurses without a depth limit over server-supplied data and is
called from the `$sys.I`/`$sys.B` handlers. `JSON.parse` happily parses
200,000-deep nesting; the subsequent recursive walk throws `RangeError`.
`RpcPeer._handleMessage`'s `try` covers only deserialization — dispatch is outside
it — and `RpcWebSocketConnection`'s text branch has no `try/catch` at all while the
binary branch's `catch` sits *outside* the per-message loop.

Consequences: on the text/`MessageChannel` transports the exception escapes
`ws.onmessage` — in a Node-hosted client that is an uncaught exception and process
exit; in a browser, every remaining message in that frame is dropped. On the binary
transport, **all** messages already decoded from the frame are discarded together
with the bad one — and since .NET batches many RPC messages per WebSocket frame,
that silently drops `$sys.Ok` replies, whose per-call timeouts default to
unbounded, so those calls hang forever.

Repro: `JSON.parse OK at depth 200000 (400001 wire bytes)` →
`resolveStreamRefs threw: RangeError - Maximum call stack size exceeded`.

- `ts/packages/rpc/src/rpc-peer.ts:520`–`:537`, `:566`–`:575`
- `ts/packages/rpc/src/rpc-connection.ts:162`–`:174`, `:199`–`:211`
- `ts/packages/rpc/src/rpc-message-channel-connection.ts:24`–`:31`
- `ts/packages/rpc/src/rpc-stream.ts:505`–`:540`

**Fix:** wrap the whole dispatch in the existing `try/catch`; move the connection
`try/catch` *inside* the per-message loop in both branches and add one to the text
and `MessageChannel` paths; give `resolveStreamRefs` a depth cap.

### I12 · MEDIUM · **O** — V5 binary parser reads `argDataLen` **signed**, so the read cursor can move backwards

`view.getInt32(pos, true)` is signed and used directly as `pos + argDataLen`. A
negative value moves the cursor backwards, so `bytesRead` can be far smaller than
the true envelope size (reproduced: `bytesRead: 1` for a 7-byte envelope).
`splitBinaryFrame` only guards `bytesRead <= 0`, so the frame is re-parsed at
overlapping offsets and yields more "messages" than it contains — each non-`$sys`
one allocating an `RpcInboundCall`, failing `getMethodDef`, and emitting a
`$sys.Error` response. That is a CPU/allocation and outbound-bandwidth amplifier
over an unauthenticated connection. Separately, `skipHeaders` reading past the end
produces `NaN` offsets that silently poison `bytesRead`, and `getInt32`/`getUint32`
throw `RangeError` on a truncated tail — which, per **I11**, discards every message
already decoded from that frame.

- `ts/packages/rpc/src/rpc-serialization.ts:175`–`:183`, `:327`–`:330`, `:335`, `:350`, `:386`–`:396`, `:509`, `:521`

**Fix:** read unsigned and validate (`argDataLen < 0 || pos + argDataLen >
data.length` ⇒ throw); port the C# ceilings into `RpcLimits`; require
`bytesRead >= minimumEnvelopeSize` and `Number.isFinite(bytesRead)`; cap
messages-per-frame.

### I13 · MEDIUM · **C** — A late `FinalizationRegistry` callback can delete a live replacement computed

`ComputedRegistry` finalizers carry only a string key and unconditionally
`_entries.delete(key)`. If an old computed is collected, its dead weak entry is
observed and replaced by a fresh registration, and *then* the old finalizer runs,
it deletes the live successor. Explicit `.invalidate(args)` can then no longer find
the computed, so cached/dependent data stays stale, and subsequent calls build
duplicate dependency graphs for the same key.

- `ts/packages/fusion/src/computed-registry.ts:5`–`:8`, `:14`–`:20`, `:25`–`:35`
- `ts/packages/fusion/src/compute-method.ts:79`–`:88`, `:101`–`:114`

**Fix:** register a held value containing the key *and* the exact `WeakRef`
(or a generation token), and delete only if `_entries.get(key)` still equals it.
Apply the same identity check in `unregister`.

### I14 · LOW · **O** — `$sys.Reconnect` stale-generation check is bypassed by a non-numeric index

The guard is `if (typeof handshakeIndex === 'number' && handshakeIndex !==
peer.ownHandshakeIndex)`. A peer sending the index as a *string* (or omitting it)
skips the check entirely, letting a remote peer force `resendResult()` on arbitrary
known inbound call ids of the live generation. C# has no such escape — the
parameter is a typed `int` and the comparison unconditional. Impact is limited to
duplicate results, which the outbound tracker tolerates; but the check as written
provides no guarantee at all.

- `ts/packages/rpc/src/rpc-system-call-handler.ts:270`

**Fix:** `if (typeof handshakeIndex !== 'number' || handshakeIndex !== peer.ownHandshakeIndex)`.

### I15 · LOW · **O** — Dependency edges are never pruned, and `ComputedState.dispose()` leaves its computed consistent and linked

A dependant removes itself from its dependencies' `_dependants` maps only when
*invalidated*. `ComputedState.dispose()` aborts the update loop and sets
`_isDisposed` but leaves the final computed `Consistent`, so its edges survive as
dead `WeakRef` entries. TS has no equivalent of .NET's `ComputedGraphPruner`, so
nothing reclaims them. A long-lived remote computed accumulates one dead entry per
mounted-then-unmounted React component that ever depended on it; `invalidate()`
walks the whole map, so invalidation latency degrades monotonically over a session.

- `ts/packages/fusion/src/computed.ts:223`–`:237`, `:288`–`:292`
- `ts/packages/fusion/src/computed-state.ts:57`–`:63`

**Fix:** invalidate the state's computed in `dispose()`/`State._onDisposed()`, and
add a periodic `pruneDependants()` — a direct port of `Computed.PruneDependants`.

### I16 · LOW · **O** — Service-handler exception messages are forwarded verbatim to the remote peer

Any exception thrown by a registered service implementation is serialized as
`{ TypeRef: RemoteException, Message: "<name>: <message>" }` and sent on with no
filtering; Node error messages routinely embed absolute file paths, connection
strings, SQL fragments and hostnames. For a TS-hosted RPC server this is
unauthenticated internal-detail disclosure. Parity issue with .NET (**B10**), hence
LOW — but the TS side has no equivalent of a production error-shaping hook at all.

- `ts/packages/rpc/src/rpc-error.ts:20`–`:24`; `rpc-peer.ts:631`–`:644`; `rpc-service-host.ts:81`

**Fix:** a hub-level `errorFilter` applied in `RpcSystemCallSender.error` /
`toExceptionInfo`.

### Ruled out in the TypeScript area (recorded so a later pass doesn't repeat it)

- **Prototype pollution:** no reachable sink. `JSON.parse` creates `__proto__` as
  an own data property; `@msgpack/msgpack@3.1.3` throws `"The key __proto__ is not
  allowed"`; `resolveStreamRefs` writes through `Object.keys` + `obj[key] = …` on
  objects that already own `__proto__`. `RpcServiceHost._methods`,
  `RpcMethodRegistry` and `hub.peers` are all `Map`s.
- **XSS / code-execution sinks:** zero hits for `innerHTML`,
  `dangerouslySetInnerHTML`, `eval`, `new Function`, dynamic `import()`,
  `document.write` across the partition. `fusion-react` renders nothing.
- **Session-id storage:** the TS packages never read or write session tokens;
  `localStorage` use is confined to log-level persistence with type-checked entries.
- **Text wire-format injection:** `JSON.stringify` escapes `\n`, `\x1E` and `\x1F`,
  so a hostile string argument cannot forge an extra message or argument — and by
  the same token cannot inject into `ComputeFunction.buildKey`'s separator.
- **`$sys.Reconnect` decompression** cannot be used for unbounded allocation
  (≥1 input byte per decoded id, ≈8× amplification bounded by frame size).
- **`AsyncLock`, `PromiseSource`, `RingBuffer`, `RetryDelaySeq`/`RetryDelayer`,
  `awaitWithCleanup`, `throttle`/`debounce`, `abortPromise`** read line by line —
  lock hand-off, abort-listener removal, cleanup idempotence and ring-buffer index
  math are correct; reconnect backoff is exponential with jitter and a
  premature-disconnect guard.
- **`useComputedState` / `useMutableState`** create and dispose state inside
  `subscribe`, use `useSyncExternalStore`, and guard the async loop with a
  `cancelled` flag — no stale state or listener leak.

---

## Verification results

Seven claims were re-examined by independent adversarial verifiers instructed to
*refute* them and to default to "refuted" under uncertainty. All experiments ran in
a dedicated worktree or in `tmp/` repro projects against published packages; the main
tree was never built. Full write-ups: `audit-v2/VERIFY-*.md`.

| # | Claim | Verdict | Severity change |
|---|-------|---------|-----------------|
| — | **B1 + C1** — client-selectable JSON format + `object`-widened slot ⇒ arbitrary type instantiation | **CONFIRMED** (live server + hostile client) | HIGH; CRITICAL only where a gadget assembly is loaded |
| — | **B11 / H1** — backend-command gate | **CONFIRMED, root cause different from both reviewers** | → **CRITICAL** |
| V1 | **F4** — identity secrets on the wire | CONFIRMED mechanically | HIGH → **MEDIUM** |
| V2 | **C2** — MessagePack `TrustedData` | PARTIALLY CONFIRMED | HIGH → **MEDIUM** for the comparer half; the crash is real but has a different cause |
| V3 | **C3** — type-cache keys alias the transport buffer | **CONFIRMED, both halves** | **HIGH stands** |
| V4 | **E1** — inbound compute calls accumulate | CONFIRMED on every sub-question | HIGH → **MEDIUM** |
| V5 | **E3** — shared cache / peer-less key | PARTIALLY CONFIRMED | HIGH → **LOW–MEDIUM** |

**Notes that change the picture, not just the rating:**

- **The JSON-format chain is broader than claimed and narrower than feared.** The
  server has no format allow-list at all — its only gate is "is this key registered"
  — and a live server accepted a client that pinned `?f=njson5`. But the core
  primitive is **not Newtonsoft-specific**: the default-registered
  System.Text.Json format (`json5`) has the same weakness, because ActualLab's *own*
  `TextTypeSerializer` header + `TypeRef.Resolve` → `Type.GetType` does the
  unrestricted resolution (Newtonsoft's `Auto` is an additional, worse vector because
  it also honours nested `$type`). Streams are not the only sink: **any RPC method
  with an `object` or abstract parameter is a direct sink**. Conversely the default
  `mempack6` and all binary formats are safe on *this* path — their closed formatter
  registry rejects `object` and unregistered types. So a `?f=` allow-list is
  defence-in-depth but insufficient; the real fix is to gate polymorphic type
  resolution against an allow-list run on the *resolved* type. (The separate
  `ImmutableOptionSet` route in **C1** bypasses all of this and *is* format-independent
  — it was reproduced through `mempack6` and `msgpack6` round-trips — because the
  binary formatter only ever sees strings, and Newtonsoft runs inside the type's own
  property.)
- **The backend gate: neither reviewer had the mechanism right.** One blamed
  declared-vs-runtime typing, the other declared the gate sound. The truth is a
  misspelled string constant that has made `IBackendCommand` decorative in every
  release since v10.3 — see **H1**. The declared-vs-runtime hole is *additionally*
  real and survives the constant fix, but needs an unusual application declaration
  (**H10**).
- **C2's stack overflow is real but `TrustedData` is not why.**
  `MaximumObjectGraphDepth` is 500 and *is* enforced in both security modes (depth 600
  throws). The reproduced process kill therefore comes from the type-decorating hop
  **resetting MessagePack's depth counter on each nested `Deserialize` call**, so the
  500 limit never accumulates across levels. That makes the serializer-independent
  nesting budget the load-bearing fix, not `WithSecurity`. The `TrustedData` comparer
  half is separately real but scoped to *unmanaged* dictionary keys (strings already
  use .NET's randomized comparer in both modes), measured at ≈700× amplification
  (720 KB → 2.1 s CPU) — no in-`src` contract exposes such a dictionary, so it is a
  latent hazard for consuming apps.
- **C3 survived a direct rebuttal and is the strongest of the single-source
  findings.** The "`RpcInboundCall` copies the bytes" objection is wrong:
  `ArgumentData = default` drops a *reference*, it does not copy, and it runs **after**
  the cache insertion. Verified by reflection that the cached `ByteString` key's
  backing array *is* the frame buffer, that overwriting it makes the entry permanently
  unreachable and the correct marker permanently miss — so growth is monotonic under
  **ordinary polymorphic traffic**, not only under attack — and that 5,000 forged-hash
  variants of a single type name each got their own entry.
- **F4 is self-disclosure, not cross-user disclosure.** All four serializers do ship
  `Identities` (key `Google/1234567890`, value = the stored secret) via the
  `JsonCompatibleIdentities` surrogate, and `ToClientSideUser()` has **zero call sites**
  in `src/`. But `IAuthBackend` is not RPC-exposed, so there is no path to another
  user's secrets, and stock Fusion writes `""` as the secret — it only bites
  applications that store real tokens there.
- **E1 is the two missing guardrails, not the model.** Each distinct call id gets its
  own registration, there is no expiry or `RpcLimits` cap, and a socket drop does not
  release them (the server peer waits 3–15 minutes for reconnect, so connect/abandon
  churn works). Retaining one inbound call per live subscription is Fusion's intended
  invalidation-push design; the defect is the absence of a cap and a lease.
- **E5's payoff needs a peer-dependent router.** All three mechanical facts hold, but
  default routing is a pure function of the call, and the only in-repo consumer shares
  one cache precisely *because* routing is deterministic. Also the static is not
  load-bearing — the plain per-container cache has the same missing namespace.

---

## Maintainer decisions — do not re-raise these

Recorded 2026-07-27 after the fix round. A later review pass that rediscovers these
findings should read this section before reporting them again.

### Rejected — the finding is wrong or the behaviour is deliberate

| Finding | Decision |
|---|---|
| **A4**, "`WebSocket.SendAsync` gets `CancellationToken.None`" | **Wrong.** `WebSocket.Abort()` faults pending operations, and the read loop already registers an abort on `readerToken` — the cancellation path exists, it just runs through `Abort` rather than the token. Passing a token forces per-operation registration work on the hottest write path for no additional capability. Both reviewers flagged this; both were wrong. |
| **B3**, "bound the `RpcStream` receive channel" | **Still rejected — but this was never B3's main fix.** A bounded channel would put a blocking or failing write inside `OnItem`, which runs on the peer's inbound message-processing path — that is head-of-line blocking for *every* call on that peer, worse than the problem. That reasoning stands. What it does *not* cover is the lever this row itself names: enforcing the existing `AckAdvance` window and failing the stream. That is now implemented in .NET too (see **B3** above) and is not a bounded channel — nothing blocks and no write fails. Closing B3 on this row was an error of attention: the fallback got answered and the primary fix was never evaluated. |
| **I6** (the TS twin of B3) | **Fixed via the window lever**, same as B3. The TS receiver enforces the `AckAdvance` window it advertises and fails the stream with a protocol violation + `$sys.AckEnd` when the remote runs past it. Its original form still credited the window from the last ack sent, which a peer could ratchet by forcing gaps; it now measures from consumption and caps batch length, matching .NET. |
| **A4**, "bound the outbound write channel" | **Accepted risk.** Reaching it requires an application-level bug: RPC streams stop quickly on their own ack window, and stuck calls surface as stuck calls. Where the victim is a client, the client fails and that is tolerable. Not worth a bound on the 9M-calls/s write path. |

### Decided differently from the review's recommendation

| Finding | Decision |
|---|---|
| **A5** | Not "decouple `ClientId`". Keep `ClientId = Id.ToBase64Url()` and keep sending it; add a server-issued per-peer secret delivered in `RpcHandshake`, plus `c` (counter) and `p` (HMAC proof) URL parameters. Reject with 403 at the door — so `GetPeerChangeKind` is **not** modified and the incumbent is never disconnected on a failed proof. `RequireReconnectProof` option, default `false`, flipped once clients ship. Spec: `audit-v2/SPEC-reconnect-proof.md`. |
| **B11**/**H10** | Root cause was neither reviewer's theory — it was a misspelled string constant (**H1**). The declared-vs-runtime gap is additionally real but needs an unusual app-side declaration; closed by the `RpcInboundCommandHandler` check. |
| **C2** | The reproduced process kill is **not** caused by `MessagePackSecurity.TrustedData`. `MaximumObjectGraphDepth` is 500 and enforced in both modes; the crash comes from the type-decorating hop resetting MessagePack's depth counter per nested `Deserialize`. The `TrustedData` half is separately real but MEDIUM and scoped to unmanaged dictionary keys. |
| **C4**/**H2** | No type allow-list. Instead: correlate `$sys.Error` before resolving; cache only canonical spellings; structural pre-resolution checks on exception type names (generic arity ≤ 1, name must end in `Exception`/`Error`). Scope limited to `ExceptionInfo` — the general polymorphic path remains a documented gap. |
| **C1** | `OptionSet`/`ImmutableOptionSet` marked `[Obsolete]` and retired from Fusion's own types rather than migrated onto a schema. Legacy `_Sessions.OptionsJson` rows read back as empty options — accepted silent data loss. |
| **Session hash** | `Session.Hash` keeps its legacy XxHash3 value — it is on the wire via `SessionAuthInfo.SessionHash` and `Auth_SignOut.KickUserSessionHash`. A new `Sha256Hash` exposes the strong digest as 32 cached bytes. `ToString()` is redacted to `{4-char Id prefix}:{Hash}`. |

### Known-open, deliberately

| Finding | Decision |
|---|---|
| **A3** | Cannot be closed by the A5 proof work: a first-time client has no secret, so an unknown `clientId` must always be able to create a peer. Needs a resource-shaped fix instead — create the peer after `AcceptWebSocketAsync`, seconds-scale grace for never-handshaken peers, caps on live peers. |
| **C7** | `PropertyBag` still binds to `TypeSchema.Any` project-wide. Tightening it is a separate decision. |
| **B4** call cap | Default `int.MaxValue` on purpose — a Fusion server legitimately retains one inbound call per live client subscription, so 100K+ open calls is normal operation. `NoWait` calls are invisible to the cap; documented on the option. |
| **I2**, **I4**, **I5** | **Deferred, not rejected.** The TypeScript packages are used almost entirely as an RPC *client*; server-side hostile-peer hardening of the inbound path (peer/timer lifetime, framing and deserialization ceilings, inbound call concurrency caps) is not worth the complexity while that stays true. Revisit if a TS-hosted RPC server ever ships. |
| **I3** | Same reason as above — arity validation guards a *server-side* compute cache, and TS servers were never in scope. Left open. |
| **I15** | Dependency-edge pruning and `ComputedState.dispose()` leaving a consistent computed: acceptable for now. No TS `ComputedGraphPruner`. |
| **C2** | Not fixed in this round — left to whoever needs it. |
| **E2** | Invalidation replay semantics: current behaviour is the best available trade-off. No redeliver-vs-durable-queue redesign. |
| **F5** | `IKeyValueStore` quotas: it's a demo API, no quota numbers needed. |

---

## Cross-cutting themes

Most of the individual findings are instances of five recurring patterns. Fixing the
pattern is worth more than fixing the instances.

1. **Wire-supplied type names drive `Type.GetType`.** C1, C4/H2, C6, C7, F7, H3, and
   the `$sys.Error` trigger. There is no allow-list anywhere on *what may be resolved*
   — every check is `IsAssignableFrom` applied *after* resolution. One
   contract-derived allow-list, consulted before `Type.GetType`, closes most of this
   area at once.
2. **Unbounded caches and registries keyed by remote input.** A2 (version sets), A3
   (peers), B4 (inbound calls), B5 (shared objects), C3 (type markers), C4/H2
   (`ResolveCache`), E11 (client cache), F3 (shards), I2/I5/I6/I7 (the TS mirror).
   `RpcLimits` currently contains only *time*-based limits — there is no
   `MaxInboundCalls`, `MaxSharedObjects` or `MaxPeers` anywhere.
3. **Credentials travelling in URLs and landing in logs.** D2, A5, H4, F9. The TS
   client already redacts (`sanitizeUrl`); the server never got the equivalent, and
   `Session.ToString()` returns the raw id.
4. **Backpressure asserted by the sender, never enforced by the receiver.** A4, B3,
   B5, I6, I7. The `AckAdvance`/`AckPeriod` protocol exists and is enforced only on
   the sending side, so it protects against a slow peer but not a hostile one.
5. **A guardrail that exists but is switched off in every shipped configuration.**
   H1 (the `IBackendCommand` constant), C7 (`TypeSchema.Any` everywhere), C8
   (schemas checked on read only), F1 (`IKeyValueStore` not marked backend),
   `Errors.BackendCommandRequiresBackendPeer` never thrown, `INotLogged` not honoured
   in CommandR. These are the cheapest fixes and the highest-value ones.

---

## What was not covered

Recorded so a round 3 knows where to look. Nothing below was reviewed to the depth
of the areas above.

- **`MemoryPackReader.Advance(int)` behaviour on negative/oversized wire-supplied
  deltas** — called with attacker-controlled `int` values in
  `StringAsSymbolMemoryPackFormatter` *and in every MemoryPack-generated
  version-tolerant formatter*. If it is unchecked, that is memory-unsafety reachable
  from any MemoryPack payload. **Highest-value single follow-up.**
- **`src/ActualLab.Generators`** (the Roslyn proxy generator) — build-time code with
  no runtime attack surface, so it was deprioritised; but it produces every proxy's
  method table and slot indices, which `ProxyMethodTable`/`InterceptorBinding` index
  with deliberately-unchecked `Unsafe.Add`. A codegen-correctness pass on emitted slot
  ordering is warranted.
- **`ArgumentList-Generated.cs` (~12k lines)** — arities 0 and G1 read in full, the
  rest pattern-matched by grep. The `Gn`-with-simple-tail hybrids (arities 5–10, where
  `GenericItemCount` is clamped to 4) are where a divergent arity would hide.
- **`Collections/Fixed/FixedArray.cs`** — a wrong `N` in a `MemoryMarshal.CreateSpan`
  call would be an out-of-bounds `Span`; only `FixedArray0`/`1` were read.
- **The remaining ~24 Nerdbank converters**, and the `Api/*` collection formatters
  (`ApiList`, `ApiMap`, `ApiSet`, `ApiOption`, `ApiNullable`) — wire-facing
  `Deserialize` methods that deserve the C5 treatment (length handling, `DepthStep`,
  duplicate-key behaviour).
- **`Core/Result.cs`, `Option.cs`, `StringExt.cs`, `Diagnostics/LoggerExt.cs`** — not
  read; a dedicated log-injection sweep over `LoggerExt` was never performed.
- **`Core/Compatibility/`** and the `netstandard2.0`/`net472` conditional branches
  throughout — skimmed only.
- **TypeScript tests and mocks** (`ts/packages/*/tests/**`) — out of scope by the
  brief, so `mock-ws.ts` / `rpc-test-connection.ts` were not audited.
- **`rpc-xxhash3.ts` numerics** were not differentially tested against .NET's
  `RpcMethodRef.ComputeHashCode`; a cross-language vector test is worthwhile if
  `msgpack6c` is used in production.
- **Dependency CVEs.** No inventory was taken of `@msgpack/msgpack`, `denque`,
  `react`, MessagePack-CSharp, MemoryPack or Newtonsoft.Json versions against known
  advisories.
- **No load, soak or fuzzing campaign** was run against any finding. The repros are
  targeted demonstrations, not stress tests.
