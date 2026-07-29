### F1. The handshake does not bind the wire peer identity to the connection's client identity

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** access-control
- **Location:** `src/ActualLab.Rpc/RpcPeer.cs:541`
- **What:** The handshake accepts `RemotePeerId` and `RemoteHubId` entirely from the peer and never verifies either against the `clientId`/route or the authenticated connection. The default client makes `clientId` from the same `RpcPeer.Id` it sends as `RemotePeerId`, but the server does not enforce that relationship.
- **Why it matters / failure scenario:** A party that obtains a client's query-string `clientId` can connect to the public WebSocket or HTTP RPC endpoint using that value, causing the server to select and replace the victim's `RpcServerPeer`. It can put the victim's decoded peer ID in the handshake so the replacement looks like an unchanged peer, or choose a new ID and receive pending calls whose default execution mode allows resend to a changed peer. This can disconnect the victim and route subsequent or resent server-to-client traffic to the wrong connection.
- **Evidence:** The default client sets `ClientId = Id.ToBase64Url()` (`src/ActualLab.Rpc/RpcClientPeer.cs:20`) and sends `Id` in its handshake (`src/ActualLab.Rpc/RpcPeer.cs:329`). `ProcessHandshake` validates only the method reference, call type, related ID, bound method, and argument type; it returns the attacker-supplied handshake without validating its IDs (`src/ActualLab.Rpc/RpcPeer.cs:541`, `src/ActualLab.Rpc/RpcPeer.cs:557`). Peer continuity then trusts only `RemotePeerId` (`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:25`). The public server selects a peer from the query-derived ref and replaces its old connection (`src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62`, `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:75`), while changed-peer reconnect resends calls that allow it (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:279`, `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:286`).
- **Fix:** Add a configurable handshake identity validator to `RpcPeerOptions` and invoke it before publishing `NextConnected`. For the default clients, carry the expected peer GUID from the validated `clientId`/connection properties and require it to equal `RemotePeerId`; also bind the connection key to the authenticated principal/session. Do not replace an established peer until the incoming connection has passed this validation.

### F2. Wire-supplied version sets permanently grow the method-resolver cache

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:15`
- **What:** `RpcServiceRegistry` keeps an unbounded, lifetime-long `ConcurrentDictionary` keyed by the `VersionSet` received in each handshake. There is no validation, allow-list, capacity, or eviction, so every distinct remote-supplied version set becomes a permanent cache entry.
- **Why it matters / failure scenario:** An unauthenticated peer can repeatedly connect using one valid `clientId`, complete the handshake with a fresh small version set (for example, a unique irrelevant scope), disconnect, and repeat. Every successful handshake adds another resolver and retains the supplied `VersionSet` until the `RpcHub` is disposed, allowing low-rate persistent memory growth without needing to keep connections open.
- **Evidence:** `RemoteApiVersionSet` is a serialized handshake field (`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:17`). Publishing any connected state calls `GetServerMethodResolver(newState.Handshake)` (`src/ActualLab.Rpc/RpcPeer.cs:614`), which passes the remote set to the registry (`src/ActualLab.Rpc/RpcPeer.cs:563`). The registry uses `_legacyServerMethodResolvers.GetOrAdd(versions, ...)` with no bound or eviction (`src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:110`, `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:115`).
- **Fix:** Validate the handshake set against configured scopes and reasonable count/length limits, discard irrelevant scopes, and avoid caching arbitrary combinations. Prefer precomputing the finite supported legacy mappings; otherwise use a bounded cache with eviction and expose a metric for rejected/distinct version sets.

### F3. Remote client IDs can retain an unbounded number of heavyweight server peers

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/RpcHub.cs:23`
- **What:** The hub peer registry has no admission bound, while each distinct remote-controlled `clientId` creates a new `RpcServerPeer`. After a failed or short connection, the default lifecycle keeps that peer waiting for another connection for at least three minutes before it becomes terminal and can be removed.
- **Why it matters / failure scenario:** An unauthenticated caller can open RPC upgrades with supported serialization and unique `clientId` values, then close or fail the handshake. Each request creates a peer with serializers, trackers, state sources, cancellation objects, and a worker; at a sustained request rate, the three-minute retention window permits an arbitrarily large live peer population and server memory exhaustion.
- **Evidence:** The server ref factory directly uses the query `clientId` (`src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30`), and the endpoint calls `Hub.GetServerPeer` (`src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62`). `GetPeer` inserts every new route into `Peers` and starts its worker without a limit (`src/ActualLab.Rpc/RpcHub.cs:124`, `src/ActualLab.Rpc/RpcHub.cs:138`). After disconnection, `RpcServerPeer` waits for the next connection until `ServerPeerShutdownTimeoutProvider` expires (`src/ActualLab.Rpc/RpcServerPeer.cs:78`), whose default minimum is three minutes (`src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54`).
- **Fix:** Put a validated length/format and ownership check on `clientId`, add per-principal/IP and global peer admission limits, and cap idle server peers. Remove never-handshaken peers immediately (or after a short, separately configured grace period) instead of applying the normal reconnect grace period.

### F4. Transport output queues are unbounded and inbound dispatch does not apply backpressure

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:77`
- **What:** All remote frame transports default to an unbounded outbound `Channel<RpcOutboundMessage>`, and `Send` always accepts into it while the socket writer may be blocked. Simultaneously, the peer read loop launches each inbound call without awaiting it, so a remote peer can continue causing responses faster than the connection can transmit them.
- **Why it matters / failure scenario:** After handshaking, a malicious peer can continue sending cheap calls while it stops reading server responses. Once `WebSocket.SendAsync`, pipe flush, or stream flush blocks on network backpressure, inbound messages continue to be dispatched and response messages accumulate without a count or byte budget, retaining contexts, arguments, and payloads until the process exhausts memory.
- **Evidence:** WebSocket, pipe, and stream options all use `UnboundedChannelOptions` by default (`src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:33`, `src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:28`, `src/ActualLab.Rpc/Infrastructure/RpcStreamTransport.cs:26`). `Send` enqueues immediately and fire-and-forgets the asynchronous path (`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:77`), while the writer cannot drain beyond a blocked prior flush (`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:176`). The peer read loop dispatches without awaiting completion (`src/ActualLab.Rpc/RpcPeer.cs:419`). Even a missing-object system `Ack` produces a response (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:154`, `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:157`), giving an attacker a cheap response-producing request.
- **Fix:** Give each transport a bounded message and byte budget. When the budget is exhausted, either pause inbound dispatch until capacity returns or fail/close the connection with a resource-limit error; do not spawn one pending `WriteAsync` task per overflowed message. Add per-peer in-flight call/rate limits so input processing and output capacity are coupled.

### F5. A pre-handshake WebSocket message may allocate about 142 MB per unauthenticated connection

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:25`
- **What:** The default WebSocket message cap is derived from a 130 MB argument allowance plus the worst-case JSON envelope, producing a cap of 142,261,962 bytes. The same cap is used before the first message has been identified as the tiny handshake, and the receive loop grows and retains the assembly buffer up to that size.
- **Why it matters / failure scenario:** An unauthenticated caller can open several WebSockets and send large fragmented first messages before completing them. Each connection can make the server rent/retain roughly 142 MB during the handshake window; a small number of concurrent connections can exhaust the process memory before RPC-level validation sees a handshake.
- **Evidence:** `MaxMessageSize` is computed from the maximum text/binary argument data sizes (`src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:25`), both of which default to 130,000,000 bytes (`src/ActualLab.Rpc/Serialization/RpcTextMessageSerializer.cs:13`, `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`). `ReadAll` repeatedly expands the buffer for fragments until that cap (`src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:109`, `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:125`); it only closes after probing a byte beyond the cap (`src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:167`). Handshake validation occurs only after the complete message is yielded (`src/ActualLab.Rpc/RpcPeer.cs:334`, `src/ActualLab.Rpc/RpcPeer.cs:339`).
- **Fix:** Enforce a small dedicated first-message/handshake limit in the transport before switching to the negotiated normal limit. Lower the general default, add aggregate receive-buffer admission accounting across connections, and require streaming/chunking for very large application payloads.

### F6. Default HTTP transports accept outbound payloads their receivers always reject

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:26`
- **What:** The default pipe and stream transports reject inbound frames above 16,000,000 bytes, but their outbound path has no corresponding frame-size check and the default serializers accept argument data up to 130,000,000 bytes. A valid locally-issued RPC call can therefore be serialized and sent into a frame that the remote default HTTP transport deterministically rejects.
- **Why it matters / failure scenario:** An application using the default full-duplex HTTP client calls a normal RPC method with a payload between roughly 16 MB and 130 MB. The sender accepts and writes it, the receiver throws `InvalidItemSize`, the connection drops, and reconnect/resend can repeat the same impossible call until its call timeout, making a documented/accepted payload size fail only at the remote transport boundary.
- **Evidence:** Pipe and stream defaults set `MaxFrameSize = 16_000_000` (`src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:26`, `src/ActualLab.Rpc/Infrastructure/RpcStreamTransport.cs:24`) and reject larger received lengths (`src/ActualLab.Rpc/Infrastructure/RpcPipeTransport.cs:134`, `src/ActualLab.Rpc/Infrastructure/RpcStreamTransport.cs:145`). The shared writer serializes a message and flushes after it crosses `FrameSize`, with no `MaxFrameSize` validation (`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:168`, `src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:176`). The default byte/text serializers allow 130,000,000 bytes (`src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`, `src/ActualLab.Rpc/Serialization/RpcTextMessageSerializer.cs:13`), and `RpcHttpClient` uses pipes by default (`src/ActualLab.Rpc/Clients/RpcHttpClientOptions.cs:18`).
- **Fix:** Define one effective per-transport message/frame limit and enforce it symmetrically before enqueueing or writing. Either derive HTTP `MaxFrameSize` from the active serializer limit, or lower the serializer/call limit for HTTP and fail the outbound call locally with a clear size-limit exception so it is not retried.

### F7. Disposing a client hub schedules uncancellable five-minute peer-retention tasks

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** leak
- **Location:** `src/ActualLab.Rpc/RpcPeer.cs:498`
- **What:** Every client peer schedules an uncancellable delayed removal task when its run loop stops, including when it stops because the containing `RpcHub` is being disposed. The default delay is five minutes, and the task captures the peer and hub, keeping the disposed RPC graph rooted for the full delay.
- **Why it matters / failure scenario:** A process that repeatedly creates and disposes client service providers/hubs (tests, tenant scopes, app reloads, or reconnectable embedded hosts) accumulates disposed hubs, peers, trackers, serializers, and delay tasks for five minutes. Disposal reports completion even though these object graphs remain rooted, producing avoidable transient memory growth under churn.
- **Evidence:** Hub disposal awaits disposal of all current peers (`src/ActualLab.Rpc/RpcHub.cs:63`). Each peer's `finally` obtains `PeerRemoveDelayProvider` and starts `Task.Run` with `Task.Delay(..., CancellationToken.None)` before calling back into `Hub.RemovePeer` (`src/ActualLab.Rpc/RpcPeer.cs:498`, `src/ActualLab.Rpc/RpcPeer.cs:502`). The default delay for client peers is five minutes (`src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:66`, `src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:70`).
- **Fix:** Remove peers immediately when `RpcHub` or the peer is explicitly disposing. For genuine post-terminal retention, use a hub-owned cancellable eviction mechanism (or timer set) that is canceled and drained during hub disposal rather than one independent `Task.Run` per peer.

## Areas examined

- All files under `src/ActualLab.Rpc/WebSockets/`, `src/ActualLab.Rpc/Clients/`, and `src/ActualLab.Rpc/Configuration/`.
- All project-root `src/ActualLab.Rpc/*.cs` files, with a deep pass over `RpcPeer`, `RpcClientPeer`, `RpcServerPeer`, `RpcHub`, `RpcConnection`, `RpcRef`, and `RpcRoute`.
- P1 infrastructure for handshakes, peer connection state, message envelopes, and frame transports: `RpcHandshake`, `RpcPeerConnectionState*`, `RpcTransport`, `RpcFrameBasedTransport`, `RpcWebSocketTransport`, `RpcPipeTransport`, `RpcStreamTransport`, and `RpcSimpleChannelTransport`.
- Keep-alive/reconnect support in `RpcObjectTrackers`, `RpcCallTrackers`, `RpcSystemCallSender`, and `RpcSystemCalls`.
- Supporting public entry paths in the ASP.NET Core WebSocket and full-duplex HTTP servers, relevant audit/regression tests, `VersionSet`, and serializer size/framing contracts needed to prove the findings.

## Areas NOT examined

- P2 call routing, method authorization, object/stream semantics, and call-table correctness beyond the narrow response, reconnect, and keep-alive paths needed as supporting evidence.
- P3 serializer implementation correctness beyond message-size constants and `RpcFrameCodec` framing/progress behavior.
- P4 endpoint security as an independent review; server endpoint files were read only to establish reachability into P1.
- Fusion state/cache, sessions/auth persistence, Core concurrency primitives, TypeScript, and all other P5-P9 areas.
- No dynamic experiment was run; the findings above were verified by complete source call-path tracing.
