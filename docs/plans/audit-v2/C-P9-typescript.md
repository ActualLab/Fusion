### F1. Accepted server peers have no handshake deadline and accumulate per-connection state

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `ts/packages/fusion-rpc/src/fusion-hub.ts:152`, `ts/packages/rpc/src/rpc-hub.ts:74`, `ts/packages/rpc/src/rpc-peer.ts:232`, `ts/packages/rpc/src/rpc-peer.ts:1283`, `ts/packages/rpc/src/rpc-peer.ts:1314`, `ts/packages/rpc/src/rpc-limits.ts:71`
- **What:** Each accepted connection receives a fresh random peer identity, is retained in `RpcHub.peers`, and immediately owns a periodic maintenance timer. `RpcServerPeer.accept` can remain in `Handshaking` forever; if the socket closes, the unique peer is still retained for the default 180-second reconnect grace period even though the public accept APIs can never reselect it.
- **Why it matters / attack path:** An unauthenticated remote client can repeatedly open WebSockets and never send a handshake. Every open socket then retains a peer, its trackers, handlers, and a maintenance interval indefinitely. Alternatively, opening and immediately closing connections retains one unique peer and close timer per attempt for three minutes, so a sufficiently high connection rate drives server memory, timer, and event-loop work upward without requiring a valid RPC session.
- **Evidence:** `FusionHub.acceptConnection` and `acceptRpcConnection` generate `server://${crypto.randomUUID()}` before every `getServerPeer` call (`fusion-hub.ts:152-165`). `RpcHub` stores every created peer in an unbounded `Map` (`rpc-hub.ts:74-76`, `rpc-hub.ts:158-168`), while the base peer constructor creates a `setInterval` (`rpc-peer.ts:232-242`). Server acceptance only changes state to `Handshaking` and installs handlers; it does not arm a handshake timeout (`rpc-peer.ts:1283-1295`). Closed server peers are removed only by the timer at `rpc-peer.ts:1314-1329`, whose default is 180,000 ms (`rpc-limits.ts:71-77`). The existing `handshakeTimeoutMs` is described and used only as the client-side wait for a server response (`rpc-limits.ts:38-39`).
- **Fix:** Add and enforce a server-side handshake deadline that closes and removes the peer. Apply global and per-origin caps to accepted/handshaking peers. Either assign a stable, validated identity that `acceptRpcConnection` can reuse for reconnection, or remove these per-connection random peers immediately on close instead of granting an unusable reconnect grace period; delay the per-peer maintenance interval until a handshake succeeds.

### F2. RPC framing and deserialization have no inbound resource limits

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** deserialization
- **Location:** `ts/packages/rpc/src/rpc-connection.ts:94`, `ts/packages/rpc/src/rpc-connection.ts:140`, `ts/packages/rpc/src/rpc-serialization.ts:48`, `ts/packages/rpc/src/rpc-serialization.ts:307`, `ts/packages/rpc/src/rpc-serialization.ts:380`, `ts/packages/rpc/src/rpc-limits.ts:32`
- **What:** The WebSocket receive path places no limit on frame bytes, messages per frame, arguments per message, decoded collection sizes, or nesting. Both text and binary splitters fully materialize attacker-controlled collections before dispatch.
- **Why it matters / attack path:** A hostile server can freeze or exhaust a browser/Node client by sending oversized frames, delimiter-dense text frames, or MessagePack argument data containing very many values. The same path is reachable before authentication when a TypeScript server accepts a hostile client. Repeating such frames amplifies the transport allocation into arrays, substrings, decoded objects, and dispatch work without any library-level budget or forced disconnect.
- **Evidence:** `RpcWebSocketConnection` constructs a default, unbounded `Decoder` (`rpc-connection.ts:94-96`) and passes each complete binary frame to `_splitBinary`, which first builds a `messages` array (`rpc-connection.ts:140-170`); text frames are fully split before iteration (`rpc-connection.ts:199-210`). `splitFrame` is an unrestricted `frame.split(...)`, and each message unrestrictedly splits/maps its arguments (`rpc-serialization.ts:48-50`, `rpc-serialization.ts:53-78`). The V5 decoder trusts wire lengths, pushes every `decodeMulti` result into `args`, and `splitBinaryFrame` pushes every envelope into `results` (`rpc-serialization.ts:307-363`, `rpc-serialization.ts:380-396`). `RpcLimits` exposes timing and completed-call settings but no receive-size or decode-count budget (`rpc-limits.ts:32-77`).
- **Fix:** Add configurable hard limits for inbound frame bytes, envelopes per frame, arguments per envelope, method/header lengths, nesting, and decoded string/binary/array/map sizes. Configure the MessagePack decoder with those limits, validate every V5 length as a nonnegative in-bounds safe integer before slicing, stream dispatch rather than materializing an entire frame where practical, and close the connection on the first limit violation.

### F3. Remote peers can create unbounded concurrent inbound calls

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-peer.ts:573`, `ts/packages/rpc/src/rpc-peer.ts:577`, `ts/packages/rpc/src/rpc-peer.ts:647`, `ts/packages/rpc/src/rpc-call-tracker.ts:232`, `ts/packages/rpc/src/rpc-call-tracker.ts:250`
- **What:** Every inbound RPC call is dispatched immediately with no concurrency or in-flight-call cap. Calls expecting results remain in an unbounded `Map` until their handler completes, while `noWait` calls bypass tracking but still launch unlimited asynchronous work.
- **Why it matters / attack path:** A remote peer can invoke a registered slow, blocking, or nonterminating method repeatedly with unique `RelatedId` values. On a TypeScript server this is reachable directly through the accepted RPC message path, including before handshake completion because regular inbound messages are dispatched without a connection-state gate. On a client, a hostile server can invoke client-exposed services. Pending calls retain their arguments, promises, abort state, and handler work, allowing memory and CPU exhaustion even when each individual frame is small.
- **Evidence:** The regular inbound path calls `_handleInbound` directly (`rpc-peer.ts:573-575`), whose `dispatch` awaits the service method (`rpc-peer.ts:577-644`). `noWait` starts `dispatch(undefined)` without admission control (`rpc-peer.ts:647-650`); other calls are inserted by attacker-selected ID and started immediately (`rpc-peer.ts:653-662`). `RpcInboundCallTracker` uses an unrestricted `Map` and `getOrRegister` always inserts a new ID (`rpc-call-tracker.ts:232-256`). Its only cap applies after calls have completed (`rpc-call-tracker.ts:259-272`), so it does not bound pending calls.
- **Fix:** Add per-peer and global maximum in-flight counts plus a bounded concurrency semaphore/queue. Reject or disconnect peers that exceed the budget, validate call IDs, and account for `noWait` work under the same concurrency limit. Gate ordinary calls until the handshake is complete.

### F4. A stream sender can overrun an unbounded receiver buffer

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-system-call-handler.ts:136`, `ts/packages/rpc/src/rpc-stream.ts:138`, `ts/packages/rpc/src/rpc-stream.ts:252`, `ts/packages/rpc/src/rpc-stream.ts:276`, `ts/packages/rpc/src/rpc-stream.ts:355`
- **What:** `RpcStream` accepts every sequential item or batch into an unbounded `Denque`; it never enforces the advertised acknowledgement window or any hard buffered-item/byte limit. Flow-control acknowledgements therefore constrain only cooperative senders.
- **Why it matters / attack path:** After returning a valid stream reference and letting the consumer start it, a hostile server can ignore acknowledgements and send sequential `$sys.I` or `$sys.B` calls faster than the application consumes them. A hostile client can do the same when a TypeScript server consumes a client-provided stream. The indices remain protocol-valid, so the receiver continually appends values until the tab or process runs out of memory.
- **Evidence:** `$sys.I` and `$sys.B` route wire-controlled indices and payloads directly to `onItem`/`onBatch` (`rpc-system-call-handler.ts:136-167`). Remote streams use a `Denque` with no capacity (`rpc-stream.ts:138-148`). A sequential item is always pushed (`rpc-stream.ts:252-273`), and a sequential batch pushes every element (`rpc-stream.ts:276-296`); neither path compares occupancy with `ackAdvance` or another limit. Consumption removes only one item per iterator `next()` (`rpc-stream.ts:355-379`).
- **Fix:** Enforce a receiver-side credit window and a hard per-stream buffered item/byte cap. Validate indices and batch lengths as nonnegative safe integers, and fail the stream or connection when a sender advances beyond granted credit. Make the limit configurable but finite by default.

### F5. Stream ACK flooding creates an unbounded, quadratically drained queue

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-system-call-handler.ts:196`, `ts/packages/rpc/src/rpc-stream-sender.ts:135`, `ts/packages/rpc/src/rpc-stream-sender.ts:182`, `ts/packages/rpc/src/rpc-stream-sender.ts:501`
- **What:** Every accepted `$sys.Ack` is appended to an unbounded JavaScript array. Draining repeatedly calls `shift()`, making a backlog quadratic to process, even though only the latest index plus the aggregate reset flag are used.
- **Why it matters / attack path:** Once a peer has obtained a locally hosted stream—for example, a hostile client calling a TypeScript server streaming method, or a hostile server receiving a client upload—it can flood that sender with ACK calls while the source iterator is slow or blocked. The ACK array then grows without bound; when the pump resumes, repeated front-removal can monopolize the event loop with quadratic copying and may exhaust memory first.
- **Evidence:** `$sys.Ack` passes wire-controlled values to `sender.onAck` (`rpc-system-call-handler.ts:196-206`). The sender stores ACKs in a plain array (`rpc-stream-sender.ts:135-137`), performs no finite/safe-integer, monotonicity, or queue-size validation, and pushes every accepted ACK (`rpc-stream-sender.ts:182-215`). `_tryProcessAcks` explicitly reduces the entire queue to the most recent `nextIndex` and OR of `mustReset`, but does so through repeated `shift()` calls (`rpc-stream-sender.ts:501-520`).
- **Fix:** Coalesce ACK state at receipt into one latest index and one accumulated reset bit instead of queueing every message. Also validate indices as finite nonnegative safe integers, reject regressions outside valid reset semantics, and impose a small hard cap if a queue is retained.

### F6. A late finalizer can delete a live replacement computed value

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** race
- **Location:** `ts/packages/fusion/src/computed-registry.ts:5`, `ts/packages/fusion/src/computed-registry.ts:14`, `ts/packages/fusion/src/computed-registry.ts:25`, `ts/packages/fusion/src/compute-method.ts:79`
- **What:** `ComputedRegistry` finalizers carry only a string key and unconditionally delete the current entry for that key. If an old computed is collected, the dead weak entry is observed and replaced before its delayed finalizer runs, that old finalizer deletes the live successor.
- **Why it matters / attack path:** This is a valid `FinalizationRegistry` scheduling sequence: a predecessor's `WeakRef` clears, `get` deletes the dead entry, the same input is recomputed and registered, and the predecessor's cleanup callback runs later. Explicit `.invalidate(args)` then cannot find the live computed, so cached/dependent data can remain stale; subsequent calls can also construct duplicate dependency graphs for the same key.
- **Evidence:** The registry maps each key to a `WeakRef`, while its callback receives only the key and executes `_entries.delete(key)` (`computed-registry.ts:5-8`). `get` removes cleared weak entries (`computed-registry.ts:14-20`), and `register` installs a new `WeakRef` under the same key without associating the finalizer with that particular reference/generation (`computed-registry.ts:25-35`). Both decorated and wrapped explicit invalidation depend solely on `ComputedRegistry.get(key)` finding the current entry (`compute-method.ts:79-88`, `compute-method.ts:101-114`).
- **Fix:** Register a held value containing the key and the exact `WeakRef` or a generation token. In the cleanup callback, delete only if `_entries.get(key)` still equals that reference/generation. Apply the same identity check in `unregister` so an obsolete instance cannot remove its successor.

### F7. Disconnect messages conflate remote and locally shared object ID spaces

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `ts/packages/rpc/src/rpc-system-call-handler.ts:219`, `ts/packages/rpc/src/rpc-remote-object-tracker.ts:3`, `ts/packages/rpc/src/rpc-shared-object-tracker.ts:3`
- **What:** A received `$sys.Disconnect` applies every numeric ID to both `remoteObjects` and `sharedObjects`, although these are independent namespaces and commonly contain the same low IDs. A disconnect for one direction can therefore terminate an unrelated stream in the opposite direction.
- **Why it matters / attack path:** Both peers allocate locally hosted stream IDs starting from small integers. If a client is simultaneously consuming remote stream ID 1 and uploading locally shared stream ID 1, a legitimate disconnect of the remote stream also aborts the upload. A hostile server can likewise send a guessed low ID to kill unrelated client-hosted streams multiplexed on the connection.
- **Evidence:** The disconnect handler looks up and calls `disconnect()` on the remote object, then repeats the operation on the shared object using the same ID (`rpc-system-call-handler.ts:219-243`). `RpcRemoteObjectTracker` and `RpcSharedObjectTracker` are separate maps keyed only by `localId` (`rpc-remote-object-tracker.ts:3-21`, `rpc-shared-object-tracker.ts:3-20`), and the shared namespace begins at 1 (`rpc-shared-object-tracker.ts:4-8`).
- **Fix:** Treat received disconnect IDs as referring only to objects hosted by the sender (`remoteObjects`). If the protocol must also allow a peer to cancel receiver-hosted/shared objects, encode the object direction/kind in a distinct system call or typed identifier instead of probing both maps.

### F8. Untyped stream-reference traversal corrupts JSON results and misses nested MessagePack streams

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `ts/packages/rpc/src/rpc-hub.ts:325`, `ts/packages/rpc/src/rpc-stream.ts:58`, `ts/packages/rpc/src/rpc-stream.ts:505`
- **What:** Every ordinary RPC result is recursively scanned, and any string with four to six comma-separated, `parseInt`-compatible fields is silently replaced by an `RpcStream`. Conversely, nested MessagePack stream-reference objects are not recognized because the recursive walker only calls `parseStreamRef` for strings.
- **Why it matters / attack path:** A server returning an ordinary DTO string such as `a,1,30,61` changes the application's data type and behavior without the method being declared as streaming. For binary RPC, a valid nested `{ SerializedId, AckPeriod, AckAdvance, ... }` stream reference remains a plain object, so the same API breaks depending on negotiated serialization format. A hostile server can deliberately trigger the JSON misclassification anywhere in a returned object graph.
- **Evidence:** All regular client methods pass their result through `resolveStreamRefs` unconditionally (`rpc-hub.ts:325-337`). The text parser accepts any host string and uses permissive `parseInt` for the next three fields (`rpc-stream.ts:58-71`). Although `parseStreamRef` supports object-shaped binary references (`rpc-stream.ts:73-86`), `resolveStreamRefs` invokes it only in the string branch (`rpc-stream.ts:505-516`); its object branch immediately walks properties and never tests the object itself (`rpc-stream.ts:519-536`).
- **Fix:** Materialize streams only in positions identified by RPC method/type metadata, using an explicit wire tag rather than heuristically interpreting arbitrary result strings. At minimum, require a strict tagged representation and exact safe-integer fields, and invoke object-reference parsing before recursively walking a MessagePack object.

## Areas examined

- Read all shipped source files under `ts/packages/core/src`, `ts/packages/rpc/src`, `ts/packages/fusion/src`, `ts/packages/fusion-rpc/src`, and `ts/packages/fusion-react/src`.
- Traced WebSocket connection setup, text and V5 binary framing, MessagePack/JSON decoding, handshake/reconnect/keepalive state, peer and call trackers, service dispatch, cancellation, system calls, remote/shared object lifecycles, bidirectional stream flow control, and client proxy result handling.
- Traced Fusion computed creation, weak registry lifecycle, invalidation/dependency propagation, state/update paths, and Fusion RPC invalidation handling.
- Examined core async/event/collection primitives used by the above paths, React subscription hooks, package manifests/build/test configuration, and the `ts/e2e` harness.
- Read targeted RPC, stream, reconnect, limits, serialization, computed-registry, Fusion, and React tests to confirm intended behavior and identify missing adversarial coverage.
- Searched the P9 source for dynamic execution, DOM/XSS sinks, storage/session handling, unsafe object merging, prototype-sensitive keys, unbounded maps/queues, listener/timer creation, deserialization entry points, and exception/log propagation.

## Areas NOT examined

- Third-party dependency implementations were not comprehensively audited and no dependency-CVE inventory was performed; dependencies were inspected only where needed to understand P9 call behavior.
- Generated package artifacts and published npm bundles were not compared byte-for-byte with source.
- Test files were read selectively around security and lifecycle behavior rather than every test file line-by-line; test-only defects are out of scope.
- No runtime experiment, build, fuzzing campaign, or browser memory profile was run, because verification did not require modifying or building the main working tree and the review rules prohibit doing so there.
- C# projects, server hosting integrations, samples, and documentation outside P9 were consulted only for protocol/context checks and were not independently reviewed.
