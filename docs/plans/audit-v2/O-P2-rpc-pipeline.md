# P2 — RPC call pipeline, routing, streams & access control

Reviewer partition: `src/ActualLab.Rpc/Infrastructure/` (non-peer parts),
`Middlewares/`, `Internal/`, `Caching/`, `Attributes/`, `Diagnostics/`, plus
`RpcMethodDef` / `RpcServiceDef` / `RpcMethodResolver` / `RpcServiceRegistry`
resolution and `RpcStream*`.

---

### F1. Unbounded, permanently-retained method-resolver cache keyed by the remote peer's handshake `VersionSet`

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:115`
  (also `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:15`,
  `src/ActualLab.Rpc/RpcPeer.cs:614`, `src/ActualLab.Rpc/RpcPeer.cs:563`,
  `src/ActualLab.Rpc/RpcPeer.cs:350`)
- **What:** `RpcServiceRegistry` (a process-wide singleton) caches a
  `RpcMethodResolver` per distinct `VersionSet` in a `ConcurrentDictionary`
  that is never pruned or bounded. The `VersionSet` used as the key comes
  straight off the wire — it is `RpcHandshake.RemoteApiVersionSet`, supplied by
  the remote peer before any application-level authentication.
- **Why it matters / attack path:**
  1. Attacker opens a WebSocket to `/rpc/ws` (anonymous; the session is a
     per-call argument in Fusion, so the upgrade itself is unauthenticated).
  2. It sends its handshake with `RemoteApiVersionSet` set to a unique value,
     e.g. `"s0=1.0.0.<n>"` (the `VersionSet` wire form is a single string that
     is parsed into a `Dictionary<string, Version>` — see
     `src/ActualLab.Core/Collections/VersionSet.cs:117`).
  3. `RpcPeer.OnRun` accepts the handshake, and `SetConnectionState` calls
     `GetServerMethodResolver(newState.Handshake)` →
     `RpcServiceRegistry.GetServerMethodResolver(versions)` →
     `_legacyServerMethodResolvers.GetOrAdd(versions, …)`.
  4. Each distinct `VersionSet` allocates and permanently retains a new
     `RpcMethodResolver` **plus the attacker-sized `VersionSet` itself**. The
     `VersionSet` dictionary size is bounded only by the argument-data limit
     (`RpcByteMessageSerializer.Defaults.MaxArgumentDataSize` = 130 MB, see
     `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`), so one
     handshake can retain tens of MB, and a few thousand cheap reconnects can
     exhaust the heap. The entries survive peer disposal because the registry
     is a singleton.
  5. Building each resolver also walks every service × method
     (`RpcMethodResolver` ctor, `src/ActualLab.Rpc/Configuration/RpcMethodResolver.cs:95-143`),
     adding a CPU cost per connection.
- **Evidence:**
  ```csharp
  // RpcServiceRegistry.cs:15
  private readonly ConcurrentDictionary<VersionSet, RpcMethodResolver> _legacyServerMethodResolvers = new();
  // RpcServiceRegistry.cs:110-119
  public RpcMethodResolver GetServerMethodResolver(VersionSet? versions) {
      if (versions is null) return ServerMethodResolver;
      return _legacyServerMethodResolvers.GetOrAdd(versions,
          static (versions, self) => new RpcMethodResolver(self, versions, self.ServerMethodResolver, self.Log), this);
  }
  ```
  ```csharp
  // RpcPeer.cs:350-351 — guarantees the argument is never null, so the
  // `versions is null` fast path above is dead for real connections
  if (handshake.RemoteApiVersionSet is null)
      handshake = handshake with { RemoteApiVersionSet = new() };
  // RpcPeer.cs:614
  _serverMethodResolver = GetServerMethodResolver(newState.Handshake);
  ```
- **Fix:** Cap and validate the handshake `VersionSet` before it reaches the
  cache: reject sets with more than a handful of scopes and with scope names
  outside a known allow-list (`RpcDefaults`' registered scopes). Then bound the
  cache itself — e.g. an LRU with a hard entry cap (a legitimate deployment has
  a tiny number of distinct client version sets), or normalize the key by
  dropping unknown scopes so all garbage collapses to one entry. Also add a
  much smaller dedicated size limit for the handshake message.

---

### F2. Stream-batch arguments are deserialized with the expected type widened to `object`, disabling the polymorphic type allow-check

- **Severity:** HIGH
- **Confidence:** CONFIRMED (missing check) / PLAUSIBLE (escalation to RCE, depends on the negotiated format and the app's dependency closure)
- **Category:** deserialization
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:235`
  (guard being bypassed: `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:88`
  and `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:67`)
- **What:** For `$sys.B` (stream batch) with a polymorphic item type,
  `RpcSystemCalls.IsValidCall` deliberately replaces the expected argument type
  `T[]` with `object` so that the argument serializer takes the polymorphic
  path. The polymorphic readers only accept a wire-supplied type if
  `expectedType.IsAssignableFrom(itemType)` — and `typeof(object)` is
  assignable from *everything*. The batch payload therefore has **no type
  restriction at all**, unlike every other RPC argument.
- **Why it matters / attack path:**
  1. Establish an `RpcStream<T>` across the connection where `T` is an
     interface/abstract type or `object` (`RpcArgumentSerializer.IsPolymorphic`,
     `src/ActualLab.Rpc/Serialization/RpcArgumentSerializer.cs:40`). Both
     directions work: a hostile *client* can pass such a stream as a method
     argument that the server enumerates; a hostile *server* can return one to
     a client.
  2. Send `$sys.B(relatedId = <stream local id>, index, items)` with a type
     token naming **any** type the process can resolve.
  3. `RpcInboundCall.DeserializeArguments`
     (`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:234-241`) calls
     `IsValidCall`, which sets `arguments = ArgumentList.New<long, object>(...)`
     and `needsArgumentPolymorphism = true`.
  4. `RpcByteArgumentSerializerV4.PolyDeserialize` /
     `RpcTextArgumentSerializerV4.Deserialize` then call
     `ReadDerivedItemType(ref data, typeof(object))`, which resolves the
     attacker's type name via `TypeRef.Resolve()` (`Type.GetType`) and passes
     the assignability check unconditionally, then hands the payload to the
     base serializer with that type.
  5. Only *after* the object has been constructed does
     `RpcStream<T>.OnBatch` do `(T[])items!`
     (`src/ActualLab.Rpc/RpcStream.cs:304`) and throw — too late.

     With MemoryPack/MessagePack the practical effect is arbitrary type
     resolution plus formatter construction (usually a throw, but it is a
     `Type.GetType` / assembly-probe primitive driven by a 64 KB attacker
     string). With the `njson5` / `njson5np` formats — which a client can select
     unilaterally, see **OP1** — it is Newtonsoft.Json deserialization into an
     arbitrary type, i.e. classic gadget territory.
- **Evidence:**
  ```csharp
  // RpcSystemCalls.cs:229-237
  context.RelatedObject = stream;
  needsArgumentPolymorphism = RpcArgumentSerializer.IsPolymorphic(stream.ItemType);
  arguments = needsArgumentPolymorphism
      // We need to force polymorphic deserialization of the second argument ...
      // TItem[] is non-abstract & non-object, so RpcArgumentSerializer
      // won't use polymorphic deserialization ... unless we "reset" its type to object.
      ? ArgumentList.New<long, object>(0L, null!)
      : stream.CreateStreamBatchArguments();
  ```
  ```csharp
  // ByteTypeSerializer.cs:84-93
  public static Type ReadDerivedItemType(ref ReadOnlyMemory<byte> data, Type expectedType) {
      var itemType = ReadItemType(ref data);
      if (itemType is null) return expectedType;
      if (expectedType.IsAssignableFrom(itemType)) return itemType; // always true for object
      ...
  ```
- **Fix:** Don't widen the type. Either add an explicit "force polymorphism for
  slot N against expected type `T[]`" signal to `RpcArgumentSerializer`
  (a per-slot expected-type override), or keep
  `stream.CreateStreamBatchArguments()` (`ArgumentList<long, T[]>`) and set the
  polymorphism flag separately so `ReadDerivedItemType` is invoked with
  `typeof(T[])`. As defence in depth, re-validate the runtime type in
  `RpcStream<T>.OnBatch` *before* the payload body is deserialized, not after.

---

### F3. Remote-side stream receive path has no flow control: a hostile peer can push unbounded items into an unbounded channel

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/RpcStream.cs:194`,
  `src/ActualLab.Rpc/RpcStream.cs:285`, `src/ActualLab.Rpc/RpcStream.cs:307`
- **What:** `RpcStream<T>` (remote/consumer side) buffers incoming items in
  `Channel.CreateUnbounded<T>()` and accepts every in-order item without ever
  checking how far the sender has run ahead of the last acknowledged index. The
  entire `AckAdvance`/`AckPeriod` flow-control protocol is enforced **only on
  the sending side** (`RpcSharedStream<T>.OnRun`,
  `src/ActualLab.Rpc/Infrastructure/RpcSharedStream.cs:221`), i.e. it depends
  on the remote peer being well-behaved.
- **Why it matters / attack path:**
  - *Client → server:* an app method that accepts an `RpcStream<T>` argument
    (a supported, documented framework feature) makes the server the consumer.
    A malicious client acquires the stream id from its own serialized stream and
    then floods `$sys.I` / `$sys.B` at line rate, ignoring the acks. Every item
    lands in the unbounded channel; if the server's consumer is slower than the
    network (it almost always is), the process OOMs.
  - *Server → client:* symmetric — a compromised/MITM'd server OOMs the .NET or
    Blazor client, which is explicitly in scope per the threat model.
  - Because the channel is unbounded, `TryWrite` never fails and there is no
    natural back-pressure point at which the framework could notice.
- **Evidence:**
  ```csharp
  // RpcStream.cs:194
  _remoteChannel = Channel.CreateUnbounded<T>(RemoteChannelOptions);
  // RpcStream.cs:270-287  (OnItem)
  if (index > _nextIndex) { SendResetFromLock(_nextIndex); return; }
  _remoteChannel.Writer.TryWrite((T)item!); // Must always succeed for unbounded channel
  _nextIndex++;
  // RpcStream.cs:304-309  (OnBatch — same, per item, with no cap on the array length)
  ```
  There is no comparison of `_nextIndex` against the last index the consumer
  acknowledged anywhere in `RpcStream<T>`.
- **Fix:** Enforce the contract on the receiving side. Track the last acked
  index and drop/close the stream (send `AckEnd` + `RpcStreamInvalidPosition`)
  when `_nextIndex - lastAckedIndex > AckAdvance` (with a small tolerance).
  Alternatively make `_remoteChannel` a bounded channel of capacity
  `max(BufferSize, AckAdvance) + BatchSize` with `FullMode = DropWrite` plus a
  protocol violation error. Also cap `T[]` length in `OnBatch` at
  `RpcStream.MaxBatchSize`.

---

### F4. `$sys.Reconnect` accepts an attacker-shaped `Dictionary<int, byte[]>` — hash-flooding / algorithmic-complexity DoS, pre-auth

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (the O(n²) behaviour depends on the concrete
  dictionary formatter pre-sizing the `Dictionary` with the wire-declared
  count; I did not run it)
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:51`
  (declaration at `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:15`)
- **What:** `IRpcSystemCalls.Reconnect(int handshakeIndex, Dictionary<int, byte[]> completedStagesData, …)`
  is a system method callable by **any** remote peer (system services are not
  `IBackendService`, so the backend gate in `RpcInboundContext` does not apply).
  Its second parameter is a `Dictionary<int, …>` deserialized from the wire.
  `Dictionary<int, V>` uses the identity hash of the key and — unlike the
  `string` case — .NET never falls back to a randomized/collision-resistant
  comparer. Bucket counts come from the deterministic `HashHelpers` prime table,
  so an attacker who controls the entry count also knows the bucket count and
  can pick keys that all collide.
- **Why it matters / attack path:** send a single `$sys.Reconnect` message with
  ~10⁵ colliding `int` keys (a few hundred KB with MessagePack small-int
  encoding). Insertion degenerates to O(n²) — ~5×10⁹ comparisons — burning a
  core for minutes per message, and the messages can be pipelined. Note that
  the `ownHandshake.Index != handshakeIndex` guard at
  `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:61` does **not** help: the
  dictionary is fully materialised during argument deserialization, before the
  method body runs.
- **Evidence:**
  ```csharp
  // RpcSystemCalls.cs:51-56
  public Task<byte[]> Reconnect(
      int handshakeIndex,
      Dictionary<int, byte[]> completedStagesData,
      CancellationToken cancellationToken)
  {
      var context = RpcInboundContext.GetCurrent();
      ...
      if (ownHandshake.Index != handshakeIndex)   // checked only after deserialization
          throw Errors.TooLateToReconnect(...);
  ```
- **Fix:** Change the wire type to something with an inherent bound — the set of
  valid completed stages is tiny and known (`RpcCallStage`), so a small
  fixed-size array or `(int Stage, byte[] Data)[]` with a length cap is the
  right shape. At minimum, reject payloads with more than N entries during
  deserialization. The same concern applies to any `Dictionary<int, …>` /
  `Dictionary<long, …>` used as an RPC argument type anywhere in the codebase.

---

### F5. `$sys.Reconnect` lets a peer re-trigger `ProcessStage1Plus` on the same in-flight inbound call an unlimited number of times

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos / logic
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:146`
  (caller: `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:74-82`)
- **What:** `RpcInboundCall.TryReprocess(0, …)` starts a **new**
  `ProcessStage1Plus` continuation on an already-running call, overwriting
  `WhenProcessed`. Nothing detects that the call is already being processed and
  nothing limits how often this can happen — the only guards are "the call is
  still registered" and "`ResultTask` is not null".
- **Why it matters / attack path:**
  1. Attacker issues one long-running inbound call, id `1` (any method that
     awaits — a DB query, a compute method, `Task.Delay`, …).
  2. Attacker then sends `$sys.Reconnect(<server handshake index>, {0: seq([1])})`
     in a tight loop. The handshake index is known: the server sends its own
     handshake to the client at connect time.
  3. Each call runs `ProcessStage1Plus` → `CompleteAsync()` → `await
     ResultTask.SilentAwait(false)`, appending yet another async state machine
     and continuation to the *same* `Task`'s continuation list. These are all
     retained until the original call completes. A ~40-byte message buys a
     couple of hundred retained bytes plus a `Task` allocation — a cheap
     amplifying memory sink, and every one of them will also call
     `SendResult()` when the call finishes, emitting N duplicate `$sys.Ok`
     responses for one request.
  4. The duplicate `Complete()` calls also mean `SendResult()` runs even though
     `UnregisterFromLock()` returned `false` — see
     `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:201-217`.
  The same path is reachable without `Reconnect` by simply re-sending a call
  message with a duplicate `RelatedId`
  (`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:114-120`).
- **Evidence:**
  ```csharp
  // RpcInboundCall.cs:146-158
  public virtual Task? TryReprocess(int completedStage, CancellationToken cancellationToken) {
      lock (Lock) {
          var existingCall = Context.Peer.InboundCalls.Get(Id);
          if (existingCall != this || ResultTask is null) return null;
          return WhenProcessed = completedStage switch {
              >= 1 => Task.CompletedTask,
              _ => ProcessStage1Plus(cancellationToken)   // no "already processing" check
          };
      }
  }
  ```
- **Fix:** Make `TryReprocess` idempotent: if `WhenProcessed` is already set and
  not completed, return it instead of starting a second pipeline. Additionally
  rate-limit / de-duplicate `Reconnect` per connection (a legitimate client
  issues it once per reconnect), and make `Complete()` a no-op when
  `UnregisterFromLock()` returns `false`.

---

### F6. No limit on concurrent inbound calls, on the inbound-call table, or on per-peer shared objects/streams

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:59`,
  `src/ActualLab.Rpc/RpcPeer.cs:420`,
  `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:214`,
  `src/ActualLab.Rpc/Configuration/RpcLimits.cs:9`
- **What:** `RpcPeer`'s read loop dispatches every inbound message with
  `_ = ProcessMessage(...)` and never awaits or throttles. Inbound calls are
  registered in a `ConcurrentDictionary<long, RpcInboundCall>` keyed by the
  **attacker-chosen** `RelatedId`, with no cap. Each registered call also
  allocates a linked `CancellationTokenSource` chained to the peer's
  `peerChangedCts`. `RpcSharedObjectTracker.Register` is likewise unbounded, and
  shared streams are only reclaimed after `ObjectReleaseTimeout` (125 s) of
  silence. `RpcLimits` contains only time-based limits — there is no
  `MaxInboundCalls`, `MaxSharedObjects`, or concurrency limit anywhere.
- **Why it matters / attack path:** a single unauthenticated WebSocket can
  issue millions of concurrent calls to any slow server method (or open
  thousands of server-side streams, each with a background worker, a
  `RingBuffer<Result<T>>` of `AckAdvance+1` entries and an unbounded ack
  channel), and hold them all open. Memory and CTS-registration growth is
  linear in the attacker's request rate with no back-pressure point.
- **Evidence:**
  ```csharp
  // RpcPeer.cs:419-420
  while (await reader.MoveNextAsync().ConfigureAwait(false))
      _ = ProcessMessage(reader.Current, peerChangedToken, readerToken);
  // RpcCallTrackers.cs:59-67
  public RpcInboundCall GetOrRegister(RpcInboundCall call) {
      if (call.NoWait || Calls.TryAdd(call.Id, call)) return call;
      return Calls.GetOrAdd(call.Id, static (_, call1) => call1, call);
  }
  // RpcObjectTrackers.cs:221-222
  if (!_objects.TryAdd(id.LocalId, obj)) throw Internal.Errors.RpcObjectIsAlreadyUsed();
  ```
- **Fix:** Add `RpcLimits.MaxInboundCalls` / `MaxSharedObjects` /
  `MaxRemoteObjects` (per peer) and reject with a proper error message (or drop
  the connection) once exceeded. Optionally gate the read loop on a
  `SemaphoreSlim` so the transport applies TCP back-pressure instead of
  buffering unbounded work.

---

### F7. Server-side exception type and message are forwarded verbatim to the remote peer with no sanitization hook

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:132`,
  `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:146`,
  `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:312`
  (payload shape: `src/ActualLab.Core/Serialization/ExceptionInfo.cs:44`)
- **What:** Whenever an inbound call fails, `DefaultSendResult` wraps
  `resultTask.Exception.GetBaseException()` and `RpcSystemCallSender.Error`
  serializes it as `ExceptionInfo` — the assembly-qualified exception type name
  plus the raw `Exception.Message`. There is no filter, allow-list, or
  transformation hook anywhere on this path.
- **Why it matters / attack path:** any unhandled server-side exception is
  returned to whichever peer made the call, including anonymous clients.
  In practice this leaks: internal assembly and namespace names (useful for
  fingerprinting and for picking deserialization gadgets, cf. F2/OP1), EF Core /
  Npgsql messages that frequently embed table, column, constraint and sometimes
  connection details, `FileNotFoundException`/`DirectoryNotFoundException`
  messages with absolute server paths, and `ArgumentException` messages
  containing internal parameter values. The middleware pipeline
  (`src/ActualLab.Rpc/Middlewares/`) has no error-mapping middleware to
  intercept it, and the only knob is app-level try/catch in every method.
- **Evidence:**
  ```csharp
  // RpcInboundCall.cs:310-321
  else if (resultTask.Exception is { } error)
      result = new Result<TResult>(default!, error.GetBaseException());
  ...
  systemCallSender.Complete(peer, this, result, MethodDef.HasPolymorphicResult, ResultHeaders);
  // RpcSystemCallSender.cs:131-133
  var context = new RpcOutboundContext(peer, callId, headers);
  var call = context.PrepareCallForSendNoWait(ErrorMethodDef, ArgumentList.New(error.ToExceptionInfo()))!;
  // ExceptionInfo.cs:44-51 — TypeRef (assembly-qualified) + exception.Message
  ```
- **Fix:** Add an `RpcInboundCallOptions.ErrorTransformer`
  (`Func<Exception, RpcPeer, ExceptionInfo>`) applied in
  `RpcSystemCallSender.Error`, defaulting on non-backend peers to a
  "pass through only exceptions marked as client-safe (e.g. `RpcException`,
  `ValidationException`, `OperationCanceledException`), otherwise return an
  opaque `RemoteException` with a correlation id" policy. Log the full detail
  server-side only.

---

### F8. `RpcMethodDef.IsBackend` for command methods is derived from the *declared* parameter type, while the actually dispatched command is the *runtime* type

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (framework-level gap; exploitability requires the
  app to declare an RPC command method whose parameter type is abstract/
  interface and has `IBackendCommand` descendants)
- **Category:** auth-bypass
- **Location:** `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:103-105`,
  `src/ActualLab.Rpc/Configuration/RpcMethodDef.Static.cs:28-42`,
  `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`
- **What:** The only access-control decision in the inbound pipeline is
  `if (MethodDef.IsBackend && !Peer.Ref.IsBackend) → NotFound`. For a
  command-shaped method (`Task<T> M(TCommand cmd, CancellationToken ct)`),
  `IsBackend` is computed once at registration time from
  `IsCommandType(parameterTypes[0], out isBackend)` — i.e. from the *static*
  parameter type. But if that parameter type is abstract or an interface,
  `HasPolymorphicArguments` is true and the client chooses the concrete command
  type on the wire; the polymorphic reader only enforces
  `expectedType.IsAssignableFrom(itemType)`. A concrete `IBackendCommand`
  subtype of a non-backend declared type therefore passes the gate and is
  dispatched by CommandR on its runtime type.
- **Why it matters / attack path:** an app exposes
  `Task<Unit> Run(ISomeCommand command, CancellationToken ct)` (or any abstract
  command base) on a client-facing service; a client sends
  `SomeBackendCommand : ISomeCommand, IBackendCommand` as the polymorphic
  argument. `RpcInboundContext` sees `MethodDef.IsBackend == false` and lets it
  through; CommandR then routes it to the backend handler.
- **Evidence:**
  ```csharp
  // RpcMethodDef.cs:103-105
  Kind = GetMethodKind(out var isBackend);
  IsBackend = service.IsBackend || isBackend;
  // RpcMethodDef.cs:173-177
  if (parameterTypes.Length == 2 && parameterTypes[1] == typeof(CancellationToken))
      return IsCommandType(parameterTypes[0], out isBackend) ? RpcMethodKind.Command : RpcMethodKind.Query;
  // RpcInboundContext.cs:47 — the only backend gate, evaluated before args are read
  if (MethodDef.IsBackend && !Peer.Ref.IsBackend) { ... NotFound ... }
  ```
- **Fix:** Re-check backend-ness after argument deserialization, in a
  middleware: for `RpcMethodKind.Command`, if
  `IsCommandType(args.GetUntyped(0)?.GetType(), out var isBackendCommand)` and
  `isBackendCommand && !Peer.Ref.IsBackend`, reject with the same
  `EndpointNotFound` error used by the static gate. Alternatively refuse to
  register a non-backend command method whose declared command type is
  polymorphic and has backend descendants.

---

### F9. `$sys.KeepAlive` amplifies an unbounded attacker-supplied `long[]` into several large allocations and an equally large reply

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:259-276`
  (entry point `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:133`)
- **What:** `RpcSharedObjectTracker.KeepAlive(long[] localIds)` sizes a pooled
  buffer from `localIds.Length` (wire-controlled), collects every *unknown* id
  into it, and then sends the whole collection straight back as a
  `$sys.Disconnect(long[])` message. There is no length cap and no check that
  the ids could plausibly belong to this peer.
- **Why it matters / attack path:** a peer sends `$sys.KeepAlive` with ids
  `1..N` (none of which exist). With `MaxArgumentDataSize` = 130 MB this is up
  to ~16 M longs. Server-side the single message produces (a) the deserialized
  `long[]`, (b) a pooled `long[]` of the same length, (c) `buffer.ToArray()`,
  and (d) an outbound message that *retains* (c) in the transport's **unbounded**
  write channel (`src/ActualLab.Rpc/Infrastructure/RpcFrameBasedTransport.cs:73`).
  An attacker that stops reading its socket makes (d) permanent for the life of
  the connection, so a handful of pipelined requests exhaust the heap.
  `$sys.Disconnect` (`RpcSystemCalls.cs:141`) has the same unbounded-array shape
  minus the echo.
- **Evidence:**
  ```csharp
  // RpcObjectTrackers.cs:259-276
  public void KeepAlive(long[] localIds) {
      LastKeepAliveAt = Moment.Now;
      var buffer = new RefArrayPoolBuffer<long>(ArrayPools.SharedInt64Pool, localIds.Length, mustClear: false);
      try {
          foreach (var id in localIds) {
              if (Get(id) is { } obj) obj.KeepAlive();
              else buffer.Add(id);
          }
          if (buffer.Count > 0)
              Peer.Hub.SystemCallSender.Disconnect(Peer, buffer.ToArray());
      }
      ...
  ```
- **Fix:** Cap `localIds.Length` against the peer's actual `SharedObjects.Count`
  (plus slack) and drop the connection on violation; cap the echoed disconnect
  list at a small constant (the legitimate case is "a handful of stale ids").
  Independently, bound the transport write channel so a non-reading peer cannot
  make the server buffer without limit.

---

### F10. `RpcCallTypes.Get` indexes an 8-element array with a wire-supplied byte

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Configuration/RpcCallTypes.cs:32`
  (reached from `src/ActualLab.Rpc/Infrastructure/RpcInboundInvalidCallTypeCall.cs:17-19`
  via `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:58-66`)
- **What:** `RpcCallTypes.Registry` has 8 slots. The byte serializers pack the
  call type into 3 bits so they are safe, but the JSON envelope carries
  `CallType` as a full unvalidated `byte`
  (`src/ActualLab.Rpc/Serialization/Internal/JsonRpcMessage.cs:12`; the
  `ValidateInboundEnvelope` at
  `src/ActualLab.Rpc/Serialization/RpcTextMessageSerializerV3.cs:102` does not
  check it). A JSON client sending `{"CallType":200,...}` reaches
  `RpcCallTypes.GetDescription(200)` → `Registry[200]` → `IndexOutOfRangeException`.
  The exception is caught in `RpcInboundCall.Process` and returned to the caller,
  so this is not a crash — but the client gets a nonsense error instead of the
  intended `InvalidCallTypeId` message. Secondary defect on the same lines:
  `RpcInboundContext.cs:60-62` assigns `MethodDef = NotFoundMethodDef` *before*
  reading `MethodDef.CallType.Id`, so the reported "expected" call type is
  always the `NotFound` method's, not the real one.
- **Evidence:**
  ```csharp
  // RpcCallTypes.cs:22 / 31-37
  Registry = new RpcCallType?[8];
  public static RpcCallType? Get(byte callTypeId) => Registry[callTypeId];
  public static string GetDescription(byte callTypeId) => Get(callTypeId) is { } callType ? ... ;
  // RpcInboundContext.cs:58-62
  if (MethodDef.CallType.Id != message.CallTypeId && message.CallTypeId != RpcCallTypeIds.Regular) {
      MethodDef = Peer.Hub.SystemCallSender.NotFoundMethodDef;   // clobbers the expected id
      ...
      Call = new RpcInboundInvalidCallTypeCall<Unit>(this, MethodDef.CallType.Id, message.CallTypeId) { ... };
  ```
- **Fix:** Make `RpcCallTypes.Get` bounds-check (`callTypeId < Registry.Length`),
  validate `CallType <= 7` in `ValidateInboundEnvelope`, and capture the
  expected call type id into a local before reassigning `MethodDef`.

---

### F11. W3C trace context is accepted from remote peers unvalidated and with a 64 KB `tracestate`

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** info-leak / dos
- **Location:** `src/ActualLab.Rpc/Diagnostics/RpcActivityInjector.cs:21-31`,
  `src/ActualLab.Rpc/Diagnostics/RpcDefaultCallTracer.cs:54-59`
- **What:** Every inbound call adopts the `~p` / `~s` headers as its parent
  `ActivityContext` with no size or trust check. Header values are allowed up to
  `RpcByteMessageSerializer.MaxHeaderSize` = 65536 bytes
  (`src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:17`), while the
  W3C spec caps `tracestate` at 512 characters.
- **Why it matters:** an anonymous client can (a) inject arbitrary trace ids to
  splice its requests into another tenant's traces or to poison trace-based
  correlation/alerting, and (b) attach a 64 KB `tracestate` that is then
  propagated verbatim into every downstream span and log record, inflating
  telemetry cost and storage.
- **Evidence:**
  ```csharp
  // RpcActivityInjector.cs:23-31
  var traceParent = headers.TryGet(WellKnownRpcHeaders.W3CTraceParent);
  ...
  return ActivityContext.TryParse(traceParent, traceState, true, out activityContext);
  // RpcDefaultCallTracer.cs:54-59 — used as the parent for the inbound activity
  ```
- **Fix:** Only honour inbound trace context on backend peers
  (`Peer.Ref.IsBackend`) or when explicitly opted in; enforce the W3C 512-char
  `tracestate` limit (and 55-char `traceparent`) before parsing.

---

## Out-of-partition findings

### OP1. Any client can force the server to use Newtonsoft.Json with `TypeNameHandling.Auto` by picking the `njson5` serialization format

- **Severity:** HIGH (CRITICAL if a deserialization gadget exists in the app's
  dependency closure)
- **Confidence:** CONFIRMED (format is selectable and the settings are as
  stated) / PLAUSIBLE (escalation to RCE)
- **Category:** deserialization
- **Location:** `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`,
  `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:27`,
  `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:67`,
  `src/ActualLab.Rpc/Configuration/RpcSerializationFormatResolver.cs:11`,
  `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:31`
  (owned by P3/P4 — reported here because it is what turns F2 into an RCE
  candidate)
- **What:** `NewtonsoftJsonSerializer.DefaultSettings` sets
  `TypeNameHandling = TypeNameHandling.Auto` with the default
  `DefaultSerializationBinder`. On read, Json.NET honours `$type` for any member
  whose declared type is `object` (or an interface/abstract with a compatible
  descendant), resolving it via assembly-qualified name and constructing it. The
  `njson5` / `njson5np` formats are registered in
  `RpcSerializationFormat.All`, which is the default set used by
  `RpcSerializationFormatResolver.Default`, and the **client** picks the format
  via the `?serializationFormat=` query parameter on the WebSocket upgrade.
- **Why it matters / attack path:** even an application that only ever uses
  `mempack6` gets an attacker-selectable Newtonsoft path. Connect with
  `...?clientId=x&serializationFormat=njson5`, then call any method whose
  argument graph contains an `object`-typed member (or use the unconstrained
  `$sys.B` slot from **F2**) and supply `$type`. The `objectType.IsAssignableFrom`
  check inside Json.NET is vacuous for `object`.
- **Fix:** (1) Remove `TypeNameHandling.Auto` from the default settings, or at
  minimum install a `SerializationBinder` with an allow-list. (2) Do not register
  all formats by default — make the accepted format set explicit per deployment,
  and default it to the binary formats only. (3) Reject a client-requested
  format that is not in the server's configured allow-list (this part already
  works via `Hub.SerializationFormats.TryGet`, but the default list is
  "everything").

### OP2. Server peer identity is entirely the client-supplied `clientId`; presenting a victim's `clientId` hijacks their server peer

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (mechanism) / requires knowledge of the victim's
  `clientId`, which is a v4 GUID — but it is written to the log at
  `Information` level on every connection
- **Category:** auth-bypass / info-leak
- **Location:** `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30-32`,
  `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:61-76`,
  `src/ActualLab.Rpc/RpcRef.cs:134-137`
- **What:** The server `RpcRef` is built solely from the `clientId` query
  parameter, and `RpcRef` equality is by `Address`. `RpcWebSocketServer.Invoke`
  looks up (or creates) the peer for that ref and, if it is already connected,
  **disconnects the existing connection** and attaches the new socket to the
  same `RpcPeer`. There is no binding between the peer and the authenticated
  identity/session of the HTTP upgrade request.
- **Why it matters:** anyone who learns a victim's `clientId` (server logs
  contain the full request URI — `RpcWebSocketServer.cs:61` logs
  `"Accepting RPC connection for {Request}"` with the query string; proxy/CDN
  access logs, browser history, referrer leaks, etc.) can evict the victim and
  inherit their server peer, receiving the results of the victim's in-flight
  and long-living calls (which were computed with the victim's session) and
  their server→client stream traffic.
- **Fix:** Bind the peer ref to the authenticated principal / session in
  addition to the `clientId` (e.g. `HMAC(serverKey, sessionId) + clientId`), or
  reject a reconnect whose HTTP identity differs from the one that created the
  peer. Also stop logging the full request URI (or scrub the `clientId`
  parameter) at `Information` level.

### OP3. `MaxArgumentDataSize` defaults to 130 MB per message

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:13`,
  `src/ActualLab.Rpc/Serialization/RpcTextMessageSerializer.cs:13`
- **What:** A single inbound message may carry 130 MB of argument data, and the
  transport must buffer it before dispatch. Combined with the absence of any
  concurrent-connection or concurrent-call limit (F6), this alone is an easy
  pre-auth OOM, and it is the multiplier behind F1 and F9.
- **Fix:** Lower the default drastically (a few MB) and make large payloads
  opt-in per method/service. Owned by P1/P3.

---

## Areas examined

Read in full:

- `src/ActualLab.Rpc/Infrastructure/`: `RpcInboundCall.cs`, `RpcInboundContext.cs`,
  `RpcInboundMessage.cs`, `RpcInboundNotFoundCall.cs`, `RpcInboundInvalidCallTypeCall.cs`,
  `IRpcInboundNotFoundCall.cs`, `RpcOutboundCall.cs`, `RpcOutboundContext.cs`,
  `RpcOutboundMessage.cs`, `RpcOutboundCallSetup.cs`, `RpcCall.cs`, `RpcCallStage.cs`,
  `RpcCallTrackers.cs`, `RpcObjectTrackers.cs`, `RpcObjectId.cs`, `IRpcObject.cs`,
  `RpcSystemCalls.cs`, `RpcSystemCallSender.cs`, `RpcSharedStream.cs`,
  `RpcSendHandlers.cs`, `RpcHeader.cs`, `RpcHeaderKey.cs`, `RpcHeadersExt.cs`,
  `WellKnownRpcHeaders.cs`, `RpcInterceptor.cs`, `RpcServiceBase.cs`,
  `RpcHandshake.cs`, `RpcTransport.cs`, `RpcFrameBasedTransport.cs`,
  `RpcStreamTransport.cs`, `IRpcPolymorphicArgumentHandler.cs`, `IRpcSystemService.cs`.
- `src/ActualLab.Rpc/Middlewares/`: all 5 files.
- `src/ActualLab.Rpc/Internal/`: `Errors.cs`, `IncreasingSeqCompressor.cs`,
  `RpcInternalServices.cs`, `RpcModuleInitializer.cs`, `RpcRefAddress.cs`,
  `RpcPeerInternalServices.cs`.
- `src/ActualLab.Rpc/Caching/`: all 4 files.
- `src/ActualLab.Rpc/Attributes/`: all 3 files.
- `src/ActualLab.Rpc/Diagnostics/`: `RpcCallLogger.cs`, `RpcDefaultCallTracer.cs`,
  `RpcActivityInjector.cs`, `RpcInstruments.cs` (plus skimmed the small trace types).
- `src/ActualLab.Rpc/RpcStream.cs`, `RpcRef.cs`, `RpcRef.Static.cs`, `RpcRoute.cs`,
  `RpcHub.cs`, `RpcServiceBuilder.cs`, `RpcServiceMode.cs`.
- Method/service resolution: `Configuration/RpcMethodDef*.cs`, `RpcMethodRef.cs`,
  `RpcMethodResolver.cs`, `RpcServiceDef.cs`, `RpcServiceRegistry.cs`,
  `RpcCallType(s).cs`, `RpcLimits.cs`, `RpcSerializationFormat*.cs`,
  `LegacyName(s).cs`, `Options/RpcInboundCallOptions.cs`, `Options/RpcOutboundCallOptions.cs`.

Read as supporting context (not audited as my partition):

- `src/ActualLab.Rpc/RpcPeer.cs` (handshake + read loop + `SetConnectionState`).
- `src/ActualLab.Rpc/Serialization/`: `RpcByteMessageSerializerV5.cs`,
  `RpcByteMessageSerializerV5Compact.cs`, `RpcTextMessageSerializerV3.cs`
  (partially), `RpcArgumentSerializer.cs`, `RpcByteArgumentSerializerV4.cs`,
  `RpcTextArgumentSerializerV4.cs`, `RpcTextArgumentSerializerV4NP.cs`,
  `Internal/ByteTypeSerializer.cs`, `Internal/TextTypeSerializer.cs`,
  `Internal/JsonRpcMessage.cs`, `RpcMessageSerializer.cs`.
- `src/ActualLab.Core/Serialization/ExceptionInfo.cs`,
  `src/ActualLab.Core/Reflection/TypeRef.cs`,
  `src/ActualLab.Core/Collections/VersionSet.cs`,
  `src/ActualLab.Core/Collections/SpanExt.ReadWriteVarUInt.cs`,
  `src/ActualLab.Core/Async/CancellationTokenSourceExt.cs`,
  `src/ActualLab.Core/Collections/ArrayPools.cs`,
  `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs` (settings only).
- `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs`,
  `RpcWebSocketServerDefaultDelegates.cs` (to establish reachability only).

No builds, tests, or experiments were run; every finding is based on source
reading and call-path tracing. Nothing in the repository was modified apart from
this report file.

## Areas NOT examined

- **P1 material:** `WebSockets/`, `Clients/`, `RpcPeer.cs` connection lifecycle,
  reconnect/backoff, `RpcClientPeer*`, `RpcServerPeer.cs`,
  `RpcPeerConnectionState*`, `RpcPipeTransport.cs`, `RpcSimpleChannelTransport.cs`,
  keep-alive timing. Only touched where a P2 defect required proving
  reachability.
- **P3 material:** the serializer internals themselves (MemoryPack/MessagePack
  formatter correctness on hostile bytes, `MemoryReader`/`SpanWriter` bounds
  arithmetic, `ByteString`/UTF-8 handling, `ArrayPool` lifetimes). I read enough
  of `ByteTypeSerializer`/`TextTypeSerializer`/`RpcByteArgumentSerializerV4` to
  reason about the polymorphic type gate (F2, OP1) but did not audit them.
  In particular I did **not** verify the concrete `Dictionary<int, byte[]>`
  formatter pre-sizing behaviour that F4 depends on.
- **P4 material:** `RpcWebSocketServer`/`RpcHttpServer` beyond the `clientId` and
  `serializationFormat` handling; origin/CSRF checks on the upgrade; the .NET
  Framework variants.
- **P5/P6 material:** Fusion's `RpcInboundComputeCall` / `RpcOutboundComputeCall`
  overrides of `CompletedStage` / `TryReprocess` / `SetResult` (these subclass
  types I reviewed and could carry additional stage-related races), the client
  computed cache that consumes `RpcCacheKey`/`RpcCacheValue`, and all
  session/auth code.
- **P9 material:** the TypeScript client's handling of `$sys.I`/`$sys.B`, which
  I expect to have the same unbounded-buffer property as F3 but did not read.
- Runtime verification of any finding (no worktree build, no fuzzing). F1, F3,
  F5, F6, F9 in particular would be cheap to demonstrate with a small hostile
  client against a test server and are worth confirming empirically before
  prioritising fixes.
