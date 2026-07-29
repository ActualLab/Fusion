# P9 — TypeScript client (`ts/packages/*`) — security & severe-bug review

Reviewer scope: `ts/packages/{core,rpc,fusion,fusion-rpc,fusion-react}`.
`ts/node_modules` and `ts/dist` excluded. C# (`src/ActualLab.Rpc`,
`src/ActualLab.Fusion`) read as reference for protocol/behaviour parity.

All repros below were run from the session scratchpad against the **prebuilt,
git-ignored** `ts/packages/*/dist` bundles. **No file in the repository working
tree was created, modified, staged, or committed** other than this report.

---

### F1. Server-side Fusion compute results are garbage-collectible, silently dropping their invalidation subscription — clients then serve permanently stale data

- **Severity:** HIGH
- **Confidence:** CONFIRMED (end-to-end repro below)
- **Category:** logic / cache-coherence
- **Location:** `ts/packages/fusion-rpc/src/fusion-hub.ts:205` (`_wrapServerMethod`),
  `ts/packages/fusion-rpc/src/fusion-hub.ts:218`,
  `ts/packages/fusion/src/computed-registry.ts:5`,
  `ts/packages/rpc/src/rpc-call-tracker.ts:184` (`RpcInboundCall` — no `computed` field)
- **What:** On a TS-hosted Fusion server the only thing that links an inbound
  compute call to its `Computed` is the `computed.whenInvalidated()` subscription
  created inside `_wrapServerMethod`. That subscription is stored **on the
  computed itself**, and `ComputedRegistry` holds only a `WeakRef`. Nothing else
  references the computed once the wrapper returns, so V8 collects it — taking the
  invalidation subscription with it. The client's `RpcOutboundComputeCall` stays
  registered with the old value and never receives `$sys-c.Invalidate`.
- **Why it matters / attack path:** No attacker needed — ordinary GC pressure is
  the trigger. Sequence: (1) client makes a compute call, gets value `V`;
  (2) a GC runs on the server; (3) the underlying data changes and the server-side
  `MutableState`/dependency is invalidated; (4) the collected computed has no
  dependants left to notify, so no `$sys-c.Invalidate` is emitted; (5) the client's
  Fusion cache keeps serving `V` forever (until reconnect). This is exactly the
  guarantee Fusion exists to provide, and its failure is silent and
  non-deterministic. .NET does not have this hole: `RpcInboundComputeCall`
  keeps a **strong** `Computed` reference for the lifetime of the inbound call.
- **Evidence:**
  ```ts
  // fusion-hub.ts:205-236 — the computed is a local, dropped on return
  const computed = await cf.invoke(impl, cleanArgs);
  if (context?.callType === FUSION_CALL_TYPE_ID) {
      void computed.whenInvalidated().then(() => { setTimeout(() => { ...invalidate(conn, ..., callId) }, 0); });
  }
  return computed.value;
  ```
  ```ts
  // computed-registry.ts:5-6 — weak only
  private static _entries = new Map<string, WeakRef<Computed<unknown>>>();
  private static _finalization = new FinalizationRegistry<string>(key => { ... });
  ```
  Repro (two isolated Node processes, real `FusionHub` server + `RpcClientPeer`
  client over `createMessageChannelPair`, only difference is a `global.gc()` between
  the compute call and the mutation):
  ```
  RESULT forceGc=false clientCachedValue=0 serverValueNow=42 clientGotInvalidate=true
  RESULT forceGc=true  clientCachedValue=0 serverValueNow=42 clientGotInvalidate=false
  ```
  Isolated micro-repro of the same root cause:
  `ComputedRegistry.size` goes `1 → 0` after one `global.gc()`, with zero
  invalidation callbacks ever fired.
- **Fix:** Give the inbound call a strong reference to the computed, mirroring
  .NET's `RpcInboundComputeCall.Computed`: store the `Computed` on the
  `RpcInboundCall` created in `RpcPeer._handleInbound` (thread it back through
  `RpcDispatchContext`), and release it only when the invalidation has been sent
  or the call is cancelled/unregistered. A registry-side strong-ref set keyed by
  "has a live remote subscriber" would work too.

---

### F2. Every accepted server connection leaks a peer + a 1 Hz timer for 3 minutes after disconnect — unauthenticated connection churn exhausts a TS-hosted RPC server

- **Severity:** HIGH
- **Confidence:** CONFIRMED (repro below)
- **Category:** dos / leak
- **Location:** `ts/packages/rpc/src/rpc-peer.ts:238` (`_maintainTimer` created in the
  ctor), `ts/packages/rpc/src/rpc-peer.ts:488` (cleared **only** in `close()`),
  `ts/packages/rpc/src/rpc-peer.ts:1314` (`_armCloseTimer`),
  `ts/packages/rpc/src/rpc-limits.ts:77` (`serverPeerCloseTimeoutMs = 180_000`),
  `ts/packages/fusion-rpc/src/fusion-hub.ts:153` / `:162` (`acceptConnection` /
  `acceptRpcConnection` mint a **fresh** `server://{uuid}` ref per connection)
- **What:** Two defects compound. (a) The per-peer maintenance `setInterval`
  (1 Hz by default) is created in the `RpcPeer` constructor and cleared only in
  `close()` — it keeps ticking for the whole 3-minute post-disconnect grace
  window, unlike .NET where maintenance loops are scoped to the connection.
  (b) `acceptConnection`/`acceptRpcConnection` generate a brand-new
  `server://{crypto.randomUUID()}` ref for every socket, so the "re-accept within
  the close window keeps this peer alive" optimisation that `serverPeerCloseTimeoutMs`
  exists for is **never** exercised — the 180 s retention is pure cost.
- **Why it matters / attack path:** An unauthenticated client opens a WebSocket,
  lets the handshake complete (or not — `accept()` arms the peer regardless), and
  hangs up. Repeat at rate *N*/s. Steady state: `180·N` live `RpcServerPeer`
  objects in `hub.peers`, each with its `RpcInboundCallTracker` (up to 1000
  retained completed calls), its trackers, and a 1 Hz interval that scans the
  outbound-call map. At a very modest 100 conn/s that is 18 000 peers and
  18 000 timer callbacks per second, with no authentication required at any point.
- **Evidence:**
  ```ts
  // rpc-peer.ts:238-243 — armed in the ctor, never disarmed on disconnect
  const checkPeriod = hub.limits.callTimeoutCheckPeriodMs;
  if (checkPeriod > 0 && Number.isFinite(checkPeriod)) {
      this._maintainTimer = setInterval(() => this._maintainOutboundCalls(), checkPeriod);
  ```
  ```ts
  // fusion-hub.ts:153-159 — new ref every time, so accept() never reuses a peer
  const ref = `server://${crypto.randomUUID()}`;
  ```
  Repro — 200 immediate connect/disconnect cycles against a real `FusionHub`:
  ```
  serverPeerCloseTimeoutMs = 180000  callTimeoutCheckPeriodMs = 1000
  after 200 connect/disconnect cycles: serverHub.peers.size = 200
                                       peers with a live 1Hz maintain timer = 200
  ```
- **Fix:** (1) Move the maintenance interval into the connected phase — start it on
  the transition into `Connected`, stop it in `_setConnectionState(Disconnected)`
  (alongside `_disarmKeepAliveWatchdog`). (2) Only arm the 180 s close timer for
  peers whose ref can actually be re-accepted (i.e. a client-supplied stable peer
  id); for the UUID-per-connection path, close the peer immediately on
  `conn.closed`. (3) Cap `hub.peers` size and/or the number of peers in the
  "disconnected, awaiting close" state.

---

### F3. No wire-argument arity validation — a hostile client bypasses the server-side Fusion compute cache completely by appending a junk argument

- **Severity:** HIGH
- **Confidence:** CONFIRMED (repro below)
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-service-host.ts:72` (`dispatch` — args are
  spread verbatim, never truncated to `entry.def.argCount`),
  `ts/packages/fusion-rpc/src/fusion-hub.ts:207` (`cleanArgs = args.slice(0, -1)`),
  `ts/packages/fusion/src/compute-function.ts:53` (`buildKey` folds **all** args into
  the cache key)
- **What:** `RpcServiceHost.dispatch` never checks the inbound argument count
  against the method definition. For compute methods the full (over-long) argument
  list flows into `ComputeFunction.buildKey`, so a request carrying one extra,
  attacker-chosen argument produces a **different cache key for the same logical
  call** while still invoking the real handler with the correct leading arguments.
- **Why it matters / attack path:** Fusion's compute cache is the primary
  protection between an RPC endpoint and the backing store. A client that appends
  `"nonce-<random>"` to every call for `Svc.getValue:2` forces a full handler
  execution (DB/query/IO) on **every single request**, and each execution also
  registers a new `Computed` plus an invalidation subscription. In .NET this is
  impossible: arguments are deserialized into a strongly-typed `ArgumentList` whose
  arity is fixed by `RpcMethodDef`, so a mismatch is a deserialization error.
- **Evidence:**
  ```ts
  // rpc-service-host.ts:86-89 — args come straight off the wire, unvalidated
  if (context !== undefined && entry.def.callTypeId !== 0)
      return await entry.fn.call(entry.receiver, ...args, context);
  return await entry.fn.call(entry.receiver, ...args);
  ```
  ```ts
  // compute-function.ts:53-59
  buildKey(instance, args) { let key = String(getInstanceId(instance)) + RS + this.id;
      for (const arg of args) key += RS + this.argToString(arg); return key; }
  ```
  Repro against a real `FusionHub` server (`Svc.getValue:2`, handler counts its
  own executions):
  ```
  20 identical calls        -> handler executions: 1
  20 calls + junk extra arg -> handler executions: 20  (ComputedRegistry.size = 21)
  ```
- **Fix:** In `RpcServiceHost.dispatch`, reject (or truncate) inbound argument
  lists whose length does not match `entry.def.argCount` before invoking the
  handler — `throw new Error('Bad argument count')` is the closest analogue of
  .NET's behaviour and keeps the `$sys.Error` response path intact. Truncating with
  `args.slice(0, def.argCount)` is the minimum acceptable fix, since it makes the
  compute key canonical again.

---

### F4. `$sys.Disconnect` tears down *shared* objects using *remote* object ids — a server closing one stream kills an unrelated outgoing stream on the client

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (repro below; verified against the C# handler)
- **Category:** logic
- **Location:** `ts/packages/rpc/src/rpc-system-call-handler.ts:219-246` (in
  particular `:236-242`), `ts/packages/rpc/src/rpc-shared-object-tracker.ts:4`,
  `ts/packages/rpc/src/rpc-remote-object-tracker.ts:4`
- **What:** `$sys.Disconnect` carries ids from the **sender's shared-object**
  namespace, i.e. the receiver's *remote*-object namespace. The TS handler looks
  each id up in `peer.remoteObjects` (correct) **and then also** in
  `peer.sharedObjects` (wrong namespace) and disconnects whatever it finds.
  Both counters start at 1, so numeric collisions are the norm, not the exception.
- **Why it matters / attack path:** No hostility required. A client that both
  consumes a server stream and pushes one of its own (audio/video/upload — the
  documented use case of `RpcStreamSender`) will have its **outgoing** stream
  aborted the moment the server tears down the incoming stream that happens to
  share the id. `RpcStreamSender.disconnect()` aborts the source `AbortSignal` and
  force-closes the iterator, so a recording/upload dies mid-flight. A hostile
  server can also deliberately kill all of a client's outgoing streams with a
  single `$sys.Disconnect [1..K]`.
- **Evidence:**
  ```ts
  // rpc-system-call-handler.ts:227-242
  const remoteObj = peer.remoteObjects.get(id);
  if (remoteObj && typeof remoteObj.disconnect === 'function') remoteObj.disconnect();
  // Also check shared objects — server may disconnect a client-to-server ...
  const sharedObj = peer.sharedObjects.get(id);
  if (sharedObj && typeof sharedObj.disconnect === 'function') sharedObj.disconnect();
  ```
  C# does only the first half — `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:139`:
  ```csharp
  public Task<RpcNoWait> Disconnect(long[] localIds) {
      ... peer.RemoteObjects.Disconnect(localIds); ...
  ```
  Repro:
  ```
  shared(outgoing) localId = 1  remote(incoming) localId = 1
  before: outgoing sender aborted = false
  after : outgoing sender aborted = true   (should be false)
  ```
- **Fix:** Delete the `sharedObjects` branch from the `$sys.Disconnect` handler.
  If the intent was to let a server terminate a client-to-server stream sender,
  that needs its own message (or the ids must be tagged with their namespace) —
  silently overloading the id space cannot be made correct.

---

### F5. `resolveStreamRefs` misclassifies ordinary strings as stream references — every regular RPC result is silently corrupted when it contains a `"a,1,2,3"`-shaped string

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (repro below)
- **Category:** logic / data corruption
- **Location:** `ts/packages/rpc/src/rpc-stream.ts:58-72` (`parseStreamRef`),
  `ts/packages/rpc/src/rpc-stream.ts:505-540` (`resolveStreamRefs`),
  `ts/packages/rpc/src/rpc-hub.ts:337` (applied to **every** non-stream result)
- **What:** `RpcHub._createClientMethod` runs `resolveStreamRefs` over the whole
  deserialized result of every regular RPC call, recursively replacing any string
  that `parseStreamRef` accepts with a live `RpcStream`. The acceptance test is a
  pure shape heuristic: 4–6 comma-separated parts where parts 1..3 are
  `parseInt`-able. Ordinary data matches it constantly.
- **Why it matters / attack path:** Any method returning `"10,20,30,40"` (a CSV
  row, a coordinate tuple, a comma-joined id list, a version string, a
  `"lat,lng,alt,acc"` payload) — at the top level or nested at any depth in an
  object/array — hands the caller an `RpcStream` object instead of the string. The
  caller's type annotation says `string`, so the corruption is invisible until
  something does string arithmetic on it. A hostile server can also use this to
  inject `RpcStream` objects into arbitrary fields of otherwise-typed results.
  `parseInt` tolerance widens the blast radius further (`"x,1,2,3xyz"` matches).
- **Evidence:**
  ```ts
  // rpc-stream.ts:60-72
  const parts = value.split(',');
  if (parts.length < 4 || parts.length > 6) return null;
  const localId = parseInt(parts[1], 10); ... if (isNaN(...)) return null;
  return { hostId: parts[0], localId, ackPeriod, ackAdvance, ... };
  ```
  Repro against a real client/server pair:
  ```
  getCsv() returned: RpcStream (!!) instead of the string "10,20,30,40"
  getRow().coords  : RpcStream (!!) instead of the string "1,2,3,4"
  ```
  Direct `parseStreamRef` probes: `"1,2,3,4"`, `"a,1,2,3"`, `"10,20,30,40"`,
  `"John,1,2,3,4,5"`, `"x,1,2,3xyz"`, `"2024,01,02,03"` **all** parse as refs.
- **Fix:** Do not infer stream-ness from value shape. Either (a) only run
  `resolveStreamRefs` on methods/positions whose `RpcMethodDef` declares a stream
  in the result (mirroring .NET's type-driven deserialization), or (b) require an
  unambiguous marker — a strict GUID `hostId` plus a distinct sentinel prefix
  (e.g. `"$stream:"`), and validate `hostId` with a GUID regex before converting.

---

### F6. Client-side outbound compute calls and their computeds are never released — the `WeakRef`/`FinalizationRegistry` eviction in `ComputedRegistry` is defeated

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (repro below)
- **Category:** leak
- **Location:** `ts/packages/fusion-rpc/src/fusion-hub.ts:316-336` (`bindComputeCall`,
  esp. `:325`), `ts/packages/fusion-rpc/src/rpc-outbound-compute-call.ts:6`
  (`removeOnOk = false`), `ts/packages/rpc/src/rpc-system-call-handler.ts:56-60`,
  `ts/packages/fusion/src/computed-registry.ts:5`
- **What:** An `RpcOutboundComputeCall` stays registered in `peer.outboundCalls`
  after `$sys.Ok` (`removeOnOk = false`), and `bindComputeCall` attaches
  `call.whenInvalidated.then(() => computed.invalidate())`. That promise reaction
  is retained by `call.whenInvalidated.promise`, which is retained by the call,
  which is retained by the peer's `Map` — so the reaction's closure **strongly
  pins the `Computed`** (and its result payload) from a GC root. The registry's
  `WeakRef` + `FinalizationRegistry` can therefore never fire. Nothing removes the
  call except a server-sent `$sys-c.Invalidate`, a local invalidation, or peer
  close.
- **Why it matters / attack path:** A long-lived SPA retains one live outbound
  call + one `Computed` + one full result payload for **every distinct
  (method, args) tuple it has ever queried**, whether or not anything still uses
  the value. Browsing N entities pins N results for the session. Each retained
  call also keeps the corresponding server-side inbound compute call alive, so the
  cost is paid on both ends. Combined with F1 (which prevents the invalidation
  that would release them) the set is effectively monotonic.
- **Evidence:**
  ```ts
  // fusion-hub.ts:325 — reaction stored on call.whenInvalidated, captures `computed`
  void call.whenInvalidated.then(() => computed.invalidate());
  ```
  Repro — 300 distinct compute calls through the real client proxy, all results
  discarded immediately, then three full `global.gc()` passes:
  ```
  client peer.outboundCalls.size = 300
  ComputedRegistry.size          = 300
  ```
- **Fix:** Break the strong edge from the call to the computed: hold the computed
  in a `WeakRef` inside the `whenInvalidated` reaction (`const ref = new WeakRef(computed);
  void call.whenInvalidated.then(() => ref.deref()?.invalidate())`), and register the
  computed with a `FinalizationRegistry` that removes the call from
  `peer.outboundCalls` and sends `$sys.Cancel` — this is the TS analogue of .NET's
  `~RemoteComputed() => Dispose()` → `call.CompleteAndUnregister(notifyCancelled: true)`.

---

### F7. Hostile-server payload can throw out of the message-receive path, killing the rest of the WebSocket frame (and crashing a Node client)

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (stack-overflow repro below; frame-loss path confirmed by reading)
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-peer.ts:520-537` (the `try` covers **only**
  deserialization; `:566-575` dispatch is outside it),
  `ts/packages/rpc/src/rpc-connection.ts:199-211` (text branch has **no** `try/catch`
  at all), `ts/packages/rpc/src/rpc-connection.ts:162-174` (binary `catch` sits
  *outside* the per-message loop),
  `ts/packages/rpc/src/rpc-stream.ts:505-540` (unbounded recursion),
  `ts/packages/rpc/src/rpc-message-channel-connection.ts:24-31` (no `try/catch`)
- **What:** `resolveStreamRefs` recurses without a depth limit over
  server-supplied data and is called from the `$sys.I` / `$sys.B` handlers
  (`rpc-system-call-handler.ts:148`, `:165`). `JSON.parse` in V8 happily parses
  200 000-deep nesting, but the subsequent recursive walk throws
  `RangeError: Maximum call stack size exceeded`. `RpcPeer._handleMessage` does not
  guard the dispatch, so the throw escapes into `EventHandlerSet.trigger`.
- **Why it matters / attack path:** A compromised/MITM'd server sends one ~400 KB
  `$sys.I` frame containing `[[[[…1…]]]]`.
  * **Text transport** (`json5np`, the default) and `MessageChannel` transport:
    nothing catches it — the exception escapes `ws.onmessage`. In a Node-hosted
    TS client (`ws` library, the load-test/SSR harnesses) that is an uncaught
    exception → process exit. In a browser it is an uncaught error and every
    remaining message in that frame is dropped.
  * **Binary transport:** the `catch` is outside the per-message loop, so **all**
    messages already decoded from the frame are discarded together with the bad
    one. Since .NET batches many RPC messages into a single WebSocket frame, this
    silently drops `$sys.Ok` replies — and per-call timeouts default to
    `undefined`/unbounded (`rpc-call-timeouts.ts:21`), so those calls hang forever.
- **Evidence:**
  ```
  JSON.parse OK at depth 200000 (wire bytes: 400001)
  resolveStreamRefs threw: RangeError - Maximum call stack size exceeded
  ```
  ```ts
  // rpc-peer.ts:532-537 — try ends here; everything below is unguarded
  } catch (e) { errorLog?.log(`...Failed to process inbound message:`, received, e); return; }
  ...
  if (method.startsWith('$sys')) { this.hub.systemCallHandler.handle(message, args, this); return; }
  ```
  ```ts
  // rpc-connection.ts:203-210 — text path, no try/catch
  const messages = splitFrame(data);
  for (const msg of messages) if (msg.length > 0) this.messageReceived.trigger({ kind: 'text', raw: msg });
  ```
- **Fix:** (1) Wrap the whole of `_handleMessage`'s dispatch (system-call handling
  and `_handleInbound`'s synchronous prologue) in the existing `try/catch`.
  (2) Move the `try/catch` in `RpcWebSocketConnection.onmessage` *inside* the
  per-message loop (both branches) so one bad message cannot discard its
  siblings; add one to the text and `MessageChannel` branches. (3) Give
  `resolveStreamRefs` a depth cap (e.g. 64) and bail out — matching the fact that
  no legitimate ActualLab payload nests that deep.

---

### F8. V5 binary parser: signed `argDataLen`, no size limits, `pos` can move backwards

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (offset math reproduced)
- **Category:** deserialization / dos
- **Location:** `ts/packages/rpc/src/rpc-serialization.ts:335` and `:350`
  (`deserializeBinaryMessage`), `:509` and `:521`
  (`deserializeCompactBinaryMessage`), `:386-396` (`splitBinaryFrame` loop),
  `:327-330` (`methodLen` unbounded), `:175-183` (`skipHeaders`)
- **What:** The argument-data length is read with `view.getInt32(pos, true)` —
  **signed** — and used directly as `pos = pos + argDataLen`. A negative value
  moves the read cursor backwards, so `bytesRead` can be far smaller than the true
  envelope size (reproduced: `bytesRead: 1` for a 7-byte envelope, `bytesRead: 4`
  for an 8-byte one). `splitBinaryFrame` only guards `bytesRead <= 0`, so the frame
  is re-parsed at overlapping offsets and yields more "messages" than it contains.
  Separately, the TS parser has **none** of the C# size guards
  (`RpcByteMessageSerializerV5.Read` uses `ReadLVarMemory(MaxMethodRefSize)` and
  `ReadL4Memory(MaxArgumentDataSize)`), and `view.getInt32`/`getUint32` throw a
  `RangeError` on a truncated tail — which, per F7, discards every message already
  decoded from that frame. `skipHeaders` reading past the end produces `NaN`
  offsets that silently poison `bytesRead`.
- **Why it matters / attack path:** A hostile peer (client→TS server, or
  server→TS client) sends one binary frame in which each envelope declares a
  negative `argDataLen`. The splitter emits many bogus messages per frame; each
  non-`$sys` one reaches `_handleInbound`, allocates an `RpcInboundCall`, fails
  `getMethodDef`, and emits a `$sys.Error` response. That is a CPU/allocation and
  outbound-bandwidth amplifier over an unauthenticated connection, with no
  message-count or frame-size ceiling anywhere in the TS stack.
- **Evidence:**
  ```ts
  // rpc-serialization.ts:335-350
  const argDataLen = view.getInt32(pos, true);   // signed
  pos += 4;
  const argEnd = pos + argDataLen;
  if (argDataLen > 0) { ... }
  pos = argEnd;                                   // can be < the header end
  ```
  ```ts
  // rpc-serialization.ts:392
  if (bytesRead <= 0) break; // defensive — avoid infinite loop on malformed data
  ```
  Reproduced with the exact parser logic: an envelope declaring `argDataLen = -6`
  yields `{ method: '', relatedId: 1, argDataLen: -6, bytesRead: 1 }`.
- **Fix:** Read the length as unsigned (`getUint32`) and validate it:
  `if (argDataLen < 0 || pos + argDataLen > data.length) throw`. Port the C#
  ceilings (`MaxArgumentDataSize`, `MaxMethodRefSize`, `MaxHeaderSize`) into
  `RpcLimits` and enforce them in both binary deserializers. In `splitBinaryFrame`,
  require `bytesRead >= minimumEnvelopeSize` and `Number.isFinite(bytesRead)`, and
  cap messages-per-frame.

---

### F9. Remote `RpcStream` receive buffer is unbounded — the `ackAdvance` window is advisory only

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (code); .NET parity noted
- **Category:** dos
- **Location:** `ts/packages/rpc/src/rpc-stream.ts:271` (`onItem`),
  `ts/packages/rpc/src/rpc-stream.ts:294` (`onBatch`),
  `ts/packages/rpc/src/rpc-stream.ts:139` (`_buffer: Denque<T>`)
- **What:** The receiving side accepts and buffers every in-order item the sender
  pushes. `ackAdvance` is enforced only by a cooperative sender
  (`rpc-stream-sender.ts:446`); the receiver never rejects or drops items that
  exceed the advertised window, and the `Denque` has no capacity.
- **Why it matters / attack path:** A compromised server answers a stream request
  and then floods `$sys.I` with strictly increasing indices, ignoring the client's
  acks. If the consumer is slower than the wire (or never iterates after the lazy
  start), the client's heap grows without bound until the tab/process dies. The
  parallel `_pendingSendTimes` array on the sender side
  (`rpc-stream-sender.ts:249`) is likewise unbounded while disconnected.
  *Note:* .NET's `RpcStream<T>` also uses `Channel.CreateUnbounded` on the remote
  side, so this is a shared design weakness rather than a port-only regression —
  but it is fully reachable from the P9 threat model (hostile server → client).
- **Evidence:**
  ```ts
  // rpc-stream.ts:265-273 — only out-of-order items are rejected; in-order is always buffered
  if (index < this._nextExpectedIndex) { this._maybeSendAck(...); return; }
  this._buffer.push(item);
  this._nextExpectedIndex = index + 1;
  ```
- **Fix:** Bound the receive buffer at `ackAdvance` (or an explicit
  `maxBufferSize`); on overflow, complete the stream with a protocol-violation
  error and send `$sys.AckEnd`, exactly as the gap path already does when
  `allowReconnect` is false.

---

### F10. `$sys.Reconnect` stale-generation check is bypassable by sending a non-numeric handshake index

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `ts/packages/rpc/src/rpc-system-call-handler.ts:270`
- **What:** The guard is
  `if (typeof handshakeIndex === 'number' && handshakeIndex !== peer.ownHandshakeIndex)`.
  A peer that sends the index as a **string** (or omits it) skips the check
  entirely and the reconnect reconciliation proceeds against a possibly stale
  connection generation. C# (`RpcSystemCalls.cs:59-62`) has no such escape — the
  parameter is a typed `int` and the comparison is unconditional.
- **Why it matters:** The `TooLateToReconnect` guard exists to stop a
  `$sys.Reconnect` that references an older handshake from re-driving result
  re-sends on the current connection. Bypassing it lets a remote peer force
  `resendResult()` on arbitrary known inbound call ids of the live generation.
  Impact is limited (duplicate results, which the outbound tracker tolerates), so
  LOW — but the check as written provides no guarantee at all.
- **Fix:** Reject non-numeric indices explicitly:
  `if (typeof handshakeIndex !== 'number' || handshakeIndex !== peer.ownHandshakeIndex) { …error… }`.

---

### F11. Dependency-graph edges are never pruned, and `ComputedState.dispose()` leaves its computed consistent and still linked

- **Severity:** LOW
- **Confidence:** CONFIRMED (code)
- **Category:** leak
- **Location:** `ts/packages/fusion/src/computed.ts:288-292` (`addDependency` inserts
  a `WeakRef` into `dependency._dependants`), `ts/packages/fusion/src/computed.ts:223-237`
  (entries removed only on invalidation), `ts/packages/fusion/src/computed-state.ts:57-63`
  (`dispose()` does not invalidate `this._computed`)
- **What:** A dependant removes itself from its dependencies' `_dependants` maps
  only when it is **invalidated**. `ComputedState.dispose()` aborts the update loop
  and flags `_isDisposed` but leaves the final computed `Consistent`, so its edges
  into every dependency survive as dead `WeakRef` entries. TS has no equivalent of
  .NET's `ComputedGraphPruner` (`PruneEdges` / `PruneDisposedInstances`), so nothing
  ever reclaims them.
- **Why it matters:** A long-lived remote computed accumulates one dead
  `Map`-entry + `WeakRef` per mounted-then-unmounted React component that ever
  depended on it. Memory cost is small, but `invalidate()` walks the whole map, so
  invalidation latency degrades monotonically over a long session.
- **Fix:** Invalidate the state's computed in `ComputedState.dispose()` (and
  `State._onDisposed()`), and add a periodic `pruneDependants()` that drops
  entries whose `WeakRef.deref()` is `undefined` — a direct port of
  `Computed.PruneDependants` (`src/ActualLab.Fusion/Computed.cs:535`).

---

### F12. Service-handler exception messages are forwarded verbatim to the remote peer

- **Severity:** LOW
- **Confidence:** CONFIRMED (code)
- **Category:** info-leak
- **Location:** `ts/packages/rpc/src/rpc-error.ts:20-24` (`toExceptionInfo`),
  `ts/packages/rpc/src/rpc-peer.ts:631-644` (the inbound-call `catch`),
  `ts/packages/rpc/src/rpc-service-host.ts:81` (`Method not found: ${wireMethod}`)
- **What:** Any exception thrown by a registered service implementation is
  serialized as `{ TypeRef: RemoteException, Message: "<name>: <message>" }` and
  sent to the peer with no filtering. Node error messages routinely embed absolute
  file paths, connection strings, SQL fragments, and hostnames.
- **Why it matters:** For a TS-hosted RPC server this is unauthenticated internal
  detail disclosure — an attacker probes methods with malformed arguments and
  harvests the resulting messages. (.NET has the same default, so this is a
  parity issue rather than a regression — hence LOW — but the TS side has no
  equivalent of a production error-shaping hook.)
- **Fix:** Add a hub-level `errorFilter` (default: replace the message with a
  generic string plus a correlation id in production builds), applied inside
  `RpcSystemCallSender.error` / `toExceptionInfo`.

---

## Notes on things checked and found *not* to be defects

Recorded so the next pass does not re-cover them:

- **Prototype pollution:** no reachable sink. `JSON.parse` creates `__proto__` as
  an own data property (no pollution), `@msgpack/msgpack@3.1.3` explicitly throws
  `"The key __proto__ is not allowed"` (`dist.esm/Decoder.mjs:543`), and
  `resolveStreamRefs` writes back through `Object.keys` + `obj[key] = …` on objects
  that already own `__proto__`, which is shadowed. `RpcServiceHost._methods`,
  `RpcMethodRegistry`, and `hub.peers` are all `Map`s, not plain objects.
- **XSS / code-execution sinks:** grepped the whole partition for `innerHTML`,
  `dangerouslySetInnerHTML`, `eval`, `new Function`, dynamic `import()`,
  `document.write` — zero hits. `fusion-react` renders nothing.
- **Session-id storage:** the TS packages never read or write session tokens.
  `localStorage`/IndexedDB use is confined to log-level persistence
  (`core/src/logging-init.ts`), and `applySnapshot` type-checks every entry.
  `sanitizeUrl` (`rpc-peer.ts:144`) redacts the `session` query parameter before
  the connect URL is logged.
- **Text wire-format injection:** `serializeMessage` uses `JSON.stringify` per
  argument, which escapes `\n` (`ENVELOPE_DELIMITER`), `\x1E` (`FRAME_DELIMITER`),
  and `\x1F` (`ARG_DELIMITER`) as `\uXXXX` — a hostile string argument cannot
  forge an extra message or argument. The same escaping means
  `ComputeFunction.buildKey`'s `\x1E` separator cannot be injected via string args.
- **`skipHeaders` layout** matches C# `WriteHeaders`
  (`RpcByteMessageSerializerV4.cs:156`): `L1Span(key)` + `LVarSpan(value)`.
  `RpcHandshake` msgpack array order `[PeerId, ApiVersionSet, HubId, ProtocolVersion, Index]`
  matches `src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:16-20`.
- **`$sys.Reconnect` decompression:** `IncreasingSeqCompressor.deserialize` cannot
  be used for unbounded allocation — each decoded id costs ≥ 1 input byte
  (≈ 8× amplification, bounded by frame size).
- **`RpcInboundCallTracker`** growth is bounded by
  `completedInboundCallsLimit` (1000) for completed calls; unknown methods complete
  immediately via the error path, so they are covered by the cap.
- **`AsyncLock`, `PromiseSource`, `RingBuffer`, `RetryDelaySeq`/`RetryDelayer`,
  `awaitWithCleanup`, `throttle`/`debounce`, `abortPromise`** were read line by
  line; lock hand-off, abort-listener removal, cleanup idempotence, and ring-buffer
  index math are correct. Reconnect backoff is exponential with jitter and a
  premature-disconnect guard — no unbounded hot reconnect loop.
- **`useComputedState` / `useMutableState`** correctly create and dispose state
  inside `subscribe`, use `useSyncExternalStore`, and guard the async
  `whenUpdated` loop with a `cancelled` flag — no stale-state or listener leak.

---

## Areas examined

Read in full:

- `ts/packages/rpc/src/` — **all 33 source files**, including `rpc-peer.ts`,
  `rpc-connection.ts`, `rpc-serialization.ts`, `rpc-serialization-format.ts`,
  `rpc-call-tracker.ts`, `rpc-system-call-handler.ts`, `rpc-system-call-sender.ts`,
  `rpc-stream.ts`, `rpc-stream-sender.ts`, `rpc-hub.ts`, `rpc-service-host.ts`,
  `rpc-service-def.ts`, `rpc-client.ts`, `rpc-remote-object-tracker.ts`,
  `rpc-shared-object-tracker.ts`, `rpc-method-registry.ts`,
  `increasing-seq-compressor.ts`, `base64.ts`, `msgpack-map-patch.ts`,
  `rpc-limits.ts`, `rpc-message.ts`, `rpc-decorators.ts`, `rpc-error.ts`,
  `rpc-ref-builder.ts`, `rpc-message-channel-connection.ts`,
  `rpc-peer-state{,-monitor}.ts`, `rpc-client-peer-reconnect-delayer.ts`,
  `rpc-call-{stage,timeouts}.ts`, `rpc-object.ts`, `rpc-server.ts`, `logging.ts`.
- `ts/packages/core/src/` — **all 27 source files** (async/locking/promise
  primitives, `logging.ts`, `logging-init.ts`, `retry*`, `throttle.ts`,
  `ring-buffer.ts`, `result.ts`, `serialize.ts`, `async-context.ts`, `events.ts`,
  `decorators.ts`, `polyfills.ts`, …).
- `ts/packages/fusion/src/` — **all 14 source files** (`computed.ts`,
  `compute-function.ts`, `compute-context.ts`, `computed-registry.ts`,
  `compute-method.ts`, `computed-input.ts`, `computed-options.ts`, `state.ts`,
  `computed-state.ts`, `mutable-state.ts`, `update-delayer.ts`,
  `ui-update-delayer.ts`, `ui-action-tracker.ts`, `logging.ts`).
- `ts/packages/fusion-rpc/src/` — all 3 source files.
- `ts/packages/fusion-react/src/` — all 3 source files.
- `ts/packages/*/package.json`, `ts/packages/*/src/index.ts`.

Reference material (outside the partition, read for parity):
`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs`,
`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs`,
`src/ActualLab.Rpc/Serialization/RpcByteMessageSerializerV5.cs`,
`src/ActualLab.Rpc/Serialization/RpcByteMessageSerializerV4.cs` (`WriteHeaders`),
`src/ActualLab.Rpc/RpcStream.cs`,
`src/ActualLab.Fusion/Client/RemoteComputed.cs`,
`src/ActualLab.Fusion/Client/RemoteComputedExt.cs`,
`src/ActualLab.Fusion/Client/Internal/RpcOutboundComputeCall.cs`,
`src/ActualLab.Fusion/ComputedRegistry.cs`,
`src/ActualLab.Fusion/Internal/ComputedGraphPruner.cs`,
`ts/node_modules/@msgpack/msgpack/dist.esm/Decoder.mjs` (prototype-pollution guard).

Repros executed from the scratchpad against the prebuilt `dist` bundles:
server-computed GC eligibility; end-to-end invalidation loss with/without GC;
server-peer + timer accumulation over 200 connect/disconnect cycles; compute-cache
bypass via an extra wire argument; `$sys.Disconnect` cross-namespace teardown;
stream-ref misdetection on ordinary strings; deep-nesting stack overflow in
`resolveStreamRefs`; V5 binary offset math with a negative `argDataLen`.

## Areas NOT examined

- **`ts/packages/*/tests/**`** — read only two files (`fusion-rpc/tests/e2e-rpc.test.ts`
  for wiring, `rpc/tests/rpc-binary-serialization.test.ts` for
  `readPolymorphismMarker` usage). Test-only code is out of scope per the brief;
  I did not audit the mocks (`mock-ws.ts`, `rpc-test-connection.ts`) for defects.
- **`ts/e2e/`** — the partition definition says "if relevant"; the directory does
  not exist in this checkout, so nothing was reviewed there.
- **`ts/*.ts` root config files** — `tsup.config.ts` / `vitest` config were not
  reviewed; they are build configuration with no runtime attack surface in the
  shipped packages.
- **`ts/node_modules` and `ts/packages/*/dist`** — excluded by the task, except
  for (a) reading `@msgpack/msgpack`'s decoder to confirm the `__proto__` guard,
  and (b) *executing* the prebuilt `dist` bundles from the scratchpad to produce
  repros. I did **not** audit third-party dependencies (`@msgpack/msgpack`,
  `denque`, `react`) for their own vulnerabilities.
- **`rpc-xxhash3.ts` numerics** — I read the file and the registry that consumes
  it, but did not differentially test the XXH3-64 implementation against .NET's
  `RpcMethodRef.ComputeHashCode`. A wrong hash would break the `msgpack6c`
  compact format outright (loud failure, not a silent security issue), and
  `RpcMethodRegistry.register` skips on collision, so I deprioritised it. Worth a
  cross-language vector test if `msgpack6c` is used in production.
- **Browser-specific behaviours** that cannot be exercised in Node: `Blob`
  fallback path in `RpcWebSocketConnection.onmessage`, real `WebSocket` close-code
  semantics (`RPC_CLOSE_CODE_UNSUPPORTED_FORMAT` is `4001` in
  `rpc-peer.ts:115` while its own doc comment and `unsupportedFormat`'s JSDoc say
  "close code 4010" — a doc/constant mismatch that I could not resolve against a
  live .NET server, so it is not reported as a finding).
- **Load/soak behaviour** of `RpcStreamSender._run` under adversarial ack
  sequences: I traced that a hostile `$sys.Ack` with a huge or non-numeric
  `nextIndex` drives `_nextIndex` past the buffer and stalls the pump (drains the
  source without sending), but did not build a repro; it is a stream-availability
  issue only for the peer that owns the stream, so I left it out rather than
  report it unverified.
