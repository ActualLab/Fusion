# Changelog

All notable changes to **ActualLab.Fusion** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

`+HexNumber` after version number is the commit hash of this version.
It isn't included into the NuGet package version.

To track updates in real time, see ["Fusion/🎉Releases" on Voxt.ai](https://voxt.ai/chat/s-1KCdcYy9z2-uJVPKZsbEo).


## 14.2.39+3b5885367 | npm: 14.2.23

Release date: 2026-07-30

**.NET 11 support.** Every package gains a `net11.0` asset, and nothing else moves:
no target framework was dropped and no third-party dependency range changed on any
existing one, verified by diffing the packed `.nuspec` files against `14.2.34`. If you
stay on your current .NET, upgrading to this release is a no-op &mdash; the only new
thing is a target framework you aren't using yet. NuGet-only release (npm stays at
`14.2.23`).

### Added

- **`net11.0` target framework across the board**, alongside `net10.0` down to
  `netstandard2.0`. Its assets depend on the .NET 11 preview packages
  (`11.0.0-preview.6.26359.118` for ASP.NET Core, `Microsoft.Extensions.*` and
  EF Core where applicable), so treat `net11.0` as preview-grade until .NET 11 ships.
  `Npgsql.EntityFrameworkCore.PostgreSQL 11.0.0-preview.6` backs
  `ActualLab.Fusion.EntityFramework.Npgsql` there.
- **`EnableDotNet11` build switch** (`TargetFrameworks.props` in the repository root).
  It's `true` by default, which makes `net11.0` the primary target for the whole
  repository; `-p:EnableDotNet11=false` puts everything back on `net10.0`. Relevant
  only when building Fusion from source, not to package consumers.

### Changed

- **`ActualLab.Fusion.EntityFramework` no longer pins EF Core on `net10.0`/`net11.0`** &mdash;
  it declares an open `[10.0.0,)` range, so the app picks EF Core 10 or 11. The package
  is provider-agnostic, so it has no reason to decide for you. Provider packages can't
  offer the same freedom: `Npgsql`, `Sqlite`, `SqlServer` and `Pomelo` each cap EF Core
  to a single major version, so whichever provider you reference decides your EF Core
  version.

### Infrastructure

- Fixed the `pack` build target consuming a `project.assets.json` left behind by a
  separate `restore` target. Inside the single-process publish chain that could be a
  single-target-framework restore, which failed every inner build with `NETSDK1005`
  ("Assets file doesn't have a target for 'net5.0'"). `pack` now runs its own restore,
  so the assets can't disagree with the target frameworks being built.


## 14.2.34+ae17db7ca | npm: 14.2.23

Release date: 2026-07-29

The outcome of a second full security and severe-bug review of the library: 120
findings, of which both CRITICALs and all 35 HIGHs are now closed. Almost
everything here is a fix on the path from a wire message to a resolved type, an
allocation, or an authorization decision &mdash; **treat this as a security
release and read the breaking changes before upgrading.** The TypeScript client
gets its own round of fixes and one breaking change, so npm moves to `14.2.23`
alongside NuGet.

### Breaking Changes

- **The backend-service / backend-command gate actually works now.** `RpcMethodDef`
  compared the command's interface against `"ActualLab.CommandR.IBackendCommand"`,
  but `IBackendCommand` lives in `ActualLab.CommandR.Commands` &mdash; so the
  ordinal comparison never matched and `IsBackend` collapsed to the service-level
  flag alone. The marker had been decorative in every release since v10.3. An app
  that was (unknowingly) invoking an `IBackendCommand` from a non-backend peer
  will now be rejected. *Migration: audit which of your commands carry
  `IBackendCommand` and route those calls through a backend peer &mdash; or drop
  the marker if the command was never meant to be backend-only.*
- **`IKeyValueStore` is an `IBackendService`.** It was registered as an ordinary
  frontend RPC service, so with the gate above dead its `Set`/`Remove` were
  reachable from any peer &mdash; a shipped cross-user data read and write.
  *Migration: client-side code must use `ISandboxedKeyValueStore`, which is what
  the session- and user-scoped key constraints exist for.*
- **`njson5` and `njson5np` are denied to clients by default.** The server had no
  format allow-list at all &mdash; its only gate was "is this key registered" &mdash; so
  a client could pin `?f=njson5` and run Json.NET with `TypeNameHandling.Auto` and
  no binder. Client-selectable formats are now gated on all three endpoints.
  *Migration: `RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys =
  ImmutableHashSet<string>.Empty;` at startup restores the old behavior.*
- **Session options moved from `ImmutableOptionSet` to `PropertyBag`.**
  `ImmutableOptionSet` stores its values as `NewtonsoftJsonSerialized<object>`, so
  deserializing one runs Json.NET with `TypeNameHandling.Auto` on wire data &mdash;
  reproduced through the *default* `mempack6` and `msgpack6` round-trips, because
  the binary formatter only ever sees strings and Newtonsoft runs inside the type's
  own property. `SessionInfo.Options`, `AuthBackend_SetSessionOptions.Options`,
  `AuthBackend_SetupSession.Options` and `DbSessionInfo.Options` are now
  `PropertyBag`, which carries the type out of band. Member ordinals are unchanged,
  but the payload at the slot is not: `mempack6` tolerates empty options in both
  directions, `msgpack6` does not even when empty, and populated options are
  incompatible in every format. Legacy `_Sessions.OptionsJson` rows read back as
  empty options &mdash; accepted, contained silent data loss. *Migration: change your
  `ImmutableOptionSet` variables to `PropertyBag`; its type-keyed `Set<T>(value)` /
  `Get<T>()` become `KeylessSet<T>(value)` / `KeylessGet<T>()`, and string-keyed access
  is `Set(key, value)` / `Get<T>(key)`. Upgrade clients and servers together if you
  use `msgpack6`.*
- **`OptionSet` and `ImmutableOptionSet` are `[Obsolete]`.** *Migration: use
  `MutablePropertyBag` and `PropertyBag`.*
- **Every RPC size ceiling is explicit, enforced both ways, and much lower.** A
  single pre-handshake WebSocket message could pin ~136 MB (allocated as 256 MiB)
  per connection, before any authentication; sizes previously fell out of whatever
  the buffer happened to grow to. Header and method-ref limits go 64 KiB &rarr;
  1 KiB, the frame limit 33,489,152 &rarr; 16,711,680, argument data 16 MiB &rarr;
  15.5 MiB, and the worst-case text envelope 12,261,961 &rarr; 244,297. An
  over-limit header, method reference or payload is rejected on read and the
  message is dropped **without an error reply**, so the remote sees the call as
  never answered. Nothing in Fusion comes near these limits &mdash; the longest real
  method reference measured anywhere is 59 bytes. *Migration: none expected; if you
  genuinely move payloads above 15.5 MiB, raise
  `RpcByteMessageSerializer.Defaults.MaxArgumentDataSize` and its text counterpart,
  and see [Size Limits](PartR-Serialization.md#size-limits) for what else must move
  with it.*
- **`ApiMap.Empty` and `ApiSet.Empty` are removed.** Both are mutable collections,
  so a shared static instance was a corruption hazard (`User.ToClientSideUser()`
  was writing into the process-global one); turning it into a fresh-instance
  property only made `Empty` lie in the other direction. *Migration: write `new()`.*
- **`Session.ToString()` is redacted** to `{4-char Id prefix}:{Hash}`. It used to
  return the raw Id &mdash; a bearer credential &mdash; so anything that formatted a
  `Session` leaked it. *Migration: use `session.Id` where the actual value is
  needed; nothing about the wire representation changed.*
- **TypeScript: `resolveStreamRefs` is replaced by `toRpcStream`,** and stream
  references nested in ordinary results are no longer converted automatically. The
  old inference replaced any string of 4&ndash;6 comma-separated parts whose first
  three parsed as integers with a live `RpcStream`, so `"1,2,3,4"`, a CSV row or a
  coordinate tuple all became streams. TypeScript has no typed method definitions,
  so the heuristic is unfixable in principle and the wire format can't change.
  *Migration: `toRpcStream(value, peer)` at the call site; it returns `null` when
  the value isn't a stream reference, and unlike the old inference it also handles
  the object shape the binary formats use.*
- **The plugin cache moved and changed format.** `FileSystemPluginFinder` cached
  discovered plugins under `Path.GetTempPath()` &mdash; a path any local user could
  pre-create &mdash; and deserialized it with `TypeNameHandling.Auto` and no binder,
  which is local code execution out of the box. The cache now lives in a per-user
  location (`Environment.SpecialFolder.LocalApplicationData` &rarr; `ActualLab/…`;
  a group- or other-writable directory is refused on Unix), round-trips through
  System.Text.Json with no `$type`, and re-validates every cached type against the
  exact predicate scanning applies. *Migration: none &mdash; the cache key version was
  bumped, so old entries are simply ignored. Override the location via
  `FileSystemPluginFinder.Options.CacheDir` if you relied on the old one.*

- **`INotLogged` is gone.** It suppressed a command's contents everywhere it could
  be logged, which cost four near-identical branches at the log sites and threw
  away the whole command even when one member was sensitive. A command that carries
  a credential now redacts itself instead, so the log still says which command
  failed and what else was in it. `CompletionProducer.Options.IgnoreNotLogged` goes
  with it &mdash; it existed only to opt back out of the suppression. *Migration:
  implement `ISanitized` and override `ToString()`/`PrintMembers` on the command,
  masking the members that need it &mdash; see `AuthBackend_SetupSession`. The
  interface is gone, so every declaration that named it stops compiling &mdash; but
  the obvious fix is to delete the marker, and that alone turns full logging back
  on for a command that was marked precisely because it carries something
  sensitive. Redact the command before you drop the marker, not after.*
- **`ApiMap<TKey, TValue>.Empty` and `ApiSet<T>.Empty` are gone.** Both were
  `static readonly` instances of types that derive from `Dictionary<,>` and
  `HashSet<>` &mdash; mutable, shared process-wide. `User.ToClientSideUser()` masked
  a user's identities by starting from `ApiMap<UserIdentity, string>.Empty` and
  calling `TryAdd` on it, so the one shared map accumulated every masked user's
  identity schemas and every masked `User` pointed at it: user A's client-side
  user listed user B's identity schemas, and those are serialized to the client.
  Concurrent request threads mutating that `Dictionary` unsynchronized could also
  corrupt its buckets, and it is enumerated on every `User` serialization.
  Returning a fresh instance from a property would have fixed the sharing while
  leaving the shape that invites it &mdash; and `Dictionary`/`HashSet` mutators are
  non-virtual, so a genuinely frozen shared instance can't be enforced through the
  base-class entry points on every target framework. The members were removed
  outright instead. `Empty` was a static member, not a wire member, so the
  serialized shape of both types is unchanged. *Migration:
  `ApiMap<TKey, TValue>.Empty` &rarr; `new ApiMap<TKey, TValue>()`, `ApiSet<T>.Empty`
  &rarr; `new ApiSet<T>()`. Every use site is a compile error, so nothing changes
  silently. `ApiArray`, `ApiOption`, `ApiNullable` and `PropertyBag` are immutable
  structs and `ApiList` has no `Empty`, so these two types were the only ones
  affected.*
- **`RedirectUrlChecker` is now `RedirectUrlHelper`.** The delegate could only
  accept or reject, so a rejected `returnUrl` was silently replaced with `"/"` &mdash;
  and since `IUrlHelper.IsLocalUrl` rejects every absolute URL, including a
  same-origin one addressing a page of this very app, that quietly broke the
  sign-in and sign-out popups and the render-mode switcher. The replacement is a
  DI-registered class whose `Normalize` reduces an accepted URL to a relative one
  rather than discarding it, with `AllowedHosts` and `MustStripHost` as init-only
  options. *Migration: replace a custom `RedirectUrlChecker` registration with a
  `RedirectUrlHelper` subclass overriding `Check`;
  `FusionWebServerBuilder.DefaultRedirectUrlCheckerFactory` is now
  `DefaultRedirectUrlHelperFactory`. An absolute `returnUrl` that used to end up at
  `"/"` now lands on its own path.*
- **`Session.ToString()` and `User.ToString()` no longer print their secrets.**
  `Session` renders a 4-char prefix plus a hash; `User` prints its claim *names*
  and identity *schemas* instead of their values &mdash; `User.Identities` maps an
  identity to that provider's secret, and a whole `User` travels inside
  `AuthBackend_SignIn`. Serialization is untouched, so nothing on the wire or in the
  database changes. *Migration: none, unless you parsed either `ToString()`. Wrap a
  diagnostic in `Sanitization.Suspend()` to get the full contents back.*

### Added

- **A sanitization framework** (`ActualLab.Compliance`), for keeping secrets out
  of logs without keeping them off the wire. `SanitizedString<TSanitizer>` is a
  drop-in for a `string` member that masks itself when rendered while serializing
  byte-identically, `ISanitized` marks a type whose `ToString()` does the same,
  and `Sanitizers` supplies the policies (`Hidden`, `LengthHint`,
  `PrefixAndLengthHint`, `Fingerprint`, `SessionString`, `UriQuery`,
  `RpcRequestQuery`). Masking is off until a scope turns it on, and
  `AddSanitizingLoggerFactory()` is what opens one around each log call &mdash; so
  secrets are masked in the log and nowhere else. See
  [Sanitization](PartSan.md).
- **RPC request queries are sanitized by an allow-list.** `RpcQuerySanitizer` is
  replaced by `Sanitizers.RpcRequestQuery`, which is deny-by-default: a parameter
  is readable only if listed, so one added later can't start leaking a credential
  because nobody remembered to mask it. Session parameters render as
  `Session.ToString()` does, everything else as a prefix and length bucket.
- **Reconnect proof of possession** (.NET and TypeScript). The connect URL's
  `clientId` used to be the whole peer-selection key, and the incumbent connection
  was disconnected before anything was verified &mdash; so anyone who read a
  `clientId` out of a proxy log, browser history or a `Referer` chain could keep
  the victim permanently offline, or inherit its server-side peer state. The
  server now mints a per-peer CSPRNG secret and ships it in a new `RpcHandshake.Secret`
  member, over the established connection; subsequent connects carry `c` (a
  monotonic counter) and `p` (`Base64Url_NoPad(HMAC_SHA256(secret, clientId + "\n" + c))`).
  Verification runs before the peer lookup, before the socket is accepted and before
  any disconnect, so a failed proof is a bare 403 with the incumbent untouched, and
  all three server endpoints share one policy function.
  `RpcWebSocketServerOptions.RequireReconnectProof` defaults to `false`, yet the gate
  still protects by default: a peer that has proven once may not downgrade to a
  no-proof reconnect. See [`RequireReconnectProof`](PartR-CO.md#requirereconnectproof).
- **`RpcWebSocketServerOptions.OriginValidator`** plus
  `RpcWebSocketServerOriginValidators.AllowAll` / `SameOrigin` / `Allow(origins)`.
  The WebSocket upgrade is exempt from CORS and from preflight, so a CORS policy
  never protected the RPC endpoint against cross-site WebSocket hijacking of a
  cookie-bound session. The default stays `AllowAll` so nothing breaks on upgrade;
  **`WarnOnUnvalidatedOrigin` (default `true`)** logs one startup warning when
  nothing validates the `Origin`, from both the ASP.NET Core and the OWIN server.
- **`RpcLimits.CallCountLimit` and `RpcLimits.ObjectCountLimit`** &mdash; per-peer
  backstops on inbound+outbound calls and on shared+remote objects, checked once
  per `ObjectReleasePeriod` and resetting the peer when exceeded. The call cap
  defaults to `int.MaxValue` on purpose (a Fusion server legitimately holds one
  inbound call per live subscription); the object cap defaults to 64K. `NoWait`
  calls are invisible to the cap.
- `Session.Sha256Hash` &mdash; a strong, collision-resistant digest of the session id,
  cached as 32 bytes. `Session.Hash` keeps its legacy XxHash3 value because it's on
  the wire via `SessionAuthInfo.SessionHash` and `Auth_SignOut.KickUserSessionHash`.
- `PruningCache<TKey, TValue>` &mdash; a capacity-bounded cache with lock-free reads
  that amortizes its size check through a `StochasticCounter`, so the hot path stays
  a plain `ConcurrentDictionary` read. It now bounds both `TypeRef` caches and the
  legacy method-resolver cache.
- `Symbol`, `Moment`, `ByteString`, `FilePath` and `TypeRef` can be used as
  System.Text.Json dictionary keys &mdash; STJ routes non-string keys through
  `ReadAsPropertyName`/`WriteAsPropertyName`, whose defaults throw.
- `VersionSet` filtering extensions, and clamped-capacity `GetMemory`/`GetSpan`/
  `EnsureCapacity` overloads on `RefArrayPoolBuffer<T>`, mirroring `ArrayPoolBuffer<T>`.
- TypeScript: `hub.systemCallSender.errorFilter` with a ready-made
  `genericErrorFilter`. Handler exception messages were forwarded to the remote peer
  verbatim, and Node error text routinely embeds absolute paths and connection
  strings. The default stays pass-through, so nothing changes unless you opt in.

### Fixed

- Sign-in, sign-out and the render-mode switch all redirected to the app's home
  page. `fusionAuth.js` built its `returnUrl` with
  `new URL(..., document.baseURI).href` and `RenderModeHelper` passed
  `NavigationManager.Uri`, both absolute &mdash; and the server's redirect check
  rejects absolute URLs, so all three silently fell back to `"/"`. The visible
  symptom differed per flow because `ServerAuthHelper` gates them differently:
  `AllowSignIn` is `AllowAnywhere`, so sign-in worked and only its popup failed to
  close, while `AllowChange` and `AllowSignOut` require the `/fusion/close` request
  that never arrived &mdash; so sign-out cleared the ASP.NET cookie while leaving the
  Fusion session authenticated, and "sign out everywhere" then churned sessions as
  `IsSignOutForced` invalidated each one `SessionMiddleware` minted. Both callers
  now send relative URLs, and `RedirectUrlHelper` recovers an absolute one anyway.
- Publication ordering on lock-free read paths. `Computed.TrySetOutput` stored the
  `Consistent` flag *before* `_output`, so a reader that saw `Consistent` could read
  a default output and return `null` from a compute method that had succeeded &mdash;
  a store-store ordering error x64 doesn't mask. `LazySlim` (all three arities),
  `DbHub`, `RedisTaskSub` and `RpcConfiguration.Freeze` had broken double-checked
  locking: a lock orders nothing on its own, because `Monitor.Exit` fences before it
  exits, leaving the constructor's writes and the reference store mutually
  unordered. `RandomInt32/64Generator` read their shared buffer after releasing the
  lock, so two concurrent callers could get the same value from a generator whose
  point is cryptographic randomness.
- `RpcPeer.SetConnectionState` released its lock twice. The "state is already final"
  early return sat inside the `try`, so it exited the lock and then ran the
  `finally`, which performed the whole transition &mdash; publishing the transport,
  marking states, cancelling the old reader token source &mdash; with no lock held on a
  path where nothing had transitioned, then exited the lock again.
- Wire-driven type resolution and its caches. Type markers were cached under a
  `ByteString` key that aliased the pooled transport buffer, so the next frame
  overwrote it &mdash; the entry became permanently unreachable and the correct marker
  permanently missed, growing under ordinary polymorphic traffic rather than only
  under attack. `TypeRef.ResolveCache` was unbounded and keyed by the raw wire
  spelling, which `Type.GetType` accepts in many variants, so one type could occupy
  many permanent entries; only the canonical spelling is cached now and failures
  never are. `ExceptionInfo` resolved the remote type *before* correlating
  `$sys.Error` with an outbound call, so an uncorrelated error still reached
  `Type.GetType`; the error is correlated first, and type names get structural
  pre-checks before any assembly probing.
- Polymorphic reading is no longer reachable through a widened slot type.
  `$sys.B` replaced the declared argument type `T[]` with `object` whenever `TItem`
  was polymorphic, purely to reach the polymorphic serializer &mdash; but the declared
  type is also the bound the wire-supplied type name is checked against, so widening
  it removed the check. `IsPolymorphic` now recurses into array element types, so the
  slot stays exactly `T[]`.
- The legacy method-resolver cache is bounded. It was a process-wide, unbounded
  `ConcurrentDictionary` keyed by `RpcHandshake.RemoteApiVersionSet` &mdash; taken
  verbatim from the first message a remote peer sends, pre-authentication. The key
  is normalized to the scopes the registry knows (so all garbage collapses to one
  entry), the handshake caps the set at 16 scopes and 512 chars, and the cache is
  bounded by `PruningCache`. `VersionSet.HashCode` also swaps its XOR fold for an
  additive one, since XOR is linear over GF(2)^32 and any 33+ hashes contain a
  subset that XORs to zero.
- Session ids and command payloads are no longer logged unredacted on every
  connection and every command failure, and `INotLogged` is honored.
- A duplicate inbound call id no longer leaks a linked `CancellationTokenSource`
  (plus its registration on the long-lived peer-change token). A duplicate id
  carrying a different method reference or call type is now rejected as a protocol
  violation instead of silently resolving to the registered call; the same leak on
  the sibling path, where `DeserializeArguments` returns null after registration, is
  fixed with it.
- `ApiArrayNerdbankConverter` and `PropertyBagNerdbankConverter` no longer
  preallocate from the wire-declared count. A 4 MB payload declaring 4M items went
  from 32 MB to ~40 KB (`ApiArray`) and 64 MB to ~73 KB (`PropertyBag`).
- `RetryPolicy.Apply` observes its `CancellationToken` and backs off on
  `SuperTransient` errors instead of retrying with no delay &mdash; together an
  uncancellable 100% CPU spin, reproduced at 1.9M iterations in 5 s.
- `Auth_SignOut` on an unknown session id no longer inserts a session row plus an
  operation-log row, and unregistered shard tags no longer become permanent
  per-shard cache keys &mdash; both let unauthenticated input create persistent state.
- `User.ToClientSideUser()` no longer mutates a process-global `ApiMap`, and
  `InMemoryAuthService`'s intermediate `GetUserSessions` overload is now a
  `[ComputeMethod]` &mdash; it is on no interface, so it was never intercepted and every
  invalidation of it did nothing, leaving `IAuth.GetUserSessions` subscribers with a
  stale session list indefinitely.
- The default invalid-session handler no longer loops. It signed out and redirected
  to the same URL without clearing the rejected Fusion cookie, so every redirected
  request presented the same cookie and got another redirect; the unconditional
  `SignOutAsync()` also threw when no default sign-out scheme was configured, turning
  the same cookie into a persistent 500. The cookie is now rewritten before the
  handler can short-circuit, and a `ReloadGuardCookieName` cookie breaks the loop
  after one attempt.
- RestEase no longer buffers an unbounded 500-response body before parsing it.
- `ActivatorExt.CreateInstance` works for value types &mdash; it threw for every struct,
  because the constructor delegate's return type was cast to `Func<..., object>`,
  a conversion delegate return types only allow for reference types.
- `ByteSpanExt.ToHexString` had a dead `#if NET5_0_OR_GREATER1`, which silently
  pinned every build to the fallback branch &mdash; which also emitted a different casing.

### Fixed (TypeScript)

- `Computed` lifetime, in both directions. An outbound compute call stayed in
  `peer.outboundCalls` after `$sys.Ok` and its reaction pinned the `Computed` and its
  result from a GC root, so a long-lived SPA retained one live call, computed and
  full result per distinct `(method, args)` tuple ever queried; 300 discarded calls
  went from `outboundCalls=300, collected=0` to `outboundCalls=1, collected=299`,
  and the server dropped its 300 retained computeds with them. Conversely, an
  inbound call now owns its `Computed` strongly for as long as it owes the caller an
  invalidation. A late `FinalizationRegistry` callback no longer deletes a live
  replacement computed, and dependency edges are pruned on dispose &mdash; 200
  mount/dispose cycles left 200 dependants, 199 of them dead; now 0.
- `$sys.Disconnect` looked ids up in `sharedObjects` as well as `remoteObjects`,
  which is the wrong namespace &mdash; and both counters start at 1, so collisions were
  the norm: a client that both consumed a server stream and uploaded one of its own
  had its outgoing stream aborted the moment the server tore down an unrelated
  incoming one.
- The remote stream receiver enforces the acknowledgement window it advertises
  (overrun fails the stream with `$sys.AckEnd`), `$sys.Ack` is coalesced at receipt
  instead of accumulating in an array drained with `shift()`, and a huge `nextIndex`
  can no longer stall a stream permanently. The `$sys.Reconnect` stale-generation
  check no longer skips a non-numeric handshake index.
- The binary receive path is hardened. `argDataLen` was read signed and used
  directly as `pos + argDataLen`, so a negative value moved the cursor backwards and
  a 7-byte envelope could report `bytesRead = 1` &mdash; the frame was then re-parsed at
  overlapping offsets, yielding more messages than it contained, each allocating an
  inbound call and emitting a `$sys.Error` reply. Lengths are read unsigned and
  validated as in-bounds safe integers before any slice.
- One bad message no longer discards every message already decoded from the same
  frame. `.NET` batches many RPC messages per frame, and the peer's `try` covered
  deserialization but not dispatch, the text branch had no `try`/`catch` at all, and
  the binary branch's `catch` sat outside the per-message loop &mdash; so `$sys.Ok`
  replies were dropped and, since per-call timeouts default to unbounded, those calls
  hung forever. Errors are now contained per message.
- Inbound compute arguments are truncated to the declared arity, which closes
  compute-cache poisoning.


## 14.1.78+f65f331ed | npm: 14.1.5

Release date: 2026-07-27

A NuGet-only release (npm stays at `14.1.5`) that repairs MessagePack code
generation for everyone who puts a `PropertyBag` in a MessagePack contract.
**If you're on 14.1.71 or 14.1.73, upgrade.**

### Fixed

- MessagePack's source generator no longer dies on the `PropertyBag` family.
  MessagePack 3.1.8 crashes its own generator and analyzer with an
  `IndexOutOfRangeException` whenever a `[MessagePackObject]` type has a member
  whose type is a **closed generic struct defined in a referenced assembly** —
  generic classes and non-generic structs are fine. `TypeSchema` gave
  `PropertyBag`, `PropertyBagItem` and `TypeDecoratingUniSerialized` exactly that
  shape in 14.1.71, so since then any assembly with a `PropertyBag` in a
  MessagePack contract silently lost its *entire* generated resolver — the
  generator "will not contribute to the output" — which breaks trimming and
  NativeAOT. The three types now carry `[MessagePackFormatter]` with hand-written
  formatters that reproduce the previously generated wire format byte for byte,
  the same thing `Option<T>`, `Result<T>`, `ApiArray<T>` and `Box<T>` already do.
  `MutablePropertyBag` needed nothing — it's a class, and only broke through its
  `PropertyBagItem[]` member.
- The NativeAOT roots added in 14.1.73 are now complete. `KeepSerializable<T>`
  keeps the type and its serializers but not its MessagePack formatter, so
  `TypeDecoratingUniSerialized`'s formatter was still missing, as was the
  `ArrayFormatter` both bags need for their `PropertyBagItem[]` contents.


## 14.1.73+02df3329a | npm: 14.1.5

Release date: 2026-07-27

A small NuGet-only release (npm stays at `14.1.5`) that fixes trimming /
NativeAOT retention for the types Fusion always serializes. Worth taking if you
publish trimmed or AOT-compiled apps.

### Fixed

- `Unit`, `PropertyBag`, `MutablePropertyBag` and their serializers are now
  retained by `ActualLab.Core` itself. `DefaultMessagePackResolver` builds its
  formatters via `CreateInstance` / `MakeGenericType`, so nothing referenced
  their constructors and full trimming dropped them — every app had to keep
  `UnitMessagePackFormatter` from its own startup code. Core has a module
  initializer of its own now, matching the RPC / CommandR / Fusion ones.
- The `PropertyBag` family's generated MessagePack formatters are rooted
  explicitly. They were non-generic before `TypeSchema`, so the generated
  resolver looked them up statically; since 14.1.71 they are closed via
  `MakeGenericType`, which ILC can't follow.


## 14.1.71+a733921f | npm: 14.1.5

Release date: 2026-07-27

A NuGet-only release (npm stays at `14.1.5`). It adds `TypeSchema` — a way to
constrain which types a `PropertyBag` is allowed to materialize, which matters
for bags filled from remote input — plus a per-method consolidation comparer,
and it turns a silently ignored `ConsolidationDelay` into a startup error.

### Breaking Changes

- `TypeDecoratingUniSerialized<T>` and the four single-format
  `TypeDecorating*Serialized<T>` wrappers now take `<TSchema, T>`. Open generics
  can't be aliased, so these call sites need updating —
  `TypeDecoratingUniSerialized<TypeSchema.Any, T>` preserves today's behavior.
  `PropertyBag`, `MutablePropertyBag` and `PropertyBagItem` are spelled as
  before via global using aliases.
- `ConsolidationDelay` on an RPC-exposed compute method of a
  `RpcServiceMode.Distributed` service now throws at registration time instead
  of being silently ignored. Such calls are served by
  `RemoteComputeMethodFunction`, which always produces a plain
  `ComputeMethodComputed` — so no consolidation ever happened, even when routing
  resolved to the local peer. Move the consolidation to a non-RPC-visible (e.g.
  `protected virtual`) compute method and have the public one derive from it.
  `Local`, `Server`, `Client` and `ServerAndClient` services are unaffected.

### Added

- `TypeSchema` (`ActualLab.Serialization`) — a phantom type parameter threaded
  through `PropertyBag`, `MutablePropertyBag`, `PropertyBagItem` and the
  type-decorating serialized wrappers. It supplies the type filter the
  type-decorating serializers already accept, so a disallowed type is rejected
  right after `TypeRef.Resolve()` and never materializes; `PropertyBagItem` also
  checks on write, so a bad value fails where it's added. `TypeSchema.Any` keeps
  today's behavior, `TypeSchema.PrimitiveOnly` allows the primitives plus
  `string`, `Guid`, the date/time types, `Moment` and `Symbol`. The wire format
  is unchanged — `PropertyBag<TypeSchema.Any>` serializes byte-identically to
  the previous `PropertyBag`.
- `ComputeMethodAttribute.ConsolidationComparer` — the `IEqualityComparer<T>`
  type used to compare two consecutive outputs while consolidating, where `T` is
  the method's unwrapped return type. Result types with referential equality
  (or collections whose `Equals` is referential) could never consolidate before,
  since the default comparer bottoms out in `Equals(x.Value, y.Value)`. The
  comparer is validated and instantiated once per type at startup, and errors
  keep the existing error-aware comparison.

### Fixed

- Consolidation sources no longer share a `FusionMonitor` category with the
  targets that absorb them. `ComputeMethodDef` builds both from the same type
  and `MethodInfo`, so a churning source was conflated with its quiet target;
  the source now reports `~<FullName>`, mirroring the `*` prefix
  `RemoteComputeMethodFunction` uses for remote replicas.


## 14.1.62+ab9673b6 | npm: 14.1.5

Release date: 2026-07-26

Another NuGet-only release (npm stays at `14.1.5`). Its centerpiece is a fix for
an HTTP RPC deadlock that could stall a client indefinitely: every connection
now gets its own `HttpClient`. It also folds in the unreleased `14.1.53`
packaging work, which makes each package render a README on nuget.org.

### Breaking Changes

- `RpcHttpClient`'s `protected HttpClient HttpClient` property is replaced by
  `protected virtual HttpClient CreateHttpClient()`, called once per connection.
  Subclasses that read or override the property should override the method
  instead.
- `Options.HttpClientFactory` is now invoked per connection rather than once per
  client. A custom factory that returns a shared or cached `HttpClient` will
  reintroduce the deadlock described below — return a fresh instance.

### Added

- `IHasRetryDelay` (`ActualLab.Resilience`) — an error implementing it extends
  its cached `Computed`'s auto-invalidation delay to at least `RetryDelay`, so a
  retry can't be issued sooner than the error asks for. Useful for errors caused
  by request volume, where retrying early makes the underlying condition worse.

### Fixed

- HTTP RPC connections no longer deadlock when two of them are open to the same
  host. An RPC connection holds its request open for its whole lifetime, and
  `SocketsHttpHandler` can't carry two such requests over one HTTP/2 connection —
  the second never receives its response headers. Since `RpcHttpClient` cached a
  single `HttpClient`, this hit every reconnect (the new connection overlaps the
  old one's teardown) and every client with both a client and a backend peer,
  leaving the peer retrying forever. Each connection now creates and disposes its
  own `HttpClient`, and closing a connection resets its HTTP/2 stream instead of
  leaving it half-open.

### Infrastructure

- Every NuGet package now ships a README rendered on nuget.org, along with a
  corrected project URL, an embedded icon, and cleaned-up descriptions.
- The test runner aborts a hung test group instead of letting it wedge the
  whole run.

### Documentation

- Reworked the docs-site home hero cards and the MCP page intro.
- Added `bool`/`Task` naming and member-ordering rules to `CODING_STYLE.md`.


## 14.1.47+493d3cc7 | npm: 14.1.5

Release date: 2026-07-21

This is a NuGet-only release (npm stays at `14.1.5`) that overhauls Fusion's
OpenTelemetry story: RPC call and connection metrics, database log pipeline
metrics, invalidation pass and persistent remote cache monitoring, plus a
set of distributed-tracing fixes — see the updated
[Part "OpenTelemetry"](https://fusion.actuallab.net/PartOtel) for the full
instrument reference.

### Breaking Changes

- Per-method RPC instruments (`rpc.server.{Service}/{Method}.*`) are gone;
  RPC call metrics are now a fixed set of instruments carrying the bounded
  `rpc.method` attribute, and `rpc.server.duration` is renamed to
  `rpc.server.call.duration`. Use metric views to drop `rpc.method` where
  per-method breakdown isn't needed.
- Live-count instruments (e.g. `rpc.{transport}.transport.count`) are
  `ObservableGauge`-s now instead of `ObservableCounter`-s — update
  dashboards that treated them as monotonic sums.

### Added

- RPC call instruments: `rpc.client.call.duration` (logical call duration
  including reroutes), `rpc.client.reroute.count`, open-call gauges
  `rpc.server.call.open` / `rpc.client.call.open` (by call stage), and
  `rpc.client.call.event.count` for delayed / resend / timeout observations.
- RPC connection instruments: `rpc.client.connection.attempt.count`,
  `rpc.client.connection.attempt.duration`, and client/server
  `rpc.*.connection.uptime` histograms, tagged with `rpc.connection.kind`
  and `outcome`.
- Database log pipeline instruments (`ActualLab.Fusion.EntityFramework`):
  `db.event_log.processing.delay`, `db.log.batch.size`, and
  `db.log.batch.duration`.
- Command and operation retry instruments (`operation.retry.*`) in CommandR /
  Fusion operations.
- Safe invalidation pass monitoring: `invalidation.pass.duration` and
  `invalidation.pass.command.count`; partial invalidation failures mark the
  enclosing span as an error.
- Persistent remote cache instruments: `remote_computed.cache.request.count`,
  `remote_computed.cache.lookup.duration`, and
  `remote_computed.cache.stale_value.count`.
- `RpcDiagnosticsOptions.MustPropagateAmbientActivityContext` — lets
  perf-sensitive hosts skip the `Activity.Current` lookup on the outbound
  call path.

### Fixed

- Distributed trace context is preserved across RPC calls, and call traces
  are renewed after rerouting.
- RPC spans are aligned with OpenTelemetry semantic conventions, and
  response serialization is traced.
- Entity Framework spans use the proper EF activity source.
- Blazor circuits suppress only connection-scoped activities, so deliberate
  ambient activities (e.g. .NET 10 Blazor circuit spans) are preserved; the
  suppression predicate is virtual for custom policies.
- Database log batch outcome distinguishes cancellation from error, and
  retry instruments skip tag construction when disabled.
- Inbound call trace completion tolerates incomplete result tasks.

### Performance

- Call-path diagnostics overhead is nearly eliminated when telemetry is off:
  no per-outbound-call trace allocation, and the inbound call lock is
  skipped when there is no trace to complete.
- Enabled-instrument checks and staged RPC call counts are cached.

### Infrastructure

- The test suite runs in parallel groups with an exclusive phase for
  time-sensitive tests: the full default run went from ~38 to ~14 minutes.


## 14.1.3+79939c2a | npm: 14.1.5

Release date: 2026-07-21

This release makes RPC peer refs **stable**: there is now one `RpcRef` per
logical target (shard, host, "default"), cached forever and safe to return
from routers with no factory ceremony. A topology change no longer mints a
new ref — the ref's **route** (a new per-generation `RpcRoute`) is reset
instead, while peers stay 1:1 with route generations, so the entire
battle-tested reroute pipeline is preserved.

npm `14.1.5` is the TS side of the same release (published from
[0abe15b7](https://github.com/ActualLab/Fusion/commit/0abe15b7)).

### Breaking Changes

- TS (`@actuallab/rpc`): `RpcPeerRefBuilder` is renamed to `RpcRefBuilder`.
  TS refs stay plain strings — there's no `RpcRoute` counterpart on the TS side.
- [`RpcPeerRef` is renamed to `RpcRef`, and `RpcRouteState` is replaced by `RpcRoute`](https://github.com/ActualLab/Fusion/commit/99468623)
  — see the ["RpcPeerRef: renamed to RpcRef in v14.1"](https://fusion.actuallab.net/PartR-CallRouting#rpcpeerref-renamed-to-rpcref-in-v14-1)
  migration note. Key points:
  - Custom refs override `CreateRoute()` to mint a route per generation
    instead of being re-created on topology changes; ref caches collapse to a
    plain `ConcurrentDictionary.GetOrAdd`. `RpcRef.Route` re-mints lazily
    when the current route is marked as changed; `RpcRoute.NewStatic()`
    denotes refs that never reroute.
  - `RpcRouteStateExt` is merged into `RpcRoute`; `IsChanged` / `WhenChanged`
    are properties now, and per-generation target data (host id, endpoint)
    belongs on `RpcRoute` subclasses.
  - `RpcPeer` is constructed from `RpcRoute` (`peer.Ref` == `peer.Route.Ref`);
    the pipeline reads `peer.Route` — the generation the peer is bound to.
  - `RpcHub.GetPeer(RpcRoute)` is the primary overload (uses the exact
    generation); `GetPeer(RpcRef)` resolves the current one; `RpcHub.Peers`
    is keyed by route.
  - Delegate signature changes: `RpcPeerOptions.PeerFactory` is
    `Func<RpcHub, RpcRoute, RpcPeer>`, `ConnectionKindDetector` is
    `Func<RpcRoute, RpcPeerConnectionKind>`; server-side delegate renames:
    `RpcWebSocketServerPeerRefFactory` → `RpcWebSocketServerRefFactory`,
    `RpcHttpServerPeerRefFactory` → `RpcHttpServerRefFactory`, `PeerRefFactory`
    properties → `RefFactory`.
  - `RpcPeerStateMonitor` is constructed from `(RpcHub, RpcRef?)` and
    transparently restarts across reroutes.
- [The operation log processing delay metric is renamed](https://github.com/ActualLab/Fusion/commit/4dc18e91)
  from `db.operation.log.processing.delay` to `db.operation_log.processing.delay`
  (OTel naming conventions: "operation log" is a single snake_case component).
  The Prometheus-flattened name is unchanged.

### Changed

- Rerouted peers are now removed from `RpcHub.Peers` with zero delay once
  drained — the 5-minute removal delay applies only to terminally-failed
  client peers.
- A burst of topology churn with no interleaved calls coalesces into a
  single route re-resolution — something the previous ref-per-version model
  couldn't do.
- Peer-bound logging now renders the route generation
  (`<address> [vN->target]`, cached), so overlapping generations are
  distinguishable in logs; `RpcRoute.GetTargetString()` supplies the target.
- `FusionEntityFrameworkInstruments` now follows the shared instruments
  pattern (adds `ActivitySource`); the TodoApp Aspire sample registers the
  `ActualLab.Fusion.EntityFramework` meter, so the operation log delay
  histogram reaches the dashboard.

### Tests

- New `RpcRefRouteTest` and `MeshStableRefTest` suites: route re-mint and
  churn coalescing, `Reset()`, `GetPeer` replacement and races, and
  local↔remote shard flips with computed invalidation on a stable ref.


## 14.0.37+1d67c8de | npm: 14.0.17

Release date: 2026-07-20

This release focuses on **cross-host invalidation latency**: the operation log
reader now reports its processing delays (metric + rate-limited warnings), the
out-of-order-commit path reacts to notifications instead of polling them out,
and both the Npgsql and Redis log watchers coalesce their change notifications.

### Breaking Changes

- [`RpcWebSocketServerOptions.ConfigureWebSocket` is now an `RpcWebSocketServerAcceptContextFactory`](https://github.com/ActualLab/Fusion/commit/8e140bea)
  receiving `(server, context, peerRef)` instead of a plain
  `Func<WebSocketAcceptContext>`, so the accept context can vary per connection
  (e.g. to enable WebSocket compression selectively based on the request or
  peer ref). The OWIN/.NET Framework server gains the same hook.

  **Migration:** if you assign a custom `ConfigureWebSocket` delegate, update
  it to the new signature; the default behavior is unchanged.

### Added

- [Operation log processing delay reporting](https://github.com/ActualLab/Fusion/commit/49b4b9a4):
  every remote operation applied by the log reader records a
  `db.operation.log.processing.delay` histogram (tagged with shard and the
  processing path: batch / gap / reprocess), and delays above
  `ProcessingDelayWarningThreshold` (1 s by default) produce rate-limited
  warnings naming that path — enough to tell out-of-order-commit gap polling
  from lost-notification check-period fallbacks.
- [`TaskCoalescer` in `ActualLab.Core`](https://github.com/ActualLab/Fusion/commit/eccd8dc4):
  coalesces concurrent runs of a task factory — at most one run in flight plus
  one queued behind it, so any burst of requests is served by at most two runs.

### Performance

- [Out-of-order commits now invalidate near-instantly](https://github.com/ActualLab/Fusion/commit/95e51c11):
  a notification-triggered wake-up of the operation log reader forces young
  pending gaps to be re-checked immediately, collapsing the former up-to-~1 s
  gap-poll delay to one notification round-trip plus one query.
- [Npgsql and Redis log watchers coalesce change notifications](https://github.com/ActualLab/Fusion/commit/c3787c58)
  via `TaskCoalescer`: a burst of N commits costs at most two NOTIFY/PUBLISH
  round-trips instead of N, and the Npgsql watcher no longer serializes sends
  behind a per-shard lock.

### Tests

- PostgreSql-backed tests randomly split between the Redis and Npgsql
  operation log watchers, so both notification transports get coverage.

### Documentation

- Performance doc refresh: benchmark numbers updated to 14.0.17, external
  grpc_bench cross-check, layout cleanups.


## 14.0.17+ddd1df1b | npm: 14.0.17

Release date: 2026-07-16

Major release. Three things land together: a **proxy/interception overhaul**
that replaces the per-interceptor dispatch path with compile-time method slots
(breaking — see below), a **hot-path performance campaign** across the proxy,
compute, locking, and RPC layers, and a **systematic correctness audit** of the
entire .NET codebase (Core, Fusion, RPC, Interception + generators, CommandR,
Blazor, and the persistence/EF/Redis/Npgsql supporting libraries) that fixed a
large batch of edge-case and boundary defects.

### Breaking Changes

- [**Proxy method slots + array-based interceptor dispatch.**](https://github.com/ActualLab/Fusion/commit/2780de31)
  Generated proxies now assign a compile-time integer slot to every intercepted
  method and cache the resolved handler per proxy instance, per slot — a warm
  call is a field load + delegate invoke, with no dictionary probe and no
  virtual `SelectHandler` dispatch. This changes several `ActualLab.Interception`
  contracts:
  - `IProxy.Interceptor` is replaced by `IProxy.MethodTable` (static
    `ProxyMethodTable`) + `IProxy.Binding` (`InterceptorBinding`).
  - `Invocation` now carries `(MethodTable, MethodIndex)`; `Method` resolves via
    the table. The legacy `MethodInfo`-based `Invocation` constructor is removed.
  - New public types `ProxyMethodTable`, `ProxyMethodRef`, and
    `InterceptorBinding`; `InterceptorExt`/`InvocationExt` surfaces changed.

  **Migration:** rebuild — the source generator emits the new proxy shape, so a
  clean rebuild regenerates all proxies. Only code that constructs `Invocation`
  by hand or reads `IProxy.Interceptor` directly needs source changes.

### Performance

- [Compute method leaf invalidation, empty-computed invalidation, and registry-slot carry-through](https://github.com/ActualLab/Fusion/commit/19f89bf3)
- [Cached compute-input lookup, initial computed registration, and completed-task result adaptation](https://github.com/ActualLab/Fusion/commit/12d6c660)
- [`AsyncLockSet`: atomic entry lifecycle, uncontended-release fast path, and fewer redundant locks during invalidation cascades](https://github.com/ActualLab/Fusion/commit/5fe003c1)
- [Inlining hints on proxy dispatch, compute lookup, and the RPC/Fusion hot paths](https://github.com/ActualLab/Fusion/commit/2bc21f0c)
- [`VarUInt` encode/decode fast paths (VarUInt32/VarUInt64)](https://github.com/ActualLab/Fusion/commit/d8993893)
- [WebSocket receive transfers use tuple-assigned metadata](https://github.com/ActualLab/Fusion/commit/311a4e94)
- [Skip inactive RPC inbound/outbound traces, redundant simple-channel disposal, and redundant envelope-encoding scans](https://github.com/ActualLab/Fusion/commit/82b596ce)

### Added

- [Invalidation handler logging helper exposed, kept in its handler set](https://github.com/ActualLab/Fusion/commit/c65058e4)
- [Typed delegate invocation-list helper added to Core](https://github.com/ActualLab/Fusion/commit/90320f01)
- [Fusion redirect-checker defaults are now factory-based](https://github.com/ActualLab/Fusion/commit/3ae18bb5)

### Changed

- [Use `127.0.0.1` instead of `localhost` in service connection strings](https://github.com/ActualLab/Fusion/commit/7111fb5b)
- [Complete builder wiring for pre-registered implementations](https://github.com/ActualLab/Fusion/commit/12f0c88b)
- [Read JSON property names case-insensitively in `SystemJsonSerializer`](https://github.com/ActualLab/Fusion/commit/cfcc7354)

### Fixed

**RPC**
- [Frame buffer corruption on transport send failures](https://github.com/ActualLab/Fusion/commit/e3a8c37e)
- [Always notify the client when a response send fails](https://github.com/ActualLab/Fusion/commit/63d1df85)
- [Enqueue completion and stream flow-control correctness](https://github.com/ActualLab/Fusion/commit/29772fba); [handshake/WebSocket boundary hardening](https://github.com/ActualLab/Fusion/commit/d467da61) and [clearer handshake-mismatch errors](https://github.com/ActualLab/Fusion/commit/dc00589c)
- [Backend validation and NetFx WebSocket ownership](https://github.com/ActualLab/Fusion/commit/65d6af0d)
- [Keep the TS stream reset flag across drained ack batches](https://github.com/ActualLab/Fusion/commit/e152a571)

**Interception + generators**
- [Proxy generator identity, nesting, and incremental retention](https://github.com/ActualLab/Fusion/commit/9f97421c)
- [Reject unsupported proxy signatures and deduplicate diamond methods](https://github.com/ActualLab/Fusion/commit/70edbb7c)
- [Prevent `ArgumentList` struct invokers from crashing the process](https://github.com/ActualLab/Fusion/commit/18b58d73)

**Fusion**
- [State, synchronization, and invalidation contracts](https://github.com/ActualLab/Fusion/commit/b6562e8c); [state notifications, dependency snapshots, and Blazor metadata](https://github.com/ActualLab/Fusion/commit/d4a4e5be); [Blazor lifecycle and render-point contracts](https://github.com/ActualLab/Fusion/commit/b9a4fc79)
- [Secure redirects and session-binding boundaries](https://github.com/ActualLab/Fusion/commit/f70abf56); [web and key-value service boundary defects](https://github.com/ActualLab/Fusion/commit/8ae0a9a0)
- [Lifecycle, monitoring, and session edge cases](https://github.com/ActualLab/Fusion/commit/eca3cec7); [cache, pruner, comparer, and session-tag defects](https://github.com/ActualLab/Fusion/commit/847b6c7e)

**Core**
- [Encoding, reflection, framing, and sampling bugs](https://github.com/ActualLab/Fusion/commit/69d57147); [sharding, numeric, random, and conversion boundaries](https://github.com/ActualLab/Fusion/commit/86eacdaf)
- [Collection, timer, and convenience API defects](https://github.com/ActualLab/Fusion/commit/8e1865b1); [file, activation, collection, and hash-ring edge cases](https://github.com/ActualLab/Fusion/commit/75e5cd0c); [disposal, channel, and pooled-buffer boundaries](https://github.com/ActualLab/Fusion/commit/3b016058)

**CommandR**
- [RPC filter evaluation and parameter-count diagnostics](https://github.com/ActualLab/Fusion/commit/8579fb25)

**Persistence + supporting libraries**
- [Persistence boundaries, watcher, and shard-factory lifecycles](https://github.com/ActualLab/Fusion/commit/27cf340f); [Nerdbank alignment and plugin lifecycle correctness](https://github.com/ActualLab/Fusion/commit/f1d6bd1f)
- [EF save guards and Npgsql SQL generation](https://github.com/ActualLab/Fusion/commit/b6383609); [Redis isolation and atomic sequence resets](https://github.com/ActualLab/Fusion/commit/da25cf35)
- [Honor REST query formats and preserve plugin cleanup failures](https://github.com/ActualLab/Fusion/commit/97b8172e)

### Infrastructure

- [BenchmarkDotNet performance runner](https://github.com/ActualLab/Fusion/commit/abcf94b4) with RPC argument-codec, WebSocket-transfer, and VarUInt benchmarks
- Test deflaking and faster test hosts: [cut web-host shutdown grace from 3s to 50ms](https://github.com/ActualLab/Fusion/commit/bf6e8294), [deflake Redis streamer](https://github.com/ActualLab/Fusion/commit/038f22ae) and [just-disconnected peer state](https://github.com/ActualLab/Fusion/commit/7eb86799) tests


## 13.0.163+7e1e746a | npm: 13.0.167

Release date: 2026-07-15

TypeScript-only follow-up to the 13.0.163 hardening release. Promotes two
primitives that until now lived only in ActualChat's synced copy into the
shared Fusion TS packages so both repos stay in lockstep, and fixes the TS
typecheck script to read current source instead of a stale build.

**npm-only release.** There are no .NET framework source changes, so the NuGet
packages were not republished — the latest on nuget.org remains **13.0.163**.
The `13.0.167` version reflects the npm package and the Nerdbank git-height
version of this commit.

### Added (TypeScript)
- [`AsyncSignal` (`actuallab-core`) — auto-reset, edge-triggered async wakeup](https://github.com/ActualLab/Fusion/commit/3ada9207)
- [`RpcStreamSender.minRttMs` (`actuallab-rpc`) — windowed minimum of send→ack round-trip times](https://github.com/ActualLab/Fusion/commit/3ada9207)

### Fixed (Tooling)
- [Typecheck against source, not stale `dist` — a dedicated src-only `tsconfig.typecheck.json` clears 14 phantom `tsc -b` errors](https://github.com/ActualLab/Fusion/commit/7e1e746a)
- [Make `decorators.ts` type-safe under strict no-unsafe-* lint (`fn.toString()`)](https://github.com/ActualLab/Fusion/commit/3ada9207)


## 13.0.163+ab89147d | npm: 13.0.163

Release date: 2026-07-15

TypeScript-port hardening release. This is the payload of a full audit of the
TS port against C# Fusion (contracts + robustness): 70+ findings across Core,
RPC, the compute/state kernel, and the React bindings were triaged and fixed,
bringing the TS runtime to behavioral parity with .NET on invalidation,
cancellation, reconnect, and error propagation. The npm package advances to
**13.0.163** to match NuGet. No .NET framework source changes.

### Fixed (TypeScript)

**Core (`actuallab-core`)**
- [Abortable `delayAsync`, `retry`, `RetryDelayer.getDelay`, and `AsyncLock` waiters](https://github.com/ActualLab/Fusion/commit/7267af92)
- [Robustness fixes for TS-port audit items C1, C2, C4, C7, C9](https://github.com/ActualLab/Fusion/commit/0f4c4cc1)

**RPC (`actuallab-rpc`)**
- [`addClient` extends the shared proxy instead of dropping methods (F8)](https://github.com/ActualLab/Fusion/commit/ab89147d)
- [RPC lifecycle hardening — R10, R11, R12, R17, D4](https://github.com/ActualLab/Fusion/commit/ab121bb3)
- [Harden `RpcPeer` handshake/reconnect/dedup (R5, R8, R9, R13, R16)](https://github.com/ActualLab/Fusion/commit/c11c7e19)
- [Align the RPC wire layer with .NET V5/`ExceptionInfo` contracts](https://github.com/ActualLab/Fusion/commit/636ef089)
- [Stream lifecycle + tracker identity invariants (R3, R4, R21, R22)](https://github.com/ActualLab/Fusion/commit/8adf5579)
- [Correct dispatch receiver, decorator metadata isolation, and wire arity](https://github.com/ActualLab/Fusion/commit/a389c015)

**Fusion compute/state kernel (`actuallab-fusion`)**
- [`ComputedState.value` rethrows the stored error instead of masking it (S2)](https://github.com/ActualLab/Fusion/commit/fb665799)
- [Reconnect reporting, client-proxy dedup, server-peer auto-dispose (F6, F8, F11)](https://github.com/ActualLab/Fusion/commit/5576fcf8)
- [Settle/retry pre-result invalidations, propagate cancellation (F2, F3, F4, F5)](https://github.com/ActualLab/Fusion/commit/3634f6b0)
- [State + UI robustness parity (S8, S9, S10, S17, S18)](https://github.com/ActualLab/Fusion/commit/af9be82a)
- [Thread compute `AsyncContext` + ALS backing, capture rework, reentrancy guard (K3, K11, K14)](https://github.com/ActualLab/Fusion/commit/4b3072f8)
- [Server-side invalidation parity (F1, F9, F7, F10)](https://github.com/ActualLab/Fusion/commit/05244f28)
- [Versioned `whenUpdated` + prompt dispose + abort-safe delayers (S11, S5, S14, S4)](https://github.com/ActualLab/Fusion/commit/1addf4bf)
- [`ComputedOptions` + cancellation caching, keying, renewer (K5, K6, K13, K15)](https://github.com/ActualLab/Fusion/commit/c6c1d307)
- [State-layer C# parity for `MutableState`/`ComputedState` (S1, S13, S3, S16, S15)](https://github.com/ActualLab/Fusion/commit/0a3e1530)
- [Kernel invalidation-path robustness (K4, K17, K12, K10, K9)](https://github.com/ActualLab/Fusion/commit/ba2be741)
- [Kernel graph/registration parity (K1, K2, K7, K8, K16)](https://github.com/ActualLab/Fusion/commit/877178f8)

**React bindings (`actuallab-fusion-react`)**
- [Rebuild hooks on `useSyncExternalStore`; last-non-error computed (S6, S7, S12)](https://github.com/ActualLab/Fusion/commit/ab8410c9)

### Fixed (Samples & tooling)
- [Update the TodoApp TS UI for the current Fusion TS-port APIs](https://github.com/ActualLab/Fusion/commit/8a8c5ed9)
- [Make ESLint deterministic from a clean checkout; fix tooling globs](https://github.com/ActualLab/Fusion/commit/1da36a8d)

### Documentation
- [Updated TS-port docs for the fix-campaign behavior/API changes](https://github.com/ActualLab/Fusion/commit/fcff8c3c)
- [Fixed the Server-Side Usage sample — compose compute methods via a `@computeMethod` class](https://github.com/ActualLab/Fusion/commit/d9f6acdb)
- Added the TypeScript port gap audit (contracts & robustness vs C# Fusion) and its
  per-item decisions under `docs/plans`.

## 13.0.126+c30df2eb | npm: 13.0.25

Release date: 2026-07-14

Maintenance release: dependency floor bumps and build fixes; no framework source
changes. The npm package is unchanged at v13.0.25.

### Changed
- Bumped crucial dependency floors: **MessagePack** `[3.1.8,)` (was 3.1.6) and
  **StackExchange.Redis** `[3.0.17,)` (was 2.9.32 — a major-version bump that
  affects `ActualLab.Redis` consumers).
- Pinned Roslyn (`Microsoft.CodeAnalysis.CSharp`) to the `[4.3.0,)` lower
  boundary and decoupled it from the C# runtime-binder version. A source
  generator only loads in a Roslyn host at least as new as the one it was
  compiled against, so this keeps `ActualLab.Generators` loadable under Unity's
  older build stack. `Microsoft.CodeAnalysis.Analyzers` held at 3.3.4 to match.
- Pinned the test SDK/runner (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`)
  to their low floors — used only by Fusion's own tests; newer majors caused
  issues.

### Fixed
- Repaired the `-p:UseMultitargeting=true` build. `System.Memory` was pinned to
  4.6.0, but the `Microsoft.Extensions.Logging.Abstractions` floor now resolves
  to 10.0.5 (via `ILogger.Moq`), which requires `System.Memory >= 4.6.3` →
  NU1605 downgrade error on the netstandard2.0/net472/net48 target frameworks.
  Bumped to 4.6.3. Also guarded a test's fake `WebSocket` `Memory<byte>`
  overrides for those older TFMs.

## 13.0.101+4292afe9 | npm: 13.0.25

Release date: 2026-07-14

.NET-focused release; the npm package is unchanged at v13.0.25. The bulk of this
release is a distributed-invalidation audit and the correctness wave it produced:
Fusion's compute-call sharing, `IState.Invalidated` semantics, the operation-log
reader, and operation-completion listeners were all hardened against races,
duplicate delivery, and silent loss under load and reconnects.

### Breaking Changes
- Compute services that also expose command handlers must now be registered as
  **singletons**. `AddComputeService` rejects a scoped/transient registration of
  such a service at DI-build time instead of silently breaking invalidation
  replay (the handler was resolved from the root provider, faulting every
  completion). Migration: register these services as singletons. Scoped/transient
  compute services **without** command handlers (e.g. UI-scoped services) are
  unaffected.
- `DatabaseFacadeExt.DisableAutoTransactionsAndSavepoints()` (in the
  `...Internal` namespace) was renamed to
  `DisableAutoTransactions(bool allowSavepoints = true)`. Call
  `DisableAutoTransactions(allowSavepoints: false)` for the old behavior.

### Added
- `NonTransientErrorInvalidationDelay` is now a settable `[ComputeMethod]` option,
  letting you tune how long a non-transient error result is cached before
  auto-invalidation on a per-method basis (documented alongside the other compute
  method options).

### Changed
- Error auto-invalidation is now routed by error transiency: **Terminal** errors
  use `AutoInvalidationDelay`, while **NonTransient** errors use the minimum of
  the relevant delays. A throwing `TransiencyResolver` is treated as transient.
- Reworked the operation-log gap-set cadence: due-gated queries, no reader
  starvation, and horizon expiry applied to all pending entries.
- `MutableState` is documented as an exception to
  `NonTransientErrorInvalidationDelay`.

### Fixed
- `IState.Invalidated` now fires **exactly once per generation** (no duplicate or
  skipped invalidation events across generations).
- Serve-stale no longer leaves a predecessor's `SynchronizedSource` uncompleted,
  and the cache-update path no longer double-binds the shared RPC call; the
  hand-off marker was fixed so successor invalidation still cleans up the shared
  RPC call.
- Fixed a `KeyConflictStrategy` race on `_events` inserts (actual-chat#4049), and
  restored flush-and-retry recovery for version-checked event updates while
  avoiding unnecessary event-conflict flushes.
- Hardened the operation-log reader: gap pending-set tracking, bounded retry with
  a corrected failed-entry retry cadence, and a coverage-loss sweep so entries
  can't be silently dropped.
- Operation-completion listeners are now reliable under failure: synchronous
  listener throws are routed through the external-terminal path, an external
  completion-command failure is terminal (propagated and unmarked), and
  at-least-once delivery is documented and asserted.
- Fixed a misleading discard log on the operations-log reprocess path and
  enriched `OperationCompletionNotifier` assertion-failure logs with context.
- Fixed the multitargeted build by disambiguating `FirstOrDefaultAsync` in
  `DbOperationScope`.

### Documentation
- Added a distributed-invalidation audit report and course-of-action plan.
- Docs site moved to Cloudflare Pages with extensionless canonical URLs and
  IndexNow submission on deploy; improved documentation search indexing;
  fixed homepage hydration mismatches and compressed GIF assets.
- Linked the live sample demos (incl. Board Games, TownHall) from the docs and
  README.

### Tests
- Added a must-not-throw test harness for operation-completion listeners, plus
  operation-log reader gap/budget tests and reprocessor tests (Uuid preservation
  across retries, no-retry-after-commit).

### Infrastructure
- Dependency bumps: ActualLab.Core → 13.0.12, MessagePack → 3.1.6,
  AwesomeAssertions → 9.4.0, and several dev/CI tool updates.


## 13.0.28+5c8f5ae1 | npm: 13.0.25

Release date: 2026-07-06

.NET-only release; the npm package is unchanged at v13.0.25. Fixes MessagePack
serialization code being trimmed away in Native AOT / fully trimmed apps, and
introduces feature switches to control which serializers `CodeKeeper` preserves.

### Added
- Serialization feature switches (both on by default), mirroring
  `ArgumentList.AllowGenerics`:
  - `MemoryPackByteSerializer.IsEnabled`
  - `MessagePackByteSerializer.IsEnabled`

  An app that uses only one serializer can disable the other via a trimmed
  `RuntimeHostConfigurationOption` to drop its keep-code from the published
  binary. See the new "Feature Switches" section in the
  [Native AOT and Trimming](./PartAOT.md#feature-switches) doc.

### Fixed
- `CodeKeeper.KeepSerializable<T>()` now preserves **MessagePack** serialization
  code in addition to MemoryPack. Previously, apps whose RPC uses MessagePack
  formats (e.g. `msgpack6c`) had that code trimmed under full trimming, so types
  with runtime-resolved formatters (`ApiMap<,>`, `ImmutableArray<T>`, ...)
  failed to deserialize with `"Cannot deserialize inbound call arguments"`.

### Tests
- New RPC **keep-alive** test suites in both .NET (`RpcKeepAliveTest`) and TS
  (`rpc-keep-alive.test.ts`): connections stay up while keep-alives flow and
  are dropped when they stop, including half-open link and reconnect scenarios.


## 13.0.12+ed8b32fc | npm: 13.0.25

Release date: 2026-05-27

TypeScript-only release. .NET package unchanged from v13.0.12. Fixes the
TS RPC client's handshake parsing on the text (`json5np`) transport when
talking to a .NET server, and adds cross-casing E2E coverage for `$sys.*`
payloads.

### Fixed
- TS RPC: the client now reads **camelCase** `$sys.Handshake` fields, not
  just PascalCase. A .NET server serializes `RpcHandshake` through
  `System.Text.Json` with `JsonSerializerDefaults.Web`, so over the text
  transport the wire keys are `index` / `remoteHubId` — which previously
  parsed as `undefined`. The fallout (text transport only):
  `_remoteHandshakeIndex` defaulted to `0`, so every `$sys.Reconnect` was
  rejected by the server (`"own handshake index N != 0"`) and forced a
  resend-all; and peer-change detection (keyed off `RemoteHubId`) never
  fired across server restarts. The MessagePack transport was unaffected
  (positional array). The parse now accepts the array, PascalCase, and
  camelCase shapes.

### Tests
- New **JSON casing** E2E suite (`rpc-handshake-casing.test.ts`): drives
  the real client `run()` loop against a mock .NET-style server that emits
  `$sys.*` payloads in camelCase or PascalCase, asserting the client parses
  the handshake `Index` (echoed back in `$sys.Reconnect`), `RemoteHubId`
  (peer-change detection), and `$sys.Error` info identically in both
  casings. Confirms the audit finding that `$sys.Handshake` was the only
  affected type — `$sys.Error` / `$sys.End` already read both casings, and
  all other `$sys.*` arguments are positional or stage-keyed.


## 13.0.12+63e3d65b | npm: 13.0.20

Release date: 2026-05-27

TypeScript-only follow-up. .NET package unchanged from v13.0.12 — fixes
a Fusion-on-TS race exposed by the new reconnection-matrix tests, and
ports those tests across all three layers (.NET unit, TS unit, TS↔.NET
E2E).

### Fixed
- TS Fusion: invalidating a still-computing `Computed` no longer throws
  `"Cannot set output on a non-computing Computed."` It now mirrors
  .NET semantics — `invalidate()` called in `Computing` state sets a
  `_invalidatePending` flag, and `setOutput` applies the deferred
  invalidation immediately after transitioning to `Consistent`. This
  surfaced on reconnect when the server delivered result + invalidation
  back-to-back: the `fusion-rpc` client's
  `outboundCall.whenInvalidated.then(...)` microtask fired before
  `compute-function.ts`'s `setOutput`. Covered by the new
  invalidate-during-Computing unit test in `computed.test.ts`.

### Tests
- New **reconnect lifecycle matrix** covering every
  (disconnect-stage × reconnect-stage) cell for both regular RPC and
  Fusion compute calls, in three layers:
  - .NET pure: `RpcReconnectionMatrixTest`,
    `FusionRpcReconnectionMatrixTest`.
  - TS unit: `computed.test.ts` invalidate-during-Computing cases.
  - TS-client ↔ .NET-server E2E: `TypeScriptRpcE2ETest.ReconnectMatrix`
    (driven by `ts/e2e/ts-dotnet-e2e.ts`).
  The F4 cell (disconnect mid-exec, server invalidates during outage,
  reconnect) is exactly the scenario that surfaced the bug above.


## 13.0.12+f471d693 | npm: 13.0.15

Release date: 2026-05-26

Follow-up release to v13.0 focused on connection-loss recovery and
TypeScript catching up to .NET. New HTTP/2 transport guide,
shorter keep-alive defaults, WebSocket leak / hang fixes on both .NET
and TS sides, and `RpcLimits` ported to TypeScript.

### Changed
- RPC: keep-alive defaults tightened from 15 s / 55 s to **10 s / 25 s**
  (`RpcLimits.KeepAlivePeriod` / `KeepAliveTimeout`). The new timeout
  still tolerates a full ~15 s server stall on top of one keep-alive
  cycle while cutting dead-connection detection from ~55 s to ~25 s.
  Reconnects are cheap; the old budget paid for nothing. Override per
  process via `RpcLimits.Default` or per peer if your environment needs
  the old values.
- RPC: `RpcPeer.SetConnectionState` is now `private` and must be
  called only from `OnRun`. External callers (rare) should drive state
  through the transport / connection layer instead.

### Added
- TS RPC: **`RpcLimits`** class mirroring the .NET shape, plumbed
  through `RpcHub.limits` (defaults to a process-wide
  `RpcLimits.Default`). Replaces the loose `CONNECT_TIMEOUT_MS` /
  `HANDSHAKE_TIMEOUT_MS` / `KEEP_ALIVE_*_MS` consts. Three override
  paths now match .NET: mutate `RpcLimits.Default`, assign
  `hub.limits = new RpcLimits({ ... })`, or set the matching `*Ms`
  field on a peer. Peers snapshot values from `hub.limits` at
  construction.
- TS core: ported the remaining `promises.ts` utilities from
  ActualChat so `@actuallab/core` is self-sufficient — `delayAsync` /
  `delayAsyncWith`, `PromiseSourceWithTimeout`, `throttle` / `debounce`
  / `ResettableFunc`, `serialize` (fixes a latent queue-poisoning bug
  from the original), `retry` + `catchErrors`, `abortPromise`,
  `ResolvedPromise.Void/True/False`, `TimedOut` sentinel, and a
  per-package `getLogs` factory. 48 new tests.
- TS core: `PromiseSource<T>` again `implements Promise<T>` — exposes
  `then`/`catch`/`finally` delegators and
  `[Symbol.toStringTag] = 'Promise'`, so it can be awaited or passed
  anywhere a `Promise<T>` is expected. The initial port had dropped
  this surface and forced callers through `.promise`; that's reverted.
  `.promise`, boolean-returning `resolve()`/`reject()`, and
  `isCompleted` semantics are unchanged; `PromiseSourceWithTimeout<T>`
  inherits the restored surface. `.promise` is retained at the RPC
  client proxy (`rpc-client.ts`), which hands the value to user code
  where exposing the `PromiseSource` would risk accidental
  `.resolve()`/`.reject()` calls.
- TS core: `TimeoutError` + `withTimeout(promise, ms, message)`
  helpers, used by the new `connectTimeoutMs` path and the existing
  handshake-timeout block (replaces a string compare on `e.message`).

### Fixed
- RPC: `RpcWebSocketClient` no longer leaks `ClientWebSocket`
  instances when `ConnectAsync` hangs or doesn't honor cancellation —
  the socket is now disposed on the cancellation path. Covered by
  `RpcWebSocketClientConnectLeakTest`.
- RPC: `RpcWebSocketTransport` aborts the underlying WebSocket on
  `ReadAll` cancellation instead of relying on transport disposal,
  preventing hangs in `ReceiveAsync`. New `AbortWebSocket` helper;
  covered by `RpcWebSocketTransportCancellationTest`.
- TS RPC: hung WebSocket connect (mobile after network change /
  device sleep, half-open TCP) no longer blocks the reconnect loop
  for the browser's ~2 min internal timeout. New
  `connectTimeoutMs` (default 10 s) force-closes the socket so the
  loop iterates to the retry-delay branch. Mirrors .NET's
  `RpcLimits.ConnectTimeout`. Covered by
  `rpc-connect-timeout.test.ts`.

### Documentation
- New [HTTP/2 transport guide](PartR-HttpTransport.md) — setup,
  use cases, performance comparison, and configuration options.
  Linked from the RPC sidebar and homepage.
- Videos and Slides page: improved layout, split-button quick-access
  to local decks.


## 13.0.3+26a8a3bd | npm: 13.0.4

Release date: 2026-05-23

This is a **major release** introducing new HTTP/2 transport for ActualLab.Rpc,
`Stream`-based and `PipeReader + PipeWriter`-based transports, and
`RpcAlternatingClient` capable of switching between transports on failures. 

### Breaking Changes
- RPC: `RpcPeer.IsConnected()`, `IsConnected(out handshake, out  transport)`, 
  and `IsConnectedOrHandshaking()` are gone from `RpcPeer`. 
  Equivalent checks now live on `RpcPeer.ConnectionState`:
  - `peer.IsConnected()` &rarr; `peer.ConnectionState.Value.IsConnected()`
  - `peer.IsConnected(out h, out t)` &rarr; `peer.ConnectionState.Value.IsConnected(out h, out t)`
  - `peer.IsConnectedOrHandshaking()` &rarr; `peer.ConnectionState.Value.IsConnectingOrConnected()`
- RPC: `RpcPeerConnectionState.IsHandshaking()` renamed to
  `IsConnecting()`; `IsConnectedOrHandshaking()` renamed to
  `IsConnectingOrConnected()`. Two new helpers: `IsDisconnected()`
  and `IsTerminal()`. State is now also exposed via the new
  `Kind` property (`RpcPeerConnectionStateKind`: `Disconnected`,
  `Connecting`, `Connected`, `Terminal`).

### Added
- RPC: full-duplex **HTTP/2 transport** similar to gRPC's &mdash;
  `RpcHttpClient` / `RpcHttpClientOptions` on the client and
  `RpcHttpServer` / `RpcHttpServerBuilder` / `RpcHttpServerOptions`
  on the server. The client streams requests via `DuplexHttpContent`;
  the server is wired through `EndpointRouteBuilderExt` with default
  delegates in `RpcHttpServerDefaultDelegates`. Protocol negotiation
  in `RpcWebHost` defaults to HTTP/2 when `UseHttpClient` is used.
- RPC: **`RpcAlternatingClient`** &mdash; a composite client that
  alternates connection attempts across multiple inner `RpcClient`s,
  tracking failed endpoints and rotating through them on reconnect.
  Useful for primary/secondary deployments and multi-region failover.
- RPC: **`RpcFrameBasedTransport`** &mdash; new base class for
  batched, frame-oriented transports. `RpcPipeTransport` (PipeReader/
  PipeWriter), `RpcStreamTransport` (Stream-based framing), and the
  existing `RpcWebSocketTransport` all derive from it, sharing frame
  composition, metrics, and buffer renewal logic.
- RPC: `RpcPeer.Extensions` (`MutablePropertyBag`) &mdash; an
  extension-point property bag for attaching ad-hoc state to a peer.

### Changed
- RPC: WebSocket transport updated to share frame composition with
  the new HTTP/2 / pipe / stream transports via
  new `RpcFrameBasedTransport` and `RpcFrameCodec`. 
  The public `RpcWebSocketTransport` surface is preserved, 
  but the internals are now shared.
- RPC: connection-state machine reshaped around a single `Kind`
  discriminator (`Disconnected` / `Connecting` / `Connected` / `Terminal`) 
  instead of inferring state from `Handshake` / `Connection` fields. 
  The `RpcClientPeer` path now fires `RpcClient.OnConnectionStateChange` 
  on every transition to enable error history-based transport changes.

### Documentation
- Homepage / SEO refresh: per-page meta descriptions, OG/Twitter
  cards, JSON-LD, self-referencing canonical URLs, refreshed copy,
  social card, and a sun hero background image.
- Added [Fusion intro slide deck](https://fusion.actuallab.net/slides/fusion-intro/) 
- Linked "Videos and Slides" to the local slide decks.
- Clarified the comment and documentation style guidelines in `CODING_STYLE.md`.

### Tests
- `RpcAlternatingClientTest` covering the alternating logic and
  reconnection behaviour.
- `RpcHttpBasicTest` and `RpcHttpPerformanceTest` for the new
  HTTP/2 transport; obsolete HTTP/2 window-size configuration was
  removed from the test harness.


## 12.5.2+080e9963 | npm: 12.5.2

Release date: 2026-05-09

### Breaking Changes
- RPC: `RpcStream.BufferSize` is split into `RpcStream.AckAdvance` (the
  wire-level flow-control window, formerly named `BufferSize`) and a
  new local-only `RpcStream.BufferSize` (sender ring buffer capacity
  hint, default `0` = inherit `AckAdvance`). The MessagePack wire key
  changes from `BufferSize` to `AckAdvance`; the comma-separated text
  format keeps the same field position. Update any code that sets
  `RpcStream.BufferSize` &mdash; rename to `AckAdvance` if you meant
  the in-flight window, or leave it for the new local-buffering hint
  (and add `AckAdvance = ...` if you want a non-default window).
- TypeScript RPC: `RpcStreamRef.bufferSize`, `RpcStream.bufferSize`,
  and the `bufferSize` option in `RpcStreamOptions<T>` &rarr;
  `ackAdvance`. New optional `bufferSize` controls the local sender
  ring buffer (resolved at `RpcStreamSender` construction time and
  exposed as `sender.bufferSize`).

### Added
- RPC: `RpcStream.BufferSize` (local-only, .NET) /
  `RpcStream.bufferSize` (TypeScript, optional) lets real-time senders
  pre-buffer items past the in-flight ACK window so a freshly arrived
  ACK is served from RAM rather than waiting on the source. Values
  smaller than `AckAdvance` are clamped up to `AckAdvance` and log a
  warning.
- TypeScript RPC: `RpcStreamSender.onBuffered(count)` callback fires
  after every push onto the local ring buffer. Combined with the
  existing `onAckProcessed` (drain side), this gives controllers a
  complete picture of buffer utilisation &mdash; the source pull is
  paused when `bufferedCount === sender.bufferSize`.
- TypeScript RPC: new `RpcConnectionState.Handshaking` state between
  `Connecting` and `Connected`, mirroring .NET's
  `RpcPeerConnectionState` phases. The run loop transitions to it on
  WS open; `RpcServerPeer.accept()` transitions to it immediately.

### Changed
- TypeScript RPC: outbound calls now self-manage their connection
  wait, mirroring .NET `RpcOutboundCall.SendAsync`. Each call
  registers up front and either sends immediately (if `_isConnected`)
  or attaches a one-shot `connectionStateChanged` listener that fires
  on the next `Connected` transition. Removes the peer-level
  `_pendingSends` queue, the `_flushPendingSends` pump, and the
  `_reconnectFlushInProgress` flag. As a side effect, the
  `$sys.Reconnect` mid-reconcile deadlock is gone &mdash; the inner
  reconcile call no longer competes with a peer-level flush gate.

### Documentation
- Updated `PartR-RpcStream.md`, `PartR-D.md`, and `PartTS-Rpc.md` to
  describe `AckAdvance` vs `BufferSize` (and when each applies).

### Tests
- `.NET`: 3 new end-to-end cases in `RpcStreamRealTimeTest`
  (`BufferSizeAboveAckAdvance_PreBuffersBeyondAckWindow`,
  `BufferSizeUnset_FallsBackToAckAdvance`,
  `BufferSizeBelowAckAdvance_ClampsUpToAckAdvance`) backed by a
  burst-tracked source.
- `RpcStreamBasicTest`: defaults, independent property setters,
  `BufferSize` not in wire format.
- TypeScript: 3 new `onBuffered` cases plus refreshed wire-format
  tests asserting `AckAdvance` is now the binary key.


## 12.4.8+77552387 | npm: 12.4.10

Release date: 2026-05-04

### Added
- TypeScript RPC: `RpcStreamSender` observability surface &mdash;
  `nextIndex`, `lastAckIndex`, and `skipCount` getters plus an
  `onAckProcessed` callback that fires once per ACK drain (coalesced
  when multiple ACKs are processed together; listener errors are
  swallowed so they can't break the pump). `RpcStream.sender` accessor
  exposes the local `RpcStreamSender` for callers that need to read
  these metrics &mdash; intended for quality controllers that watch a
  real-time stream's backpressure / skip behaviour without reaching
  into private state.

### Tests
- `rpc-stream-realtime.test.ts`: 7 new cases covering the new metrics
  &mdash; `nextIndex` / `lastAckIndex` accounting, drain coalescing,
  throwing-listener resilience, and a deterministic `skipCount` during
  real-time compaction.


## 12.3.79+2fc189f6 | npm: 12.4.6

Release date: 2026-05-01

### Breaking Changes
- RPC: `RpcStream.AckAdvance` is renamed to `RpcStream.BufferSize` (both
  .NET and TypeScript). Wire format is unchanged &mdash; only the property
  name and the corresponding TypeScript option (`ackAdvance` &rarr;
  `bufferSize`) differ. Update any code that explicitly sets the
  buffer-ahead limit on `RpcStream<T>`.

### Changed
- RPC: `RpcStream.IsRealTime` skipping is now reactive instead of
  speculative. Previously, on hitting the buffer-ahead ceiling the
  sender drained the source itself looking for the next `CanSkipTo`
  item; now the sender waits for an ACK and, when one arrives,
  compacts the already-buffered unsent suffix down to the latest
  skippable item. The sender no longer pulls ahead from the source
  just to hunt for a skip target. Reconnect-time skip-ahead also runs
  only when `IsRealTime` is set; non-realtime streams keep
  back-pressure semantics unchanged. `RpcSharedStream` is consolidated
  to a single `OnRun` path covering both modes.

### Documentation
- Expanded `CODING_STYLE.md` with rules for flow-control spacing,
  class member ordering, primary constructors, sealed classes,
  preferred types like `FilePath`, and TypeScript-side conventions
  mirroring the .NET ones.

### Infrastructure
- Updated Fusion / OAuth / CliWrap package pins in
  `Directory.Packages.props`. `CommunityToolkit.HighPerformance`
  briefly moved to 8.4.2 and was reverted to 8.4.0 (a regression in
  8.4.x is in flight upstream).

### Tests
- `NerdbankCrossCompatTest` is now guarded by `NET8_0_OR_GREATER`,
  and `ShardMapTest` constructs its `HashSet` in a way that compiles
  on .NET Framework targets.


## 12.3.79+d2bf83a0 | npm: 12.3.85

Release date: 2026-04-30

### Added
- TypeScript RPC: `RpcConnectionUrlResolver` may now return `string |
  Promise<string>`, and the connect path `await`s it. This unblocks
  resolvers that need to fetch a per-connection token (e.g. a session
  token) before forming the WebSocket URL.
- TypeScript RPC: `sanitizeUrl(url)` utility exported from
  `@actuallab/rpc` &mdash; redacts `?session=...` (URL-parsed when
  possible, regex fallback otherwise) so the connect-attempt log line no
  longer leaks bearer-style query parameters. Declared as `export let`
  so library users can swap in a different sanitizer (e.g. one that
  redacts additional query keys).


## 12.3.79+a7608a16 | npm: 12.3.83

Release date: 2026-04-30

### Fixed
- TypeScript RPC: proper backpressure in `RpcStream` &mdash; acknowledgements
  are now consumer-driven instead of producer-driven. Previously the
  receiver sent an ACK as soon as an item landed in the buffer, which
  signalled false capacity to the producer and effectively disabled flow
  control. The stream now tracks `_nextConsumedIndex` and only acks up to
  what the iterator has actually yielded; duplicate-frame fast paths cap
  their ack at the consumed index as well, and the iterator loop emits a
  fresh `_maybeSendAck` after each batch is drained. Adds
  `rpc-stream.test.ts` cases covering the consumer-driven ACK behavior.


## 12.3.79+ef249695 | npm: 12.3.81

Release date: 2026-04-29

### Added
- TypeScript RPC: `RpcError` class &mdash; failed remote calls now reject with
  an `RpcError` (instead of a plain `Error`) that carries the remote
  exception's `typeName` when available. The handler parses `.NET`'s
  assembly-qualified `TypeRef` string (e.g. `"System.InvalidOperationException,
  System.Private.CoreLib"`) and exposes the type name only, so TypeScript
  callers can branch on remote exception types. The internal
  `RpcRerouteException` log path now matches against the fully-qualified
  `ActualLab.Rpc.RpcRerouteException`. Exported from
  `@actuallab/rpc`.
- `error-propagation` E2E scenario in `TypeScriptRpcE2ETest` /
  `ts-dotnet-e2e.ts` validating .NET → TS exception propagation.


## 12.3.79+a4fdbdd9 | npm: 12.3.79

Release date: 2026-04-28

### Fixed
- RPC: prevent connection stacking during a mid-handshake state. Connection
  state checks now use `IsConnectedOrHandshaking` instead of `IsConnected`,
  so new connections no longer pile up against peers stuck in transient
  handshake states. Adds teardown safeguards and tightens disconnect
  resolution in edge cases. Affects `RpcPeer`, `RpcServerPeer`,
  `RpcPeerConnectionState`, `RpcWebSocketServer`, and the TypeScript
  `RpcPeer`.


## 12.3.76+d67c674e | npm: 12.3.76

Release date: 2026-04-25

### Added
- RPC: compute methods can now serve a Regular call type (compute-to-regular
  downgrade). When an inbound message targets a `[ComputeMethod]` but its
  `CallTypeId` is `Regular`, the server returns the result immediately and
  skips invalidation tracking &mdash; no entry is retained in the inbound call
  registry past completion. Useful for callers that want a one-shot value
  from a compute method without subscribing to invalidations. Implemented in
  `RpcInboundContext` (accepts the alternate call type) and
  `RpcInboundComputeCall` (new `IsRegularCall` path that unregisters after
  `SendResult`).


## 12.3.74+279ac90c | npm: 12.3.70

Release date: 2026-04-23

### Added
- Nerdbank MessagePack converters for a core set of RPC and serialization
  types: `Result<T>`, `ExceptionInfo`, `VersionSet`, `RpcCacheKey`,
  `RpcCacheValue`, `RpcHandshake`, `RpcHeader`, `RpcHeaderKey`,
  `RpcMethodRef`, and `RpcObjectId`. All converters emit the same
  array-based wire shape used by MessagePack-CSharp, so `msgpackX`
  and `nmsgpackX` RPC formats are now fully byte-compatible across
  runtimes &mdash; clients and servers can mix Nerdbank.MessagePack
  and MessagePack-CSharp without re-serialization. Registered in the
  default `NerdbankMessagePackByteSerializer` configuration.
- `NerdbankCrossCompatTest` cases covering the new RPC converters.


## 12.3.72+94144fd7 | npm: 12.3.70

Release date: 2026-04-23

### Fixed
- Nerdbank `ApiMapNerdbankConverter<TKey, TValue>` and
  `ImmutableOptionSetNerdbankConverter` now also accept the legacy
  array-of-kv-pairs wire shape (`[[k, v], [k, v], ...]`) in addition to
  the standard map shape (`{k: v, k: v, ...}`). Keeps DB blobs written
  by the MessagePack-CSharp source-generated collection formatter
  readable after the v12.3.70 migration to Nerdbank, so no migration
  step is required for existing stored payloads.


## 12.3.70+04d6f22d | npm: 12.3.70

Release date: 2026-04-22

### Added
- Nerdbank MessagePack converters for `PropertyBag`, `ImmutableOptionSet`,
  `ApiMap<TKey, TValue>`, and `TypeDecoratingUniSerialized<T>` &mdash;
  closes the wire-compat gap where Nerdbank's default reflection shape
  couldn't express the legacy `[Key(N)]` index-based layouts used by
  MessagePack-CSharp. Stored blobs written by the legacy serializer
  remain readable. Registered in the default
  `NerdbankMessagePackByteSerializer` configuration.
- `TextSerializedNerdbankConverter<T, TSerialized>` (and closed-over
  `NewtonsoftJsonSerializedNerdbankConverter<T>`) &mdash; fixes a cross-
  serializer wire gap where MessagePack-CSharp wrote
  `NewtonsoftJsonSerialized<T>` as `[Data]` while Nerdbank emitted
  `{Data: ...}`, breaking every composite embedding such a value
  (notably a populated `ImmutableOptionSet`).
- `NerdbankCrossCompatTest` suite &mdash; drives bytes directly between
  MessagePack-CSharp and Nerdbank readers/writers to catch wire-format
  divergence that self-round-trip tests miss.
- TypeScript: `useBigInt64: true` in the default `msgpack` encoder &mdash;
  `bigint` values now serialize as msgpack int64/uint64, required for
  .NET `long` field compatibility when the value exceeds
  `Number.MAX_SAFE_INTEGER`.

### Changed
- `TypeDecoratingUniSerialized<T>` wire format aligned with
  MessagePack-CSharp's `[Key(0)] MessagePackData` layout &mdash; a
  1-element array with type-decorated inner bytes &mdash; so the same
  payload now cross-reads between Nerdbank and MessagePack-CSharp.
- TypeScript: upgraded `@msgpack/msgpack` to v3.1.3; `Encoder`
  initialization refactored to the new object-based configuration API.

### Fixed
- TypeScript RPC: `Dictionary<int, byte[]>` arguments now serialize
  correctly over msgpack (unblocking the `$sys.Reconnect` method
  argument) via a new `msgpack-map-patch.ts` alongside the v3.1.3
  upgrade.


## 12.3.63+373bb905 | npm: 12.3.64

Release date: 2026-04-21

### Added
- TypeScript: `RpcPeer.disconnect()` &mdash; closes the current
  WebSocket connection without disposing the peer. For `RpcClientPeer`,
  the run loop detects the disconnect and reopens the connection; the
  peer stays in the hub and all client proxies bound to it remain valid.

### Changed
- `RpcStream.Disconnect()` is now public (previously an explicit
  `IRpcObject.Disconnect()` implementation forwarding to a protected
  abstract). Callers holding an `RpcStream` reference can now end it
  directly without casting to `IRpcObject`.

### Fixed
- `RpcStream`: reordered the disconnect path so the `$sys.AckEnd`
  close message is always sent before `_isDisconnected` flips. This
  closes a race where a stream disconnecting during the "cannot
  reconnect" branch of `Reconnect` would skip its close notification,
  leaving the remote side waiting.


## 12.3.60+21bf6f76 | npm: 12.3.60

Release date: 2026-04-20

### Added
- Custom MessagePack formatters for core serialization types &mdash;
  `ImmutableBimapMessagePackFormatter`, `ResultMessagePackFormatter`,
  `BoxMessagePackFormatter`, and `MutableBoxMessagePackFormatter`,
  registered in `DefaultMessagePackResolver` and wired up via
  `[MessagePackFormatter]` attributes on `ImmutableBimap`, `Result`,
  `Box`, `MutableBox`, and `ApiArray`. Works around a MessagePack
  source-generator bug that emits incorrect code for struct fields
  relying on default formatters.

### Fixed
- TypeScript: `RpcPeer` could enter a zombie state on reconnect when a
  disconnect happened mid-handshake. Connection tracking now uses a
  dedicated `_isConnected` flag (independent of WebSocket state), which
  tightens state transitions, outbound call gating, and `close()`
  behavior so silent no-ops and lingering zombie peers no longer occur.


## 12.3.56+9d28308c | npm: 12.3.50

Release date: 2026-04-19

### Added
- Fusion: `RemoteComputeMethodFunction` races `SendRpcCall` against peer
  disconnect events &mdash; remote compute calls no longer hang
  indefinitely when the peer drops mid-call; a stale cached value is
  served instead when one is available.
- `RpcPeer.WhenDisconnected` and `MarkDisconnected` &mdash;
  disconnection is now a first-class, awaitable state.
- `RpcPeer.WhenConnectedOrReroute` &mdash; waits for a live connection
  and surfaces reroute exceptions so callers can re-resolve the peer
  instead of blocking on a dead one.
- `RpcRouteState` gained a reroute-aware hook used by the above.

### Changed
- `RpcPeerConnectionStateExt` removed; the `WhenConnected` extension
  methods are gone - use the identical regular method directly, or
  the new `RpcPeer.WhenConnectedOrReroute` helper. State transitions
  (`MarkConnected` / `MarkDisconnected` / `MarkTerminated`) now live on
  `RpcPeerConnectionState` itself.
- RPC peer connection-state handling simplified: state transitions
  consolidated into `RpcPeerConnectionState`, and the disconnection /
  connection-timeout flow in `RpcPeer` tightened. `RpcTestConnection`
  updated to match.

### Fixed
- Additional safeguards around serving stale cache on disconnect so
  reroute exceptions and terminal errors propagate correctly, closing
  race windows that could produce spurious failures right after a peer
  dropped.


## 12.3.50+c3a95b95 | npm: 12.3.50

Release date: 2026-04-18

### Breaking Changes
- TypeScript: `RpcClientPeer` API reshaped &mdash; `peer.run(factory)` is
  replaced by the `webSocketFactory` field plus `peer.start()` (ctor also
  gained a `mustStart = true` parameter); `peer.connected` /
  `peer.disconnected` events are gone &mdash; subscribe to
  `peer.connectionStateChanged` (emits `RpcConnectionState`) or await
  `peer.whenConnected()`; `peer.connectionKind` renamed to
  `peer.connectionState`; and `peer.reconnectDelayer` moved up to
  `RpcHub.reconnectDelayer` so every client peer on a hub shares the
  delayer. Migration: drop `void peer.run()` (auto-starts by default) or
  set `webSocketFactory` + call `peer.start()` when you need
  `mustStart = false`; replace event-based checks with
  `connectionStateChanged` or `whenConnected()`; route
  `cancelDelays()` / `delays = ...` through `hub.reconnectDelayer`.

### Added
- Fusion: remote compute methods now serve the last cached value when
  the peer is disconnected (instead of failing), and auto-invalidate via
  the new `InvalidateWhenReconnected` path once the peer reconnects
  &mdash; improves UI resiliency across brief disconnects.
- TypeScript: `RpcPeerRefBuilder` helper for composing peer refs.
  `RpcPeerRefBuilder.forClient(url, format)` bakes the serialization
  format into the URL via `?f=...`; `RpcPeerRefBuilder.forServer(id)`
  returns `server://{id}`.
- TypeScript: `RpcClientPeer.webSocketFactory` field for injecting a
  custom WebSocket constructor (Node.js / tests).

### Changed
- TypeScript: `RpcClientPeerReconnectDelayer` is now a single instance
  shared via `RpcHub.reconnectDelayer` and centralized there &mdash;
  swap in an app-level subclass (e.g. signal-gated) on the hub before
  peers start.

### Fixed
- TypeScript: reconnection edge cases in `RpcClientPeer` &mdash;
  handshake timeouts now close the connection and retry after the
  configured delay; resolved deadlock scenarios during `$sys.Reconnect`
  calls; addressed lost-close race conditions during `run()` iterations.

### Documentation
- Rewrote `PartTS-Rpc.md`, `PartTS.md`, `PartTS-FusionRpc.md`, and
  `PartTS-React.md` examples against the new `RpcClientPeer` API
  (`start()` / `whenConnected()` / `connectionStateChanged` /
  `hub.reconnectDelayer`).

### Tests
- Added regression coverage for the reconnect edge cases above
  (`rpc-reconnect-edge-cases.test.ts`) and migrated
  `fusion-rpc-run-reconnection.test.ts` to the new API.
- Added `ComputeMethodResultStashTest` (11 cases covering roundtrip,
  disposal cleanup, stash-twice / after-dispose errors, per-key
  serialization, and shared-lock-set ctor) plus a `StashComputeService`
  integration test.


## 12.3.42+3661472f | npm: 12.3.33

Release date: 2026-04-18

### Performance
- Replaced unnecessary `Interlocked.Exchange` calls with `Volatile.Write` /
  `Volatile.Read` across Core, Fusion, and RPC hot paths (`BatchProcessor`,
  `SafeAsyncDisposableBase`, `ArrayPoolBuffer`, `Connector`, `Computed`,
  `ComputedRegistry`, `ComputedSynchronizer`, `ComputedGraphPruner`,
  `RpcObjectTrackers`, `RpcSharedStream`, `RpcWebSocketTransport`).
  Added `InterlockedExt.VolatileRead` / `InterlockedExt.VolatileWrite`
  helpers for cases where a full interlocked operation isn't needed.


## 12.3.39+4e55ed7e | npm: 12.3.33

Release date: 2026-04-17

### Breaking Changes
- `RpcRouteState.LocalExecutionAwaiter` signature changed from
  `Func<CancellationToken, ValueTask>` to
  `Func<bool, CancellationToken, ValueTask>`, and
  `RpcRouteStateExt.PrepareLocalExecution` gained a required `addDependency`
  parameter before `cancellationToken`. Custom awaiters and any direct
  `PrepareLocalExecution` callers must accept and forward the new
  `bool addDependency` argument. `addDependency` is `true` for compute
  method calls - you can use it to actually add a dependency for such
  call on, e.g., the current routing state.

### Documentation
- Reworked all code snippets so every block in the docs is consumed directly
  from a compiled snippet source (no drifted hand-written duplicates).
- Fixed placement of `MustStore(false)` in the operation-context setup
  example and clarified its usage.
- Miscellaneous prose and formatting cleanup across the docs set.


## npm: 12.3.33+59552c71

Release date: 2026-04-16

### Added
- TypeScript: scoped logging API in `@actuallab/core` — `Log`, `LogLevel`,
  and `createLogProvider(prefix, defaults)` factory for per-package, typed
  `getLogs(scope)` helpers. Each `Log.get(scope)` returns a bag of optional
  loggers (`debugLog`, `infoLog`, `warnLog`, `errorLog`) that are `null`
  when below the scope's minimum level — call sites use
  `debugLog?.log(...)` so disabled logs cost a single nullish check.
- TypeScript: `initLogging()` persists user-set minimum levels to
  `sessionStorage` (3-day TTL) and installs a `globalThis.logLevels`
  controller exposing `override(scope, level)`, `overrideAll(prefix, level)`,
  `dump()` (prints every known scope as a `console.table`), `reset()`, and
  `clear()` for runtime tweaking from the browser dev console.
- TypeScript: per-package `getLogs` helpers in `@actuallab/rpc` and
  `@actuallab/fusion` with package-prefixed scopes (e.g. `'rpc.RpcPeer'`,
  `'fusion.ComputedState'`) and explicit per-scope `LogLevel` defaults.
  Global baseline is `Warn`; `rpc.RpcPeer` is `Info` so connection-lifecycle
  events surface out of the box. All other scopes are `Warn` — quiet by
  default; users opt in via `logLevels.override(...)`.

### Changed
- TypeScript: replaced ad-hoc `console.warn` calls across `rpc-peer`,
  `rpc-stream`, `rpc-stream-sender`, `rpc-system-call-handler`,
  `rpc-system-call-sender`, `rpc-hub`, `rpc-service-host`, `rpc-connection`,
  `rpc-peer-state-monitor`, `computed-state`, and `ui-action-tracker` with
  the new scoped logger, mirroring the corresponding .NET log calls.


## 12.3.25+0a851ea7 | npm: 12.3.29

Release date: 2026-04-16

### Breaking Changes
- Removed the `RpcWebSocketServerOptions.ChangeConnectionDelay` option (both
  ASP.NET Core and OWIN/NetFx variants). Stale-connection teardown now
  happens synchronously *before* the WebSocket upgrade, so the dedicated
  delay is no longer meaningful. Drop any code that sets this option.

### Added
- TypeScript: `RpcStream` source factories of the form
  `(abortSignal: AbortSignal) => AsyncIterable<T>` now get a grace
  period (`RpcStream.disconnectGracePeriodMs`, default 100ms) on
  `disconnect()` to honor the AbortSignal and exit cooperatively
  before the sender force-closes via `iterator.return()`. Plain
  `AsyncIterable<T>` sources (which can't observe the signal) are
  force-closed immediately as before.
- TypeScript: `RpcCallStage` constants (`ResultReady`, `Invalidated`,
  `Unregistered`) and `completedStage` tracking on `RpcOutboundCall`,
  both ported from .NET.
- TypeScript: `IncreasingSeqCompressor` in `@actuallab/rpc` — LEB128-based
  sorted-integer-sequence compression, wire-compatible with .NET. Shared
  fixtures between the TS test suite and a new .NET `Theory` confirm
  byte-for-byte wire compatibility for the `$sys.Reconnect` protocol.
- TypeScript: `RingBuffer<T>` in `@actuallab/core` — fixed-capacity
  circular buffer matching .NET `ActualLab.Collections.RingBuffer<T>`.
- TypeScript: `RpcPeer.format` is now a mutable property
  (getter/setter); `RpcServerPeer` accepts an explicit format override,
  letting the test harness align both peers on any supported wire
  format.

### Fixed
- RPC server: stale connections are now disconnected *before* the new
  WebSocket upgrade rather than after. Previously the old-connection
  teardown could consume the client's `HandshakeTimeout` budget on a
  dead socket; performing it before the upgrade consumes `ConnectTimeout`
  instead, which is the correct budget for "waiting for server to be
  ready to talk".
- RPC WebSockets: reduced the default `RpcWebSocketTransport.CloseTimeout`
  from its previous value to **1 second** to limit effective
  `ConnectTimeout` shrinkage and lower the abrupt/graceful-close ratio
  impact on connection handling.
- TypeScript: `RpcClientPeer._reconnect` now runs the `$sys.Reconnect:3`
  protocol on same-peer reconnects to ask the server which call IDs it
  no longer recognizes, and only resends those. Previously the client
  blindly re-sent every in-flight outbound call on every reconnect,
  causing the server to spawn a second handler for streaming calls
  (e.g. ActualChat's `PushAudio`) and double-process the stream. Matches
  .NET `RpcOutboundCallTracker.Reconnect`. TS also now handles incoming
  `$sys.Reconnect` calls: when a peer acts as server, the inbound-call
  tracker is consulted to produce the set of unknown call IDs, wrapped
  in `$sys.Ok` exactly as .NET does.
- TypeScript: `RpcClientPeer._reconnect` now disposes client-owned shared
  objects (e.g. `RpcStreamSender` instances) on peer change, matching
  .NET `RpcPeer.Reset()`. Previously these senders lingered indefinitely
  after a reconnect to a server with a different `hubId`, causing an
  unbounded leak of stream-sender state and source iterators.
- TypeScript: `RpcStreamSender.writeFrom` is now ACK-driven (mirroring
  .NET `RpcSharedStream<T>`): the main loop blocks waiting for a client
  ACK, so while the peer is disconnected no source items are pulled.
  Previously `sendItem` was a no-op when disconnected but the pump kept
  pulling from the source, silently discarding up to thousands of items
  per disconnect window. A bounded replay buffer holds unacknowledged
  items so they can be resent on reconnect.

### Documentation
- Documented `RpcRemoteExecutionMode` in the Call Routing reference page.

### Tests
- New `.NET` `TypeScriptRpcE2ETest.ReconnectNoDuplicate` cross-language
  E2E theory exercises `$sys.Reconnect:3` end-to-end (Node-hosted TS
  client ↔ ASP.NET Core .NET server) for `json5`, `msgpack6`, and
  `msgpack6c`. Verifies the server invokes a long-running `SlowEcho`
  handler exactly once across a same-peer reconnect — the regression
  guard for the audio-double-processing bug.
- New `rpc-reconnect-wire-format.test.ts` locks in byte-level wire
  fixtures for both the JSON and MessagePack shapes of the
  `completedStages` argument.
- New `.NET` `IncreasingSeqCompressorTest.CrossPlatformWireFormatFixtures`
  theory locks in the exact byte output of 10 representative inputs. The
  same fixtures are asserted by the TypeScript
  `increasing-seq-compressor.test.ts` suite, giving us a bidirectional
  wire-compatibility contract for `$sys.Reconnect`.

### Infrastructure
- TypeScript: `Run-Tests.cmd` now sets `CI=1` and `NO_COLOR=1` and uses
  Vitest's `basic` reporter for consistent, silent CI output.


## 12.3.16+47f5b5a0 | npm: 12.3.14

Release date: 2026-04-16

### Added
- New `RpcRemoteExecutionMode` `[Flags]` enum (`AwaitForConnection`, `AllowReconnect`, `AllowResend`)
  giving per-method control over outbound RPC connection waiting, reconnection, and resending behavior
- `RpcMethodAttribute.RemoteExecutionMode` property for overriding the default (`AwaitForConnection | AllowResend`)
  on a per-interface or per-method basis; `NoWait` methods use `0`, compute methods must use `Default`
- TypeScript: matching `RpcRemoteExecutionMode` support in `rpc-service-def`, `rpc-client`, and decorators
- `ShardMapBuilder.Maglev` — Google's 
  [Maglev consistent hashing](https://static.googleusercontent.com/media/research.google.com/en//pubs/archive/44824.pdf)
  algorithm as a new shard map builder with perfect balance (max-min ≤ 1) and lower disruption than
  Rendezvous at higher node counts
- TypeScript: `AbortSignal`-based cancellation for local `RpcStream` sources — `RpcStreamSource<T>`
  now also accepts a factory `(abortSignal: AbortSignal) => AsyncIterable<T>`, letting sources
  release resources (camera, microphone, etc.) promptly on disconnect
- TypeScript: `RpcServerPeer.accept()` now disconnects shared objects on connection close,
  and `onAckEnd()` delegates to `disconnect()` for proper iterator cleanup

### Tests
- Expanded `ShardMapTest.BuilderComparisonTest` with additional `InlineData` scenarios,
  winner/tie tracking, and detailed comparison metrics across builder strategies
- New `RpcRemoteExecutionModeTest` (.NET) and `rpc-remote-execution-mode.test.ts` covering
  connection waiting, reconnection, in-flight calls, and method-definition validation
- New `rpc-stream-cancellation.test.ts` and `NoReconnectStreamSourceCancellationTest` verifying
  source enumerator finalization on client disconnect with `AllowReconnect=false`

### Infrastructure
- TypeScript: ESLint warning cleanup across all packages


## npm: 12.3.6+872c4869

Release date: 2026-04-15

### Changed
- TypeScript: tightened ESLint rules — removed overly permissive overrides for `@typescript-eslint/no-unsafe-*` rules
- TypeScript: cleaned up code, fix ESLint warnings, and removed redundant ESLint disable directives across all packages
- TypeScript: removed `noUncheckedIndexedAccess` from tsconfig and cleaned up all non-null assertion operators (`!`) that were only needed for it


## 12.3.2+f23f4ac2 | npm: 12.3.2

Release date: 2026-04-15

### Added
- .NET and TypeScript: Real-time stream mode with skip-to-keyframe support in `RpcStream`: 
  - New `IsRealTime` / `isRealTime` property
  - New `CanSendTo` / `canSkipTo` property
  - When `IsRealTime` and `AllowReconnect` are both true, reconnection clears the stale buffer and skips to the next `CanSkipTo` item
- TypeScript: `RpcStream` is now dual-mode (local + remote), matching the .NET design — service methods can return 
  `new RpcStream(source, { isRealTime: true, ... })` with full configuration
- TypeScript: `RpcStream.toRef(peer)` method creates and registers an `RpcStreamSender`, starts pumping items, and returns 
  the serialized stream reference (text or binary format)
- TypeScript: `RpcStream.whenSent` property — a `Promise` that resolves when the sender finishes pumping all items
- TypeScript: `RpcStreamOptions<T>` interface for configuring local streams
- TypeScript: `RpcSerializationFormat` and `RpcSerializationFormatResolver` types similar to the .NET ones
- TypeScript: MessagePack format support (`msgpack6` and `msgpack6c`)
- TypeScript: "Compact" call format support (name hash-based method resolution)
- TypeScript: bundled XXH3-64 hash implementation for method name hashing

### Changed
- TypeScript: `rpc-peer.ts` stream dispatch now wraps raw `AsyncIterable` results in `RpcStream` and delegates to `toRef()` instead of creating `RpcStreamSender` directly
- TypeScript: adopted Voxt.ai TypeScript coding style (4-space indent, single quotes, prettier)
- Consolidated C# E2E tests into single class with `[Theory]`

### Documentation
- Documented `IsRealTime`, `CanSkipTo`, and real-time reconnection behavior in `PartR-RpcStream.md`
- Added TypeScript dual-mode `RpcStream` API documentation with `toRef()` and `whenSent` examples

### Tests
- .NET: Comprehensive tests for `RpcStream.IsRealTime` feature 
- .NET: New tests verifying that after disconnect/reconnect, the first item is a keyframe (multiple of `keyFrameInterval`)
- TypeScript: E2E tests for all 3 serialization formats and `isRealTime` stream feature
- TypeScript: Added local-mode `RpcStream` tests (construction, config, iteration, `toRef`, `whenSent`, E2E config propagation)
- TypeScript: Added real-time, reconnect tests, and tests verifying `canSkipTo` filtering on reset ACK
- TypeScript: XXH3-64 implementation tests

### Infrastructure
- TypeScript: added `Run-Lint.cmd` build script, renamed `Install-Packages.cmd` to `Npm-Install.cmd`


## 12.2.4+09f1dc55 | npm: 12.1.115

Release date: 2026-04-04

### Fixed
- Fixed NativeAOT downcast bug in additional places (`MethodDef`, `RpcMiddlewareContext`) 
  with conditional `Unsafe.As` workaround


## 12.2.1+41e24193 | npm: 12.1.115

Release date: 2026-04-03

### Breaking Changes
- `CodeKeeper` API overhauled:
  - New and simpler `XxxCodeKeeper.IExtension` extension points.
  - Removed instance-based `Get<T>()`/`Set<T, TImpl>()`, `AddAction()`, `RunActions()`, `KeepUnconstructable()`, 
    `CallSilently()`, and `FakeCallSilently()` methods 
  - `CodeKeeper` is now a static utility with simplified `Keep<T>()`, `Keep(Type)`, `KeepSerializable<T>()` methods
  - Removed `TypeCodeKeeper`, `SerializableTypeCodeKeeper`, `RpcMethodDefCodeKeeper`
  - `RpcProxyCodeKeeper`, `CommanderProxyCodeKeeper`, `FusionProxyCodeKeeper` are replaced by 
    `ProxyCodeKeeper.IExtension` implementations (`RpcProxyCodeKeeperExtension`, `CommanderProxyCodeKeeperExtension`, 
    `FusionProxyCodeKeeperExtension`)
- `RpcCallTimeouts.LogTimeout` renamed to `DelayTimeout`; `RpcCallTimeouts.DefaultLogTimeout`
  renamed to `DefaultDelayTimeout` — update any code referencing these properties
- `RpcMethodAttribute.LogTimeout` renamed to `DelayTimeout` — update attribute usages in service interfaces
- `RpcOutboundCallOptions.ReroutingDelayer` signature changed — now takes `(RpcMethodDef, int, CancellationToken)`
  instead of `(int, CancellationToken)`

### Added
- `CodeKeeper.IExtension` interface for pluggable trimming/NativeAOT code retention
- `RpcDelayedCallAction` flags enum for configurable delayed call handling: 
   `None`, `Abort`, `Resend`, `Log`, `LogAndAbort`, `LogAndResend`
- `RpcMethodAttribute.DelayAction` property to control per-method delayed call behavior
- `RpcOutboundCallOptions.DelayHandler` for custom delayed call handling logic
- Delayed compute calls are now automatically re-sent by default (`LogAndResend`), improving reliability

### Changed
- NativeAOT sample restructured: all `CodeKeeper.Set`/`RunActions` calls are removed (they're unnecessary now)

### Fixed
- Fixed `RpcSharedStream` using `IsCompleted` instead of `IsCompletedSuccessfully` for task state checks, 
  preventing incorrect behavior when tasks are faulted or cancelled

### Documentation
- Updated benchmark data to v12.1.130 (.NET 10.0.5) with new latency tables


## 12.1.130+4189e1bf | npm: 12.1.115

Release date: 2026-04-01

### Added
- Added `AllowExecuteDeleteAsync` option to `DbLogTrimmerOptions` and `DbSessionInfoTrimmer.Options`, allowing control over whether `ExecuteDeleteAsync` (bulk SQL DELETE) is used in trimmer operations. Defaults to `true` on .NET 7+ and `false` on older targets
- When `AllowExecuteDeleteAsync` is `false` (or on pre-.NET 7), trimmers now fall through to the row-by-row deletion path even on .NET 7+, enabling compatibility with EF providers that don't support `ExecuteDeleteAsync`

### Changed
- `DbAuthServiceBuilder` now registers `DbSessionInfoTrimmer.Options.Default` via factory instead of direct `TryAddSingleton<Options>()`, allowing easier default customization


## 12.1.128+31a47345 | npm: 12.1.115

Release date: 2026-03-30

### Fixed
- Fixed `NullReferenceException` in `AsyncLockSet.Releaser.Dispose()` when the releaser is default-valued (uninitialized)


## 12.1.125+ba936b35 | npm: 12.1.115

Release date: 2026-03-26

### Fixed
- Fixed potential socket errors (SocketError 125 / ECANCELED) during WebSocket connection
  in `RpcWebSocketClient` — the connect timeout `CancellationTokenSource` could fire after
  a successful connect, aborting the already-established socket. Now disposed immediately
  after successful connection to prevent late cancellation from affecting the live socket


## 12.1.123+9dc3aaeb | npm: 12.1.115

Release date: 2026-03-24

### Fixed
- Prevented race condition in `RpcCallTrackers` on mobile app resume that caused
  misleading "delayed call" reports — added keep-alive timeout check to avoid
  premature call timeouts during app resume scenarios

### Changed
- `RpcCallStage` now outputs "None" for stage value 0 instead of a numeric representation


## 12.1.119+8e01cd91 | npm: 12.1.115

Release date: 2026-03-20

### Breaking Changes
- Removed `Arithmetics`, `ArithmeticsProvider`, `Range`, `Tile`, `TileLayer`, `TileStack`
  and all associated types/extensions from `ActualLab.Core` Mathematics namespace.
  These abstractions are no longer part of the library
- Removed `RangeModelBinder` and `RangeModelBinderProvider` from `ActualLab.Fusion.Server`

### Fixed
- `RpcSystemCallSender` and `RpcSharedStream.Batcher._isPolymorphic` now use
  `RpcArgumentSerializer.IsPolymorphic` for polymorphic type checks


## 12.1.114+a74e74b2 | npm: 12.1.115

Release date: 2026-03-18

### Added
- `RpcSerializableAttribute` (`[RpcSerializable]`) — marks abstract types as non-polymorphic 
  for RPC serialization, allowing the underlying serializer's union support 
  (`[JsonDerivedType]`, `[MemoryPackUnion]`, `[Union]`) to handle type discrimination 
  instead of RPC's `TypeRef` wrapping
- `RpcSerializationFormatException` and `RpcWebSocketCloseCode.UnsupportedFormat` — better 
  error handling when client requests a serialization format unknown to the server

### Documentation
- Added "Polymorphic Serialization" section to RPC Serialization docs covering `[RpcSerializable]` usage

### Tests
- Added tests for `[RpcSerializable]` with `NonPolymorphicBase` hierarchy, 
  including `RpcStream` scenarios
- Added tests for unsupported serialization format handling in `RpcWebSocketTest`


## 12.1.107+fe771590 | npm: 12.1.100

Release date: 2026-03-17

### Added
- `SharedFloatPool` and `SharedDoublePool` in `ArrayPools` (`ActualLab.Core`) — 
  shared array pools for `float` and `double` types

### Changed
- Removed `unmanaged` constraint from `NonPoolingArrayPool<T>`, allowing it to work with any type

### Tests
- Added unit tests for `NonPoolingArrayPool<T>` in `ActualLab.Tests`


## 12.1.102+00807c35 | npm: 12.1.100

Release date: 2026-03-17

### Fixed
- `AsyncTaskMethodBuilderExt.FromTask` didn't work — introduced `GenericAccessors<T>` to workaround
  unsafe accessors for generics due to runtime limitations; refactored both generic and untyped
  `FromTask` variant 

### Tests
- Added unit tests for `FromTask` and `GenericFromTask` to validate functionality and correctness


## 12.1.100+b7edfdd5 | npm: 12.1.100

Release date: 2026-03-16

### Breaking Changes (.NET)
- Removed `IsReconnectable` property from `IRpcSharedObject` — replaced by `AllowReconnect` on `IRpcObject`
- `RpcStream.New<T>()` factory method parameter renamed from `isReconnectable` to `allowReconnect`

### Added (.NET)
- `AllowReconnect` property on `IRpcObject` interface — controls whether an RPC object (stream)
  should reconnect or immediately disconnect when a peer connection drops
- `RpcStream<T>` now serializes `AllowReconnect` as a 5th field in the wire format
  (backward-compatible: old 4-field format defaults to `AllowReconnect = true`)
- Server-side `RpcSharedStream` rejects reconnect attempts for `AllowReconnect = false` streams
  and auto-disposes them on disconnect
- `RpcRemoteObjectTracker` disconnects non-reconnectable remote objects on peer disconnect

### Added (TypeScript)
- TypeScript RPC: `allowReconnect` support in `RpcStream`, `RpcStreamSender`, `parseStreamRef()`,
  and `RpcRemoteObjectTracker`

### Documentation
- Updated [RpcStream](PartR-RpcStream.md) docs to reflect `AllowReconnect` replacing `IsReconnectable`

### Tests
- Added `NoReconnectStreamTest` and `NoReconnectStreamDisconnectTest` (.NET)
- Added TypeScript unit tests for `allowReconnect` in `RpcStream`, `RpcStreamSender`, and `parseStreamRef`
- Added TypeScript end-to-end tests for `allowReconnect = false` disconnect behavior
- Cross-language E2E test (`StreamNoReconnect`) verifying `AllowReconnect = false` behavior
  between TypeScript client and .NET server


## 12.1.98+62afac4f | npm: 12.1.69

Release date: 2026-03-10

### Added
- `CpuTimestampBasedVersionGenerator` — a new `VersionGenerator<long>` based on `CpuTimestamp` ticks,
  useful for in-process only high-resolution monotonic versioning
- `IState.GetExistingComputed()` method (it was available via `ComputedInput`, but wasn't a part of `IState`)

### Changed
- Renamed `Versioning/Providers/` folder to `Versioning/Generators/` and moved
  `ClockBasedVersionGenerator` to `ActualLab.Versioning` namespace

### Fixed
- Stale state bug in `ComputedState` during `Recompute`: concurrent invalidation could target
  an already-replaced computed instance, causing the state to miss updates.
  `StateExt.Invalidate` and `Recompute` now use `GetExistingComputed()` instead of `Snapshot.Computed`
  to address that

### Documentation
- Added [Standalone Authentication](PartAA-X.md) guide — explains how to extract Fusion's auth
  system into your own project for full control and simpler code
  

## 12.1.89+ee8734c3 | npm: 12.1.69

Release date: 2026-03-04

### Breaking Changes
- `ShardMap<TNode>` constructor no longer accepts `Func<TNode, IEnumerable<int>>` 
  for custom hashing — use `ShardMapBuilder` parameter instead
- Default shard mapping algorithm changed from greedy to rendezvous hashing — 
  existing shard assignments will differ after upgrade
- `DbLogReader.ProcessBatch` return type changed from `Task<int>` to `Task<Moment>` — 
  subclasses must update their override signatures

### Added
- `ShardMapBuilder` abstraction with two built-in strategies: 
  `GreedyShardMapBuilder` (old behavior) and `RendezvousShardMapBuilder` (new default) 
   for optimal minimal reallocation when nodes change
- `DbEventLogReader.GetMinDelayUntil` for precise delayed event scheduling — 
  log reader now sleeps until the next delayed entry instead of polling on a fixed interval

### Changed
- Improved log processing queries in `DbEventLogReader`: it uses `== LogEntryState.New` filter 
  and `OrderBy(DelayUntil)` for more reliable index utilization

### Fixed
- Operation event processing now schedules precisely based on the earliest
  pending entry's `DelayUntil`, avoiding unnecessary polling
- `DbLogReader.ProcessNewEntries` reworked to support precise sleep-until
  scheduling based on `ProcessBatch` return value

### Documentation
- Replaced Mermaid diagrams with SVG images in architecture docs
- Added animated SVG diagrams for distributed scaling, dependency graphs, caching, and recomputation
- Added TypeScript port documentation section

### Tests
- Added `ShardMapBuilder` comparison tests (greedy vs rendezvous)
- Added delayed event processing test with precise scheduling verification


## 12.1.69+10006328 | npm: 12.1.69

Release date: 2026-02-22

### Added (TypeScript)
- `RpcStream` support in TypeScript RPC client — full streaming with batching,
  reconnection, and end-of-stream handling
- Stream performance test (`StreamInt32`) for benchmarking TypeScript RPC stream throughput
- `RpcType.stream` return type in TypeScript service definitions

### Changed (TypeScript)
- TypeScript RPC method definitions now use `returns: RpcType.noWait` instead of
  a `noWait` boolean flag, improving API consistency
- Simplified wire argument count calculations in TypeScript by assuming
  `CancellationToken` slot as default (removed `ctOffset` option)

### Tests (TypeScript)
- Added comprehensive `RpcStream` unit tests in TypeScript covering batching,
  reconnection, multiple enumeration, and disposal
- New stream performance benchmarks


## 12.1.61+045f13f0

Release date: 2026-02-18

### Added
- "No polymorphism" JSON serialization formats (`json5np`, `njson5np`) for strict
  non-polymorphic RPC serialization
- `RpcLimits.PrematureDisconnectTimeout` for improved connection backoff logic

### Changed
- Connection retry logic now uses `ConnectionAttemptIndex` instead of `TryIndex`
  with handling for premature connection closures
- Default serialization format for `RpcClientPeer` (TypeScript) changed to `json5np`

### Documentation
- `RpcLimits` class documentation

### Tests
- Added TypeScript RPC performance harness


## 12.1.51+8e1051d0

Release date: 2026-02-17

### Added
- `IsSynchronized` and `WhenSynchronized` methods on `State`, `ComputedState`,
  and `MutableState` for streamlined synchronization checks
- `IState.IsSynchronized()`, `IState.WhenSynchronized()`, and `IState.Synchronize()`
  extension methods in `StateExt`
- `KeepProcessedItems` and `KeepDiscardedItems` retention settings in `DbLogReaderOptions`
  for fine-grained control over log item lifecycle

### Tests
- Added TypeScript RPC reconnection scenario tests (both unit and E2E against .NET server)

## 12.1.41+718a0325

Release date: 2026-02-14

### Added
- **Work in progress: TypeScript Fusion client.** Core abstractions, compute methods, 
  `ComputedState`, `MutableState`, invalidation, and `fusion-react` package 
  with React bindings.
- React-based "Todo v3" page in the TodoApp sample showing how to use the client.

### Fixed
- Missing `HasName(...)` calls in `FusionBuilder` for `AddClient`, `AddServer`,
  and `AddDistributedService` methods
- Proper `ArgumentData` formatting in `RpcInboundCall` for improved readability

### Infrastructure
- Added TypeScript monorepo under `ts/` with `@actuallab/core`, `@actuallab/rpc`,
  `@actuallab/fusion`, and `@actuallab/fusion-rpc` packages
- Integrated TypeScript build pipeline into the Todo sample Host project via MSBuild targets


## 12.1.14+28a7e73e

Release date: 2026-02-11

### Fixed
- RPC WebSocket disconnect detection was delayed by ~50 seconds instead of being instant.
  When the server shut down, `RpcPeer.OnRun` awaited `maintainTasks` in a `finally` block
  before cancelling `readerTokenSource`, so `SharedObjects.Maintain()` kept running its
  keep-alive check loop for up to 55s (`KeepAliveTimeout`) before detecting the timeout.
  The fix moves the `readerTokenSource` cancellation before the `maintainTasks` await.


## 12.1.12+0475b1ca

Release date: 2026-02-11

### Breaking Changes
- `mempack6(c)` / `msgpack6(c)` binary protocols no longer persist message size, 
  fixing the compatibility issue with pre-v12 protocols. This seems to be the very
  first release issue attributed to Claude Code: it somehow concluded the size has 
  to be persisted while migrating `WebSocketChannel` to `RpcWebSocketTransport` API, 
  but in reality is wasn't (the code branch persisting the size was disabled via other logic).
  Sorry we caught this just now: the issue is there for two weeks already (from v12.0.9).

### Fixed
- `Option<T>` now always uses explicit `MessagePackFormatter` instead of conditional
  `MessagePackObject(true)` with `SuppressSourceGeneration` on .NET 8+, fixing serialization
  consistency across target frameworks

### Tests
- Added conditional flags (`UseSystemJsonSerializer`, `UseNewtonsoftJsonSerializer`,
  `UseMessagePackSerializer`, `UseMemoryPackSerializer`) in `SerializationTestExt`
  for selectively enabling/disabling specific serializers in tests
- Added serialization round-trip tests for `DbUser`, `DbChat`, `DbMessage`


## 12.1.4+7b59e831

Release date: 2026-02-07

### Added
- New `ActualLab.Serialization.NerdbankMessagePack` package &ndash; optional Nerdbank.MessagePack
  serialization support. You can also register new `nmsgpack6`/`nmsgpack6c`
  RPC formats by calling `RpcNerdbankSerializationFormat.Register()` at startup

### Fixed
- `ApiNullable<T>`, `ApiNullable8<T>`, `ApiOption<T>` now always use explicit
  `MessagePackFormatter` instead of conditionally using `MessagePackObject(true)` on .NET 8+

### Documentation
- Added [api-index.md](https://github.com/ActualLab/Fusion/blob/master/docs/api-index.md) &ndash; condensed type reference (~300 lines) alongside [api-index-full.md](api-index-full.md) (full API index)


## 12.0.85+53469221

Release date: 2026-02-06

### Breaking Changes
- `CpuTimestamp.PositiveInfinity` and `CpuTimestamp.NegativeInfinity` renamed to `MaxValue` and `MinValue`
- `CoarseCpuClock` removed as mostly useless, use `CpuClock` instead; 
- `MomentClockSet` no longer has a `CoarseCpuClock` property and its constructor no longer accepts a `coarseCpuClock` parameter

### Changed
- RPC keep-alive tracking now uses `Moment` (wall-clock time) instead of `CpuTimestamp`,
  fixing reliability issues on Unix systems where CPU sleep could stall timestamps

### Fixed
- `IRpcSharedObject.LastKeepAliveAt` changed from `CpuTimestamp` to `Moment` &ndash;
  `CpuTimestamp` could freeze during CPU sleep on Unix, causing incorrect keep-alive tracking
- `RpcHub.Clock` renamed to `RpcHub.SystemClock` (now uses `SystemClock` instead of `CpuClock`)
  by the same reason

### Documentation
- XML summary descriptions added (auto-generated with Claude Code) to all public types and members
- Added `Api-Index.md` &ndash; a comprehensive type catalog listing all public types
  across `ActualLab.Fusion` NuGet packages


## 12.0.76+7e668fb2

Release date: 2026-02-04

### Fixed
- `RpcRerouteException` handling code no longer attempts to reroute during disposal of `IServiceProvider`,
  preventing potential rerouting cycles during shutdown


## 12.0.70+1775d374

Release date: 2026-02-03

### Fixed
- `RpcStream` with `IsReconnectable == false` fails right on the first enumeration 
  rather than after the reconnection attempt


## 12.0.65+68251969

Release date: 2026-02-03

### Added
- `RpcStream.IsReconnectable` property &ndash; controls whether a stream can be reconnected after disconnection;
  `true` by default, set to `false` to make reconnection attempts fail with `RpcStreamNotFoundException`
- `RpcStreamNotFoundException` &ndash; new exception thrown when attempting to reconnect a non-reconnectable
  or expired stream

### Tests
- Added `FlakyTest.XUnit` dependency for marking time-dependent tests as flaky
- Marked timing-sensitive tests (`ConcurrentTimerSetTest`, `ConcurrentFixedTimerSetTest`) with `[FlakyFact]`
  attribute for improved test reliability


## 12.0.60+9bb19676

Release date: 2026-02-01

### Breaking Changes
- `IOperationEventSource.ToOperationEvent()` signature changed: now accepts `IServiceProvider` parameter
  instead of no parameters (intermediate version accepted `IOperationScope`)
- `MemoryBuffer<T>` replaced with `RefArrayPoolBuffer<T>` &ndash; migrate by updating type references
  and using `ArrayPool<T>` constructor parameter
- `ArrayPoolBuffer<T>`, `ArrayOwner<T>`, and `BufferWriterExt` moved from `ActualLab.IO` to
  `ActualLab.Collections` namespace

### Added
- `RefArrayPoolBuffer<T>` &ndash; a ref struct buffer backed by `ArrayPool<T>` with configurable pool
  and clearing behavior
- `TimeSpan?.ToShortString()` extension method with customizable null fallback value
- `ArrayPools` static class providing common array pool instances

### Changed
- `ArrayPoolBuffer<T>` enhanced with additional constructor overloads and `ToArrayOwner()` method

### Fixed
- `AsyncTaskMethodBuilder` extension methods now check task completion state before accessing
  `Task.Exception`, preventing potential inner exceptions

### Tests
- Improved ActualLab.Rpc test stability with positive `maxWaitTime` validation
- Better test database isolation in `FusionTestBase`

### Documentation
- Added `BatchSize` property documentation to RpcStream flow control section
- Added Docker benchmark results to performance documentation

### Infrastructure
- Added `Clean.cmd` script for cleaning build artifacts on Windows and Unix platforms


## 12.0.45+0ee956d2

Release date: 2026-01-29

### Performance
- Simplified `RpcOutboundMessage` by replacing `WhenSerialized` Task with synchronous `SendHandler` callback;
  dependencies like `RpcOutboundCall.SendXxx`, `RpcStream.OnItem`, `OnBatch`, and `OnEnd` transitioned from `Task` 
  to `void` return type reducing task allocations in the RPC send path, bringing ~20% performance improvement.

### Tests
- Added comprehensive unit tests for `TaskCompletionHandler` covering various task states
  (completed, faulted, cancelled) and all handler variants (1, 2, 3 state objects)

### Infrastructure
- Removed obsolete `docs.sln` file
- Removed JetBrains.Annotations package reference
- Updated package versions: Blazorise 1.8.9 (used only in samples), Bullseye 6.1.0, and CliWrap 3.10.0 (used in Build.csproj)
- Updated analyzer packages: Moq.Analyzers, xunit.analyzers, Roslynator.Analyzers, Meziantou.Analyzer.


## 12.0.34+842f172c

Release date: 2026-01-28

### Added
- Native little-endian serialization support in RPC serializers with optimized memory handling
- `TestServiceProviderTag` to tag test `ServiceProvider`s and allow services to detect whether
  they're running in test containers
- New `MemoryReader` and `SpanWriter` helpers in `ActualLab.Core.IO.Internal`

### Performance
- Improved RPC performance dedicated path for little endian serialization and delegate caching
- Eliminated `RpcPeer.Send` indirection layer - `RpcTransport.Send` now handles error handling directly,
  reducing call stack depth and improving RPC throughput
- Introduced `TaskCompletionHandler` - a pooled helper for attaching completion callbacks to tasks.
  Each instance caches its delegate, and instances are pooled using thread-static pools to minimize allocations
  in hot paths like RPC transport error handling
- Enabled `UseUnsafeAccessors` build configuration for .NET 8+ targets, improving internal reflection-based
  operations performance

### Fixed
- Big endian system support: serialization, RPC, and all dependent code now correctly use
  `BinaryPrimitives.WriteInt32LittleEndian` and similar methods to ensure consistent byte ordering
  across different CPU architectures

### Tests
- Added `ResetClientServices()` calls to all relevant Fusion and RPC tests
- Refactored RPC test organization for better separation of concerns.

### Documentation
- Added interactive BarChart component for performance benchmarks visualization
- Improved Mermaid flowchart styles and edge label rendering
- Updated Performance page with new benchmark visualizations
- Integrated CHANGELOG into the documentation website
- Updated coding style guide to clarify async method naming conventions


## 12.0.9+3e71b6ef

Release date: 2026-01-27

### Breaking Changes
- New `v6` serialization format: `mempack6`, `msgpack6` and their variants with `c` suffix
- All serialization formats below `v5` are gone, use `v11.5.1` if you still need them
- `WebSocketChannel` is replaced by `RpcWebSocketTransport`
- All authentication-related types from `ActualLab.Fusion.Server` are moved to `ActualLab.Fusion.Ext.*` 
  assemblies and changed namespace from `ActualLab.Fusion.Server.Authentication` to `ActualLab.Fusion.Authentication`,
  so referencing `ActualLab.Fusion.Server` assembly now doesn't "drag" the authentication-related types into your project
  (and that was the reason for this change).

### Added
- `mempack6` and `msgpack6` serialization format versions; they offer a tiny improvement (2 bytes per call) 
  over v5, and that's only because the zero-copy serialization in RPC transport layer made v5 a bit less efficient
  (it has to reserve 5 bytes for the message length, because it uses WriteVarUInt32 for length, so v6 changes
  length encoding to regular UInt32).
- `RpcStream.BatchSize` property for controlling stream batching behavior (default: 64, range: 1..1024)
- Real-time stock ticker demo added to `TodoApp` sample.

### Changed
- Consolidated buffer size properties in `RpcWebSocketTransport` (renamed `WriteFrameSize` to `FrameSize`)
- Very significant changes in `ActualLab.Rpc` internals. In particular, old `RpcMessage` is now represented
  by `RpcOutboundMessage` and `RpcInboundMessage` types, all serialization-related methods now accept 
  different arguments, etc.

### Performance
- Zero-copy serialization in RPC transport layer. Earlier an intermediate buffer (`ArgumentData`) was used 
  to serialize RPC call arguments and results. Later `RpcMessage` (envelope) serializer was combining 
  that data with other required pieces (method reference, call ID, etc.) in the final buffer.
  Now the intermediate buffer is eliminated, which significantly boosts RPC performance on large messages.
  `Stream10K` test shows almost 3x speed improvement on 10KB items.
- Switched `WriteChannelOptions` to `UnboundedChannelOptions`
- Buffer renewal and reuse logic was significantly improved as well.

### Documentation
- New documentation website: https://fusion.actuallab.net/


## 11.4.7+3045fd2c

Release date: 2025-01-05

### Added
- Updated EntityFrameworkCore and Npgsql versions to 10.0. 
  There is currently no MySql EF Core provider for EF10, so if you want to use Fusion with MySql,
  the latest version that targets EF9 is 11.4.3; you can also add binding redirects for EF9 manually
  in your project.
- `RpcMethodAttribute` for method-level RPC configuration
- `v5` serialization formats with proper polymorphic `null` value support, 
  including  `json5`, `njson5`, `msgpack5`, `msgpack5c`, `mempackc`, and `mempack5c`.
- New `IRpcMiddleware` stack replacing `IRpcInboundCallPreprocessor`
- `RpcLocalExecutionMode` enum and new `RpcLocalExecutionMode.Constrained`, `RpcLocalExecutionMode.ConstrainedEntry` modes
- `IOperationEventSource` interface with `Operation.AddEvent(...)` overload for event sourcing
- `IWorker.Run` overload with `CancellationToken`.
- Much more robust RPC (re)routing logic in `RpcInterceptor`, `RpcRoutingCommandHandler`, 
  and `RemoteComputeMethodFunction`.

### Changed
- Moved all `RpcXxx` delegates to `RpcXxxOptions` members for clarity.
  E.g., `RpcOutboundCallOptions.RouterFactory` replaces `RpcCallRouter` delegate.
- Renamed `RpcShardRoutingMode` to `RpcLocalExecutionMode`
- Renamed `RpcDefaultSessionInboundCallPreprocessor` to `RpcDefaultSessionReplacer`
- Simplified `RpcSerializationFormatResolver` (the legacy resolver for the "unspecified" format is gone)
- Improved `RpcServiceDef` and `RpcMethodDef` constructors
- Improved `ComputedOptions` caching
- Updated .NET SDK to version 10.0.101.

### Performance
- Multiple improvements in inbound call processing performance, such as
  handcrafted server invokers for most frequent RPC system calls like `$sys.Ok`
- `WebSocketChannel.Options` got `ReadMode`, which can be `Buffered` or `Unbuffered`.
  The new `Unbuffered` mode allows reading directly from `WebSocket` bypassing `ChannelReader`,
  it's used by default now.
- `GetUnsafe` in `GenericInstanceCache` to eliminate some unnecessary type casts
- Overall, v11.4.X is ~5-10% faster on RPC benchmarks.

### Documentation
- Migrated Parts 01-13 from the old tutorial, though only parts 01-03 are truly edited at this point
- Added TOCs to videos on Fusion and ActualLab.Rpc
- Added GitHub workflow for deploying documentation to GitHub Pages: https://fusion.actuallab.net/
- Documentation is a work in progress, and you're welcome to contribute!

### Fixed
- Multiple issues related to RPC rerouting
- `RpcCommandHandler` repeatedly sending commands to the server
- A new bug in `FrameDelayers.MustDelay` method (introduced in late v11.3.X), 
  which effectively disabled RPC frame delaying
- Use of incorrect Handshake index on some reconnection attempts – the issue was rare, but once it happened,
  it was blocking RPC reconnects for ~5 min.
- `Task.Result` usage is replaced with `.GetAwaiter().GetResult()` everywhere (it's faster and safer)
- `$csys.Invalidate` calls (remote invalidation notifications) now trigger 
  `Computed.Invalidate(immediately: true)` call rather than just `Computed.Invalidate()`,
  which eliminates double delay for RPC compute methods that use invalidation delay
- Various minor fixes.

### Tests
- `WebTestHelpers.GetUnusedLocalUri` helper method in `ActualLab.Testing`
- `CapturingLogger` and `CapturingLoggerProvider` in `ActualLab.Testing`
- Improved cancellation and timeout handling in RPC tests
- Added benchmark tests for `Task.Result` vs `Task.GetAwaiter().GetResult()`
- Added `CapturingLogger` unit test


## 11.0.15+ec823882

Release date: 2025-11-05

### Changed
- Returned back `IMutableState.Value` setters (they were removed in v11.0.8)
- Added "Must" prefix to `RpcDefaultCallTracer.TraceInbound` and `TraceOutbound` flags for consistency with bool field naming rules.

### Fixed
- Adjusted `RpcDefaultDelegates.CallTracerFactory` to enable full tracing for server (`RuntimeInfo.IsServer`), and no tracing for the client.

### Changed in Samples
- TodoApp: Updated Aspire SDK to version 9.5.2 and centralized its version configuration
- TodoApp: Temporarily disabled Microsoft Account authentication (the current credentials are expired).


## 11.0.8+1fd1d61afb

Release date: 2025-11-02

### Breaking Changes
- Revamped `RpcServiceBuilder` API. Its new `Inject` method "injects" a service described by `RpcServiceBuilder` into `IServiceCollection` 
- `RpcBuilder` and `FusionBuilder`'s `AddXxx` methods now rely on `RpcServiceBuilder.Inject`
- `IMutableState` and `MutableState` lost `Value` setter; use `Set(...)` methods to set it. Invalidation path tracking is the reason of this change: new `.Set(...)` overloads use `[CallerFilePath]`, `[CallerMemberName]`, and `[CallerLineNumber]` to propagate the origin of change to the invalidation logic, and there is no way to achieve the same with `.Value` property. I may end up returning it with `[Obsolete]` attribute though.
- Renamed `IHasIsDisposed` to `IHasDisposeStatus`
- Removed `RpcServiceMode.DistributedPair` mode and related logic (`Distributed` mode offers more anyway)
- Removed `RpcSwitchInterceptor`;

### Added
- Quite useful `ComputedOptions.ConsolidationDelay` and related APIs (see Releases chat on https://voxt.ai/chat/s-1KCdcYy9z2-uJVPKZsbEo for details) 
- Invalidation path tracking: `InvalidationSource`, `Invalidation.TrackingMode`, and other changes
- `Computed.ToString(InvalidationSourceFormat)` is `Computed.ToString()`, but with invalidation path info
- `FusionMonitor` is now capable of gathering invalidation path statistics. 

### Changed
- More readable output of `MethodInfo.ToShortString()` is now used to dump compute service methods

### Fixed
- Another issue in `RpcCallTracker.TryReconnect` that may block stateful reconnect
- A couple places in Fusion RPC stack where `Computed.Invalidate(immediately: false)` was used instead of `Computed.Invalidate(immediately: true)`  
- Incorrect use of `Sampler`-s in `FusionMonitor`: missing "not" was making it to sample where it had to skip, and vice versa :( That's why statistics was typically 7x exaggerated there (the probability of logging `EveryNth(8)` is 7/8, i.e. close to 1, but it was reporting it as 1/8).


## 10.6.38+254f4ef775

Release date: 2025-10-28

### Changed
- **Breaking**: Replaced `RpcCallRouteOverride` with `RpcCallOptions`, 
  expect more changes in this area
- Removed `RpcNonRoutingInterceptor` and related infrastructure;
  `RpcRoutingInterceptor` does a bit more and equally fast
- Updated CODING_STYLE.md

### Fixed
- `RpcCallTracker.TryReconnect` - `IncreasingSeqCompressor.Serialize` was getting 
  a potentially misordered sequence making fast (stateful) reconnect impossible,
  though stateless reconnect still worked in these cases  
- Renamed `CompleteAsync` class to `Completion` to correct a previous wrong rename
- `RpcRoutingInterceptor` now properly reroutes local calls as well

### Tests
- Added new MeshRpc tests; will be extending them in the near future
- Refactored `FusionTestBase` descendants to move DI of test-specific services to specific tests
- Reorganized DB model classes under `DbModel` namespace in tests


## 10.6.18+af2a52320f

### Fixed
- ActualLab.Generators now add #if-s suppressing [UnconditionalSuppressMessage] and [ModuleInitializer] for .NET Framework 4.7.2 and .NET Standard 2.0.

## 10.6.16+d0431715b7

Release date: 2025-10-27

### Changed
- Renamed `ChannelReadMode` to `WebSocketChannelReadMode`
- Removed `IChannelWithReadMode`, updated `WebSocketChannel` to implement `IAsyncEnumerable` directly.

### Performance
- Adjusted `BoundedChannelOptions` for `ReadChannel` (120→100) and `WriteChannel` (120→500)
  in `WebSocketChannel` to optimize buffering behavior

### Infrastructure
- Added `UnbufferedPushSequence<T>` for unbuffered async data streaming
- Added `[UnsafeAccessor]`-based helpers for `AsyncTaskMethodBuilder<T>` in `AsyncTaskMethodBuilderExt`.

### Documentation
- Replaced `.instructions.md` with updated `AGENTS.md` and `CODING_STYLE.md`


## 10.6.4

Release date: 2025-10-25

### Breaking Changes
- **Breaking**: Removed `FusionDefaults` and `FusionMode` types.
  Use `RuntimeInfo.IsServer`, `ComputedRegistry.Settings`, and `Timeouts.Settings` instead.
- **Breaking**: Removed `RpcMode` type. Use `RuntimeInfo.IsServer` instead.
- **Breaking**: `ComputedRegistry.Instance` is gone, the whole class is now static
- **Breaking**: `Timeouts` and `IGenericTimeoutHandler` are moved to
  `ActualLab.Time` namespace.
- **Breaking**: reworked `OperationEvent` API enabling events with quantized delay.
  Such events have Uuid, which includes its DelayUntil value, and have quantized DelayUntil.
  So you can use them to implement reliable throttling for such activities as post-change
  indexing or AI processing
- **Breaking**: `IHasDelayUntil` interface is removed, and thus its support in
  `OperationEvent` handling logic.
- **Breaking**: .NET Framework 4.7.1 target is replaced with .NET Framework 4.7.2

### Added
- .NET 10 RC2 support
- `allowInconsistent` flag in `Computed/AnyState/ComputedSource.Use()` and
  `.UseUntyped()` methods. Get the value even if it's inconsistent; when called
  inside a compute method, instantly invalidates the newly created computed
  if an inconsistent dependency gets captured.
- `bool Operation.MustStore` property allowing to disable the operation log entry creation;
  this is useful when you're sure you don't need distributed invalidation for the current
  operation - e.g., you know that only the local machine is responsible for exposing modified data.
  When `MustStore` is `false`, a `DbEvent` (in `Processed` state) is created instead of 
  an `DbOperation` entry to verify commit in case of commit failure.
- `OperationScope.CompletionHandlers` allowing to register operation completion handler
  right inside the command handler
- `RpcCallRouteOverride` type allowing to override outgoing RPC call routing 
  (e.g., set destination peer)
- `Moment.Floor`, `Moment.Ceiling`, `Moment.Round`, and `Moment.Convert` methods;
  `Moment.Ceiling` is used to quantize delayed events.  

### Changed
- Moved command routing logic to `RpcRoutingCommandHandler`
- Renamed `ComputeServiceCommandCompletionInvalidator` to `InvalidatingCommandCompletionHandler`
- Improvements in `RetryPolicy` / `IRetryPolicy`, including exception filters
- `ArgumentListG*<...>` is now IL/AOT-trimmable via `ArgumentList.AllowGenerics` feature switch

### Fixed
- Native AOT support in .NET 10
- A bug in `ComputeRegistry` that could cause `ComputedRegistry` 
  to expose a wrong `Computed` due to a race between `ComputedRegistry.Get` 
  and `ComputedRegistry.Register`. If `Get` gets paused between a moment it
  read a handle and resolved its `Target`, the handle with the same `IntPtr` value 
  might get re-allocated.
  This is extremely rare, but nevertheless, we saw this happening in production.
- The identical bug in `RpcObjectTracker` (it's used to track `RpcStream`-s).
- `IDelegatingCommand` now "implements" `IOutermostCommand` (it was supposed to, but didn't)
- `StatefulComponent.StateChanged` handler now suppresses `ExecutionContext` flow
- Bug in `RpcSystemCallSender.Ok` breaking `RpcStream` serialization in call results
- Bug in `RpcStream` enumerator
- Proper framework version check in `CpuTimestamp` for .NET 10 WASM
- Removed `Microsoft.Extensions.Http` reference from `ActualLab.Rpc`

### Performance
- `WebSocketChannel.ReadMode` and corresponding option enabling unbuffered reads
  without use of `Reader` (which is backed by its own `Channel`).
  This option alone improves RPC performance by 5%.
- Use `CancellationToken.None` in `WebSocket` operations in `WebSocketChannel`
  without sacrificing cancellation support 
  (`WebSocket` is properly disconnected on cancellation now)
- `RpcStream` now uses `Memory<byte>` instead of `byte[]` for its buffer.
- Use of `Unsafe.As<T>(x)` instead of `(T)x` in a few key places.
- Improvements in key Fusion components, including 
  `ComputedRegistry`, `Computed`, and `ComputedState`, 
- Improvements in Blazor components, including 
  `StatefulComponentBase` and `ComputedStateComponentBase` 
- Improvements in infrastructure components, including 
  `ArgumentList`, `FixedArray`, `AsyncLock`, and `AsyncLockSet`.

### Infrastructure
- Added `WeakReferenceSlim` (currently unused)
- Added `TaskExt.NeverEnding(CancellationToken)` helper
- Added `FixedTimerSet` and `ConcurrentFixedTimerSet`
- Improved `CancellationTokenSource`-based timeout handling code across the board
- Improved tests.

### Documentation
- Added `.instructions.md` (like AGENTS.md) for agent rules
- Finalized Part01, added examples for `Computed<T>.When()` and `Changes()` methods.


## Key Changes in 2025

- **Documentation website launch** &ndash; https://fusion.actuallab.net/ with VitePress,
  migrated Parts 01–13 from the old tutorial, video TOCs, GitHub Pages deployment workflow.
- **RPC routing and distributed services overhaul** &ndash; new `RpcLocalExecutionMode`,
  `IRpcMiddleware` stack (replacing `IRpcInboundCallPreprocessor`), `RpcRoutingCommandHandler`,
  `RpcCallOptions`, `RpcServiceBuilder.Inject`, and much more robust rerouting logic
  for shard-aware distributed services.
- **Invalidation path tracking and consolidation** &ndash; `InvalidationSource`,
  `Invalidation.TrackingMode`, `ComputedOptions.ConsolidationDelay` for grouping rapid
  invalidations, and `FusionMonitor` invalidation path statistics.
- **.NET 10 support**.


## Key Changes in 2024

- **RPC serialization formats revamp** &ndash; introduced `mempack2`/`msgpack2` binary formats with
  compact method name hashes (`mempack2c`/`msgpack2c`), serialization format negotiation,
  and zero-copy serialization in the transport layer. `RpcByteMessageSerializer` and
  `FastRpcMessageByteSerializer` brought significant throughput gains.
- **`OperationEvent` introduction** &ndash; event handling with `DbEventTestBase`,
- **.NET 9 support and Native AOT** &ndash; full .NET 9 support (RC1 → release), proxy generators
  updated with AOT-compatible code generation (`ProxyCodeKeeper`, `KeepCode`), and
  `[UnsafeAccessor]`-based helpers for internal reflection.
- **.NET 9 support**.


## Key Changes in 2023

- **`ActualLab.Rpc` &ndash; new RPC framework** &ndash; built from scratch starting April 2023,
  replacing the old `Replica`/`Publisher`/`Replicator` bridge with a modern WebSocket-based
  RPC system. Includes `RpcPeer`, `RpcStream`, `RpcHub`, call routing, reconnection,
  shared object tracking, and binary serialization.
- **Compute caching (client-side)** &ndash; `ClientComputeMethodFunction` improvements and
  `RpcCacheKey` for caching RPC compute method results on the client side.
- **Blazor improvements** &ndash; `ComputedState.Options.TryComputeSynchronously` for faster
  initial rendering, improved `ComputedStateComponent`, `BlazorCircuitContext` enhancements,
  and `RpcPeerStateMonitor` for connection state UI.
- **Rename from `Stl.*` to `ActualLab.*`** &ndash; all NuGet packages, namespaces, and project files
  renamed in December 2023 (e.g., `Stl.Fusion` → `ActualLab.Fusion`, `Stl.CommandR` →
  `ActualLab.CommandR`).
- **.NET 8 support** &ndash; full .NET 8 support including `[UnsafeAccessor]` usage,
  IL trimming markup, `[DynamicDependency]` annotations, and AOT-related preparations.


## Key Changes in 2022

- **Roslyn proxy generators** &ndash; `Stl.Generators` package with `ProxyGenerator` for
  compile-time proxy generation via Roslyn source generators, replacing runtime Castle
  DynamicProxy for `[ComputeMethod]` and command service interception.
- **.NET 7 support** &ndash; added .NET 7 targeting (RC2 → release), updated framework
  dependencies, and CI workflows.


## Key Changes in 2021

- **Binary serialization** &ndash; initial `MessagePack` serialization support,
  `IByteSerializer`/`ITextSerializer` abstractions, and `TypeDecoratingSerializer`
  for polymorphic serialization. Refactored text serializers from `ITextWriter`/`ITextReader`
  to simpler Read/Write overloads.
- **OpenTelemetry support** &ndash; initial `System.Diagnostics.DiagnosticSource` integration
  with `ActivitySource`-based tracing for compute methods and command handlers.
- **.NET 6 support**.


## Key Changes in 2020

Year of project inception.

- **Fusion core** &ndash; established the foundational `Computed<T>`, `ComputeMethod`,
  `State`/`MutableState`/`LiveState` abstractions, `ComputedRegistry`, automatic
  dependency tracking, and invalidation pipeline.
- **CQRS / CommandR** &ndash; `Stl.CommandR` command processing pipeline with
  `ICommand<T>`, `CommandContext`, `CommandHandler`, and middleware pipeline
  for orchestrating side-effect-producing operations alongside Fusion's read model.
- **Replica services and WebSocket bridge** &ndash; `ReplicaService`, `Publisher`/`Replicator`
  bridge over WebSockets, `SubscriptionProcessor` for managing live subscriptions.
- **Entity Framework integration** &ndash; `Stl.Fusion.EntityFramework` with `DbOperationScope`,
  `DbAuthService`, `DbSessionInfo`, early multi-tenancy support, and PostgreSQL/MySQL/SQL Server
  provider compatibility.
- **.NET 5 and multi-targeting** &ndash; migrated from .NET Core 3.1 to .NET 5 with
  multi-targeting.
