# SPEC — Reconnect proof-of-possession for the RPC WebSocket endpoint (closes A5)

Status: **implementable specification**. The design decisions below were made by the
maintainer and are **not** open for redesign. Where the spec had to choose something the
maintainer did not specify, the choice is marked **[implementer's choice]** with a
recommendation and the alternative. Objections are collected in a single
[Concerns](#15-concerns) section at the end and must not change the implementation.

All line references were verified against the working tree at the time of writing
(branch `master`, commit `ee263d814`).

---

## 1. The problem, verified against the source

### 1.1 The peer is selected purely by an unsigned URL parameter

`src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:35-41`

```csharp
public static RpcWebSocketServerRefFactory RefFactory { get; set; } =
    static (server, context, isBackend) => {
        var query = context.Request.Query;
        var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
        var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
        return RpcRef.NewServer(clientId, serializationFormat, isBackend);
    };
```

`RpcRef.NewServer` (`src/ActualLab.Rpc/RpcRef.Static.cs:33-44`) stores the raw `clientId`
as `HostInfo`; `RpcRef.Initialize()` (`RpcRef.cs:87-104`) derives `Address` from it via
`RpcRefAddress.Format` (`src/ActualLab.Rpc/Internal/RpcRefAddress.cs:15-24`), producing

```
{connectionKind}[.backend].server[.{format}]://{clientId}
```

and `RpcRef.Equals` (`RpcRef.cs:134-137`) is `Address`-based for server refs — deliberately
so, per the comment at `RpcRef.cs:121-131`. **Confirmed:** the `clientId` query value, plus
the `f` value and the backend flag, is the entire peer-selection key. Nothing else is
consulted, nothing is signed, nothing is bound to a session.

### 1.2 Eviction: the incumbent is disconnected before anything is verified

`src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:73-88`

```csharp
Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
var peer = Hub.GetServerPeer(rpcRef);
...
if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
    Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...", rpcRef, peer);
    await peer.Disconnect(cancellationToken).ConfigureAwait(false);
}
```

**Confirmed.** The only gates upstream of this are the `IsWebSocketRequest` check
(`:36-40`) and the `OriginValidator` (`:43-49`, default `AllowAll`, see
`RpcWebSocketServerDefaultDelegates.cs:48-52`). Anyone who knows a `clientId` can loop this
request and keep the victim permanently offline. No credential required.

### 1.3 Takeover: `clientId` is reversibly the peer id, and `Unchanged` skips `Reset()`

- `src/ActualLab.Rpc/RpcClientPeer.cs:20` — `ClientId = Id.ToBase64Url();`
- `src/ActualLab.Rpc/RpcPeer.cs:329-332` — the same `Id` is sent as
  `RpcHandshake.RemotePeerId`.
- `src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:25-33` — `GetPeerChangeKind` returns
  `Unchanged` on `RemotePeerId` equality alone.
- `src/ActualLab.Rpc/RpcPeer.cs:369-377` — `Unchanged` skips
  `await Reset(Errors.PeerChanged())`.
- `RpcPeer.cs:509-524` — `Reset` is what aborts `RemoteObjects`, `SharedObjects` and
  clears `InboundCalls`.

**Confirmed.** An attacker who base64url-decodes the victim's `clientId` back into a `Guid`
and replays it as `RemotePeerId` inherits the victim's server-side peer state: in-flight
inbound calls deliver their results through the peer's *current* transport, and
`SharedObjects.Maintain` keeps pumping the victim's server→client streams onto the
attacker's socket.

### 1.4 Why a URL is the wrong place for a capability

The `clientId` is CSPRNG-derived (`Guid.NewGuid()` → `RpcPeer.cs:65`) and unguessable. The
exposure is not guessing — it is that the value travels in a **URL**, so it lands in
reverse-proxy and CDN access logs, browser history, and `Referer` chains regardless of what
Fusion itself logs. `RpcQuerySanitizer` (`src/ActualLab.Rpc/Internal/RpcQuerySanitizer.cs:16-17`)
already treats `clientId` as sensitive (hashes it) — it just cannot reach infrastructure
outside the process.

### 1.5 The same hole exists on two more endpoints

| Endpoint | File | Ref built at | Peer created at | Incumbent disconnected at |
|---|---|---|---|---|
| ASP.NET Core WebSocket | `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs` | `:55` | `:74` | `:84-88` |
| ASP.NET Core HTTP/2 | `src/ActualLab.Rpc.Server/RpcHttpServer.cs` | `:49` | `:58` | `:62-66` |
| OWIN / .NET Framework | `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs` | `:47` | `:57` | `:67-77` |

The spec below is written for the WebSocket endpoint and then applied verbatim to the
other two (§6). They are **in scope** — shipping the gate on only one of them leaves the
hole open.

---

## 2. Decided design (restated; do not deviate)

| # | Decision |
|---|---|
| D1 | `RpcClientPeer.ClientId` stays exactly as it is, including `= Id.ToBase64Url()`. No decoupling from `Id`. It remains the peer-selection key in the URL. |
| D2 | The **server** mints a per-peer CSPRNG `Secret` and delivers it to the client **inside its `RpcHandshake`** — i.e. over the established WebSocket, never in a URL. |
| D3 | The client keeps the secret **in memory only** and, on every subsequent connect attempt, appends `c` (monotonic counter) and `p` (HMAC proof over it) to the connect URL. |
| D4 | The proof is an **HMAC-SHA256** keyed by the secret — not a bare `SHA256(secret ‖ counter)` — so length-extension is not even a question to answer. |
| D5 | `RpcServerPeer` stores `Secret` and `LastCounter`. A reconnect requires `c > LastCounter` **and** a valid `p`; on success `LastCounter` advances. `p` is compared in constant time. |
| D6 | Verification runs **before everything else** — before `Hub.GetServerPeer`, before `AcceptWebSocketAsync`, before any `Disconnect`. On failure: **403 and return**. No socket accepted, no peer created or touched, incumbent untouched. |
| D7 | A **non-creating** peer lookup is added to `RpcHub` (`TryGetPeer`). |
| D8 | An **unknown `clientId` requires no proof** — there is nothing to hijack yet. The peer is created and a fresh secret issued. |
| D9 | `RpcHandshake.GetPeerChangeKind` is **NOT modified**. See §7. |
| D10 | New server option `RequireReconnectProof`, default `false`. False ⇒ absent `c`/`p` takes the legacy path; present-but-invalid is **still** rejected. Flipped to `true` once clients have shipped. |

---

## 3. Wire shapes

### 3.1 Connect URL

Unchanged shape for a first connect:

```
wss://host/rpc/ws?clientId={ClientId}&f={FormatKey}
```

Every subsequent connect attempt by a client that holds a secret:

```
wss://host/rpc/ws?clientId={ClientId}&f={FormatKey}&c={Counter}&p={Proof}
```

| Param | Type | Encoding | Notes |
|---|---|---|---|
| `clientId` | string | already URL-encoded via `UrlEncoder.Default.Encode` (`RpcWebSocketClientOptions.cs:67`) | unchanged |
| `f` | string | literal | unchanged |
| `c` | `long` | canonical decimal, invariant culture, no sign, no separators, no leading zeros | URL-safe as-is |
| `p` | string | Base64Url (RFC 4648 §5), **unpadded** — alphabet `A–Z a–z 0–9 - _` | URL-safe as-is; never percent-encoded |

Both parameter names are configurable (§5.4). Both are **sent together or not at all**:
a request carrying exactly one of them is malformed and is rejected the same as a bad
proof.

**Query sanitization.** `RpcQuerySanitizer.SanitizeValue`
(`src/ActualLab.Rpc/Internal/RpcQuerySanitizer.cs:55-65`) already redacts any parameter
that is not in `AllowedParameterNames` and not in `HashedParameterNames`, so `c` and `p`
are `<redacted>` in Fusion's own logs with **no change required**. Recommended tweak:
add `"c"` to `AllowedParameterNames` (`:14-15`) — the counter is not a secret and its
value is diagnostically useful. Leave `p` redacted.

### 3.2 `RpcHandshake`

`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:13-21` becomes:

```csharp
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record RpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] Guid RemoteHubId,
    [property: DataMember(Order = 3), MemoryPackOrder(3), Key(3)] int ProtocolVersion,
    [property: DataMember(Order = 4), MemoryPackOrder(4), Key(4)] int Index,
    [property: DataMember(Order = 5), MemoryPackOrder(5), Key(5)] string? Secret = null
) {
```

Three properties of this shape matter and were each verified:

1. **The `= null` default keeps every existing 5-argument construction site compiling** —
   notably `RpcPeer.cs:329-332` and `RpcHandshakeNerdbankConverter.cs:32`.
2. **It is append-only.** Indices 0–4 keep their meaning and their position, which is the
   invariant the remark at `RpcHandshake.cs:8-12` demands.
3. `Secret` is only ever non-null on a **server→client** handshake. A client always sends
   `null`; the server **ignores** any value a client sends. (State this in a code comment —
   it is the one place a reader could imagine a client-supplied secret matters.)

Additionally, add a `PrintMembers` override so the secret cannot leak through
`RpcCallLogger`:

```csharp
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(RemotePeerId)} = {RemotePeerId}, ");
        builder.Append($"{nameof(RemoteApiVersionSet)} = {RemoteApiVersionSet}, ");
        builder.Append($"{nameof(RemoteHubId)} = {RemoteHubId}, ");
        builder.Append($"{nameof(ProtocolVersion)} = {ProtocolVersion}, ");
        builder.Append($"{nameof(Index)} = {Index}, ");
        builder.Append($"{nameof(Secret)} = {(Secret is null ? "null" : "<redacted>")}");
        return true;
    }
```

Rationale (verified, not speculative): `RpcSystemCallSender.Handshake`
(`src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:52-64`) calls
`CallLogger.LogOutbound(call, message)` when logging is enabled;
`RpcCallLogger.LogOutbound` logs `{Call}`; `RpcOutboundCall.ToString()`
(`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:59-79`) embeds
`arguments?.ToString()`, which renders the record — i.e. **raising
`RpcPeer.DefaultCallLogLevel` would print the secret to the log** without this override.

### 3.3 Serialization formats that must be updated in lockstep

`RpcSerializationFormat.All` (`src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:68-74`)
registers 12 formats over 5 distinct serializers. Every one was checked:

| Serializer | Formats | Action required | Compatibility |
|---|---|---|---|
| MessagePack-CSharp (source-gen) | `msgpack5`, `msgpack5c`, `msgpack6`, `msgpack6c` | **None** — regenerated from `[Key(5)]`. | Safe both ways. Verified in the current generated formatter: `Serialize` writes `WriteArrayHeader(5)`, `Deserialize` loops `for (i < length)` with `default: reader.Skip()` — so an old reader tolerates a 6-element array and a new reader tolerates a 5-element one (`Secret` ⇒ `null`). |
| MemoryPack (source-gen) | `mempack5`, `mempack5c`, `mempack6`, `mempack6c` | **None** — regenerated from `[MemoryPackOrder(5)]`. | Safe both ways. Verified: `GenerateType.VersionTolerant` emits a header count + delta table; the `count > 5` throw is commented out and falls into the tolerant `else` branch that reads only as many members as it knows. |
| System.Text.Json | `json5`, `json5np` | **None** — new property serializes automatically; unknown members are ignored on read; the positional-record ctor gets `null` for a missing `secret`. | Safe both ways. |
| Newtonsoft.Json | `njson5`, `njson5np` | **None** — `MemberSerialization.OptOut` picks it up; unknown members ignored on read. | Safe both ways. |
| **Nerdbank.MessagePack** | (opt-in resolver, same `msgpack*` keys) | **Hand edit required** — see below. | Must be edited in the same commit or the Nerdbank wire silently drops the secret. |

`src/ActualLab.Serialization.NerdbankMessagePack/Internal/RpcHandshakeNerdbankConverter.cs`:

```csharp
    public override RpcHandshake? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len < 5)
            throw new MessagePackSerializationException(
                $"Expected 5+ element array for RpcHandshake, got {len}.");
        var guidConverter = context.GetConverter<Guid>(context.TypeShapeProvider);
        var versionSetConverter = context.GetConverter<VersionSet?>(context.TypeShapeProvider);
        var remotePeerId = guidConverter.Read(ref reader, context);
        var remoteApiVersionSet = versionSetConverter.Read(ref reader, context);
        var remoteHubId = guidConverter.Read(ref reader, context);
        var protocolVersion = reader.ReadInt32();
        var index = reader.ReadInt32();
        var secret = len >= 6 ? reader.ReadString() : null;   // NEW
        for (var i = 6; i < len; i++)                          // was: i = 5
            reader.Skip(context);
        return new RpcHandshake(remotePeerId, remoteApiVersionSet, remoteHubId, protocolVersion, index, secret);
    }

    public override void Write(ref MessagePackWriter writer, in RpcHandshake? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(6);                            // was: 5
        ...
        writer.Write(value.Index);
        writer.Write(value.Secret);                            // NEW - writes nil when null
    }
```

Also update the class doc comment at `:7-12` (it says "a 5-element array") to say 6, and
update the `<remarks>` at `RpcHandshake.cs:8-12` to name the TS handshake sender/receiver
explicitly (§8).

### 3.4 Proof construction — exact

```
secret     : string        // the Secret exactly as received in RpcHandshake.Secret
clientId   : string        // the ClientId exactly as it appears (pre-URL-encoding) in the URL
counterText: string        // the decimal counter text, exactly as it appears in the URL

key     = UTF8(secret)                                  // see [implementer's choice] below
message = UTF8(clientId + "\n" + counterText)           // "\n" = U+000A, one byte 0x0A
proof   = Base64Url_NoPad(HMAC_SHA256(key, message))    // 32 bytes -> 43 chars
```

- The separator is a single **LF (0x0A)**, not CRLF. It cannot occur inside a base64url
  `clientId` nor inside a decimal counter, so the encoding is unambiguous.
- Including `clientId` in the message is belt-and-braces (the key is already per-peer) but
  costs nothing and makes a secret useless if it is ever reused across peers.
- **The server MUST compute the HMAC over the `c` value string exactly as it arrived in the
  query**, and parse it into a `long` separately for the ordering comparison. It must NOT
  reparse-and-reformat before hashing — that would introduce a canonicalization mismatch
  for inputs the client never sends anyway, for no benefit.
- `Base64Url_NoPad` is `ActualLab.Text.Base64UrlEncoder.Encode`
  (`src/ActualLab.Core/Text/Base64UrlEncoder.cs:19-50`) — it already maps `+`→`-`, `/`→`_`
  and strips `=`. Output is `[A-Za-z0-9_-]{43}`.

**[implementer's choice] — HMAC key derivation.**
Recommended: **`key = UTF8(secret)`**, i.e. treat the secret as an opaque ASCII token. It
removes an entire class of cross-runtime bugs (base64url variant, padding, `-_` vs `+/`)
from the *key* path, and the token carries a full 256 bits of entropy either way.
Alternative: `key = Base64Url_Decode(secret)` (the raw 32 bytes) — more conventional, and
what a cryptographer would reach for, at the cost of a base64url **decoder** in the TS
client in addition to the encoder it needs anyway. Either is secure; pick one and pin it
with a cross-runtime test vector (§13.4).

### 3.5 Secret shape

```
SecretByteCount = 32                     // 256 bits
Secret          = Base64Url_NoPad(RandomNumberGenerator.GetBytes(32))   // 43 chars, [A-Za-z0-9_-]
```

Minted once per `RpcServerPeer` instance, in its constructor. It is **never** rotated
within the lifetime of a peer instance, and it **never** leaves the process except inside
a server→client `RpcHandshake`.

---

## 4. New shared component: `RpcReconnectProof`

**Placement.** `src/ActualLab.Rpc/Internal/RpcReconnectProof.cs`, namespace
`ActualLab.Rpc.Internal`, **`public static class`**.

It must live in `ActualLab.Rpc` (not in either server package) because:
- the .NET **client** needs `Compute` (`ActualLab.Rpc`, TFMs down to `netstandard2.0`);
- **three** server packages need `Verify`, one of which (`ActualLab.Rpc.Server.NetFx`)
  targets `net48;net472` and therefore binds to the `netstandard2.0` asset.

It must be `public` (not `internal`) because there is **no `InternalsVisibleTo`** in
`src/ActualLab.Rpc/AssemblyAttributes.cs` — verified.

```csharp
public static class RpcReconnectProof
{
    public const int SecretByteCount = 32;
    public const char SeparatorChar = '\n';

    public static string NewSecret();
    public static string Compute(string secret, string clientId, string counterText);
    public static bool Verify(string secret, string clientId, string counterText, string proof);
}
```

`Verify` computes the expected proof and compares it to `proof` in **constant time**, then
returns the result. It must return `false` (never throw) for a malformed `proof`.

### 4.1 TFM constraints (verified against the csproj files)

`ActualLab.Rpc` TFMs (`src/ActualLab.Rpc/ActualLab.Rpc.csproj:4`):
`net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netcoreapp3.1;netstandard2.1;netstandard2.0`

| API | Available from | Fallback needed |
|---|---|---|
| `HMACSHA256` (instance, `ComputeHash`) | netstandard2.0 | none |
| `HMACSHA256.HashData(key, source)` (static) | net6.0 | use the instance API below net6.0 |
| `RandomNumberGenerator.Create().GetBytes(byte[])` | netstandard2.0 | none |
| `RandomNumberGenerator.GetBytes(int)` (static) | netcoreapp3.0 | use `Create().GetBytes(...)` below that |
| `CryptographicOperations.FixedTimeEquals` | netstandard2.1 / netcoreapp2.1 | **required** for `netstandard2.0` (⇒ `net472`/`net48`) |

Constant-time fallback (must be `MethodImpl(NoInlining | NoOptimization)` so it is not
short-circuited):

```csharp
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return false;
        var accumulator = 0;
        for (var i = 0; i < left.Length; i++)
            accumulator |= left[i] ^ right[i];
        return accumulator == 0;
    }
```

Compare the **decoded 32 raw bytes**, not the base64url strings — a length-varying string
comparison leaks through its own length check, and a malformed `proof` must not throw.
Concretely: try to base64url-decode `proof`; on failure return `false`; on success compare
the 32 bytes fixed-time against the locally computed digest.

### 4.2 WASM / Blazor availability

`tmp/review-r2/WASM-crypto-coverage.md` verified, by decompiling the actual browser-wasm
runtime packs, that `SHA256` and `RandomNumberGenerator` are available on **every** TFM
Fusion ships, including Blazor WASM 3.2 (Mono classic, `netstandard2.1`). It also records
that `ActualLab.Core`'s `RandomStringGenerator.Default` static initializer already calls
`RandomNumberGenerator.Create()` unconditionally on every TFM — so any host where crypto is
unavailable already fails at `ActualLab.Core` load.

`HMACSHA256` was **not** covered by that investigation. **Action for the implementer:**
extend `tmp/review-r2/WASM-crypto-coverage.md`'s method (decompile
`System.Security.Cryptography.dll` from the browser-wasm packs; for Blazor 3.2 read
`tools/dotnetwasm/bcl/mscorlib.dll`) to confirm `HMACSHA256` is managed and not
`[UnsupportedOSPlatform("browser")]` on net5.0–net10.0, and that the Mono-classic
`HMACSHA256` does not route through `CryptoConfig`. Expected result: fine — HMAC-SHA256 is
built on the same managed SHA-256 core — but it must be **verified, not assumed**, because
a `PlatformNotSupportedException` here would break every Blazor WASM client's reconnect.

---

## 5. Server-side implementation

### 5.1 `RpcHub.TryGetPeer` — the non-creating lookup

`src/ActualLab.Rpc/RpcHub.cs:23` — `internal ConcurrentDictionary<RpcRoute, RpcPeer> Peers`.
`GetPeer(RpcRoute)` (`:124-147`) is create-if-absent. Add, immediately after
`GetPeer(RpcRef)` at `:109-110`:

```csharp
    public bool TryGetPeer(RpcRef rpcRef, [MaybeNullWhen(false)] out RpcPeer peer)
        => TryGetPeer(rpcRef.Route, out peer);

    public bool TryGetPeer(RpcRoute route, [MaybeNullWhen(false)] out RpcPeer peer)
        => Peers.TryGetValue(route, out peer);

    public bool TryGetServerPeer(RpcRef rpcRef, [MaybeNullWhen(false)] out RpcServerPeer peer)
    {
        peer = TryGetPeer(rpcRef.Route, out var p) ? p as RpcServerPeer : null;
        return peer is not null;
    }
```

**Accessibility: `public`.** Required — the callers are in `ActualLab.Rpc.Server`,
`ActualLab.Rpc.Server.NetFx` and (for tests) `ActualLab.Tests`, and there is no
`InternalsVisibleTo`.

**Why this is a correct lookup.** `Peers` is keyed by `RpcRoute`, and `RpcRoute.Equals`
(`RpcRoute.cs:141-143`) compares `Ref` and `Version`. For a server `RpcRef` the default
`CreateRoute()` (`RpcRef.cs:171-172`) returns `RpcRoute.NewStatic(this)`, whose private
constructor (`RpcRoute.cs:44-45`) leaves `Version = 0`. So two independently constructed
server refs with the same `Address` produce equal, `Version`-0 static routes and hash the
same — verified. `TryGetPeer` therefore finds the incumbent peer without minting anything.

`TryGetPeer` performs **no** `WhenDisposed` check and takes **no** lock — it is a pure
`ConcurrentDictionary` read, which is exactly what a pre-auth gate needs.

### 5.2 `RpcServerPeer` — `Secret` and `LastCounter`

`src/ActualLab.Rpc/RpcServerPeer.cs`. The type is a primary-constructor class
(`:8-9`); add the state and the counter gate:

```csharp
public class RpcServerPeer(RpcHub hub, RpcRoute route, VersionSet? versions = null)
    : RpcPeer(hub, route, versions)
{
    private volatile AsyncState<RpcConnection?> _nextConnection = new(null);
    private long _lastCounter;

    public string Secret { get; } = RpcReconnectProof.NewSecret();
    public long LastCounter => Interlocked.Read(ref _lastCounter);

    public bool TryAdvanceCounter(long counter)
    {
        while (true) {
            var lastCounter = Interlocked.Read(ref _lastCounter);
            if (counter <= lastCounter)
                return false;
            if (Interlocked.CompareExchange(ref _lastCounter, counter, lastCounter) == lastCounter)
                return true;
        }
    }
```

The CAS loop, not `lock (Lock)`, because this runs on the request path before the peer is
otherwise touched and must not contend with `SetNextConnection`'s lock. Two concurrent
connects presenting `c=5` and `c=6` against `LastCounter=4` are correctly serialized: both
`Verify` succeed, both call `TryAdvanceCounter`, and whichever loses the CAS retries and
re-evaluates against the winner's value. `c=5` losing to `c=6` then fails
(`5 <= 6`) and is rejected — correct, because only one of the two can legitimately be the
"newer" reconnect and the client never issues two attempts with an inverted order.

`Secret` is `get`-only and minted in the field initializer, so it exists before any
handshake and is stable for the peer's whole lifetime. It is **not** serialized, **not**
exposed on `RpcPeer`, and **not** present on `RpcClientPeer`.

### 5.3 `RpcPeer` — sending and capturing the secret

Two small virtual hooks, so `OnRun` itself stays as it is.

**(a) Building our own handshake.** `RpcPeer.cs:329-332` currently reads:

```csharp
    var ownHandshake1 = new RpcHandshake(
        Id, Versions, Hub.Id,
        RpcHandshake.CurrentProtocolVersion,
        ++handshakeIndex);
```

Replace with `var ownHandshake1 = CreateHandshake(++handshakeIndex);` and add to
`RpcPeer` (next to `GetServerMethodResolver`, `RpcPeer.cs:561`):

```csharp
    protected virtual RpcHandshake CreateHandshake(int index)
        => new(Id, Versions, Hub.Id, RpcHandshake.CurrentProtocolVersion, index);
```

overridden in `RpcServerPeer`:

```csharp
    protected override RpcHandshake CreateHandshake(int index)
        => base.CreateHandshake(index) with { Secret = Secret };
```

Note the surrounding context: this executes inside the `Task.Run` at `RpcPeer.cs:327-341`,
i.e. on the handshake path, before `Hub.SystemCallSender.Handshake(...)` at `:333`. That is
correct — the secret must be in the message the server sends.

**(b) Capturing the remote's handshake.** Add to `RpcPeer`:

```csharp
    protected virtual void OnHandshake(RpcHandshake handshake)
    { }
```

and call it in `OnRun` immediately after the `RemoteApiVersionSet` null-fixup at
`RpcPeer.cs:350-351` — i.e. after the protocol-version check has passed and before
`GetPeerChangeKind` at `:364`. Override in `RpcClientPeer`:

```csharp
    protected override void OnHandshake(RpcHandshake handshake)
    {
        if (handshake.Secret is { Length: > 0 } secret)
            _secret = secret;
    }
```

Note it **overwrites unconditionally** when a secret is present. That is deliberate: the
server sends its secret on *every* handshake, so a client that missed one self-heals, and a
client that reached a different server instance adopts that instance's secret. A `null`
secret (legacy server) leaves the stored value untouched.

### 5.4 `RpcClientPeer` — secret and counter

`src/ActualLab.Rpc/RpcClientPeer.cs`:

```csharp
public class RpcClientPeer : RpcPeer
{
    private volatile AsyncState<Moment> _reconnectAt = new(default);
    private volatile string? _secret;
    private long _counter;

    public string ClientId { get; protected init; }
    public string? Secret => _secret;                      // in-memory only; never persisted
    public long NextCounter() => Interlocked.Increment(ref _counter);
```

- **In-memory only.** No `IsolatedStorage`, no `localStorage`, no cookie, no file. The
  secret dies with the process — by design (see §9, scenario S3).
- `_counter` starts at 0, so the first `NextCounter()` returns 1 and the server's
  `LastCounter` (0) is always strictly exceeded.
- `NextCounter()` is called **once per connect attempt**, not once per successful
  connection — see §10.

### 5.5 New options

`src/ActualLab.Rpc/Clients/RpcWebSocketClientOptions.cs` (after `:17`):

```csharp
    public string ReconnectProofCounterParameterName { get; init; } = "c";
    public string ReconnectProofParameterName { get; init; } = "p";
```

`src/ActualLab.Rpc.Server/RpcWebSocketServerOptions.cs` (after `:17`) **and**
`src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServerOptions.cs` (same position) **and**
`src/ActualLab.Rpc.Server/RpcHttpServerOptions.cs`:

```csharp
    public string ReconnectProofCounterParameterName { get; init; }
        = RpcWebSocketClientOptions.Default.ReconnectProofCounterParameterName;
    public string ReconnectProofParameterName { get; init; }
        = RpcWebSocketClientOptions.Default.ReconnectProofParameterName;
    public bool RequireReconnectProof { get; init; } = false;
```

This mirrors exactly how `ClientIdParameterName` and `SerializationFormatParameterName`
already default from the client options (`RpcWebSocketServerOptions.cs:16-17`).

### 5.6 URL construction (.NET client)

`src/ActualLab.Rpc/Clients/RpcWebSocketClientOptions.cs:64-69`, replacing lines 67-68:

```csharp
        var queryStart = url.IndexOf('?') < 0 ? '?' : '&';
        url = $"{url}{queryStart}{options.ClientIdParameterName}={UrlEncoder.Default.Encode(peer.ClientId)}"
            + $"&{options.SerializationFormatParameterName}={peer.SerializationFormat.Key}";
        if (peer.Secret is { } secret) {
            var counterText = peer.NextCounter().ToString(CultureInfo.InvariantCulture);
            var proof = RpcReconnectProof.Compute(secret, peer.ClientId, counterText);
            url += $"&{options.ReconnectProofCounterParameterName}={counterText}"
                + $"&{options.ReconnectProofParameterName}={proof}";
        }
        return new Uri(url, UriKind.Absolute);
```

Neither `counterText` (decimal digits) nor `proof` (base64url) requires percent-encoding.
`ConnectionUriResolver` is `Func<RpcClientPeer, Uri?>` — synchronous — and .NET's
`HMACSHA256` is synchronous, so no signature change is needed.

**This costs zero extra round trips.** The client speaks first: `RpcWebSocketClient.ConnectRemote`
(`src/ActualLab.Rpc/Clients/RpcWebSocketClient.cs:19`) resolves the URI and *then* opens the
socket. The proof is computed from state the client already holds. Do **not** design a
server-nonce challenge-response — it would add a round trip for no gain.

### 5.7 The gate — `RpcWebSocketServer.Invoke`

**Exact insertion point: `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs`, between line 55
and line 56** — i.e. immediately after `rpcRef` is built and *before* the serialization
format check.

Why before the format check and not after: the existing unsupported-format path at
`:59-70` **accepts the WebSocket** in order to send close code 4001. Putting the gate after
it would let an unauthenticated request with a bad `f` value obtain an upgraded socket.

```csharp
            rpcRef = RefFactory.Invoke(this, context, isBackend).RequireServer();

            // Runs before GetServerPeer, before AcceptWebSocketAsync and before any Disconnect,
            // so a request that fails the proof cannot evict the incumbent connection,
            // cannot create a peer, and cannot obtain an upgraded socket.
            if (!TryVerifyReconnectProof(context, rpcRef)) {
                Log.LogWarning("'{PeerRef}': Rejected RPC connection - invalid reconnect proof for {Request}",
                    rpcRef, requestDescription);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }

            if (!Hub.SerializationFormats.TryGet(rpcRef.SerializationFormat, out _)) {
```

Note the gate sits inside the existing `try` (`:54`), so an unexpected exception still lands
in the `catch` at `:114` and yields 500 with `webSocket is null` — i.e. fail-closed.

And the method itself, added to `RpcWebSocketServer` (make it `protected virtual` so hosts
can extend it, matching the delegate-heavy style of the surrounding code):

```csharp
    protected virtual bool TryVerifyReconnectProof(HttpContext context, RpcRef rpcRef)
    {
        var query = context.Request.Query;
        var counterText = query[Options.ReconnectProofCounterParameterName].SingleOrDefault() ?? "";
        var proof = query[Options.ReconnectProofParameterName].SingleOrDefault() ?? "";
        var hasProof = counterText.Length != 0 || proof.Length != 0;

        // Unknown clientId: nothing to hijack yet, so no proof is required.
        // The peer is created by the caller and gets a fresh secret.
        if (!Hub.TryGetServerPeer(rpcRef, out var peer))
            return true;

        if (!hasProof)
            return !Options.RequireReconnectProof; // Legacy client
        if (counterText.Length == 0 || proof.Length == 0)
            return false; // Exactly one of the two - malformed
        if (!long.TryParse(counterText, NumberStyles.None, CultureInfo.InvariantCulture, out var counter)
            || counter <= 0)
            return false;
        if (!RpcReconnectProof.Verify(peer.Secret, rpcRef.HostInfo, counterText, proof))
            return false;

        return peer.TryAdvanceCounter(counter); // false = replay
    }
```

Points that are load-bearing:

- **`rpcRef.HostInfo` is the raw `clientId`** — verified at `RpcRef.Static.cs:38-44`. Using
  it (rather than re-reading the query) guarantees the value in the HMAC is the same value
  that selected the peer.
- The unknown-peer check comes **first**, so an unknown `clientId` accompanied by garbage
  `c`/`p` is accepted (D8) rather than rejected. This matters: it is exactly what a client
  that reached a different server replica sends.
- `NumberStyles.None` rejects signs, whitespace and group separators. `counter <= 0` is
  rejected explicitly, so `c=0` can never "succeed" against a fresh peer's `LastCounter=0`.
- The counter is advanced **after** the HMAC verifies, so an attacker cannot burn a
  legitimate client's counter space.
- `TryVerifyReconnectProof` **never touches the peer** other than reading `Secret` and
  CAS-ing `_lastCounter`. On any `false` return, no connection state changed.

### 5.8 What a rejected client observes

The 403 is emitted **pre-upgrade**, so there is no WebSocket and therefore no close code:

- **.NET client** — `ClientWebSocket.ConnectAsync` throws `WebSocketException`; this is
  caught at `RpcWebSocketClient.cs:70-76` and logged as "Failed to connect"; the peer's
  reconnect loop retries with the standard backoff.
- **Browser client** — `error` then `close` with code 1006. Indistinguishable from a
  network failure.

The default reconnect delayer (`src/ActualLab.Rpc/RpcClientPeerReconnectDelayer.cs:21-25`)
is `RetryDelaySeq.Exp(1, 60)` on clients with **no attempt limit**, so a locked-out client
retries indefinitely at ≤60 s intervals until the server peer expires (§9, S6). This is
intentional but has a real cost — see [Concerns](#15-concerns) C2.

### 5.9 Why this cannot be gated on `RpcHandshake.ProtocolVersion`

`ProtocolVersion` lives inside `RpcHandshake` (`RpcHandshake.cs:19`), which is exchanged
over the **already-established** WebSocket at `RpcPeer.cs:327-341`. The proof, by decision
D6, is evaluated from the **URL**, at `RpcWebSocketServer.cs:55`, before
`AcceptWebSocketAsync` at `:92-96`. **At the moment the decision is made, no handshake
exists and none can** — the socket has not been upgraded, so no bytes can flow in either
direction. There is nothing to read a version from.

This is not an implementation detail that could be worked around: any handshake-carried
signal is by construction available only *after* the point where the incumbent has already
been evicted (`:84-88`), which is precisely the vulnerability. Hence the explicit
**`RequireReconnectProof` server option** (D10) rather than a protocol-version gate.

`RpcHandshake.CurrentProtocolVersion` therefore stays at **2**
(`RpcHandshake.cs:22-23`). Adding an optional trailing field is a wire-compatible change on
all five serializers (§3.3), so no version bump is warranted and bumping would break the
`MinimumProtocolVersion`/`CurrentProtocolVersion` window check at `RpcPeer.cs:344-349`
against clients that have not shipped.

---

## 6. The other two endpoints

The identical gate, with identical semantics, goes into:

**`src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs`** — insert between line 47
(`var rpcRef = RefFactory.Invoke(...)`) and line 51 (the format check):

```csharp
        if (!TryVerifyReconnectProof(context, rpcRef)) {
            Log.LogWarning("'{PeerRef}': Rejected RPC connection - invalid reconnect proof", rpcRef);
            return HttpStatusCode.Forbidden;
        }
```

The OWIN query accessor differs: `context.Request.Query[name]` returns `string?` directly
(see `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServerDefaultDelegates.cs:36-37`), so the
`.SingleOrDefault() ?? ""` idiom becomes `?? ""`. Options type is the NetFx
`RpcWebSocketServerOptions`, accessed as `Settings` (not `Options`) — see
`.NetFx/RpcWebSocketServer.cs:25`.

**`src/ActualLab.Rpc.Server/RpcHttpServer.cs`** — insert between line 49 and line 50:

```csharp
            if (!TryVerifyReconnectProof(context, rpcRef)) {
                Log.LogWarning("'{PeerRef}': Rejected RPC connection - invalid reconnect proof for {Request}",
                    rpcRef, requestDescription);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
```

The matching client-side URL change belongs in `RpcHttpClientOptions`' connection-URI
resolver (mirroring §5.6).

**[implementer's choice]** — the three `TryVerifyReconnectProof` bodies are ~15 identical
lines over two assemblies. Recommended: put the whole body in
`RpcReconnectProof.TryVerify(RpcServerPeer? peer, string clientId, string counterText, string proof, bool requireProof)`
in `ActualLab.Rpc`, leaving each server with a 4-line adapter that only extracts the two
query values. Alternative: copy it three times (the `HttpContext`/`IOwinContext` split
already forces some duplication). The shared-helper option is preferred per the project's
reuse rule — the *policy* is exactly the thing that must not drift between endpoints.

**Not in scope:** `RpcClient.ConnectLoopback` (`src/ActualLab.Rpc/RpcClient.cs:34-59`) and
`RpcTestClient` (`src/ActualLab.Rpc/Testing/RpcTestClient.cs:45`) construct the server peer
in-process with no URL. They are unaffected and must stay unaffected.

---

## 7. `GetPeerChangeKind` is NOT modified — say it out loud

`RpcHandshake.GetPeerChangeKind` (`RpcHandshake.cs:25-33`) keeps its current body:

```csharp
    public RpcPeerChangeKind GetPeerChangeKind(RpcHandshake? lastHandshake)
    {
        if (lastHandshake is null)
            return RpcPeerChangeKind.ChangedToVeryFirst;

        return RemotePeerId == lastHandshake.RemotePeerId
            ? RpcPeerChangeKind.Unchanged
            : RpcPeerChangeKind.Changed;
    }
```

**Do not "also" harden it.** The takeover path in §1.3 exists only because an unverified
connection could reach the handshake at all. Once the gate at `RpcWebSocketServer.cs:56`
is in place, every handshake `GetPeerChangeKind` ever sees has already proved possession of
the peer's secret — so `Unchanged` is a true statement about peer identity, and skipping
`Reset()` (`RpcPeer.cs:369-377`) is exactly the behaviour that makes a fast reconnect
preserve in-flight inbound calls and `SharedObjects` streams. Changing it would degrade
every legitimate reconnect to a full `Reset()` and buy nothing.

Add a short comment above the method recording *why* it is safe to compare on
`RemotePeerId` alone, pointing at the gate — otherwise the next reviewer re-files A5.

---

## 8. TypeScript client parity

Verified locations in `ts/packages/rpc/src/`:

| What | Where |
|---|---|
| `RpcClientPeer.clientId` (`= guidToBase64Url(this.id)`) | `rpc-peer.ts:749`, assigned `:808` |
| Default URL builder | `rpc-peer.ts:126-141` (`defaultConnectionUrlResolver`) |
| Resolver type — **already async-capable** | `rpc-peer.ts:124`: `(peer) => string \| Promise<string>` |
| Resolver call site — **already awaited** | `rpc-peer.ts:888`: `const connUrl = await this.connectionUrlResolver(this);` |
| `RemoteHandshake` interface | `rpc-peer.ts:181-186` |
| Inbound handshake parsing (array + Pascal + camel) | `rpc-peer.ts:542-565` |
| Outbound handshake construction | `rpc-system-call-sender.ts:108-128` |
| Base64 helpers | `base64.ts` (`base64Encode` / `base64Decode` — **standard**, not URL-safe) |
| URL log sanitizer | `rpc-peer.ts:144-154` (`sanitizeUrl`) |

`crypto.subtle.sign` being async is **not** a problem: `connectionUrlResolver` is already
declared to return `string | Promise<string>` and is already awaited at line 888, before
`new WebSocket(connUrl)` at `:891-893`. No signature change, no new await point.

### 8.1 Changes

**(1) `rpc-peer.ts:181-186` — extend `RemoteHandshake`:**

```ts
export interface RemoteHandshake {
    RemotePeerId?: string;
    RemoteHubId?: string;
    ProtocolVersion?: number;
    Index?: number;
    Secret?: string;
}
```

**(2) `rpc-peer.ts:549-561` — parse index 5 / the `Secret` key:**

```ts
const handshake: RemoteHandshake = Array.isArray(raw)
    ? {
        RemotePeerId: raw[0] as string | undefined,
        RemoteHubId: raw[2] as string | undefined,
        ProtocolVersion: raw[3] as number | undefined,
        Index: raw[4] as number | undefined,
        Secret: raw[5] as string | undefined,
    }
    : {
        RemotePeerId: (obj.RemotePeerId ?? obj.remotePeerId) as string | undefined,
        RemoteHubId: (obj.RemoteHubId ?? obj.remoteHubId) as string | undefined,
        ProtocolVersion: (obj.ProtocolVersion ?? obj.protocolVersion) as number | undefined,
        Index: (obj.Index ?? obj.index) as number | undefined,
        Secret: (obj.Secret ?? obj.secret) as string | undefined,
    };
```

Both casings are required for the same reason the existing four fields carry both — see the
comment at `rpc-peer.ts:545-547`.

**(3) `rpc-system-call-sender.ts:116-124` — the TS *client* keeps sending 5 elements.**
It never issues a secret, so the array form stays `[peerId, null, hubId, 2, index]` and the
object form stays as-is. This is wire-legal: the .NET MessagePack reader tolerates a
5-element array (§3.3), and the Nerdbank converter's `len < 5` guard is unchanged.
**Only** if the TS `RpcServerPeer` (`rpc-server.ts`, used for in-browser server peers and
`MessageChannel` transports) ever needs to issue secrets would this change — it does not,
and must not, in this spec.

**(4) `rpc-peer.ts:732-813` — secret and counter state on `RpcClientPeer`:**

```ts
    /** Per-peer reconnect secret, delivered by the server in its handshake.
     *  In-memory only - never written to localStorage/sessionStorage/cookies. */
    private _secret: string | undefined;
    private _counter = 0;
```

Store it where the handshake is consumed, in `run()` right after
`this._remoteHandshakeIndex = remoteHandshake.Index ?? 0;` (`rpc-peer.ts:1001`):

```ts
                    if (remoteHandshake.Secret)
                        this._secret = remoteHandshake.Secret;
```

**Lifetime:** the field lives on the `RpcClientPeer` instance. `RpcHub.peers` is a
`Map<string, RpcPeer>` keyed by ref/URL (`rpc-hub.ts:76`), and `close()` removes the peer
(`:144-147`), so the secret dies with the peer object and with the page — matching the .NET
side exactly. **It must not be persisted.** A tab reload mints a new `crypto.randomUUID()`
(`rpc-peer.ts:194`) and therefore a new `clientId`, so there is nothing a persisted secret
could be useful for.

**(5) `rpc-peer.ts:129-141` — the URL resolver becomes async:**

```ts
export const defaultConnectionUrlResolver: RpcConnectionUrlResolver = async peer => {
    const formatKey = peer.serializationFormat.key;
    const proof = await peer.computeReconnectProof();   // undefined when no secret yet
    try {
        const url = new URL(peer.ref);
        url.searchParams.set('clientId', peer.clientId);
        url.searchParams.set('f', formatKey);
        if (proof) {
            url.searchParams.set('c', proof.counter);
            url.searchParams.set('p', proof.proof);
        }
        return url.toString();
    } catch {
        const sep = peer.ref.includes('?') ? '&' : '?';
        let url = peer.ref + sep + `clientId=${peer.clientId}&f=${formatKey}`;
        if (proof)
            url += `&c=${proof.counter}&p=${proof.proof}`;
        return url;
    }
};
```

`URLSearchParams.set` will not mangle either value: the counter is digits and the proof is
base64url, and `URLSearchParams` percent-encodes neither `-` nor `_`.

**(6) The proof function.** **[implementer's choice] — placement.** Recommended: a new
`ts/packages/rpc/src/reconnect-proof.ts` exporting a free function, with
`RpcClientPeer.computeReconnectProof()` as a thin method over it — the crypto is generic
and testable in isolation. Alternative: inline it as a private method on `RpcClientPeer`.
Do **not** put it in `ts/actuallab-core` — it is RPC-protocol-specific, and the project's
"promote to shared" rule applies to genuinely reusable primitives, which this is not.
(The base64url *encoder* below, by contrast, is a fair candidate for `base64.ts`
alongside the existing `base64Encode` / `base64Decode`.)

```ts
const textEncoder = new TextEncoder();

function base64UrlEncode(bytes: Uint8Array): string {
    return base64Encode(bytes).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** HMAC-SHA256 proof of possession of the server-issued reconnect secret.
 *  Mirrors ActualLab.Rpc.Internal.RpcReconnectProof.Compute. */
export async function computeReconnectProof(
    secret: string, clientId: string, counterText: string
): Promise<string> {
    const key = await crypto.subtle.importKey(
        'raw', textEncoder.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
    const signature = await crypto.subtle.sign(
        'HMAC', key, textEncoder.encode(`${clientId}\n${counterText}`));
    return base64UrlEncode(new Uint8Array(signature));
}
```

and on `RpcClientPeer`:

```ts
    async computeReconnectProof(): Promise<{ counter: string; proof: string } | undefined> {
        const secret = this._secret;
        if (!secret) return undefined;
        const counter = (++this._counter).toString();
        return { counter, proof: await computeReconnectProof(secret, this.clientId, counter) };
    }
```

`(++this._counter).toString()` is canonical decimal for all values a client will ever
reach; see §10 for the `Number.MAX_SAFE_INTEGER` note.

**(7) `rpc-peer.ts:144-154` — extend `sanitizeUrl`.** It currently redacts only `session`,
and the connect URL is logged at info level (`rpc-peer.ts:890`). Add `p` to the redaction
list. (`clientId` is also logged unredacted there today — that is finding **D2/D3**, not
this one, but it is a one-line fix in the same function and worth taking.)

**(8) `ts/packages/rpc/README.md` / the handshake doc block at `rpc-peer.ts:11-24`** — record
the 6th handshake field and the `c`/`p` parameters, so the next TS-port sync
(`fusion-ts-sync` skill) does not drop them.

### 8.2 Environments the TS client must keep working in

| Host | `crypto.subtle` | Note |
|---|---|---|
| Browser, secure context (https / localhost) | yes | the normal case |
| Browser, **insecure** context (plain http on a non-localhost host) | **`crypto.subtle` is `undefined`** | see Concerns C4 |
| Node ≥ 18 (load-test harness, `webSocketFactory` at `rpc-peer.ts:758`) | yes, `globalThis.crypto.subtle` | fine |
| Vitest / jsdom (existing test suite) | via Node's webcrypto | fine |

`computeReconnectProof` must therefore be defensive: if `crypto?.subtle` is undefined,
return `undefined` (connect without a proof) and log a single warning, rather than throwing
and killing the reconnect loop. Under `RequireReconnectProof = false` such a client still
works; under `true` it cannot reconnect — which is correct and must be documented.

---

## 9. State machine

Notation: `S` = server, `C` = client. "peer known" means
`Hub.TryGetServerPeer(rpcRef, out _)` is `true` — i.e. an `RpcServerPeer` exists for exactly
this `{connectionKind, backend, server, format, clientId}` address.

### 9.1 The gate, as a decision table

| peer known | `c`/`p` present | `RequireReconnectProof` | proof valid | `c > LastCounter` | Result |
|---|---|---|---|---|---|
| no | — | — | — | — | **Accept.** Peer created downstream; fresh secret issued in the handshake. |
| yes | no | `false` | — | — | **Accept** (legacy path). |
| yes | no | `true` | — | — | **403.** |
| yes | yes | either | no | — | **403.** |
| yes | yes | either | yes | no | **403** (replay). |
| yes | yes | either | yes | yes | **Accept.** `LastCounter := c` before anything else happens. |
| yes | one of the two | either | — | — | **403** (malformed). |

Every **403** row: no `GetServerPeer`, no `AcceptWebSocketAsync`, no `Disconnect`, no
mutation of any peer's connection state. The incumbent connection is not observably
affected at all.

### 9.2 Scenarios

**S1 — First connect (cold client, cold server).**
`C`: `_secret == null` ⇒ URL has no `c`/`p`. `S`: `TryGetServerPeer` misses ⇒ accept
(row 1) regardless of `RequireReconnectProof`. Peer created at `RpcWebSocketServer.cs:74`
with `Secret = NewSecret()`, `LastCounter = 0`. Both sides send handshakes simultaneously
(`RpcPeer.cs:327-341`); `S`'s carries `Secret`. `C` stores it in `OnHandshake` and its
`GetPeerChangeKind` yields `ChangedToVeryFirst` — no `Reset`, nothing to reset.
**In-flight calls/streams:** none.
**Earliest the client can hold a secret:** the moment it has *read* the server's handshake —
i.e. after `reader.MoveNextAsync()` at `RpcPeer.cs:334`. Because both peers send before
either reads (`:333` then `:334`), the client has already sent its own handshake by then.
This is why the first connect can never carry a proof, and why D8 is not optional.

**S2 — Normal reconnect (same client peer object, same server peer).**
`C`: holds `_secret`, `NextCounter()` ⇒ `c = n`. `S`: peer known, proof verifies,
`n > LastCounter` ⇒ accept, `LastCounter := n`, *then* `GetServerPeer` returns the
incumbent and `Disconnect` tears down its stale connection (`:84-88`) as it does today.
Handshake gives `Unchanged` (`RemotePeerId` unchanged) ⇒ **`Reset()` is skipped**, so
in-flight inbound calls and `SharedObjects` streams survive onto the new socket — the
behaviour that is *desirable* here and was the vulnerability before the gate.

**S3 — Reconnect after client restart (secret lost).**
Process/tab restart ⇒ new `RpcClientPeer` ⇒ new `Id` ⇒ **new `ClientId`** (`RpcClientPeer.cs:20`;
`rpc-peer.ts:194,808`). So this is S1 with a fresh `clientId`: peer unknown, accepted, new
peer, new secret. The old server peer is orphaned and expires on its own
(`ServerPeerShutdownTimeoutProvider`, `RpcPeerOptions.cs:54-58` — 3-15 min) then is removed
(`RpcPeer.cs:498-505`, delay `Zero` for server peers per `RpcPeerOptions.cs:60-71`).
**The client can only lose its secret while keeping its `clientId` if a host has installed a
custom `ConnectionUriResolver` that supplies a persistent `clientId`.** Such a host **must
not** enable `RequireReconnectProof` until it also persists (or drops) the secret — call
this out in the option's XML doc.

**S4 — Reconnect after server peer eviction (secret gone server-side).**
The server peer terminated and was removed from `Hub.Peers` (`RpcHub.cs:149-156`), or the
server process restarted. `C` sends `c`/`p` computed against a secret nobody remembers.
`S`: `TryGetServerPeer` misses ⇒ **row 1: accept, ignoring `c`/`p` entirely**. A new peer
with a new secret is created; the handshake delivers it; `C` overwrites `_secret`
unconditionally (§5.3b) and keeps counting from where it was. `C`'s own
`GetPeerChangeKind` sees a different `RemotePeerId` ⇒ `Changed` ⇒ `Reset(PeerChanged)` ⇒
remote objects aborted, outbound calls resent (`RpcPeer.cs:369-377`, `:407-410`). Exactly
today's behaviour.

**S5 — Two clients racing with the same `clientId`.**
Only reachable if the second party learned the `clientId` (URL leak) — the case A5 is about.
- Attacker has **no** secret. If the victim's peer exists: `RequireReconnectProof = true` ⇒
  403, victim's connection **untouched** (the whole point). `RequireReconnectProof = false`
  ⇒ legacy path, the attack still works — which is why the flag must be flipped.
- Attacker replays a **captured URL** including `c`/`p`: covered by S7.
- Attacker uses a **different `f`**: the `Address` differs (`RpcRefAddress.Format`), so a
  *different* peer is selected — the victim's peer is not found, not evicted, not affected.
  A new peer is created (that is A3, §11).
- Two *legitimate* connects racing (client reconnects faster than its previous handshake
  completes): both carry distinct, increasing counters; `TryAdvanceCounter`'s CAS admits
  the higher one and rejects the lower. The rejected one 403s and the client retries with
  a still-higher counter. The existing `IsConnectingOrConnected` teardown at
  `RpcWebSocketServer.cs:84-88` handles the accepted one exactly as today.

**S6 — Locked-out legitimate client (the failure mode to know about).**
Peer known, client's stored secret does not match it (non-sticky load balancing, or a legacy
client after the flag flip). Every attempt 403s. The client retries forever at ≤60 s
(`RpcClientPeerReconnectDelayer.cs:21-25`, no limit). Recovery is by **time**: once the
server peer's shutdown timeout elapses (3-15 min, `RpcPeerOptions.cs:54-58`) and the peer is
removed, the next attempt hits row 1 and succeeds. See Concerns C1/C2.

**S7 — Replayed URL.**
An attacker replays a captured `?clientId=…&c=57&p=…`. The proof verifies (it is genuine),
but `TryAdvanceCounter(57)` returns `false` because the legitimate use already set
`LastCounter = 57`. ⇒ 403, incumbent untouched. A replay is only ever useful in the window
between the URL being captured and the legitimate connect completing the gate — and the
gate advances the counter *synchronously, before any peer state is touched*, so that window
is a few microseconds inside `TryVerifyReconnectProof` and is closed by the CAS. **A
captured URL is single-use and already spent.**

---

## 10. Counter semantics

**Type.** `long` on the server (`RpcServerPeer._lastCounter`, `Interlocked.Read`/`CompareExchange`
require 64-bit alignment — `long` fields are naturally aligned on all supported runtimes).
`long` on the .NET client. `number` in TS.

**Who increments, and when.** The client increments **once per connect attempt**, in the
URL resolver — *not* once per successful connection. This is essential: a connect attempt
that the server accepted at the gate (advancing `LastCounter`) but that then failed before
the handshake (TCP reset, proxy timeout, `ConnectTimeout` at `RpcLimits.cs:14`) has already
burned that counter value. Reusing it would fail `c > LastCounter` and lock the client into
a retry loop it can never escape.

**Persistence.** Neither side persists anything. The client counter lives on the
`RpcClientPeer` instance; the server counter lives on the `RpcServerPeer` instance.

**What resets it.**
- Client counter → 0: only when the `RpcClientPeer` is recreated, which also mints a new
  `ClientId`, which makes the server peer unknown. Consistent.
- Server `LastCounter` → 0: only when the `RpcServerPeer` is recreated (peer expiry, server
  restart, or a first-ever connect). Also consistent: the secret is regenerated at the same
  instant, so nothing computed against the old secret could have been accepted anyway.

There is deliberately **no** scenario in which a counter resets while the paired secret
survives. Enforce it structurally: `Secret` and `_lastCounter` are both instance state of
`RpcServerPeer` with no reset path.

**Far-future counter.** A client that presents `c = long.MaxValue` with a valid proof is
accepted and sets `LastCounter = long.MaxValue`, after which *it* can never reconnect to that
peer again (it would need `c > MaxValue`). This is pure self-harm: producing a valid proof
requires already holding the secret, i.e. already being the legitimate owner of the peer.
**No jump bound is specified**, and none should be added: a `LastCounter + MaxJump` ceiling
would create a *new* lockout mode (a client whose attempts legitimately outran the server's
view) in exchange for defending against an attacker who by construction already owns the
peer. The only bound is `c > 0` (§5.7).

**Wraparound.** Not reachable. The client increments once per connect attempt; at a
sustained (and absurd) 1000 attempts/second, `long` exhausts in ~2.9×10⁸ years. The counter
is never fed from wall-clock time or any external value.

**TS `number` precision.** `Number.MAX_SAFE_INTEGER` is 2⁵³−1 ≈ 9.0×10¹⁵ — at 1000
attempts/second that is ~285,000 years. `(++this._counter).toString()` is exact and
canonical decimal for every value below that; it never produces exponential notation
(JavaScript switches to exponential only at ≥1e21). Safe. Do **not** use `BigInt` here — it
would only complicate the string formatting.

**Why `RpcHandshake.Index` must NOT be reused as the counter.** All four reasons are
independently disqualifying:

1. **It is a per-connection epoch tag, not an authenticator.** Its sole consumer is
   `RpcSystemCalls.Reconnect` (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:59-62`),
   which rejects a `$sys.Reconnect` whose `handshakeIndex` does not equal
   `ownHandshake.Index` — a staleness check for the *current* connection generation.
2. **It is seeded per `OnRun`, not per peer.** `RpcPeer.cs:255-257` initialises
   `handshakeIndex` inside `OnRun`, so it restarts every time the peer's worker restarts,
   and does not survive peer recreation.
3. **It starts at 0 under the production default.** `RpcPeerOptions.UseRandomHandshakeIndex`
   is `false` (`src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:12-15`), so the
   first handshake carries `Index = 1` for every peer, every time — a completely predictable
   value. (The `true` branch is explicitly documented as a *testing* aid.)
4. **It is `int`, and it is on the wire in both directions**, already load-bearing for the
   reconnect protocol on both .NET (`RpcSystemCalls.cs:61`) and TS
   (`rpc-peer.ts:206-209`, `:738-743`). Overloading it with a security meaning would couple
   two protocols that must be able to evolve independently.

The reconnect counter is therefore its own field, with its own lifetime, on its own side of
the connection.

---

## 11. What this does NOT fix

**A3 — an unknown `clientId` still creates a peer without proof.** This is *inherent* to D8
and cannot be otherwise: a first-time client has no secret, so the very first request for any
`clientId` must be admitted. An attacker who generates a fresh random `clientId` per request
therefore still reaches `Hub.GetServerPeer` (`RpcWebSocketServer.cs:74`) and still pins an
`RpcServerPeer` — a background `WorkerBase` plus four
`ConcurrentDictionary(ProcessorCountPo2, 131)` trackers — for the
`ServerPeerShutdownTimeoutProvider` minimum of **3 minutes**
(`RpcPeerOptions.cs:54-58`, `RpcServerPeer.cs:78-86`).

A5 and A3 are genuinely orthogonal: A5 is *"whose peer may this connection attach to"*,
A3 is *"how much may an anonymous request allocate"*. The proof gate answers the first and
does not touch the second. A3 needs a **resource-shaped** fix, out of scope here:

- create the peer only after `AcceptWebSocketAsync` (move `:74` below `:96`), so an
  unfinished upgrade allocates nothing;
- a seconds-scale grace period for a peer that has never completed a handshake, instead of
  the 3-15 minute reconnect window;
- a configurable cap on live server peers and on peers per remote IP, rejecting with 503;
- shape/length validation of `clientId` before it is used as a key.

Note the ordering dependency: **moving peer creation below `AcceptWebSocketAsync` does not
conflict with this spec** — the gate at `:56` uses `TryGetPeer`, which never creates, and
runs before both. The two changes compose.

**Also not fixed:**

- **D3 / D2 (credentials in URLs and logs).** The `clientId` still travels in the URL and
  still reaches proxy/CDN logs and browser history. What changes is that the leaked value
  is no longer *sufficient*: `RequireReconnectProof = true` makes a leaked `clientId`
  useless without the secret, which never enters a URL. That is the point of the design,
  but it is mitigation, not removal.
- **The degenerate empty `clientId`.** An absent `clientId` still defaults to `""`
  (`RpcWebSocketServerDefaultDelegates.cs:38`), collapsing all such clients onto one shared
  server peer. With this spec that peer acquires a secret like any other, so after the first
  such client connects, every *other* empty-`clientId` client is 403'd under
  `RequireReconnectProof = true`. That is arguably an improvement and arguably a new
  failure mode; either way, rejecting an empty `clientId` outright belongs to A3's
  "validate `clientId` shape" item, not here.
- **Session binding.** The proof binds a connection to *the peer that created the secret*,
  not to an authenticated user. A5's own "Fix" note also suggests keying the peer on the
  authenticated identity; that is a separate, larger change and is not attempted here.

---

## 12. Rollout

**Order: server first, then clients, then flip the flag.** The reverse order does not work —
a client that sends `c`/`p` to a server that does not know those parameter names is fine
(they are ignored), but it will never *receive* a secret, so it can never start sending
them, so nothing is gained until the server ships anyway. Server-first also means the
`Secret` field starts flowing immediately, which is what lets clients begin proving as soon
as they update.

| Phase | Server | Client | `RequireReconnectProof` | Behaviour |
|---|---|---|---|---|
| 0 | old | old | n/a | today |
| 1 | **new** | old | `false` | Server issues secrets; old clients ignore the extra handshake field (all five serializers tolerate it, §3.3) and send no `c`/`p` ⇒ legacy path. **No behaviour change.** A5 still open. |
| 2 | new | **new** | `false` | New clients store the secret and send `c`/`p`. A **valid** proof is required whenever `c`/`p` are present, so new clients are already protected against replay. Old clients still work. A5 open only for old clients. |
| 3 | new | new | **`true`** | `c`/`p` mandatory for any *known* `clientId`. A5 closed. |

**When phase 3 is safe.** Only when *every* client that can reconnect to a live server peer
speaks the protocol. Concretely: no old client has held a connection to this deployment
within the last `ServerPeerShutdownTimeoutProvider` window (3-15 min,
`RpcPeerOptions.cs:54-58`) — in practice, "no old client versions in the wild", since a
single stale mobile app is enough to produce an S6 lockout.

**What flipping too early costs.** An old client whose server peer still exists 403s on
every reconnect and recovers only when that peer expires (S6): up to 15 minutes of outage
per network blip, silently, with a `1006`/`WebSocketException` the client cannot
distinguish from a network fault. This is the single highest-risk step in the rollout.

**Make the flag per-deployment, not per-build.** `RequireReconnectProof` is an `init`
property on an options record, so it is already bindable from configuration — document
it as something to flip via config so it can be rolled back without a redeploy.

**Backport/downgrade note.** A server rolled *back* from phase 3 to phase 1 recovers
immediately (the gate stops requiring proofs). A client rolled back to "old" recovers within
one peer-expiry window. There is no persistent state anywhere, which is what makes both
directions safe.

---

## 13. Test plan

New file: `tests/ActualLab.Tests/Rpc/RpcReconnectProofTest.cs`, modelled on
`tests/ActualLab.Tests/Rpc/RpcWebSocketOriginTest.cs` — which already establishes the exact
pattern needed here: drive a real WebSocket connect against a test host, then assert on both
the **HTTP status code** and the **peer count** (`result.StatusCode`, `result.PeerCount`),
proving that a rejected request created no peer. Reuse its `Connect(...)` harness shape and
its `[Trait("Category", "Rpc")]`.

### 13.1 Gate behaviour (the core of the suite)

| # | Setup | Assert |
|---|---|---|
| T1 | Unknown `clientId`, no `c`/`p`, `RequireReconnectProof = true` | connects; peer count 1; handshake carries a non-empty `Secret` |
| T2 | Unknown `clientId`, **garbage** `c`/`p`, `RequireReconnectProof = true` | connects (D8); peer count 1 |
| T3 | Known `clientId`, valid `c`/`p` | connects; `LastCounter == c` |
| T4 | Known `clientId`, valid proof, **`c == LastCounter`** | **403**; peer count unchanged; `LastCounter` unchanged |
| T5 | Known `clientId`, valid proof, **`c < LastCounter`** | **403** |
| T6 | Known `clientId`, **tampered `p`** (flip one char) | **403** |
| T7 | Known `clientId`, `p` computed with the **wrong secret** | **403** |
| T8 | Known `clientId`, `c` present, `p` absent (and vice versa) | **403** |
| T9 | Known `clientId`, `c = "0"`, `"-1"`, `"1e3"`, `" 1"`, `"1 "`, `""` | **403** each (`NumberStyles.None`) |
| T10 | Known `clientId`, no `c`/`p`, `RequireReconnectProof = false` | connects (legacy path) |
| T11 | Known `clientId`, no `c`/`p`, `RequireReconnectProof = true` | **403** |
| T12 | `p` that is not valid base64url (`"!!!"`, wrong length) | **403**, no exception in the log |

### 13.2 The incumbent is untouched — the test that matters most

**T13.** Client A connects with `clientId = X` and completes its handshake. Start a
long-running server→client call and a `SharedObjects` stream on it. Then issue a raw
WebSocket connect for the same `clientId` with a bad proof and `RequireReconnectProof = true`.
Assert **all** of:

- the second request gets **403**;
- `Hub.Peers.Count` is unchanged;
- A's `ConnectionState.Value.IsConnected()` is **still true**, and A never observed a
  `Disconnected` transition (subscribe to `ConnectionState` and assert no transition, rather
  than sampling it once);
- the in-flight call completes normally on A's socket;
- the stream keeps delivering items to A;
- the server peer's `LastCounter` did **not** advance.

Then repeat the same request 50 times in a loop and assert A is still connected — this is
the direct regression test for the eviction DoS in §1.2.

**T14 (takeover).** Same setup; the "attacker" connect uses a *valid* replayed URL captured
from A's last connect (S7). Assert 403 and A untouched. Then, with the gate disabled
(`RequireReconnectProof = false` and no `c`/`p`), assert the attacker *does* take over and
that `GetPeerChangeKind` returns `Unchanged` — i.e. pin the pre-fix behaviour so the test
suite documents what the flag buys. Mark this one clearly as a **negative-control** test.

### 13.3 Client behaviour

- **T15** — After a first connect, `RpcClientPeer.Secret` is non-null and equals the server
  peer's `Secret`.
- **T16** — Successive connect attempts produce strictly increasing `c`; assert via a
  capturing `ConnectionUriResolver`.
- **T17** — A **failed** connect attempt still increments the counter (§10). Force a
  connect failure (unroutable host / server returning 500) and assert the next URL's `c`
  advanced by 1, not 0.
- **T18** — After the server peer is removed and recreated, the client adopts the **new**
  secret from the next handshake and the connection succeeds (S4).
- **T19** — A client with no secret emits a URL with neither `c` nor `p` (not `c=` or `p=`).

### 13.4 Cross-runtime and serialization

- **T20 — pinned test vector.** A fixed `(secret, clientId, counter) → proof` triple
  asserted in **both** `RpcReconnectProofTest` (C#) and a new
  `ts/packages/rpc/tests/reconnect-proof.test.ts`. This is the single test that catches a
  key-derivation or separator divergence between the runtimes; it must exist before either
  implementation is considered done.
- **T21** — `RpcHandshake` round-trips `Secret` on all four binary formats
  (`mempack5/6`, `msgpack5/6`) and both text formats, and a 5-element/absent-`secret`
  payload deserializes to `Secret == null`. Extend the existing handshake serialization
  tests rather than adding a new file where one already covers this.
- **T22** — `RpcHandshakeNerdbankConverter` round-trips `Secret`, **and** reads a
  legacy 5-element array as `Secret == null`, **and** its output is byte-identical to
  MessagePack-CSharp's for the same value (the invariant `RpcHandshake.cs:8-12` demands).
- **T23** — TS `rpc-handshake-casing.test.ts` (existing) extended: array index 5,
  `Secret`, and `secret` all parse; an absent 6th element yields `undefined`.
- **T24** — `RpcHandshake.ToString()` does **not** contain the secret (the `PrintMembers`
  override, §3.2). Cheap, and it is the only thing standing between a raised log level and a
  leaked secret.

### 13.5 Endpoint coverage

- **T25** — Repeat T3/T6/T11/T13 against `RpcHttpServer` (§6). The existing
  `RpcHttpReconnectTest` shows the harness shape.
- **T26** — `.NetFx` OWIN server: at minimum T6 and T11. If the NetFx test project cannot
  host an OWIN server conveniently, a direct unit test of the shared
  `RpcReconnectProof.TryVerify` policy helper (§6, implementer's choice) covers the logic;
  say so explicitly in the test file rather than leaving the gap silent.

### 13.6 Concurrency

- **T27** — 32 concurrent connects with counters `1..32` against one peer: exactly one
  ends with `LastCounter == 32`, every accepted request had a strictly increasing counter,
  and no request is accepted twice with the same counter. Run under
  `Interlocked`-instrumented counters, not timing.
- **T28** — `TryAdvanceCounter` unit test: 1000 threads racing on the same value; exactly
  one returns `true`.

---

## 14. Summary of `[implementer's choice]` items

| Where | Choice | Recommendation | Alternative |
|---|---|---|---|
| §3.4 | HMAC key derivation | `UTF8(secretString)` | base64url-decoded 32 raw bytes |
| §6 | Gate code shared vs. duplicated across 3 endpoints | shared `RpcReconnectProof.TryVerify` policy helper in `ActualLab.Rpc` | copy per endpoint |
| §8.1(6) | TS proof function placement | new `ts/packages/rpc/src/reconnect-proof.ts`; base64url encoder into `base64.ts` | private method on `RpcClientPeer` |
| §3.1 | `RpcQuerySanitizer` treatment of `c` | add `"c"` to `AllowedParameterNames`; leave `p` redacted | leave both redacted (zero change) |
| §5.7 | `TryVerifyReconnectProof` visibility | `protected virtual` on each server | `private static` |

---

## 15. Concerns

Implement the design exactly as specified above. These are recorded for the maintainer's
judgement, not as licence to deviate.

**C1 — Non-sticky multi-replica deployments turn a graceful degradation into a hard
lockout.** Today, if a client's reconnect lands on a different server replica, that replica
has no peer for the `clientId`, creates one, and the client's `GetPeerChangeKind` returns
`Changed` ⇒ `Reset` ⇒ state is rebuilt. Degraded, but working. With this scheme:

1. Client connects to replica A → stores secret `S_A`, counter 10.
2. Blip; reconnect lands on replica B → unknown `clientId` → new peer, secret `S_B`; the
   client **overwrites** `S_A` with `S_B` (§5.3b).
3. Blip; reconnect lands back on A, whose peer is still alive (3-15 min window) with secret
   `S_A` and `LastCounter = 10`. The client proves against `S_B` ⇒ **403**.
4. The client now succeeds only on B and 403s on A until A's peer expires.

Under a round-robin LB with N replicas, a client's reconnect success probability degrades
to ~1/N for up to 15 minutes, with exponential backoff on top. This is a *new* failure mode,
not an amplification of an existing one, and it will present as "intermittent reconnect
failures under load" — the hardest class of bug to attribute. Mitigations, none free:
require sticky routing before phase 3; or shorten `ServerPeerShutdownTimeoutProvider`'s
3-minute floor; or key the client's stored secret by `RpcHandshake.RemoteHubId` and send
the hub id in the URL (which reintroduces a URL-visible selector); or accept it and
document it. **Recommendation: gate phase 3 on sticky routing, and say so in the option's
XML doc.**

**C2 — A rejected client cannot tell it was rejected, so it cannot recover.** Because the
403 precedes the upgrade, the client sees `1006` / a generic `WebSocketException` — the same
thing a dropped Wi-Fi connection produces. It therefore retries the *same* doomed request
forever (no attempt limit in `RpcClientPeerReconnectDelayer`, §5.8) instead of doing the one
thing that would fix it: minting a new `clientId`. There is precedent for a distinguishable
signal in this very file — the unsupported-format path at `RpcWebSocketServer.cs:59-70`
accepts the socket purely to send close code `4001`, and the TS client acts on it
(`rpc-peer.ts:1034-1039`). An analogous `RpcWebSocketCloseCode.ReconnectProofRequired = 4002`
would let the client drop its secret and rotate its peer, collapsing C1's 15-minute lockout
to one round trip. **It is not specified here because D6 explicitly requires 403-and-return
with no socket accepted** — and that is the stronger position against A3, since accepting a
socket to say "no" is exactly the allocation an unauthenticated request should not get.
Flagging the trade-off, not disputing the call.

**C3 — The secret is disclosed to anyone who completes one legacy connect.** During phase 2
(`RequireReconnectProof = false`), an attacker holding a leaked `clientId` connects with no
proof, is admitted on the legacy path, and **receives the peer's secret in the server's
handshake** — after which they can produce valid proofs indefinitely, surviving the phase-3
flip. The window is real but bounded: it requires the `clientId` to leak *and* be exploited
before phase 3, and the victim's peer to still exist. Mitigation, if the window is deemed
unacceptable: rotate `Secret` on every successful *proof-carrying* connect (a rolling
secret), so a secret captured on a legacy connect is invalidated by the legitimate client's
next reconnect. That is a small change — regenerate in `CreateHandshake` when the connect
carried a valid proof — but it interacts with C1 (more secret churn ⇒ more lockouts) and is
therefore **not** specified.

**C4 — `crypto.subtle` is undefined in insecure browser contexts.** Plain-`http` origins
other than `localhost` have no WebCrypto. Such a deployment's browser clients simply cannot
compute a proof and are permanently locked out under `RequireReconnectProof = true`. §8.2
specifies degrading to "connect without proof" plus a warning, which is correct for phase 2
and useless in phase 3. Any deployment serving RPC over plain `http` must either move to
`https` or never reach phase 3. Worth an explicit line in the release notes: a security
feature that silently requires TLS is a support ticket waiting to happen.

**C5 — `HMACSHA256` on Blazor WASM is assumed, not verified.** `WASM-crypto-coverage.md`
verified `SHA256` and `RandomNumberGenerator` across every shipped TFM by decompiling the
actual runtime packs; `HMACSHA256` was outside its scope. The expected answer is "fine", but
if it is wrong on `netstandard2.1` (Blazor 3.2 / Mono classic) the failure mode is a
`PlatformNotSupportedException` inside the URL resolver — i.e. **every** Blazor WASM client
stops reconnecting entirely, not just failing the proof. §4.2 makes this a required
pre-implementation check; it should not be skipped on the grounds that HMAC "obviously"
works.

**C6 — Three endpoints, one policy, and nothing structurally prevents drift.** A5 exists on
`RpcWebSocketServer`, `RpcHttpServer` and the OWIN `RpcWebSocketServer` alike, and each has
its own options record and its own `RefFactory`. If the gate is copy-pasted, a future change
to one will not reach the other two — which is exactly how the three near-identical
"disconnect the incumbent first" comment blocks came to exist in the first place. This is
the reason the shared-policy-helper option in §6 is recommended rather than merely offered.
