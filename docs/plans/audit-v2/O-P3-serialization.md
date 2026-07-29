# P3 — Serialization (RPC + Core) & text/IO buffers — review report

Reviewer partition: `src/ActualLab.Rpc/Serialization/`, `src/ActualLab.Core/Serialization/`,
`src/ActualLab.Serialization.NerdbankMessagePack/`, `src/ActualLab.Core/Text/`, `src/ActualLab.Core/IO/`.

All experiments were run in a throwaway project under `tmp/p3-repro/` that references the
**published** `ActualLab.Core` / `ActualLab.Rpc` **14.1.78** NuGet packages. The repository working
tree was not modified or built.

---

### F1. Unrestricted Json.NET polymorphic deserialization (`TypeNameHandling.Auto`, no `SerializationBinder`) is reachable from the wire

- **Severity:** CRITICAL
- **Confidence:** CONFIRMED (reproduced against published 14.1.78; end-to-end RCE additionally
  requires a usable gadget type in the target process, which is the usual Json.NET caveat)
- **Category:** deserialization
- **Location:**
  - `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29` (and the whole
    `DefaultSettings` block, lines 27-34)
  - `src/ActualLab.Core/Serialization/Serialized/NewtonsoftJsonSerialized.cs:37`
  - `src/ActualLab.Core/Collections/Legacy/ImmutableOptionSet.cs:34`
  - `src/ActualLab.Core/Collections/Legacy/OptionSet.cs:33`
  - `src/ActualLab.Core/Collections/Internal/OptionSetHelper.cs:22`
  - `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:27` and `:31`
  - `src/ActualLab.Rpc/Configuration/RpcSerializationFormatResolver.cs:11`
  - `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:31`

- **What:** `NewtonsoftJsonSerializer.DefaultSettings` enables `TypeNameHandling.Auto` with the
  stock `DefaultSerializationBinder` (no allow-list, no `SerializationBinder` override). Json.NET
  honours `$type` **on read** for any `TypeNameHandling` value other than `None`, and its
  `objectType.IsAssignableFrom(specifiedType)` guard is vacuous when the declared target type is
  `object`. `ImmutableOptionSet` / `OptionSet` — which are ordinary Fusion wire contract types —
  store every value as `NewtonsoftJsonSerialized<object>`, i.e. exactly that vacuous case. This is
  the CA2326 / "Json.NET TypeNameHandling" deserialization-gadget pattern.

- **Why it matters / attack path:**
  1. **Format-independent path (worst).** `ImmutableOptionSet.JsonCompatibleItems` is the *wire*
     member for **all** formats (`[Key(0)]`, `[MemoryPackOrder(0)]`,
     `[JsonPropertyName("Items")]`). Its values are plain strings that are handed to
     `NewtonsoftJsonSerializer.Default.ToTyped<object>()` at deserialization time
     (`TextSerialized<T>.Data` init → `NewtonsoftJsonSerialized<T>.GetSerializer()`). So even under
     the *default* `mempack6` RPC format, an attacker-controlled `ImmutableOptionSet` yields
     arbitrary type instantiation with attacker-controlled member values.
     `SessionInfo.Options` (`src/ActualLab.Fusion.Ext.Contracts/Authentication/SessionInfo.cs:23`)
     and `AuthBackend_SetSessionOptions.Options`
     (`src/ActualLab.Fusion.Ext.Contracts/Authentication/IAuth.cs:38`) are both
     `ImmutableOptionSet`. `SessionInfo` flows **server → client** (`IAuth.GetSessionInfo`,
     `IAuth.GetUserSessions`), so a hostile/MITM'd server owns every .NET client;
     `AuthBackend_SetSessionOptions` and any application contract carrying an
     `ImmutableOptionSet` / `OptionSet` gives the same primitive **client → server**.
  2. **Format-selection path.** The RPC serialization format is chosen by the *client* through the
     `?f=` query-string parameter on the WebSocket upgrade
     (`RpcWebSocketServerDefaultDelegates.cs:31`) and is validated only against
     `Hub.SerializationFormats`, which defaults to `RpcSerializationFormat.All`
     (`RpcSerializationFormatResolver.cs:11`) — a set that includes `njson5` / `njson5np`. So an
     unauthenticated client can force the server to deserialize *all* RPC arguments with
     `NewtonsoftJsonSerializer.Default`, extending `$type` handling to every `object`- or
     interface-typed member of every contract DTO.

- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:27
  public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
      TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
      TypeNameHandling = TypeNameHandling.Auto,          // <-- line 29, no SerializationBinder
      ...
  ```
  ```csharp
  // src/ActualLab.Core/Collections/Legacy/ImmutableOptionSet.cs:33
  [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
  [JsonPropertyName(nameof(Items)), Newtonsoft.Json.JsonIgnore]
  public IDictionary<string, NewtonsoftJsonSerialized<object>> JsonCompatibleItems
      => OptionSetHelper.ToNewtonsoftJsonCompatible(Items);
  ```
  Reproduced (published `ActualLab.Core` 14.1.78, .NET 9):
  ```
  [A] in-memory value type: System.Text.StringBuilder
  [B] mempack bytes contain $type: True
  [C] after mempack round-trip: System.Text.StringBuilder cap=4242
  [D] after msgpack round-trip: System.Text.StringBuilder
  ```
  i.e. a wire payload of `{"$type":"System.Text.StringBuilder, System.Private.CoreLib","Capacity":4242}`
  inside an `ImmutableOptionSet` materialises an arbitrary type and drives its property setters —
  through the **default binary** RPC formats.

- **Fix:**
  1. Set `TypeNameHandling = TypeNameHandling.None` in `NewtonsoftJsonSerializer.DefaultSettings`,
     or at minimum install a strict `SerializationBinder` (deny by default; the existing
     `NewtonsoftJsonSerializationBinder.Default` is the *unrestricted* stock binder and does not
     help). Fusion does not need `$type`: polymorphism is already carried out-of-band by
     `TextTypeSerializer` / `ByteTypeSerializer` / `TypeDecoratingTextSerializer`.
  2. Stop routing `object` values through `NewtonsoftJsonSerialized<object>`: migrate
     `OptionSet` / `ImmutableOptionSet` onto the `TypeDecoratingUniSerialized<TSchema, object>`
     mechanism used by `PropertyBag`, and give them a restrictive default `TypeSchema` (see F6).
  3. Remove `njson5`/`njson5np` from `RpcSerializationFormat.All` by default (opt-in only), and
     document that `RpcSerializationFormatResolver` should be narrowed to the formats an app
     actually serves.

---

### F2. `MessagePackSecurity.TrustedData` default → uncatchable `StackOverflowException` (process kill) from a ~72 KB payload, and no hash-collision resistance

- **Severity:** HIGH (CRITICAL for any deployment whose contracts carry
  `PropertyBag` / `MutablePropertyBag` / `OptionSet` / `ImmutableOptionSet` / an `object`-typed member)
- **Confidence:** CONFIRMED (crash reproduced); reachability from a *stock* Fusion contract is PLAUSIBLE
- **Category:** dos
- **Location:**
  - `src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:35`
    (`_defaultOptions ??= new(DefaultResolver)` — leaves `Security = TrustedData`)
  - `src/ActualLab.Core/Serialization/Internal/Formatters/PropertyBagMessagePackFormatter.cs:30`
  - `src/ActualLab.Core/Serialization/Internal/Formatters/PropertyBagItemMessagePackFormatter.cs:31`
  - `src/ActualLab.Core/Serialization/Internal/Formatters/TypeDecoratingUniSerializedMessagePackFormatter.cs:29`
  - `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:39` (no recursion budget)
  - `src/Directory.Build.props:89` (`PropertyBag` alias binds to `TypeSchema.Any`)

- **What:** `MessagePackSerializerOptions` defaults to `MessagePackSecurity.TrustedData`, whose
  `DepthStep(ref reader)` is a no-op and whose `GetEqualityComparer<T>()` returns the ordinary
  (collision-prone) comparers. Fusion's hand-written formatters *do* call
  `options.Security.DepthStep(ref reader)`, but under the shipped default those calls do nothing,
  so nothing bounds nesting depth. `TypeDecoratingByteSerializer.Read` also has no recursion budget
  of its own, and because `PropertyBag` is aliased to `PropertyBag<TypeSchema.Any>` project-wide, a
  `PropertyBag` value is allowed to be another `PropertyBag`.

- **Why it matters / attack path:** deserializing an N-level-nested `PropertyBag` costs ~120 bytes
  of payload per level and recurses on the CLR stack once per level. On a default 1 MB thread stack
  ~600 levels (**≈72 KB of wire data**) overflow the stack. `StackOverflowException` cannot be
  caught in .NET — the **entire server process dies**, taking every other connection with it.
  Reachable whenever a `PropertyBag`/`OptionSet`/`object`-typed value arrives from an untrusted
  source: an application RPC contract member, the polymorphic system-call paths
  (`RpcSystemCalls.IsValidCall` sets the expected argument type to plain `object` for the `$sys.B`
  batch call, `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:231-236`, so an attacker may name
  `ActualLab.Collections.PropertyBag\`1[[ActualLab.Serialization.TypeSchema+Any, ActualLab.Core]]`
  as the item type), or a poisoned operation-log row (`Operation.Items` is a `MutablePropertyBag`).

  Second impact of the same setting: with `TrustedData` the MessagePack dictionary formatters use
  the default comparer, so any dictionary-typed contract member is open to hash flooding — e.g.
  `IRpcSystemCalls.Reconnect(int handshakeIndex, Dictionary<int, byte[]> completedStagesData, …)`
  (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:50`) accepts an attacker-built
  `Dictionary<int, byte[]>` of up to ~13 M entries (130 MB limit) whose keys can all be made to
  collide, giving quadratic insertion cost.

- **Evidence:** measured with the published packages:
  ```
  [E] MessagePack DefaultOptions.Security == TrustedData
  depth=300  payloadBytes=35931  -> Deserialized OK
  depth=600  payloadBytes=72039  -> Stack overflow.  (process terminated)
  depth=1000 payloadBytes=120839 -> Stack overflow.
  ```
  Crash stack (truncated):
  ```
  Stack overflow.
     at MessagePack.MessagePackReader.ReadArrayHeader()
     at ActualLab.Serialization.Internal.PropertyBagMessagePackFormatter`1.Deserialize(...)
     at MessagePack.MessagePackSerializer.Deserialize[PropertyBag`1](...)
     at ActualLab.Serialization.MessagePackByteSerializer`1.Read(...)
     at ActualLab.Serialization.TypeDecoratingByteSerializer.Read(...)
     ... (repeats)
  ```
  For contrast, both JSON serializers *are* depth-capped (Newtonsoft `MaxDepth` 64, STJ default 64),
  so this is specific to the MessagePack/MemoryPack + type-decorating chain.

- **Fix:**
  1. `MessagePackByteSerializer.DefaultOptions` must be
     `new MessagePackSerializerOptions(DefaultResolver).WithSecurity(MessagePackSecurity.UntrustedData)`
     (and lower `MaximumObjectGraphDepth` from 500 to something the stack can actually take, e.g. 64).
  2. Add an explicit, serializer-independent nesting budget to
     `TypeDecoratingByteSerializer.Read` / `TypeDecoratingTextSerializer.Read` (an
     `[ThreadStatic] int _depth` guard), because the type-decorating hop crosses serializer
     boundaries and MessagePack's own depth counter is reset on each nested `Deserialize` call.
  3. Default `PropertyBag`/`MutablePropertyBag` to a restrictive `TypeSchema` (see F6).

---

### F3. `ByteTypeSerializer.FromBytesCache` keys alias the pooled receive buffer: unbounded static-dictionary growth per inbound message

- **Severity:** HIGH
- **Confidence:** CONFIRMED (reproduced)
- **Category:** dos / logic
- **Location:**
  - `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:108` (key = slice of the frame)
  - `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:49-63` (`FromBytes` / `FromBytesCache`)
  - `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializerV4.cs:37`
    (`ArgumentData` — "zero-copy projection into the buffer")
  - `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:186-187`
  - `src/ActualLab.Rpc/Infrastructure/RpcStreamTransport.cs:158-165`

- **What:** `ByteTypeSerializer.ReadItemType` builds a `ByteString` **directly over the inbound
  `ArgumentData` memory** and uses it as the key of a process-wide, never-evicted
  `ConcurrentDictionary<ByteString, Type?>`. `ArgumentData` is a zero-copy slice of the pooled
  receive buffer, which the transports explicitly recycle (`buffer.Renew(...)`) as soon as the
  frame has been parsed. The stored key therefore mutates into unrelated bytes right after it is
  inserted.

- **Why it matters / attack path:** every inbound message that carries a polymorphic argument
  inserts a *new* entry into `FromBytesCache`, because the previously stored keys no longer compare
  equal to anything. The dictionary grows by one entry per message and is never trimmed →
  unbounded managed-memory growth driven purely by remote traffic (a plain memory-exhaustion DoS),
  and the cache degenerates to a 100 % miss rate so every message re-runs
  `TypeRef.Resolve` (see F4). A secondary, lower-probability consequence: `ByteString`'s hash is a
  32-bit `GetPartialXxHash3`, so a stale entry whose bytes have been overwritten can be hit by a
  probe that collides on that 32-bit hash, returning the *wrong* `Type` (the
  `expectedType.IsAssignableFrom` guard still applies, so this is type confusion within an
  assignable set, not a full bypass).

  That the surrounding code copies in the analogous situations —
  `RpcByteMessageSerializerV4.cs:34` (`new RpcMethodRef(blob.ToArray(), …)`) and
  `RpcHeaderKey`'s `Utf8Name = utf8Name.ToArray()`
  (`src/ActualLab.Rpc/Infrastructure/RpcHeaderKey.cs:63`) — indicates this is an oversight, not a
  deliberate trade-off.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:96
  public static Type? ReadItemType(ref ReadOnlyMemory<byte> data) {
      ...
      var fullLength = length + 4;
      var itemType = FromBytes(data[..fullLength].AsByteString());   // line 108 - NO copy
  ```
  ```csharp
  // src/ActualLab.Core/Text/ByteStringExt.cs:17
  public static ByteString AsByteString(this ReadOnlyMemory<byte> bytes) => new(bytes);
  ```
  Reproduced (published `ActualLab.Rpc` 14.1.78) — same type name, five simulated frames,
  buffer overwritten between frames:
  ```
  cache count (start): 0
  iter 0: resolved=String, cacheCount=1
  iter 1: resolved=String, cacheCount=2
  iter 2: resolved=String, cacheCount=3
  iter 3: resolved=String, cacheCount=4
  iter 4: resolved=String, cacheCount=5
  keys now hold garbage: EEEEEEEEEEEE | EEEEEEEEEEEE | EEEEEEEEEEEE | EEEEEEEEEEEE | EEEEEEEEEEEE
  ```

- **Fix:** copy before caching — `FromBytes(new ByteString(data[..fullLength].ToArray()))`, or
  better, look the key up first and only materialise a copy on a miss. Additionally bound
  `FromBytesCache` (and `TextTypeSerializer.FromBytesCache`) with an LRU/size cap instead of an
  unbounded `ConcurrentDictionary`.

---

### F4. Arbitrary type resolution from wire-supplied type names, performed *before* the assignability check, with permanently-cached results

- **Severity:** HIGH
- **Confidence:** CONFIRMED for the primitive; PLAUSIBLE for the exact memory-growth rate
- **Category:** deserialization / dos
- **Location:**
  - `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83-94` and `:60-61`
  - `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:62-73` and `:40-41`
  - `src/ActualLab.Core/Serialization/ExceptionInfo.cs:99`
  - `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:47`
  - `src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:73`
  - supporting (P8): `src/ActualLab.Core/Reflection/TypeRef.cs:99-106`

- **What:** every polymorphic entry point resolves the attacker-supplied assembly-qualified name
  **first** and only then checks `expectedType.IsAssignableFrom(itemType)` / `TypeFilter`. There is
  no allow-list on what may be *resolved*, and `TypeRef.Resolve` caches every **successful**
  resolution forever (`ResolveCache` only evicts `null` results).

- **Why it matters / attack path:**
  - `Type.GetType(aqn)` on a *generic* name materialises a new runtime type instantiation
    (`MethodTable`, EEClass, …) in the loader heap, which is **never** reclaimed. A remote peer can
    emit an unbounded stream of distinct, perfectly resolvable names such as
    ``System.Collections.Generic.List`1[[System.Collections.Generic.List`1[[…System.Int32…]]]]`` —
    each one permanently grows native memory *and* adds a permanent `ResolveCache` entry, until the
    process is OOM-killed. The `IsAssignableFrom` guard never runs early enough to prevent this.
  - The cheapest unauthenticated trigger does not even need a polymorphic contract:
    `IRpcSystemCalls.Error(ExceptionInfo error)` is a system call any peer may send, and
    `RpcSystemCalls.Error` unconditionally calls `error.ToException()`
    (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:98`) **before** looking up the related
    outbound call; `ExceptionInfo.TryCreateException` starts with
    `exceptionInfo.TypeRef.TryResolve()` (`ExceptionInfo.cs:99`).
  - For *unresolvable* names `Type.GetType` still triggers assembly-name probing on every call
    (nulls are deliberately not cached), turning each 100-byte message into a file-system probe —
    a cheap CPU/IO amplifier.
  - `ByteTypeSerializer.ReadDerivedItemType` with `expectedType == typeof(object)` (which
    `RpcSystemCalls.IsValidCall` deliberately produces for the `$sys.B` batch path,
    `RpcSystemCalls.cs:231-236`) makes the assignability check vacuous, so the resolved type is
    then actually deserialized — feeding F2 and, under `njson5`, F1.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83
  public static Type ReadDerivedItemType(ref ReadOnlyMemory<byte> data, Type expectedType) {
      var itemType = ReadItemType(ref data);   // <-- Type.GetType() already happened here
      if (itemType is null) return expectedType;
      if (expectedType.IsAssignableFrom(itemType)) return itemType;
  ```
  ```csharp
  // src/ActualLab.Core/Reflection/TypeRef.cs:99
  var result = ResolveCache.GetOrAdd(assemblyQualifiedName, static aqn => Type.GetType(aqn, false, false));
  if (result is null)
      ResolveCache.TryRemove(assemblyQualifiedName, out _); // Potential memory lead / attack vector
  ```
  (the in-source comment shows the risk was noticed for the null case only).

- **Fix:** resolve wire type names only through a per-`RpcServiceDef`/per-`expectedType` allow-list
  built at startup from the declared contract types plus their known subtypes; reject any name that
  is not in it *before* calling `Type.GetType`. At minimum: refuse names containing generic-argument
  syntax unless the expected type is generic, cap `ResolveCache` size, and make the RPC layer
  resolve exception types via a bounded whitelist (`ExceptionInfo.UnknownExceptionTypeResolver`
  already provides the extension point).

---

### F5. `ExceptionInfo` round-trip: arbitrary exception-type construction inbound, unfiltered exception messages outbound

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** deserialization / info-leak
- **Location:**
  - `src/ActualLab.Core/Serialization/ExceptionInfo.cs:94-128` (`TryCreateException`)
  - `src/ActualLab.Core/Serialization/ExceptionInfo.cs:41-50` (`ExceptionInfo(Exception)`)
  - `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:132` and `:146`
  - `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:174`
  - `src/ActualLab.Fusion.Server/JsonifyErrorsAttribute.cs:22` (same mechanism over HTTP)

- **What:** (a) inbound — `ExceptionInfo.ToException()` resolves an arbitrary wire-supplied type,
  accepts it if it derives from `Exception`, and constructs it via
  `type.CreateInstance(message, null)` / `type.CreateInstance(message)`. This runs the named type's
  **static constructor** and an instance constructor of the attacker's choosing, with an
  attacker-controlled `string`. (b) outbound — `new ExceptionInfo(exception)` captures
  `exception.GetType()` and `exception.Message` verbatim and ships them to the remote peer for
  *every* failed inbound call; there is no sanitisation hook.

- **Why it matters / attack path:** (a) any peer can send `$sys.Error` with an arbitrary
  `ExceptionInfo.TypeRef`; this is the cheapest reachable "instantiate a type I named" primitive in
  the codebase and the driver for F4. (b) server-side exception messages routinely embed
  connection strings, SQL fragments, file paths and internal identifiers; these are returned to
  unauthenticated remote clients by default, both over RPC and over HTTP via
  `JsonifyErrorsAttribute`.

- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Serialization/ExceptionInfo.cs:99
  var (type, message) = (exceptionInfo.TypeRef.TryResolve(), exceptionInfo.Message);
  type ??= UnknownExceptionTypeResolver?.Invoke(exceptionInfo.TypeRef);
  if (type is null || !typeof(Exception).IsAssignableFrom(type)) return null;
  var ctor = type.GetConstructor(ExceptionCtorArgumentTypes1);
  if (ctor is not null) { try { return (Exception)type.CreateInstance(message, (Exception?)null); } ... }
  ```
  ```csharp
  // src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:98
  var exception = error.ToException()!;                      // runs before the call lookup
  peer.OutboundCalls.Get(outboundCallId)?.SetError(exception, context);
  ```

- **Fix:** restrict `ExceptionInfo.ToException()` to a registered allow-list of exception types
  (defaulting to Fusion's own + a handful of BCL types, everything else → `RemoteException`), and
  add an outbound `ExceptionInfo` transform hook that is "type name only, no message" by default
  for non-`ITransientException` / non-explicitly-public exceptions.

---

### F6. Type-decorating serializers allow every type by default, and the whole framework aliases `PropertyBag` to `TypeSchema.Any`

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** deserialization
- **Location:**
  - `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:37`
    (`TypeFilter = typeFilter ?? (_ => true)`)
  - `src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:55` (same)
  - `src/ActualLab.Core/Serialization/TypeSchema.cs:13-16` (`TypeSchema.Any.IsAllowed => true`)
  - `src/Directory.Build.props:89-91` (`PropertyBag`/`MutablePropertyBag`/`PropertyBagItem`
    aliases → `TypeSchema.Any`)
  - `src/ActualLab.Serialization.NerdbankMessagePack/Internal/TypeDecoratingUniSerializedNerdbankConverter.cs:38-45`

- **What:** the `TypeSchema` allow-list mechanism exists and is plumbed through
  `PropertyBag<TSchema>`, but every alias and every default instantiation in the framework picks
  `TypeSchema.Any`, i.e. the filter is disabled everywhere it ships. `TypeDecoratingByteSerializer`
  / `TypeDecoratingTextSerializer` likewise default to `_ => true`. The Nerdbank converter goes one
  step further and hands the attacker-named type to
  `ReflectionTypeShapeProvider.Default` (`NerdbankMessagePackByteSerializer.cs:36`), which emits a
  fresh reflection-based shape (and dynamic code) for each newly named type — another unbounded,
  wire-driven code-generation path.

- **Why it matters / attack path:** it means F1/F2/F4 have no second line of defence: any value
  slot whose static type is `object` accepts any resolvable type. `PropertyBag` is the
  recommended replacement for `OptionSet` and is used for `Operation.Items`, `CommandContext.Items`
  and application contracts; with `TypeSchema.Any` its values may be arbitrary types, including
  another `PropertyBag` (which is exactly what makes F2's stack overflow constructible).

- **Fix:** make the shipped `PropertyBag`/`MutablePropertyBag` aliases bind to a restrictive schema
  (`TypeSchema.PrimitiveOnly` plus an app-extensible registry) and change the default `typeFilter`
  of `TypeDecoratingByteSerializer`/`TypeDecoratingTextSerializer` from "allow all" to "deny unless
  registered". At minimum, forbid `PropertyBag`/`OptionSet`/`TypeDecoratingUniSerialized` as *value*
  types inside a `PropertyBag` so nesting cannot be built from the wire.

---

### F7. MemoryPack serialization of a nested `PropertyBag` / `TypeDecoratingUniSerialized` is exponential in nesting depth

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (measured); remote reachability PLAUSIBLE
- **Category:** dos
- **Location:**
  - `src/ActualLab.Core/Serialization/Serialized/TypeDecoratingUniSerialized.cs:83-102`
    (`SerializeBytes`) and `:47-57` (the four computed `MemoryPack` / `MessagePack` / `Json` /
    `NewtonsoftJson` wire properties)
  - `src/ActualLab.Core/Collections/Internal/PropertyBagItem.cs:20`

- **What:** each nesting level re-invokes the child's computed `MemoryPack` property, and the
  MemoryPack `VersionTolerant` generated formatter evaluates it more than once per level, so the
  total cost roughly doubles per level. Measured with the published packages:
  ```
  mempack  depth=8  bytes=1126 ms=30
  mempack  depth=12 bytes=1722 ms=9
  mempack  depth=16 bytes=2318 ms=135
  mempack  depth=20 bytes=2914 ms=1338      (~2x per level)
  msgpack  depth=500 bytes=59931 ms=3       (linear)
  ```
  A 30-level bag (~4 KB of data) would take on the order of ~20 minutes of CPU under `mempack6`,
  **the default RPC format**.

- **Why it matters / attack path:** anywhere a `PropertyBag` obtained from an untrusted source is
  later re-serialized with MemoryPack — echoed back to a client, written to the operation log,
  stored in the client-side computed cache — a few kilobytes of attacker data pin a core
  indefinitely.

- **Fix:** cache the serialized form inside `TypeDecoratingUniSerialized` (serialize once per
  instance rather than once per property read), and/or add the nesting-depth guard from F2 so
  arbitrarily deep bags cannot be constructed in the first place.

---

### F8. `RpcFrameCodec.TryDeserializeBinaryWithSize` reads the 4-byte size prefix without checking that 4 bytes remain

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:135-136`

- **What:**
  ```csharp
  size = array.AsSpan(offset).ReadLittleEndian();          // reads past totalLength
  isSizeValid = size > 0 && offset + size <= totalLength;
  ```
  `array` is the pooled receive buffer, which is normally larger than `totalLength`, so when fewer
  than 4 bytes of the frame remain the size prefix is composed from stale bytes left over from a
  previous message. A residual `size` of 1..3 then passes the `isSizeValid` check and
  `array.AsMemory(offset + 4, size - 4)` is called with a negative length.

- **Why it matters:** the resulting `ArgumentOutOfRangeException` is caught and `offset` still
  advances, so there is no hang or crash — but the parser silently makes decisions from
  uninitialised pool data, and an attacker can influence the residual bytes by controlling the
  previous message. It is a latent correctness hazard in the one place that is supposed to
  validate the frame.

- **Fix:** `if (totalLength - offset < Int32Size) throw Errors.InvalidItemSize();` before the read,
  and require `size >= Int32Size`.

---

### F9. `RpcTextMessageSerializerV3.Read` indexes `tail[0]` on a possibly empty tail

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Serialization/RpcTextMessageSerializerV3.cs:41`

- **What:** after the JSON envelope is consumed, `var tail = data.Slice((int)reader.BytesConsumed).Span;`
  followed by `if (tail[0] == Delimiter)`. A peer that sends a syntactically valid envelope with no
  trailing delimiter yields an empty `tail` and an `IndexOutOfRangeException`.

- **Why it matters:** `RpcFrameCodec.TryDeserializeText` catches it, logs at Error level and drops
  the message — so it is a remote log-flood / message-drop, not a crash. Still, a
  bounds check is the correct handling of a wire-format edge case.

- **Fix:** `if (!tail.IsEmpty && tail[0] == Delimiter) tail = tail[1..];`

---

### F10. Malformed-frame handler logs raw attacker bytes at `Error` level, unthrottled

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** dos / info-leak
- **Location:** `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:123`, `:151`, `:176`

- **What:** every message that fails to deserialize produces
  `_errorLog?.LogError(e, "Couldn't deserialize: {Data}", new TextOrBytes(...))` with no rate
  limiting. `TextOrBytes.ToString()` truncates at 64 bytes, so the volume per message is bounded,
  but the *rate* is not: a client can emit malformed messages as fast as the link allows, each one
  producing an `Error` entry containing attacker-chosen content (in the text case, attacker-chosen
  *characters*, which is a structured-log-injection vector for downstream log consumers).

- **Fix:** log at `Debug`/`Warning` with a per-peer rate limiter, and hex-encode rather than
  passing through raw text.

---

### F11. `ListFormat.Parse(string, List<string>?)` leaks the pooled `StringBuilder`

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** leak
- **Location:** `src/ActualLab.Core/Text/ListFormat.cs:47-50`

- **What:** the `string` overload creates the `ListParser` without `using`, so
  `ListParser.Dispose()` → `ItemBuilder.Release()` never runs and the pooled `StringBuilder` is
  never returned. The `ReadOnlySpan<char>` overload immediately below (`:56`) does use `using`.

- **Fix:** `using var p = CreateParser(source);`

---

## Out-of-partition findings

- **Client-selectable serialization format** (`RpcWebSocketServerDefaultDelegates.cs:31`,
  `RpcSerializationFormatResolver.cs:11`) — P4/P1 own the endpoint, but the security consequence is
  entirely F1's; whoever owns those files should also narrow `RpcSerializationFormat.All`.
- **`RpcSystemCalls.IsValidCall` widens the expected argument type to plain `object`** for the
  `$sys.B` batch path (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:231-236`). This is P2's
  file, but it is what turns "polymorphic within an assignable set" into "any type at all" for
  F2/F4. Consider passing `stream.ItemType` and having the argument serializer force polymorphism
  explicitly rather than erasing the expected type.
- **Compact message serializers identify methods by a 32-bit hash only**
  (`RpcByteMessageSerializerV4Compact.cs:26-28`, `RpcByteMessageSerializerV5Compact.cs:26-28`;
  `ServerMethodResolver[hashCode]`). Not exploitable as long as the resolver is built strictly from
  the *peer-visible* method set (collisions then only reach methods the peer may already call), but
  P2 should confirm that invariant and consider adding a startup assertion that no two methods in a
  resolver share a hash.
- **`RpcTextArgumentSerializerV4NP.Deserialize`** (`src/ActualLab.Rpc/Serialization/RpcTextArgumentSerializerV4NP.cs:53-55`)
  justifies skipping the type prefix with "Serialize already rejects it". That reasoning is invalid
  for untrusted input (the *remote* peer serializes). It happens to be harmless today because the
  declared type is used regardless, but the comment should not be relied on.

---

## Areas examined

**`src/ActualLab.Rpc/Serialization/`** — read in full:
`RpcMessageSerializer.cs`, `RpcByteMessageSerializer.cs`, `RpcByteMessageSerializerV4.cs`,
`RpcByteMessageSerializerV4Compact.cs`, `RpcByteMessageSerializerV5.cs`,
`RpcByteMessageSerializerV5Compact.cs`, `RpcTextMessageSerializer.cs`,
`RpcTextMessageSerializerV3.cs`, `RpcArgumentSerializer.cs`, `RpcByteArgumentSerializerV4.cs`,
`RpcTextArgumentSerializerV4.cs`, `RpcTextArgumentSerializerV4NP.cs`, `RpcFrameCodec.cs`,
`NullValue.cs`, `IRequiresItemSize.cs`, `Internal/ByteTypeSerializer.cs`,
`Internal/TextTypeSerializer.cs`, `Internal/JsonRpcMessage.cs`, `Internal/RpcStreamJsonConverter.cs`,
`Internal/RpcStreamNewtonsoftJsonConverter.cs`.

**`src/ActualLab.Core/Serialization/`** — read in full:
`NewtonsoftJsonSerializer.cs`, `SystemJsonSerializer.cs`, `MessagePackByteSerializer.cs`,
`MemoryPackByteSerializer.cs`, `TypeDecoratingByteSerializer.cs`, `TypeDecoratingTextSerializer.cs`,
`TypeSchema.cs`, `ExceptionInfo.cs`, `ExceptionInfoExt.cs`, `RemoteException.cs`, `TextOrBytes.cs`,
`JsonString.cs`, `SerializerKind.cs`, `ByteSerializerExt.cs`, `TextSerializerExt.cs`,
`Internal/{Errors,TextSerializerBase,CastingTextSerializer,AsymmetricByteSerializer,`
`AsymmetricTextSerializer,SerializationFeatures,DefaultMessagePackResolver,MessagePackData,`
`NewtonsoftJsonSerializationBinder,PreferSerializableContractResolver}.cs`,
`Internal/Formatters/*` (all 9), `Serialized/{ByteSerialized,TextSerialized,UniSerialized,`
`TypeDecoratingUniSerialized,NewtonsoftJsonSerialized}.cs`.

**`src/ActualLab.Core/Text/`**: `ByteString.cs`, `ByteStringExt.cs`, `ByteSpanExt.cs`,
`CharSpanExt.cs`, `Symbol.cs`, `Base64UrlEncoder.cs`, `EncoderExt.cs`, `DecoderExt.cs`,
`EncodingExt.cs`, `JsonFormatter.cs`, `ListFormat.cs`, `ListParser.cs`, `ListFormatter.cs`,
`Internal/{ByteStringMessagePackFormatter,SymbolMessagePackFormatter,`
`StringAsSymbolMemoryPackFormatter,ByteStringTypeConverter,SymbolTypeConverter}.cs`.

**`src/ActualLab.Core/IO/`**: `FilePath.cs`, `Internal/MemoryReader.cs`, `Internal/SpanWriter.cs`,
`Internal/Utf8TextWriter.cs`, `Internal/FilePathTypeConverter.cs`.

**`src/ActualLab.Serialization.NerdbankMessagePack/`**: `NerdbankMessagePackByteSerializer.cs`,
`RpcNerdbankSerializationFormat.cs`, `Internal/TypeRefNerdbankConverter.cs`,
`Internal/TypeDecoratingUniSerializedNerdbankConverter.cs`, `Internal/PropertyBagNerdbankConverter.cs`.

**Supporting context read (outside the partition, for reachability proofs):**
`src/ActualLab.Core/Collections/{SpanExt.cs,SpanExt.ReadWriteVarUInt.cs,ArrayPoolBuffer.cs,`
`PropertyBag.cs,Internal/PropertyBagItem.cs,Internal/OptionSetHelper.cs,Legacy/ImmutableOptionSet.cs,`
`Legacy/OptionSet.cs,Internal/OptionSetItem.cs}`, `src/ActualLab.Core/Reflection/TypeRef.cs`,
`src/ActualLab.Rpc/Configuration/{RpcSerializationFormat.cs,RpcSerializationFormatResolver.cs,`
`RpcMethodRef.cs}`, `src/ActualLab.Rpc/Infrastructure/{RpcInboundCall.cs,RpcSystemCalls.cs,`
`RpcHeaderKey.cs,RpcStreamTransport.cs,RpcSystemCallSender.cs}`,
`src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs`,
`src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs`,
`src/ActualLab.Fusion.Server/JsonifyErrorsAttribute.cs`,
`src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandler.cs`,
`src/ActualLab.Fusion.Ext.Contracts/Authentication/{IAuth.cs,SessionInfo.cs}`,
`src/Directory.Build.props`.

**Experiments run** (all in `tmp/p3-repro/`, against published NuGet 14.1.78, never in the working tree):
1. Json.NET `$type` acceptance through `NewtonsoftJsonSerialized<object>` / `ImmutableOptionSet`,
   including full MemoryPack and MessagePack wire round-trips → F1.
2. `MessagePackByteSerializer.DefaultOptions.Security` inspection → F2.
3. Nested-`PropertyBag` deserialization at depths 300/600/1000 → stack overflow at 600 → F2.
4. `ByteTypeSerializer.FromBytesCache` growth with a recycled buffer (reflection on the private
   static field) → F3.
5. MemoryPack vs. MessagePack nested-`PropertyBag` serialization timings → F7.
6. Newtonsoft / System.Text.Json depth-limit behaviour (both capped at 64) — negative result,
   documented in F2.
7. `ByteString` MessagePack/MemoryPack round-trip aliasing check — negative result (both copy).

## Areas NOT examined

- **Third-party library internals.** `MemoryPackReader.Advance(int)` (called with a wire-supplied
  `int` delta in `StringAsSymbolMemoryPackFormatter.cs:40` and in every MemoryPack-generated
  version-tolerant formatter) was **not** verified for negative/oversized-count handling — no
  source or decompiler was available offline. If MemoryPack's `Advance` is unchecked, that is a
  memory-safety issue reachable from any MemoryPack RPC payload; worth a targeted follow-up.
  Similarly, MessagePack's and Nerdbank's `ReadArrayHeader` "remaining >= count" guards were assumed
  from documentation, not verified — so the ~16x allocation amplification implied by
  `new PropertyBagItem<TSchema>[arrayLen]`
  (`PropertyBagNerdbankConverter.cs:29`, `ApiArrayMessagePackFormatter.cs`) against a 130 MB
  `MaxArgumentDataSize` is noted but not reported as a finding.
- **The remaining ~24 Nerdbank converters** under
  `src/ActualLab.Serialization.NerdbankMessagePack/Internal/` (Moment, CpuTimestamp, Session,
  RpcCacheKey/Value, RpcHandshake, VersionSet, ApiMap, …). Only the type-resolution-relevant ones
  were read. The Nerdbank formats (`nmsgpack6`, `nmsgpack6c`) are opt-in (`Register()` must be
  called), so they were deprioritised.
- **Serialization *tests*** under `tests/` — not read; no attempt was made to see which of these
  findings already have (passing) regression tests.
- **Generated code** under `*/obj/**/generated/**` was only sampled (one MemoryPack and a few
  MessagePack formatters) to understand the version-tolerant skip pattern; it was not audited.
- `src/ActualLab.Core/IO/{ConsoleExt,FileExt,FilePathExt,FileSystemWatcherExt,FilePath.Extras}.cs`
  and the Newtonsoft/STJ converter shims for `Symbol`/`ByteString`/`FilePath`/`JsonString` — skimmed
  only; they are thin string wrappers with no attacker-reachable buffer arithmetic.
- **Exploitability of F1 to full RCE** was not pursued: I confirmed arbitrary type instantiation
  with attacker-controlled member values, but did not enumerate concrete gadget chains available in
  a typical Fusion server's loaded assembly set.
- **P1/P2/P4 subsystems** (transport framing limits, call routing/authorization, HTTP endpoints)
  were read only as far as needed to establish reachability for the findings above.
