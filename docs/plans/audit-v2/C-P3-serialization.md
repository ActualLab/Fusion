### F1. RPC type caches retain mutable transport-buffer slices and accept non-canonical keys

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion / race
- **Location:** `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:16`
- **What:** The process-wide inbound type caches use `ByteString` views into RPC argument buffers as keys instead of making an owned, canonical key. The binary form also includes a caller-controlled two-byte “hash” in the cache key but never validates or uses that field when resolving the type, so the same type name has 65,536 immediately available cache keys.
- **Why it matters / failure scenario:** An unauthenticated peer calls any RPC method with an `object` or abstract parameter, causing `RpcInboundCall.DeserializeArguments` to reach `ByteTypeSerializer.ReadItemType`. Varying bytes 2-3 while keeping the same valid type name repeatedly inserts into the static cache; after the frame is yielded, the WebSocket/pipe transport renews or returns the backing array, so already-inserted keys also mutate while resident in `ConcurrentDictionary`. This can grow unreachable dictionary entries without bound, retain/reuse large frame arrays, and race cache lookups against buffer reuse across connections.
- **Evidence:** `ToBytes` writes the advertised hash at `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:37-39`, but `FromBytes` reads the length and then resolves only `memory[4..]`, never checking bytes 2-3 (`src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:49-61`). `ReadItemType` passes `data[..fullLength].AsByteString()` directly into the static cache (`src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:96-109`), and `ByteString` merely stores the supplied memory (`src/ActualLab.Core/Text/ByteString.cs:50-56`). The text variant has the same borrowed-key pattern at `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:13,34-42,91-93`. The framing layer explicitly projects messages into its input array at `src/ActualLab.Rpc/Serialization/RpcFrameCodec.cs:112-119`, and the transport renews that array after synchronous processing at `src/ActualLab.Rpc/WebSockets/RpcWebSocketTransport.cs:177-187`.
- **Fix:** Parse and validate the complete marker before caching, including checking the binary hash if it remains on the wire. Cache by a canonical owned value (preferably the normalized type-name string or a stable registered type ID), never by a view into argument memory; bound or evict the inbound cache as defense in depth. Apply the same ownership/canonicalization rule to `TextTypeSerializer`.

### F2. RPC polymorphism resolves any assignable process type named by the peer

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** deserialization
- **Location:** `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83`
- **What:** RPC’s polymorphic argument protocol resolves an assembly-qualified type name supplied by the peer and validates only `expectedType.IsAssignableFrom(itemType)`. There is no contract-derived allowlist, so a parameter declared as `object` admits every process-resolvable type, while an interface/abstract parameter admits every loaded implementation.
- **Why it matters / failure scenario:** `RpcMethodDef` automatically marks abstract and `object` parameters as polymorphic, and inbound calls pass their argument buffer to `RpcByteArgumentSerializerV4` or `RpcTextArgumentSerializerV4`. A peer can name an otherwise internal concrete type and provide its serialized body; the selected base serializer then constructs that type and invokes its formatter, constructors, setters, and callbacks even though it was never part of the RPC contract. This affects the default MemoryPack formats as well as MessagePack, JSON, and registered Nerdbank formats; this finding does not assume or claim a specific RCE gadget.
- **Evidence:** `RpcArgumentSerializer.IsPolymorphic` treats abstract types and `object` this way at `src/ActualLab.Rpc/Serialization/RpcArgumentSerializer.cs:39-41`. `ByteTypeSerializer.FromBytes` constructs a `TypeRef` from wire bytes and calls `Resolve()` (`src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:49-61`), while `ReadDerivedItemType` applies only assignability (`src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:83-93`); the text implementation is equivalent at `src/ActualLab.Rpc/Serialization/Internal/TextTypeSerializer.cs:34-42,62-72`. The binary argument reader immediately passes the returned type to `baseSerializer.Read` at `src/ActualLab.Rpc/Serialization/RpcByteArgumentSerializerV4.cs:86-94`, and inbound dispatch reaches it at `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:220-240`.
- **Fix:** Replace assembly-qualified names with registered, stable discriminators whose permitted concrete types are derived from the RPC contract. Reject an unregistered discriminator before resolving a `Type` or invoking a serializer; require explicit union/derived-type registration for `object`, interface, and abstract parameters.

### F3. The default Newtonsoft RPC formats enable unrestricted `$type` deserialization

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** deserialization
- **Location:** `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:27`
- **What:** `NewtonsoftJsonSerializer.DefaultSettings` enables `TypeNameHandling.Auto` without a restrictive serialization binder. Both `njson5` and the advertised no-polymorphism `njson5np` RPC formats use that singleton, so JSON `$type` metadata remains a second, unrestricted polymorphism channel even when the RPC type decorator is disabled.
- **Why it matters / failure scenario:** A peer selects an enabled Newtonsoft RPC format and sends an argument containing `$type`. `RpcTextArgumentSerializerV4NP` deserializes the segment as the declared type, but Json.NET honors `$type` at the root or in nested `object`/abstract members and resolves it through its default binder. This lets the peer construct types outside the RPC contract; exploitability beyond unexpected type construction depends on the application’s loaded types and converters, so no specific gadget-chain/RCE claim is made here.
- **Evidence:** The default explicitly sets `TypeNameHandling.Auto` at `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:27-34`, creates a `JsonSerializer` from those settings at `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:68-71`, and calls it with peer data at `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:76-80`. `RpcTextArgumentSerializerV4NP` asserts that deserialization “never encounters polymorphic data” but delegates directly to the base serializer at `src/ActualLab.Rpc/Serialization/RpcTextArgumentSerializerV4NP.cs:52-65`. Both Newtonsoft formats are in the default format list at `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:26-33,67-73`.
- **Fix:** Use `TypeNameHandling.None` for the default/network serializer. If legacy data genuinely needs Json.NET type metadata, provide a separate opt-in serializer with an explicit allowlist binder and do not advertise it as no-polymorphism; migrate network polymorphism to contract-registered discriminators.

### F4. MessagePack RPC deserializes network data in `TrustedData` mode

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:29`
- **What:** `MessagePackByteSerializer.DefaultOptions` is constructed from only the resolver, inheriting MessagePack-CSharp’s `MessagePackSecurity.TrustedData` default. The default RPC format set exposes four MessagePack formats that feed peer-controlled argument data through these options.
- **Why it matters / failure scenario:** A peer selects `msgpack5`, `msgpack5c`, `msgpack6`, or `msgpack6c` and invokes a method accepting a dictionary or nested object graph. Trusted mode does not use collision-resistant dictionary comparers and uses the dependency’s trusted depth policy, allowing crafted keys or nesting to consume disproportionate CPU/stack; MessagePack-CSharp explicitly requires `MessagePackSecurity.UntrustedData` for network input.
- **Evidence:** The options singleton is created as `new(DefaultResolver)` with no `WithSecurity(...)` at `src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:27-36`, then passed unchanged to `MessagePackSerializer.Deserialize` at `src/ActualLab.Core/Serialization/MessagePackByteSerializer.cs:136-137`. All MessagePack RPC formats use this default serializer and are included in `All` at `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:43-49,59-73`.
- **Fix:** Construct the network/default options with `new MessagePackSerializerOptions(DefaultResolver).WithSecurity(MessagePackSecurity.UntrustedData)`. Audit every custom formatter to ensure recursive reads are wrapped with `options.Security.DepthStep(ref reader)` / `reader.Depth--`, and keep any trusted-storage fast path as a separately named opt-in serializer.

### F5. Nerdbank array converters preallocate directly from untrusted counts

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Serialization.NerdbankMessagePack/Internal/ApiArrayNerdbankConverter.cs:12`
- **What:** The custom `ApiArray<T>` and `PropertyBag<TSchema>` converters allocate their final arrays directly from an array length read off the wire. They ignore Nerdbank.MessagePack’s `context.Security.MaxCollectionPreallocation` guidance, so allocation occurs before even the first element has been validated.
- **Why it matters / failure scenario:** After an application registers `nmsgpack6`/`nmsgpack6c`, a peer calls an RPC method accepting one of these common types and sends a large declared count, one malformed first item, and enough filler bytes to satisfy the reader’s coarse “at least one byte per item” check. A relatively small payload can allocate a reference array roughly eight times its wire size and then fail immediately; the default binary argument allowance is 130 MB, so concurrent requests can drive the process into LOH pressure or OOM without ever producing a valid value.
- **Evidence:** `ApiArrayNerdbankConverter.Read` executes `new T[len]` immediately after `ReadArrayHeader` at `src/ActualLab.Serialization.NerdbankMessagePack/Internal/ApiArrayNerdbankConverter.cs:10-19`. `PropertyBagNerdbankConverter` does the same at `src/ActualLab.Serialization.NerdbankMessagePack/Internal/PropertyBagNerdbankConverter.cs:25-31`. Both converters are installed in the default Nerdbank serializer at `src/ActualLab.Serialization.NerdbankMessagePack/NerdbankMessagePackByteSerializer.cs:138-150`, which is exposed by the opt-in RPC formats at `src/ActualLab.Serialization.NerdbankMessagePack/RpcNerdbankSerializationFormat.cs:8-13`; the RPC byte limit is `130_000_000` at `src/ActualLab.Rpc/Serialization/RpcByteMessageSerializer.cs:11-14`.
- **Fix:** Honor `context.Security.MaxCollectionPreallocation`: deserialize into a capped, incrementally growing buffer and materialize the final array only after all elements validate. Also add a configurable maximum logical element count for RPC-facing collections and reject counts above it before allocation.

### F6. Malformed version-tolerant string deltas can move the MemoryPack reader backwards

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** buffer-safety
- **Location:** `src/ActualLab.Core/Text/Internal/StringAsSymbolMemoryPackFormatter.cs:28`
- **What:** When the optional string-as-symbol formatter sees an object header count other than one, it reads signed varint deltas from the payload and passes each value directly to `MemoryPackReader.Advance`. Negative deltas and cumulative advances beyond the remaining input are not rejected before the reader’s unsafe cursor is changed.
- **Why it matters / failure scenario:** An application enables `StringAsSymbolMemoryPackFormatterAttribute.IsEnabled`, then receives a MemoryPack RPC argument containing any annotated string field. A malformed version-tolerant header with a negative delta moves the MemoryPack reader before the supplied span; subsequent field/argument reads can consume memory outside the logical payload, produce corrupted values, or terminate the process. The global feature is off by default, which is why this is MEDIUM rather than a default-path remote-crash finding.
- **Evidence:** Deltas are read with the signed `ReadVarIntInt32()` at `src/ActualLab.Core/Text/Internal/StringAsSymbolMemoryPackFormatter.cs:28-30`; for any `count != 1`, every unvalidated value is passed to `reader.Advance` at `src/ActualLab.Core/Text/Internal/StringAsSymbolMemoryPackFormatter.cs:37-40`. The global switch installs this formatter at `src/ActualLab.Core/Text/StringAsSymbolMemoryPackFormatterAttribute.cs:21-33`.
- **Fix:** Reject negative deltas, checked-overflow the cumulative skip, and verify every skip is at most the reader’s remaining length before advancing. Prefer MemoryPack’s standard version-tolerant skip/read primitive rather than reproducing its cursor arithmetic.

### F7. Restricted type schemas are enforced only while reading

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:59`
- **What:** Type-decorating byte, text, and Nerdbank serializers check their `TypeFilter`/`TypeSchema` during deserialization but do not check it during serialization. A restricted serializer therefore successfully emits a payload containing a prohibited concrete type that the same serializer refuses to read.
- **Why it matters / failure scenario:** A caller obtains `TypeSchema<PrimitiveOnly>.GetTypeDecoratingSerializer(...)`, serializes an `object` whose runtime type is not primitive, stores or transmits the successful result, and later gets `UnsupportedSerializedType` on round-trip. This silently violates the schema at the producer boundary and can persist unreadable cache/database values.
- **Evidence:** The byte reader enforces `TypeFilter` at `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:47-51`, but its writer emits `value.GetType()` without a filter or assignability check at `src/ActualLab.Core/Serialization/TypeDecoratingByteSerializer.cs:59-69`. The text implementation has the same mismatch at `src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:73-77,82-97`, and the Nerdbank converter checks on read but not write at `src/ActualLab.Serialization.NerdbankMessagePack/Internal/TypeDecoratingUniSerializedNerdbankConverter.cs:37-45,50-66`.
- **Fix:** Before writing any type marker or value, verify both `declaredType.IsAssignableFrom(actualType)` and the configured filter/schema. Apply the identical rule in all three implementations and add round-trip tests showing prohibited types fail at serialization time.

### F8. `ByteString` is mutable through its source buffer despite content-based hashing

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Text/ByteString.cs:50`
- **What:** `ByteString` is documented and shaped as an immutable value/key, but its public constructors and `AsByteString` helpers retain caller-owned arrays and mutable `Memory<byte>` without copying. Equality and hash codes are recomputed from current contents, so mutating the original buffer changes an existing value’s identity.
- **Why it matters / failure scenario:** A caller wraps a byte array, inserts the `ByteString` into a `Dictionary`, `HashSet`, or `ConcurrentDictionary`, and later reuses or clears the array. The key remains in its original hash bucket while `GetHashCode` and equality now describe different bytes, causing failed removals/lookups, duplicate logical keys, and unbounded stale entries; F1 is a concrete in-library network-reachable instance of this contract violation.
- **Evidence:** The type explicitly claims immutability at `src/ActualLab.Core/Text/ByteString.cs:9-18`, but both constructors only assign the supplied storage at `src/ActualLab.Core/Text/ByteString.cs:49-56`, and `AsByteString(Memory<byte>)` forwards mutable memory at `src/ActualLab.Core/Text/ByteStringExt.cs:8-18`. Equality and hashing read the live backing contents at `src/ActualLab.Core/Text/ByteString.cs:108-117`.
- **Fix:** Make normal constructors/factories take an owned copy, and expose any zero-copy form under an explicitly unsafe/borrowed API that cannot be used as a stable hashed key. For internal hot paths, use an owned immutable key type or a canonical string rather than weakening `ByteString` value semantics.

### F9. Encoder output sizing rejects valid non-ASCII text for conforming buffer writers

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Core/Text/EncoderExt.cs:33`
- **What:** `EncoderExt.Convert` requests an output span sized to the number of input UTF-16 code units, even though encoders such as UTF-8 can require up to three bytes per code unit (or four per surrogate pair). `IBufferWriter<byte>` is allowed to return exactly the requested size, so valid text can make `Encoder.Convert` throw “output byte buffer is too small”; encoders that report zero progress instead can leave the loop repeating the same source.
- **Why it matters / failure scenario:** A public caller uses the extension with a minimal conforming `IBufferWriter<byte>` and converts a one-character string such as `€`. The method requests one byte although UTF-8 needs three, breaking serialization of valid Unicode depending on the writer’s growth policy; writers that happen to over-allocate mask the defect.
- **Evidence:** Both overloads call `GetSpan(source.Length)` at `src/ActualLab.Core/Text/EncoderExt.cs:33` and `src/ActualLab.Core/Text/EncoderExt.cs:55`, then retry from `source[charsUsed..]` without a zero-progress guard at `src/ActualLab.Core/Text/EncoderExt.cs:42-46,64-68`.
- **Fix:** Request a checked encoder-specific upper bound (for example, chunked `encoding.GetMaxByteCount`) or retry with a larger minimum span when no progress is made. Add tests using an `IBufferWriter<byte>` that returns exactly `sizeHint` for multi-byte UTF-8 and surrogate-pair inputs.

## Areas examined

- All `.cs` source files under `src/ActualLab.Rpc/Serialization/`, including binary/text argument serializers, message serializers, type markers, and `RpcFrameCodec`.
- All `.cs` source files under `src/ActualLab.Core/Serialization/`, including serializer defaults/adapters, type-decorating serializers and schemas, exception reconstruction, serialized wrappers, and custom MessagePack formatters.
- All `.cs` source files under `src/ActualLab.Serialization.NerdbankMessagePack/`, including serializer construction, RPC registration, serialized wrappers, and every custom converter.
- All `.cs` source files under `src/ActualLab.Core/Text/` and `src/ActualLab.Core/IO/`, including `ByteString`, encoding helpers, MemoryPack text formatters, base64/list/string utilities, path converters, readers/writers, file helpers, and watcher helpers.
- Supporting call paths in RPC configuration, inbound dispatch, WebSocket/pipe buffer lifetime, method polymorphism detection, and selected cache/type callers; relevant tests and installed dependency API documentation were used to prove or disprove candidates.

## Areas NOT examined

- RPC transport/peer lifecycle, routing, authorization, stream state machines, and call registries beyond the narrow supporting paths needed to establish serialization reachability and buffer lifetime; these belong to P1/P2.
- Application/session/Fusion/EF/Redis behavior and the TypeScript client except for targeted searches for serializer consumers; these belong to other partitions.
- Generated `obj`/`bin` artifacts, benchmarks, and test-only implementation details.
- A concrete Json.NET gadget chain for the assemblies of any particular host application; F3 reports the confirmed unrestricted type-resolution boundary and deliberately does not claim RCE.
- End-to-end load/exploit tests or a main-tree build, in accordance with the instruction not to build or modify the working tree for experiments.
