# P1 — RPC transport, peers & connection lifecycle

Reviewer partition: `src/ActualLab.Rpc/WebSockets/`, `src/ActualLab.Rpc/Clients/`,
`src/ActualLab.Rpc/Configuration/`, `src/ActualLab.Rpc/*.cs`, and the peer/connection
part of `src/ActualLab.Rpc/Infrastructure/`.

---

### F1. Inbound WebSocket message buffer grows to ~256 MiB per connection before the size limit fires

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:25`,
  `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:110`,
  `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:125`,
  `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:167`,
  `src/ActualLab.Core/Collections/ArrayPoolBufferCapacity.cs:27`,
  `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`,
  `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:90`
- **What:** The default `RpcWebSocketTransport.Options.MaxMessageSize` is
  `GetMaxMessageSize(130_000_000)` ≈ **142,261,962 bytes (~135.7 MiB)**, and the receive
  loop grows a single `ArrayPoolBuffer<byte>` up to that size *before* it detects the
  overflow. Because `ArrayPoolBufferCapacity.Round` rounds every allocation up to the next
  power of two, the array actually reaches **2^28 = 256 MiB**, with a transient peak of
  ~384 MiB during the last `Pool.Resize` (rent-new + copy + return-old). This buffer is
  per connection, and the server uses exactly these defaults.
- **Why it matters / attack path:**
  1. `MapRpcWebSocketServer` maps `/rpc/ws` with `endpoints.Map(...)` and **no
     authorization metadata** (`src/ActualLab.Rpc.Server/EndpointRouteBuilderExt.cs:22`),
     so the endpoint is anonymous.
  2. An unauthenticated client upgrades to WebSocket and starts sending one logical
     message as an unbounded series of *continuation* frames (`endOfMessage: false`).
  3. Each frame is appended into the same buffer; the loop only stops growing when
     `remainingCapacity == 0`, i.e. after the *entire* ~136 MiB has been buffered
     (`:110`, `:167`). Until then nothing rejects the peer.
  4. The buffer stays pinned for as long as the attacker keeps the message incomplete —
     they can send 1 byte per second and hold hundreds of MiB indefinitely.
  5. ~10 such connections (≈1.3 GB uploaded, or far less if they stop at 8–64 MiB each)
     exhaust a typical container's memory. Arrays above 1 MiB are not pooled by
     `ArrayPool<byte>.Shared`, so every one of them is a fresh LOH allocation.
- **Evidence:**
  ```csharp
  // RpcWebSocketTransport.cs:25
  public int MaxMessageSize { get; init; } = RpcTextMessageSerializerV3.GetMaxMessageSize(
      Math.Max(RpcTextMessageSerializer.Defaults.MaxArgumentDataSize,
               RpcByteMessageSerializer.Defaults.MaxArgumentDataSize));
  // RpcByteMessageSerializer.cs:13
  public static int MaxArgumentDataSize { get; set; } = 130_000_000; // 130 MB;

  // RpcWebSocketTransport.cs:110..134 — grow-until-limit
  var remainingCapacity = maxMessageSize - buffer.WrittenCount;
  var isOverflowProbe = remainingCapacity == 0;
  ...
  var requestedCapacity = Math.Min(Math.Max(bufferSize, buffer.FreeCapacity), remainingCapacity);
  _ = buffer.GetMemory(requestedCapacity);          // <- ResizeBuffer -> Round -> next power of 2
  ...
  buffer.Advance(count);
  if (!endOfMessage) continue;                      // keep appending into the same buffer
  ```
  ```csharp
  // ArrayPoolBufferCapacity.cs:27
  return Math.Max(MinCapacity, (int)Bits.GreaterOrEqualPowerOf2((ulong)capacity));
  ```
  Server side reuses the client defaults verbatim:
  ```csharp
  // RpcWebSocketServer.cs:90
  var transportOptions = WebSocketClientOptions.WebSocketTransportOptionsFactory.Invoke(peer, properties);
  ```
  `RpcWebSocketClientOptions.DefaultWebSocketTransportOptionsFactory`
  (`src/ActualLab.Rpc/Clients/RpcWebSocketClientOptions.cs:79`) returns
  `RpcWebSocketTransport.Options.Default with { FrameDelayerFactory = ... }`, i.e. the
  default `MaxMessageSize`. Nothing in `src/` overrides it (verified by grep).
- **Related (lower impact, same class):** `RpcPipeTransport.Options.MaxFrameSize` and
  `RpcStreamTransport.Options.MaxFrameSize` are `16_000_000`
  (`src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:26`,
  `src/ActualLab.Rpc/Infrastructure/RpcStreamTransport.cs:24`), which the power-of-two
  rounding turns into a **32 MiB** pinned array per HTTP/2 RPC connection
  (`RpcHttpServer` also clears Kestrel's body size limit at
  `src/ActualLab.Rpc.Server/RpcHttpServer.cs:34`). The 8.5x discrepancy between the two
  transports' limits is itself a bug: a message that is accepted over WS is rejected over
  HTTP/2.
- **Fix:**
  1. Lower the default `MaxMessageSize` by an order of magnitude (e.g. 4–16 MiB) and make
     it explicit rather than derived from `MaxArgumentDataSize`; align it with
     `RpcPipeTransport`/`RpcStreamTransport`'s `MaxFrameSize`.
  2. Cap growth independently of the limit: keep a separate `MaxGrowthStep` and, more
     importantly, close the connection as soon as `buffer.WrittenCount + count` would
     exceed the limit rather than after the limit has been fully buffered.
  3. Consider capping the *rounded* allocation (`Round` to the next power of two on a
     136 MiB request silently doubles the footprint) — clamp the rounded capacity to the
     configured maximum.

---

### F2. Unbounded, never-evicted method-resolver cache keyed by the remote peer's wire-supplied `VersionSet`

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:15`,
  `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:110`,
  `src/ActualLab.Rpc/RpcPeer.cs:614`, `src/ActualLab.Rpc/RpcPeer.cs:563`,
  `src/ActualLab.Rpc/RpcPeer.cs:350`,
  `src/ActualLab.Core/Collections/VersionSet.cs:37`,
  `src/ActualLab.Core/Collections/VersionSet.cs:109`
- **What:** `RpcServiceRegistry._legacyServerMethodResolvers` is a process-wide
  `ConcurrentDictionary<VersionSet, RpcMethodResolver>` with **no size bound and no
  eviction**, and its key is `RpcHandshake.RemoteApiVersionSet` — a value taken verbatim
  from the first message a remote peer sends. Every distinct `VersionSet` an attacker
  sends adds a permanent entry (key + a freshly built `RpcMethodResolver`) and costs a
  full scan over every registered service/method.
- **Why it matters / attack path:**
  1. Anonymous WebSocket connect to `/rpc/ws` (no auth — see F1 step 1).
  2. The very first message on the wire is the handshake; `RpcPeer.OnRun` reads it via
     `ProcessHandshake` and keeps `handshake.RemoteApiVersionSet` as-is (only substituting
     an empty set when it is `null`, `RpcPeer.cs:350`).
  3. `SetConnectionState` → `GetServerMethodResolver(newState.Handshake)` →
     `Hub.ServiceRegistry.GetServerMethodResolver(handshake.RemoteApiVersionSet)` →
     `_legacyServerMethodResolvers.GetOrAdd(versions, ...)`.
  4. `VersionSet` is deserialized from a single string (`Value`) whose only size bound is
     `MaxArgumentDataSize` (130 MB). `TryParse` expands it into a
     `Dictionary<string, Version>`. A handful of handshakes carrying multi-MB version
     strings permanently pin hundreds of MB; alternatively, a large number of cheap
     connections with distinct tiny `VersionSet`s leaks steadily and irreversibly.
  5. Aggravating factor — `VersionSet.GetHashCode` is a plain **XOR** fold over per-entry
     hashes (`VersionSet.cs:41-42`). XOR combiners are trivially collidable (solve a small
     linear system over GF(2)^32), so an attacker can also force thousands of distinct
     keys into one `ConcurrentDictionary` bucket, degrading every subsequent handshake
     lookup to an O(n) chain of full `Equals` comparisons — an algorithmic-complexity DoS
     on top of the memory leak.
- **Evidence:**
  ```csharp
  // RpcServiceRegistry.cs:15
  private readonly ConcurrentDictionary<VersionSet, RpcMethodResolver> _legacyServerMethodResolvers = new();

  // RpcServiceRegistry.cs:110-119
  public RpcMethodResolver GetServerMethodResolver(VersionSet? versions)
  {
      if (versions is null) return ServerMethodResolver;
      return _legacyServerMethodResolvers.GetOrAdd(
          versions,
          static (versions, self) => new RpcMethodResolver(self, versions, self.ServerMethodResolver, self.Log),
          this);
  }
  ```
  ```csharp
  // RpcPeer.cs:560-568
  protected virtual RpcMethodResolver GetServerMethodResolver(RpcHandshake? handshake)
      => Hub.ServiceRegistry.GetServerMethodResolver(handshake?.RemoteApiVersionSet);
  // RpcPeer.cs:614 (inside SetConnectionState, run for every accepted connection)
  _serverMethodResolver = GetServerMethodResolver(newState.Handshake);
  ```
  ```csharp
  // VersionSet.cs:41-42 — XOR-based hash
  foreach (var (scope, version) in Items)
      hashCode ^= System.HashCode.Combine(scope.GetOrdinalHashCode(), version.GetHashCode());
  ```
  Grep confirms `_legacyServerMethodResolvers` is only ever written by that `GetOrAdd` —
  there is no removal, TTL, or capacity check anywhere in `src/`.
- **Fix:**
  - Validate the inbound `VersionSet` before it is used as a cache key: reject handshakes
    whose `RemoteApiVersionSet.Count` or `Value.Length` exceeds a small bound (a handful of
    scopes / a few hundred bytes is all a legitimate client ever needs).
  - Replace the unbounded dictionary with a bounded/LRU cache (or normalize the incoming
    set to the scopes the registry actually knows about before caching — unknown scopes
    cannot affect resolution, so they should be dropped from the key entirely).
  - Replace `VersionSet.GetHashCode`'s XOR fold with an order-independent but
    non-linear combiner (e.g. sum of `HashCode.Combine` results mixed through a final
    avalanche step), or seed it per process.

---

### F3. Unbounded server-peer registry growth from an unauthenticated `clientId` query parameter

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`,
  `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62`,
  `src/ActualLab.Rpc/RpcHub.cs:23`, `src/ActualLab.Rpc/RpcHub.cs:124`,
  `src/ActualLab.Rpc/RpcServerPeer.cs:78`,
  `src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54`
- **What:** `RpcHub.Peers` is an unbounded `ConcurrentDictionary<RpcRoute, RpcPeer>`. A
  server peer is created — and a background `WorkerBase` task started — as soon as an
  anonymous WebSocket/HTTP upgrade request arrives, keyed by the caller-supplied
  `?clientId=` query value. Once created, a server peer survives for **at least 3 minutes**
  even if the client never completes the handshake or immediately disconnects.
- **Why it matters / attack path:**
  1. `RpcWebSocketServer.Invoke` builds `rpcRef` from the query string
     (`clientId`, `f`) and calls `Hub.GetServerPeer(rpcRef)` at line 62 — **before**
     `AcceptWebSocketAsync`, before the RPC handshake, and with no authentication on the
     endpoint.
  2. `RpcHub.GetPeer` inserts a new `RpcServerPeer` into `Peers` and calls
     `peer.Start(isolate: true)` — a live `Task.Run` loop plus four call/object trackers,
     two of which pre-size their `ConcurrentDictionary` to 131 buckets
     (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`,
     `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:83`). Order of ~10 KB of live
     state per peer.
  3. The attacker aborts the connection (or never sends a handshake).
     `RpcServerPeer.GetConnection` then parks on
     `ServerPeerShutdownTimeoutProvider.Invoke(this)`, whose default is
     `peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15))`
     — for a brand-new peer that clamps to **3 minutes**.
  4. Repeat with a fresh random `clientId` each time. At a few thousand requests/second
     the process accumulates 10^5–10^6 live peers and background tasks within the 3-minute
     window. There is no per-IP limit, no cap on `Peers.Count`, and no rejection path.
- **Evidence:**
  ```csharp
  // RpcWebSocketServerDefaultDelegates.cs:27-33
  public static RpcWebSocketServerRefFactory RefFactory { get; set; } =
      static (server, context, isBackend) => {
          var query = context.Request.Query;
          var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
          var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
          return RpcRef.NewServer(clientId, serializationFormat, isBackend);
      };
  ```
  ```csharp
  // RpcWebSocketServer.cs:61-62 (before AcceptWebSocketAsync at :84)
  Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
  var peer = Hub.GetServerPeer(rpcRef);
  ```
  ```csharp
  // RpcHub.cs:131-146
  lock (Lock) {
      ...
      peer = PeerOptions.PeerFactory.Invoke(this, route);
      Peers[route] = peer;
      peer.Start(isolate: true);
  ```
  ```csharp
  // RpcPeerOptions.cs:54-58
  protected static TimeSpan DefaultServerPeerShutdownTimeoutProvider(RpcServerPeer peer)
  {
      var peerLifetime = Moment.Now - peer.CreatedAt;
      return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
  }
  ```
  `RpcHttpServer.Invoke` has the identical pattern
  (`src/ActualLab.Rpc.Server/RpcHttpServer.cs:57`), as does the .NET Framework variant
  (`src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:45`).
- **Fix:**
  - Do not create the peer until the connection is actually established *and* the RPC
    handshake has been received; at minimum move `Hub.GetServerPeer` after
    `AcceptWebSocketAsync`.
  - Use a much shorter shutdown timeout for a peer that has *never* had a successful
    handshake (e.g. `HandshakeTimeout`-sized, seconds not minutes); keep the 3–15 minute
    grace only for peers that had at least one live connection.
  - Add a configurable cap on `RpcHub.Peers.Count` for server peers (reject the upgrade
    with 503 when exceeded), and validate `clientId` shape/length before using it as a key.

---

### F4. Unbounded outbound write channel with no send timeout — slow-reader memory exhaustion

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:34`,
  `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:90`,
  `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:73`,
  `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:77`,
  `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:176`
- **What:** Every frame-based transport enqueues outbound messages into an
  **unbounded** channel and flushes them with `WebSocket.SendAsync(..., CancellationToken.None)`.
  A peer that stops reading (TCP zero-window) blocks the writer loop forever while the
  channel keeps growing; nothing applies backpressure, times the send out, or drops the
  peer.
- **Why it matters / attack path:**
  1. Anonymous WebSocket connect (see F1 step 1); complete the handshake.
  2. Stop reading from the socket at the TCP level (never drain the receive buffer) but
     keep *sending*. Sending still works, so the server's keep-alive watchdog
     (`RpcSharedObjectTracker.Maintain`,
     `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:238`) never fires — it only
     checks *inbound* keep-alives.
  3. Pipeline a large number of RPC calls whose results are large. Each completed inbound
     call calls `RpcSystemCallSender.Ok` → `RpcOutboundCall.SendNoWait` →
     `Peer.Transport?.Send(message, ...)` → `_writeChannelWriter.TryWrite(message)`.
  4. The writer loop is parked in `await lastFlushTask` inside `FlushFrame` →
     `WriteFrame` → `SendAsync(..., CancellationToken.None)`; the channel grows without
     limit. Because serialization is *lazy* (`CreateOutboundMessage` stores the
     `ArgumentList`, not bytes — `RpcOutboundCall.cs:141`, `:200`), each queued message
     also pins the entire result object graph.
  5. Nothing bounds this: `RpcOutboundCallTracker.Maintain` only times out *registered*
     outbound calls; `Ok`/`Error`/stream-item responses are `NoWait` and never registered.
     Inbound-call concurrency is likewise unlimited (no semaphore anywhere in
     `src/ActualLab.Rpc`).
- **Evidence:**
  ```csharp
  // RpcWebSocketTransport.cs:34-39
  public ChannelOptions WriteChannelOptions { get; init; } = new UnboundedChannelOptions() {
      // FullMode = BoundedChannelFullMode.Wait,
      SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false,
  };
  // RpcWebSocketTransport.cs:89-90
  protected override Task WriteFrame(ReadOnlyMemory<byte> frame)
      => WebSocket.SendAsync(frame[Int32Size..], MessageType, endOfMessage: true, CancellationToken.None).AsTask();
  ```
  ```csharp
  // RpcFrameBasedTransport.cs:77-83 — no backpressure signal to the caller
  public override void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default)
  {
      if (_writeChannelWriter.TryWrite(message)) return;
      _ = Write(message, cancellationToken);
  }
  // RpcFrameBasedTransport.cs:176-179 — writer parks here
  if (WriteFrameLength >= _frameSize) {
      await lastFlushTask.ConfigureAwait(false);
      lastFlushTask = FlushFrame();
  }
  ```
  `RpcPipeTransport.Options.WriteChannelOptions` (`RpcPipeTransport.cs:29`) and
  `RpcStreamTransport.Options.WriteChannelOptions` (`RpcStreamTransport.cs:27`) are
  unbounded as well; `RpcStreamTransport.WriteFrame` at least passes `StopToken`, so it is
  cancellable, but `RpcWebSocketTransport` passes `CancellationToken.None`.
- **Fix:**
  - Track the queued byte count / message count per transport and (a) apply a send
    timeout, or (b) disconnect the peer when the queue exceeds a threshold. A
    `BoundedChannel` with `FullMode = DropWrite` + disconnect, or an explicit
    `Interlocked` byte counter checked in `Send`, both work.
  - Pass a cancellable token (e.g. `StopToken`, or a per-send timeout CTS) to
    `WebSocket.SendAsync` so a stuck flush can be aborted; today only the *read* loop's
    `AbortWebSocket` registration can unstick it, and it is only triggered by
    `StopToken`/`readerToken`.
  - Make the keep-alive watchdog bidirectional: also drop the peer when no frame has been
    successfully *flushed* within `KeepAliveTimeout`.

---

### F5. Server peer identity is established solely by an unauthenticated `clientId`; the handshake authenticates nothing

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE
- **Category:** auth-bypass
- **Location:** `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`,
  `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:72`,
  `src/ActualLab.Rpc/RpcRef.cs:134`,
  `src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:25`
- **What:** Which `RpcServerPeer` an incoming connection binds to is decided entirely by
  the `clientId` query parameter (server `RpcRef` equality is `Address`-based, and the
  address ends with the raw `HostInfo`/`clientId`). The RPC handshake carries a
  `RemotePeerId` but it is never verified against anything — it is only used to decide
  *whether the remote changed*. A connection presenting an existing `clientId`
  unconditionally evicts the current owner of that peer.
- **Why it matters / attack path:**
  - Anyone who learns a victim's `clientId` (it travels in the URL query string, so it
    lands in reverse-proxy/access logs, in the server's own Information-level log line —
    `RpcWebSocketServer.cs:61` logs the full URI including the query — and in any
    intermediary that records URLs) can connect and force
    `peer.Disconnect(...)` on the victim's live connection (`RpcWebSocketServer.cs:72-76`).
    Repeating this is a targeted, indefinite denial of service against that one client:
    the victim reconnects with the same `clientId` and gets kicked again.
  - The attacker also *adopts* the peer object: `Peer.Extensions`, `OutboundCalls`, and —
    if they can also replay the victim's `RemotePeerId` — `SharedObjects` (server-side
    streams) survive the swap, because `GetPeerChangeKind` returns `Unchanged` when the
    `RemotePeerId` matches (`RpcHandshake.cs:30-32`), which skips
    `Reset(Errors.PeerChanged())` (`RpcPeer.cs:369-377`). In that case previously-created
    server-side streams keep pushing their items to the attacker's socket.
  - Degenerate case: when the `clientId` parameter is absent, the default is `""`, so
    **all** such clients collapse onto a single shared server peer.
  - Not rated higher because `RpcClientPeer.ClientId` is `Guid.NewGuid().ToBase64Url()`
    (122 bits from a CSPRNG), so it is not guessable, and the `RemotePeerId` replay half
    of the attack additionally requires observing the encrypted handshake. The
    DoS/eviction half only requires the `clientId`.
- **Evidence:**
  ```csharp
  // RpcRef.cs:134-137 — server refs compare by Address only
  public bool Equals(RpcRef? other)
      => UseReferentialEquality ? ReferenceEquals(this, other)
         : other is not null && AddressHashCode == other.AddressHashCode && Address.Equals(other.Address);
  ```
  ```csharp
  // RpcWebSocketServer.cs:72-76 — new connection evicts the old one, no identity check
  if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
      Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...", rpcRef, peer);
      await peer.Disconnect(cancellationToken).ConfigureAwait(false);
  }
  ```
  ```csharp
  // RpcPeer.cs:541-558 — ProcessHandshake validates the *shape* of the handshake, never its identity
  ```
- **Fix:**
  - Bind the server peer key to something the server controls or authenticates, not to a
    raw query parameter: e.g. hash `clientId` together with the authenticated principal /
    session id, so a `clientId` from one identity can never select another identity's peer.
  - Reject empty `clientId` (and enforce a length/charset bound) in the default
    `RpcWebSocketServerRefFactory`.
  - Move `clientId` out of the query string (a header or subprotocol value) so it stays
    out of URL-based logs; and stop logging the full request URI at Information level
    (`RpcWebSocketServer.cs:31`, `:61`).

---

### F6. Unbalanced lock release in `RpcPeer.SetConnectionState`

- **Severity:** LOW
- **Confidence:** PLAUSIBLE (latent; I could not construct a reachable path with the
  current callers)
- **Category:** race / logic
- **Location:** `src/ActualLab.Rpc/RpcPeer.cs:604`, `src/ActualLab.Rpc/RpcPeer.cs:621`,
  `src/ActualLab.Rpc/RpcPeer.cs:653`
- **What:** The `TrySetNext`-failed early return sits **inside** the `try` block and
  releases the lock itself, but the `finally` releases it a second time. If that branch is
  ever taken, `Lock.Exit()` / `Monitor.Exit(Lock)` is executed twice for a single
  `Enter`, and the `finally` also runs the state-transition side effects
  (`_transport = ...`, `MarkConnected`/`MarkDisconnected`, `ReaderTokenSource` cancel)
  outside the lock and against a state that was *not* installed.
- **Why it matters / attack path:** `AsyncState<T>.TrySetNext` returns `this` when the
  state already has a successor or is final
  (`src/ActualLab.Core/Async/AsyncState.cs:157-161`). Today `SetConnectionState` is called
  only from `RpcPeer.OnRun` and every call site guards with `RequireNonFinal()` /
  `IsFinal`, so I could not reach it. But the method is `private` only by convention —
  any future call site, or any change that lets `_connectionState` advance from elsewhere,
  turns this into a `SynchronizationLockException` thrown from a `finally` (which, in the
  `finally` of `OnRun` at `RpcPeer.cs:480-493`, would release the *outer* `lock (Lock)`
  and then throw out of the peer's shutdown path).
- **Evidence:**
  ```csharp
  // RpcPeer.cs:599-656 (abridged)
  try {
      ...
      var nextConnectionState = connectionState.TrySetNext(newState);
      if (ReferenceEquals(nextConnectionState, connectionState)) {
          Lock.Exit();                 // <-- first release, inside try
          return connectionState;
      }
      ...
  }
  finally {
      _transport = newState.IsConnected() ? newState.Transport : null;
      ... MarkTerminated / MarkConnected / MarkDisconnected ...
      Lock.Exit();                     // <-- second release on that path
  }
  ```
- **Fix:** Move the `TrySetNext` early-return above the `try`, or replace the manual
  `Enter`/`Exit` pair with a `bool mustRunEffects` flag checked at the top of the `finally`
  (and a single `Exit`).

---

## Out-of-partition findings

- **MEDIUM / info-leak (P4/P6 territory).** `RpcWebSocketServer.Invoke` and
  `RpcHttpServer.Invoke` build `requestDescription` from the full URI *including the query
  string* and log it at `Information` level
  (`src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:25-31`, `:61`, `:104`, `:110`, `:115`;
  `src/ActualLab.Rpc.Server/RpcHttpServer.cs:25-31`, `:56`). Fusion's server accepts the
  **session id** as a query parameter (`session=`,
  `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:14`, `:29-31`), so any deployment
  that uses that form writes session ids into ordinary application logs (and into every
  reverse-proxy access log on the path). Recommend redacting the query string in these log
  statements and preferring a header/cookie for the session id.
- **Note (P2 territory).** There is no limit anywhere in `ActualLab.Rpc` on the number of
  concurrently in-flight inbound calls: `RpcPeer.OnRun`'s read loop does
  `_ = ProcessMessage(reader.Current, ...)` (`src/ActualLab.Rpc/RpcPeer.cs:420`) and moves
  on immediately, and `RpcInboundCallTracker` grows without bound
  (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:46-76`). This is the multiplier
  that makes F4 practical; P2 should confirm/own it.
- **Note (P3 territory).** `RpcFrameCodec.TryDeserializeBinaryWithSize` reads the 4-byte
  size prefix with `array.AsSpan(offset).ReadLittleEndian()`
  (`src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:135`) without first checking that
  `totalLength - offset >= 4`. It reads up to 3 bytes of stale buffer content past
  `totalLength` (still inside the rented array, so memory-safe), and the subsequent
  `isSizeValid` check plus the `catch` make the outcome benign — but it is reading data it
  should not, and the `catch` silently swallows framing corruption instead of dropping the
  peer. Worth a look from P3.

---

## Areas examined

Read in full:

- `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs`, `WebSocketOwner.cs`,
  `WebSocketExt.cs`, `RpcWebSocketCloseCode.cs`
- `src/ActualLab.Rpc/Clients/RpcWebSocketClient.cs`, `RpcWebSocketClientOptions.cs`,
  `RpcHttpClient.cs`, `RpcHttpClientOptions.cs`, `RpcAlternatingClient.cs`,
  `Internal/DuplexHttpContent.cs`, `Internal/RpcHttpConnectionOwner.cs`
- `src/ActualLab.Rpc/` root: `RpcPeer.cs`, `RpcClientPeer.cs`, `RpcServerPeer.cs`,
  `RpcClientPeerReconnectDelayer.cs`, `RpcHub.cs`, `RpcClient.cs`, `RpcConnection.cs`,
  `RpcRef.cs`, `RpcRef.Static.cs`, `RpcRoute.cs`, `RpcException.cs`
- `src/ActualLab.Rpc/Infrastructure/` (peer/connection/framing part):
  `RpcTransport.cs`, `RpcFrameBasedTransport.cs`, `RpcPipeTransport.cs`,
  `RpcStreamTransport.cs`, `RpcSimpleChannelTransport.cs`, `RpcHandshake.cs`,
  `RpcPeerConnectionState.cs`, `RpcInboundMessage.cs`, `RpcOutboundMessage.cs`,
  `RpcHeader.cs`, `RpcHeaderKey.cs`, `RpcCallTrackers.cs`, `RpcObjectTrackers.cs`,
  `RpcSystemCalls.cs`, `RpcSystemCallSender.cs`, `RpcInboundContext.cs`,
  `RpcInboundCall.cs`, part of `RpcOutboundCall.cs`
- `src/ActualLab.Rpc/Configuration/`: `RpcLimits.cs`, `RpcMethodResolver.cs`,
  `RpcServiceRegistry.cs`, `RpcServiceDef.cs`, `RpcMethodRef.cs`, `RpcConfiguration.cs`,
  `RpcSerializationFormatResolver.cs`, `RpcFrameDelayers.cs`,
  `RpcFrameDelayerFactories.cs`, `RpcCallTimeouts.Default.cs`, `LegacyNames.cs`,
  `Options/RpcPeerOptions.cs`, `Options/RpcRegistryOptions.cs`,
  `Options/RpcInboundCallOptions.cs`, `Options/RpcOutboundCallOptions.cs`
- `src/ActualLab.Rpc/Internal/RpcRefAddress.cs`
- `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs`, `RpcMessageSerializer.cs`,
  and the size-limit constants in `RpcTextMessageSerializerV3.cs` /
  `RpcByteMessageSerializer.cs` / `RpcTextMessageSerializer.cs`

Read as supporting context (outside the partition, used to prove reachability):

- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs`, `RpcHttpServer.cs`,
  `RpcWebSocketServerDefaultDelegates.cs`, `RpcWebSocketServerOptions.cs`,
  `EndpointRouteBuilderExt.cs`
- `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs`
- `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs`
- `src/ActualLab.Core/Collections/ArrayPoolBuffer.cs`, `ArrayPoolBufferCapacity.cs`,
  `ArrayPools.cs`, `VersionSet.cs`
- `src/ActualLab.Core/Async/ProcessorBase.cs`, `WorkerBase.cs`, `AsyncState.cs`
- `src/ActualLab.Core/Net/RetryDelayer.cs`, `src/ActualLab.Core/Time/RetryDelaySeq.cs`
- `src/ActualLab.Core/Serialization/ExceptionInfo.cs`
- `tests/ActualLab.Tests/Rpc/RpcWebSocketTransportSizeTest.cs`

No repository files were modified; no builds or experiments were run (all findings are
established by source reading — I did not need a worktree).

## Areas NOT examined

- **`src/ActualLab.Rpc/Infrastructure/` call-pipeline internals** — `RpcOutboundCall`
  (beyond the send path), `RpcCall`, `RpcCallHandler`, `RpcCallStage`, `RpcStream`,
  `RpcSharedStream`, `RpcObjectId`, `RpcInboundNotFoundCall`,
  `RpcInboundInvalidCallTypeCall`, `RpcSendHandlers`, `RpcOutboundContext`,
  `RpcOutboundCallSetup`, `RpcInterceptor`. These are P2's partition; I only followed the
  paths needed to prove F4 and to rule out a use-after-reuse of the transport read buffer
  (`RpcInboundCall.Process` clears `Message.ArgumentData` synchronously —
  `RpcInboundCall.cs:102`, `:110`, `:141` — so the "buffer can be reused" comments in the
  transports hold).
- **`src/ActualLab.Rpc/Middlewares/`, `Internal/` (except `RpcRefAddress`), `Caching/`,
  `Attributes/`, `Diagnostics/`** — P2's partition.
- **Serializer internals** (`RpcByteMessageSerializerV4/V5(+Compact)`,
  `RpcTextArgumentSerializerV4*`, `RpcArgumentSerializer`, `TypeRef` resolution) — P3's
  partition. I read only the size-limit constants they expose, because F1 depends on them.
- **`RpcMethodDef.*.cs`, `RpcCallType*.cs`, `RpcServiceBuilder.cs`, `RpcBuilder.cs`,
  `ServiceCollectionExt.cs`, `ServiceProviderExt.cs`, `RpcRefExt.cs`, `RpcNoWait.cs`,
  `RpcStream.cs`** — read only where they intersected a finding; these are mostly
  configuration/DI plumbing with no attacker-reachable input, and the method/service
  *resolution* aspect is P2's stated focus.
- **Origin / CSRF checks on the WebSocket upgrade** — belongs to P4
  (`RpcWebSocketServerOptions.ConfigureWebSocket` returns a bare
  `new WebSocketAcceptContext()` by default, so no origin validation is applied; P4 should
  confirm and rate it).
- **TypeScript transport client** (`ts/packages/rpc/`) — P9's partition; I did not verify
  whether the TS client honours the same framing limits or sends keep-alives.
- **No dynamic testing.** Findings F1–F4 are argued from source and constants; I did not
  build a harness to measure the actual RSS growth per connection. The numbers quoted
  (136 MiB limit → 256 MiB array, 3-minute peer lifetime, 32 MiB pipe/stream buffer) are
  computed from the constants cited, not measured.
