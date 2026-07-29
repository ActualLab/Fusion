# Verification: "njson5 / TypeNameHandling.Auto → arbitrary type instantiation (potential RCE)"

**Overall verdict: CONFIRMED** (arbitrary type *instantiation* from a remote peer) —
with the RCE step rated **PARTIALLY CONFIRMED** (conditional on a gadget being present
in the loaded assembly set; not demonstrated in a stock server).

Tested against the **latest published NuGet packages** `ActualLab.Rpc` /
`ActualLab.Rpc.Server` **14.1.78** (net9.0), not the working tree.
Repro projects: `tmp/verify-njson/` (unit-level serializer harness) and
`tmp/verify-njson/e2e/` (real WebSocket server + hostile client).

The reviewer's chain is essentially correct, but my investigation shows it is
**broader and only partly Newtonsoft-specific**:

1. The arbitrary-instantiation core does **not require Newtonsoft** — it also fires on
   the default-registered **System.Text.Json** format (`json5`), because the polymorphic
   type is resolved by ActualLab's own `TextTypeSerializer` header (`/* @=<AQN> */`) via
   `TypeRef.Resolve` → `Type.GetType(...)`, with no allow-list. Newtonsoft's
   `TypeNameHandling.Auto` is an **additional, strictly-worse** vector (nested `$type`
   inside `object`-typed members — see L4a).
2. The stream Batch path is not the only sink: **any RPC method with an `object`-typed
   (or abstract) parameter** is a direct sink on the JSON formats, no stream required.
3. The binary formats (`mempack*`, `msgpack*`) — including the **default `mempack6`** —
   are **not** vulnerable: their closed formatter registries reject unregistered/`object`
   types (L6, L7).

## Per-link table

| Link | Claim | Verdict | Evidence |
|------|-------|---------|----------|
| 1 | Client picks the wire format; all formats incl. a Newtonsoft one registered by default | **CONFIRMED** | `RpcSerializationFormat.All` includes `NewtonsoftJsonV5` ("njson5") and `NewtonsoftJsonV5NP` — `src/ActualLab.Rpc/Configuration/RpcSerializationFormat.cs:27,31,67`. `DefaultFormats = All` — `RpcSerializationFormatResolver.cs:11`. Client emits `?f=<key>` — `Clients/RpcWebSocketClientOptions.cs:68`. Server reads `?f=` — `RpcWebSocketServerDefaultDelegates.cs:31`. **Runtime (e2e):** server's registered set = `json5, json5np, …, njson5, njson5np`; server ACCEPTED client-chosen `njson5` and `json5`. |
| 1b | Server pins format / applies an allow-list | **REFUTED (no pin, no allow-list)** | Server's only gate is `SerializationFormats.TryGet(rpcRef.SerializationFormat, out _)` — i.e. "is it a registered key" — `RpcWebSocketServer.cs:44` (and `RpcHttpServer.cs:49`). Any of the 12 registered keys is accepted. Handshake (`RpcHandshake`, `Infrastructure/RpcHandshake.cs`) carries **no** format field — format is fixed by the client's query string before the socket upgrade. |
| 2 | `NewtonsoftJsonSerializer` uses `TypeNameHandling.Auto`, no `SerializationBinder`/allow-list | **CONFIRMED** | `DefaultSettings { TypeNameHandling = TypeNameHandling.Auto, … }`, no `SerializationBinder` — `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`. **Runtime:** `TypeNameHandling = Auto`, `SerializationBinder = <null>`. |
| 3 | Widening the expected type to `object` disables the polymorphic type allow-check | **CONFIRMED** | Batch path widens to `object`: `arguments = ArgumentList.New<long, object>(0L, null!)` — `Infrastructure/RpcSystemCalls.cs:232`. The bypassed check is `expectedType.IsAssignableFrom(itemType)` in `TextTypeSerializer.ReadDerivedItemType` — `Serialization/Internal/TextTypeSerializer.cs:67`; with `expectedType == object` it is vacuously true. **Runtime L3a:** `<long,object>` + header `/* @=Evil */` → `Evil` **ctor + property setter ran**. **Runtime L3d (control):** the same payload into `<long,Fruit>` (the non-widened Item path) was **REJECTED** ("expected 'Fruit' or its descendant, got 'Evil'"), proving the widening is exactly what defeats the guard. |
| 4 | Newtonsoft `Auto` honors `$type` on the `object`-typed path (declared ≠ actual) | **CONFIRMED** | With `Auto`, `$type` is honored when declared type ≠ concrete type. **Runtime L4c:** raw read of `{"$type":"…Evil…"}` into declared `object` constructed `Evil`. **L4a:** nested `$type` inside an `object`-typed property of a concrete argument constructed `Evil` under `njson5`. **L4a2:** the identical nested payload under `json5` (System.Text.Json) did **not** run Evil's ctor — so the nested-member vector is Newtonsoft-specific, while the top-level widened-object vector is common to both JSON formats. |
| 5 | Newtonsoft is a hard dependency of the default server package (no opt-in) | **CONFIRMED** | `ActualLab.Core.csproj` has an unconditional `<PackageReference Include="Newtonsoft.Json" />`; `ActualLab.Rpc` → `ActualLab.Core`; `ActualLab.Rpc.Server` → `ActualLab.Rpc`. `njson5` is registered with zero extra configuration. |

Supporting facts:
- `IsPolymorphic(type) => (type.IsAbstract || type == typeof(object)) && RpcSerializableAttribute.Get(type) is null` — `Serialization/RpcArgumentSerializer.cs`. So `object` and any attribute-free abstract type are "polymorphic".
- `TypeRef.Resolve` = `Type.GetType(aqn, false, false)` with **no** allow-list; the source itself comments `// Potential memory le[a]k / attack vector` — `src/ActualLab.Core/Reflection/TypeRef.cs:97`.
- Direct `object`-param sink: `RpcMethodDef.HasPolymorphicArguments = ParameterTypes.Any(IsPolymorphic)` — `Configuration/RpcMethodDef.cs:70`; consumed in `RpcInboundCall.DeserializeArguments` at `Infrastructure/RpcInboundCall.cs:229-238`, deserializing with the real (e.g. `object`) parameter type as `expectedType`.

## What an attacker actually gets

- **Guaranteed:** arbitrary **type instantiation** of any type resolvable by
  `Type.GetType` in the server process (default ctor + JSON-mapped property setters run),
  as long as the connection uses a JSON format (`json5` or `njson5`) **and** a reachable
  sink exists: an RPC method with an `object`/abstract parameter, or a client-supplied
  `RpcStream<T>` with polymorphic `T` (the Batch path widens even a concretely-typed
  `RpcStream<AbstractType>` to `object`). Verified end-to-end that the server honors a
  client-chosen `njson5`; verified at the serializer level that the widened-`object`
  path constructs an arbitrary `Evil` type whose ctor and setter execute.
- **Not automatic RCE.** This is CWE-502 (deserialization of untrusted data). Reaching
  code execution requires a **gadget** type in the *loaded* assembly set. In L5/L5b I
  instantiated `System.Net.Http.HttpClient` and `System.Data.DataSet` from the wire, but
  a stock headless ASP.NET Core server does **not** load the classic weaponizable gadgets
  (e.g. WPF `ObjectDataProvider` in PresentationFramework). Newtonsoft's nested-`$type`
  support (L4a) makes the JSON payloads flexible enough to drive most known gadget chains
  **if** their assemblies are present, which is deployment-dependent. Absent a gadget, the
  realistic impact is still serious: DoS (instantiate expensive/throwing/finalizer-heavy
  types), and side effects from attacker-chosen ctors/setters (SSRF, file/handle access).
- **Default deployments are materially safer than the headline suggests:** the default
  format is `mempack6`, which rejects this entirely (L6/L7). The attack requires the
  client to *choose* a JSON format, which the server currently permits unconditionally.

## True severity

**High** (CWE-502, remotely reachable, no auth beyond being an accepted RPC peer),
escalating to **Critical** only in deployments whose loaded assemblies contain a usable
deserialization gadget. The severity is bounded by two real preconditions: (a) the app
must expose a polymorphic sink (`object`/abstract parameter or polymorphic client stream),
and (b) the attacker relies on the server accepting a client-selected JSON format.

## Minimal fix

The root cause is **unrestricted type resolution during polymorphic argument
deserialization** — not Newtonsoft alone. A complete fix has two parts:

1. **Allow-list the polymorphic type resolution used by the RPC text formats.** Gate
   `TextTypeSerializer.ReadDerivedItemType` / `FromBytes` (and the `ByteTypeSerializer`
   equivalents) behind a configurable `Func<Type,bool>` type filter, defaulting to
   "types reachable from a registered RPC contract" (e.g. `[RpcSerializable]`, declared
   argument/result types, and their assignable descendants). Critically, the filter must
   run against the **actual resolved type**, independently of whether `expectedType` was
   widened to `object` — this closes both the stream-Batch widening (RpcSystemCalls.cs:232)
   and the `object`-parameter sink. `TypeDecoratingText/ByteSerializer` already carry a
   `TypeFilter` hook (currently defaulting to `_ => true`); the RPC arg path should adopt
   the same, with a safe default.
2. **Harden the Newtonsoft settings** to remove the nested-`$type` vector: set a
   restrictive `SerializationBinder` on `NewtonsoftJsonSerializer.DefaultSettings`
   (or `TypeNameHandling = None`, given ActualLab supplies its own type header), so
   `Auto` can no longer resolve arbitrary nested `$type`. Optionally, do **not** register
   `njson5`/`njson5np` in the default format set (opt-in only).

A server-side allow-list of acceptable `?f=` formats (e.g. restrict to `mempack6`/`msgpack6`
unless JSON is explicitly enabled) is a useful defense-in-depth but is **not** sufficient
alone, since `json5` shares the same `TextTypeSerializer` resolution weakness.

## Repro artifacts
- `tmp/verify-njson/Program.cs` — serializer-level harness (L1-L7). Key results:
  L3a widened-`object` constructs `Evil` (ctor+setter ran); L3d Fruit-typed path rejects
  the same payload; L4c/L4a Newtonsoft nested `$type`; L6/L7 binary formats reject.
- `tmp/verify-njson/e2e/Program.cs` — live `WebApplication` RPC server; a client pinned to
  `RpcSerializationFormatResolver.Default with { DefaultFormatKey = "njson5" }` connects and
  is served, proving the server honors a client-selected Newtonsoft format with no allow-list.
