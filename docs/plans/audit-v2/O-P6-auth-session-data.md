# P6 — Sessions, auth, extension services, EF Core & Redis persistence

Reviewer partition: `src/ActualLab.Fusion/Session/`, `src/ActualLab.Fusion.Ext.Contracts/`,
`src/ActualLab.Fusion.Ext.Services/`, `src/ActualLab.Fusion.EntityFramework{,.Npgsql,.Redis}/`,
`src/ActualLab.Redis/`, `src/ActualLab.Fusion.Blazor{,.Authentication}/`.

Findings: 4 HIGH, 4 MEDIUM, 1 LOW, plus 1 out-of-partition HIGH.

---

### F1. `IKeyValueStore` read methods are client-callable over RPC, defeating `SandboxedKeyValueStore` isolation

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** auth-bypass / info-leak
- **Location:** `src/ActualLab.Fusion.Ext.Services/Extensions/IKeyValueStore.cs:8`,
  `src/ActualLab.Fusion.Ext.Services/Extensions/IKeyValueStore.cs:16`,
  `src/ActualLab.Fusion.Ext.Services/Extensions/FusionBuilderExt.cs:66`,
  `src/ActualLab.Fusion.Ext.Services/Extensions/Services/SandboxedKeyValueStore.cs:113`
- **What:** `IKeyValueStore` is declared as `IComputeService` only — **not** `IBackendService`. Its two
  write commands are carefully marked `IBackendCommand` (so RPC rejects them from a non-backend peer),
  but its three read methods `Get(shard, key)`, `Count(shard, prefix)` and
  `ListKeySuffixes(shard, prefix, pageRef, …)` carry no such marker. When the store is registered the
  documented way (`fusion.AddDbKeyValueStore<TDbContext>()`) inside a server-mode Fusion container, those
  three methods become ordinary client-callable RPC methods with **no session argument and no
  authorization check at all**.
- **Why it matters / attack path:**
  1. `AddDbKeyValueStore` calls `fusion.AddService<IKeyValueStore, DbKeyValueStore<…>>()` with
     `RpcServiceMode.Default` (`Extensions/FusionBuilderExt.cs:66`). In a server app the default mode is
     `Server` — this is exactly what Fusion's own test harness does
     (`tests/ActualLab.Fusion.Tests/FusionTestBase.cs:136` sets
     `fusion.WithServiceMode(RpcServiceMode.Server, true)`, `:149` adds the DB key-value store).
     `FusionBuilder.AddServer` → `Configure(serviceType).IsServer(...)` registers `IKeyValueStore` in the
     RPC service registry, so it is reachable by wire name.
  2. The only RPC-level authorization gate is the backend check in
     `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`
     (`if (MethodDef.IsBackend && !Peer.Ref.IsBackend)`), and `RpcMethodDef.IsBackend` is
     `service.IsBackend || <command param is IBackendCommand>` (`src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:105`).
     A **query** method on a non-backend service is therefore never backend.
  3. `SandboxedKeyValueStore` — whose entire purpose is to constrain a client to keys prefixed with
     `@session/{session.Id}` or `@user/{user.Id}` (`SandboxedKeyValueStore.cs:113`, `:118`) — delegates
     to the very same `IKeyValueStore`. A client that does not have the contract assembly can still send
     the raw RPC method name; the wire protocol is name-based.
  4. Result: any connected client can call
     `IKeyValueStore.ListKeySuffixes("", "@user/", new PageRef<string>(10000))` to enumerate every user's
     keys, then `IKeyValueStore.Get("", "@user/<victimId>/<key>")` to read them, plus everything else the
     application stores in `_KeyValues`. Cross-user data exposure with no session or user check.
- **Evidence:**
  ```csharp
  // IKeyValueStore.cs:8
  public interface IKeyValueStore : IComputeService   // <- not IBackendService
  {
      [CommandHandler] Task Set(KeyValueStore_Set command, …);      // KeyValueStore_Set : IBackendCommand
      [CommandHandler] Task Remove(KeyValueStore_Remove command, …);// KeyValueStore_Remove : IBackendCommand
      [ComputeMethod] Task<string?> Get(string shard, string key, …);          // :16  no guard
      [ComputeMethod] Task<int> Count(string shard, string prefix, …);         // :18  no guard
      [ComputeMethod] Task<string[]> ListKeySuffixes(string shard, string prefix, …); // :20 no guard
  }
  ```
  Contrast with `IAuthBackend : IComputeService, IBackendService`
  (`src/ActualLab.Fusion.Ext.Services/Authentication/IAuthBackend.cs:10`), where the whole service is
  gated and `GetUser(shard, userId)` is consequently unreachable from a client peer.
- **Fix:** Make `IKeyValueStore : IComputeService, IBackendService`. That preserves the intended usage
  (server-side code and `SandboxedKeyValueStore` resolve it locally from DI; backend peers can still call
  it) while making every method — reads included — unreachable from client peers. If some apps genuinely
  need remote raw access, keep the marker and let them opt in explicitly with a separate,
  app-defined contract. Additionally, `AddDbKeyValueStore` / `AddInMemoryKeyValueStore` should pass
  `RpcServiceMode.Local` explicitly rather than inheriting the container default.

---

### F2. Unauthenticated `IAuth.SignOut` creates a DB session row + operation-log row for any attacker-chosen session id

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.cs:84`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbSessionInfoRepo.cs:61`,
  `src/ActualLab.Fusion.Ext.Contracts/Authentication/IAuth.cs:13`,
  `src/ActualLab.Fusion/Session/Session.cs:39`
- **What:** `IAuth.SignOut(Auth_SignOut)` is a plain (non-backend) client-callable command. Its
  DB implementation calls `Sessions.GetOrCreate(dbContext, session.Id, …)`, which **inserts** a
  `_Sessions` row when the session id is unknown, and then `Sessions.Upsert(...)`. Because a `Session`
  is accepted as long as its id is ≥ 8 characters and not `"~"`, a remote peer can drive an unbounded
  number of row insertions plus one `_Operations` row and one cluster-wide invalidation notification per
  call, with **no authentication whatsoever**.
- **Why it matters / attack path:**
  1. Open an RPC/WebSocket connection (no auth required by default).
  2. Repeatedly call `IAuth.SignOut(new Auth_SignOut(new Session(<fresh random 20 chars>)))`.
  3. Per call, server-side:
     - `session.RequireValid()` passes (`SessionExt.IsValid` only rejects `Session.Default`).
     - `DbAuthService.SignOut` → `Sessions.GetOrCreate` → `dbContext.Add(new TDbSessionInfo{…})` +
       `SaveChangesAsync` — a new `_Sessions` row (`DbSessionInfoRepo.cs:65-74`).
     - `Sessions.Upsert(...)` — an update.
     - The command runs inside a `DbOperationScope`, which always adds a `DbOperation` row
       (`Operations/DbOperationScope.cs:259-263`) and, on completion,
       `DbOperationCompletionListener` fires `NotifyChanged` to every host via LISTEN/NOTIFY or Redis
       pub/sub, causing every host in the cluster to read and replay the operation.
  4. `_Sessions` rows are only trimmed after `DbSessionInfoTrimmer.Options.MaxSessionAge = 60 days`
     (`Services/DbSessionInfoTrimmer.cs:30`), so the storage cost is durable.
     The amplification factor (one cheap RPC frame → 3 DB writes + N-host invalidation replay) makes this
     an effective pre-auth DoS against both the database and the whole cluster.
- **Evidence:**
  ```csharp
  // DbAuthService.cs:84 — the non-kick sign-out path, reached with any session id
  var dbSessionInfo = await Sessions.GetOrCreate(dbContext, session.Id, cancellationToken)…;
  var sessionInfo = SessionConverter.ToModel(dbSessionInfo);
  if (sessionInfo is null || sessionInfo.IsSignOutForced) return;
  …
  await Sessions.Upsert(dbContext, session.Id, sessionInfo, cancellationToken)…;
  ```
  ```csharp
  // DbSessionInfoRepo.cs:64-74 — GetOrCreate really inserts
  var dbSessionInfo = await Get(dbContext, sessionId, true, cancellationToken)…;
  if (dbSessionInfo is null) {
      …
      dbSessionInfo = dbContext.Add(new TDbSessionInfo() { Id = sessionId, … }).Entity;
      SessionConverter.UpdateEntity(sessionInfo, dbSessionInfo);
      await dbContext.SaveChangesAsync(cancellationToken)…;
  }
  ```
  Note `InMemoryAuthService.SignOut` correctly uses `GetSessionInfo` and returns when it is `null`
  (`InMemoryAuthService.cs:70-72`); only the DB implementation creates.
- **Fix:** In `DbAuthService.SignOut`, replace `Sessions.GetOrCreate` with `Sessions.Get(dbContext,
  session.Id, forUpdate: true, …)` and return early when the session does not exist — signing out a
  session that was never set up is a no-op anyway. (Optionally set `MustStoreOperation = false` on that
  no-op path so it does not produce an operation-log entry.) Also consider requiring a session-bound
  connection or rate-limiting session-creating commands per peer.

---

### F3. `User.ToClientSideUser()` mutates the process-global `ApiMap<UserIdentity,string>.Empty`, mixing identities across users

- **Severity:** HIGH
- **Confidence:** CONFIRMED (reproduced against the published `ActualLab.Fusion.Ext.Contracts` package)
- **Category:** race / logic / info-leak
- **Location:** `src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs:100-109`,
  `src/ActualLab.Core/Api/ApiMap.cs:10`, `src/ActualLab.Core/Api/ApiMap.cs:14`
- **What:** `ToClientSideUser()` — the helper whose job is to *mask* a user's identities before sending
  the `User` to a client — starts from the shared static `ApiMap<UserIdentity, string>.Empty` and then
  calls `TryAdd` on it. `ApiMap<TKey,TValue>` derives from `Dictionary<TKey,TValue>` and `Empty` is a
  single process-wide instance, so this mutates global state: the "empty" map is no longer empty, every
  `User` constructed afterwards silently inherits the accumulated identities, and every "masked" user
  object returned by the method is literally the *same* map object shared with every other caller.
- **Why it matters / attack path:** This is a shipped, public masking helper on the auth contract type.
  Any application that uses it as intended (`return user.ToClientSideUser();` in a compute method)
  immediately:
  - poisons `ApiMap<UserIdentity,string>.Empty`, so `User.NewGuest()` and every
    `new User(id, name)` (e.g. `DbUserConverter.NewModel()` at
    `Authentication/Services/DbUserConverter.cs:19`) start out with a non-empty `Identities` map;
  - returns to user A a `User` whose `Identities` also lists user B's identity schemas (they are the same
    object) — those are serialized to the client via `User.JsonCompatibleIdentities`
    (`User.cs:41-46`), i.e. cross-user leakage of authentication-provider information;
  - performs unsynchronised `Dictionary` mutation from concurrent request threads, which can corrupt the
    dictionary's internal buckets and hang or crash any thread that later enumerates it (and it *is*
    enumerated on every `User` serialization).
- **Evidence:**
  ```csharp
  // User.cs:100-109
  public virtual User ToClientSideUser()
  {
      if (Identities.IsEmpty) return this;
      var maskedIdentities = ApiMap<UserIdentity, string>.Empty;   // shared static instance
      foreach (var (id, _) in Identities)
          maskedIdentities.TryAdd((id.Schema, "<hidden>"), "");    // mutates the shared static
      return this with { Identities = maskedIdentities };
  }
  ```
  ```csharp
  // ApiMap.cs:10,14
  public sealed partial class ApiMap<TKey, TValue> : Dictionary<TKey, TValue>, …
  { public static readonly ApiMap<TKey, TValue> Empty = new(); … }
  ```
  Repro (`tmp/review-r2/repro-p6`, references the published NuGet package) output:
  ```
  Before: ApiMap<UserIdentity,string>.Empty.Count = 0
  After ToClientSideUser: ApiMap<UserIdentity,string>.Empty.Count = 1
    Empty now contains: Google/<hidden> => ''
  Fresh guest Identities.Count = 1
  Fresh guest Identities is the SAME object as Empty: True
  After Bob: Empty.Count = 2
  Alice's 'masked' map now also lists Bob: Github/<hidden>, Google/<hidden>
  ```
- **Fix:** Build a fresh map:
  ```csharp
  var maskedIdentities = new ApiMap<UserIdentity, string>();
  foreach (var (id, _) in Identities)
      maskedIdentities.TryAdd((id.Schema, "<hidden>"), "");
  ```
  Separately, `ApiMap<TKey,TValue>.Empty` being a mutable `Dictionary` handed out as a default value is a
  latent hazard for every other `ApiMap` user; consider making `Empty` a frozen/immutable instance or at
  minimum documenting that it must never be mutated. (`With`/`WithMany`/`Without` already clone, so this
  is the only offender I found in this partition.)

---

### F4. Per-shard caches keyed by the attacker-controlled `&s=` session tag grow without bound (multi-shard deployments)

- **Severity:** HIGH
- **Confidence:** CONFIRMED (code path traced; not executed against a live multi-shard app)
- **Category:** dos / leak
- **Location:** `src/ActualLab.Fusion.EntityFramework/Sharding/DbShardResolver.cs:58`,
  `src/ActualLab.Fusion.EntityFramework/Sharding/DbShard.cs:11`,
  `src/ActualLab.Fusion.EntityFramework/DbEntityResolver.cs:287`,
  `src/ActualLab.Fusion.EntityFramework/Sharding/ShardDbContextFactory.cs:156`
- **What:** In a sharded deployment the target shard is taken from a tag embedded in the **client-supplied
  session id** (`session.GetTag("s")`). The only validation before the value is used as a dictionary key
  is `DbShard.Validate`, which merely rejects `""` and `"__template"`. Two long-lived caches are then
  populated with `GetOrAdd` *before* the shard is checked against the shard registry, and neither entry is
  ever removed when the subsequent registry check fails.
- **Why it matters / attack path:**
  1. Attacker sends any client-callable session-taking compute call, e.g.
     `IAuth.GetSessionInfo(new Session("aaaaaaaa&s=" + random))` — no authentication required.
  2. `DbAuthService.GetSessionInfo` (`Services/DbAuthService.cs:170`) calls
     `ShardResolver.Resolve(session)` → `DbShard.Validate(session.GetTag("s"))` → the arbitrary string
     passes.
  3. `Sessions.Get(shard, session.Id)` → `DbEntityResolver.Get(shard, key)` →
     `GetBatchProcessor(shard)` → `_batchProcessors.GetOrAdd(shard, … CreateBatchProcessor …)`
     (`DbEntityResolver.cs:287`). A `BatchProcessor` is a live object with an **unbounded channel and at
     least one long-running worker task** (`BatchProcessor.Process` calls `Start()` on first use,
     `src/ActualLab.Core/Async/BatchProcessor.cs:71-77`). It is never removed from `_batchProcessors`
     (the dictionary is only drained in `DisposeAsync`).
  4. `DbHub.CreateDbContext(shard)` → `ShardDbContextFactory.GetDbContextFactorySlow` →
     `_factories.GetOrAdd(shard, …)` (`ShardDbContextFactory.cs:156`). Only afterwards does
     `CreateDbContextFactory` check `ShardRegistry.CanUse(shard)` and throw `NoShard`
     (`:170-171`). The `CacheEntry` that was just inserted is **not** removed on that failure path —
     `Remove(shard, entry)` runs only when the factory is already disposed (`:161-165`).
  5. Net effect: each distinct attacker-chosen shard string permanently costs one worker task, one
     unbounded channel, one CTS and two dictionary entries. A few thousand requests exhaust the thread
     pool / memory; the dictionaries also degrade every subsequent lookup.
  Single-shard deployments are unaffected (`DbShardResolver.Resolve` short-circuits to `DbShard.Single`
  when `HasSingleShard`), so this is specific to sharded/multi-tenant hosts — exactly the deployments
  where it matters most.
- **Evidence:**
  ```csharp
  // DbShardResolver.cs:51-58
  public override string Resolve(object source) {
      if (ShardRegistry.HasSingleShard) return DbShard.Single;
      switch (source) {
      case Session session:
          return DbShard.Validate(session.GetTag(SessionShardTag));   // SessionShardTag == "s"
  ```
  ```csharp
  // DbShard.cs:11 — the whole validation
  public static Func<string, bool> Validator { get; set; } = static shard => !IsSpecial(shard);
  ```
  ```csharp
  // ShardDbContextFactory.cs:149-166
  lock (_lock) { … entry = _factories.GetOrAdd(shard, static (shard1, self) => new CacheEntry(shard1, self), this); }
  if (Volatile.Read(ref _isDisposed) == 0)
      return entry.Value;      // throws NoShard here; `entry` stays in _factories forever
  Remove(shard, entry);        // only reached when already disposed
  ```
- **Fix:** Validate the shard against `ShardRegistry.CanUse(shard)` (or `Shards.Value.Contains`) in
  `DbShardResolver.Resolve` — i.e. reject unknown shards before the value is ever used as a cache key.
  Additionally, make `ShardDbContextFactory.GetDbContextFactorySlow` remove the entry when
  `entry.Value` throws, and have `DbEntityResolver.GetBatchProcessor` only add the processor for a shard
  the registry accepts.

---

### F5. Operation/event log entries are deserialized with `TypeNameHandling.Auto` and no serialization binder; `_Events` values are read as `typeof(object)` and executed as commands

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (configuration and code path); reachability requires DB write access
- **Category:** deserialization
- **Location:** `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`,
  `src/ActualLab.Fusion.EntityFramework/Operations/DbEvent.cs:19`,
  `src/ActualLab.Fusion.EntityFramework/Operations/DbEvent.cs:66`,
  `src/ActualLab.Fusion.EntityFramework/Operations/DbOperation.cs:18`,
  `src/ActualLab.Fusion.EntityFramework/Operations/DbOperation.cs:53`,
  `src/ActualLab.Fusion.EntityFramework/Operations/DbEventProcessor.cs:29-32`
- **What:** `DbOperation` and `DbEvent` persist their payloads with `NewtonsoftJsonSerializer.Default`,
  whose settings enable `TypeNameHandling.Auto` with `TypeNameAssemblyFormatHandling.Simple` and **no
  `SerializationBinder`**. `DbEvent.ToModel()` deserializes `ValueJson` with declared type
  `typeof(object)`, so Json.NET's assignability check is vacuous and *any* `$type` in the row is
  instantiated. If the resulting object implements `ICommand`, `DbEventProcessor.Process` hands it
  straight to `Commander.Call(command, isOutermost: true, …)` — i.e. it executes as a fully privileged
  local command, bypassing the `IBackendService` / `IBackendCommand` RPC gate entirely.
- **Why it matters / attack path:** Anyone who can insert or modify a row in `_Events` (a compromised or
  lower-privileged component sharing the database, a leaked DB credential, an SQL-injection elsewhere in
  the app, or a restored/attacker-supplied backup) obtains:
  - arbitrary type instantiation + property population on **every** host that runs the event log reader
    (classic Json.NET gadget surface, since the declared type is `object`), and
  - arbitrary privileged command execution — e.g. an `AuthBackend_SignIn` row that authenticates an
    attacker-chosen session as any user.
  `_Operations` rows are similarly deserialized into `ICommand` / `PropertyBag` / `ImmutableList<NestedOperation>`
  on every host during invalidation replay, and `PropertyBag` values are `object`-typed.
  This is defense-in-depth rather than a directly remote-reachable bug, but the brief's threat model
  explicitly puts stored operation-log payloads in scope, and the cost of the current configuration is
  full RCE from a database-write foothold.
- **Evidence:**
  ```csharp
  // NewtonsoftJsonSerializer.cs:27-34
  public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
      TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
      TypeNameHandling = TypeNameHandling.Auto,   // no SerializationBinder anywhere
      …
  };
  ```
  ```csharp
  // DbEvent.cs:63-72
  public OperationEvent ToModel() {
      var value = ValueJson.IsNullOrEmpty() ? null : Serializer.Read(ValueJson, typeof(object));
      …
  }
  ```
  ```csharp
  // DbEventProcessor.cs:29-32
  if (value is ICommand command) {
      Log.LogInformation("Processing command event {CommandType}: {Info}", eventType, info);
      await Commander.Call(command, true, cancellationToken)…;
  }
  ```
- **Fix:** Give `DbOperation.Serializer` / `DbEvent.Serializer` a dedicated `NewtonsoftJsonSerializer`
  configured with a strict `SerializationBinder` that only resolves types from an allow-list (types
  implementing `ICommand`/`IOperationEvent` from the app's own assemblies is the minimum useful policy),
  and deserialize `DbEvent.ValueJson` with a declared type of `ICommand` (or a dedicated
  `IOperationEventValue` marker) rather than `object`, so Json.NET's assignability check actually
  constrains the result.

---

### F6. `InMemoryAuthService.GetUserSessions(shard, userId)` is missing `[ComputeMethod]` — `IAuth.GetUserSessions` is never invalidated

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic (cache coherence)
- **Location:** `src/ActualLab.Fusion.Ext.Services/Authentication/Services/InMemoryAuthService.Backend.cs:150`
  (compare `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.Backend.cs:165`)
- **What:** The DB implementation declares the intermediate helper as
  `[ComputeMethod] protected virtual Task<ImmutableArray<(string Id, SessionInfo)>> GetUserSessions(string shard, string userId, …)`.
  The in-memory implementation declares the same helper **without** the attribute, so it is not
  intercepted: `ComputedOptions.Get` returns `null` when no `[ComputeMethod]` is present on the method or
  an interface it implements (`src/ActualLab.Fusion/Configuration/ComputedOptions.cs:59-65`), and this
  overload is on no interface.
- **Why it matters / attack path:** `IAuth.GetUserSessions(Session)` is a compute method whose body calls
  this helper. Because the helper is not a computed, no dependency is recorded, and the
  `_ = GetUserSessions(shard, invSessionInfo.UserId, default)` calls inside every
  `Invalidation.IsActive` block (`InMemoryAuthService.cs:47`, `InMemoryAuthService.Backend.cs:26`, `:100`)
  invalidate nothing — they just re-run the method body. Consequence: a client subscribed to
  `IAuth.GetUserSessions(session)` keeps serving a stale session list indefinitely. Sessions signed in or
  kicked on *other* devices of the same user never appear/disappear, so a "manage my sessions / sign out
  everywhere" UI built on `InMemoryAuthService` shows a session list that silently diverges from reality —
  including still listing sessions the user believes they revoked.
- **Evidence:**
  ```csharp
  // InMemoryAuthService.Backend.cs:150 — no [ComputeMethod]
  protected virtual Task<ImmutableArray<(string Id, SessionInfo SessionInfo)>> GetUserSessions(
      string shard, string userId, CancellationToken cancellationToken = default)
  ```
  ```csharp
  // DbAuthService.Backend.cs:165-167 — the correct form
  [ComputeMethod]
  protected virtual async Task<ImmutableArray<(string Id, SessionInfo SessionInfo)>> GetUserSessions(
      string shard, string userId, CancellationToken cancellationToken = default)
  ```
- **Fix:** Add `[ComputeMethod]` to `InMemoryAuthService.GetUserSessions(string, string, CancellationToken)`.
  (It is already `protected virtual` and async-shaped, so no other change is needed.) A test asserting
  that `IAuth.GetUserSessions` is invalidated when another session of the same user signs out would catch
  regressions of this kind in both implementations.

---

### F7. Session id is not rotated on sign-in (session fixation)

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic / session management
- **Location:** `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:131`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:173-181`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.Backend.cs:65-70`
- **What:** `ServerAuthHelper.UpdateAuthState` binds the *existing* session id to the newly authenticated
  user (`AuthBackend_SignIn(session, …)` → `Sessions.Upsert(dbContext, session.Id, sessionInfo)`), and
  nothing anywhere issues a new session id or a new `FusionAuth.SessionId` cookie at that moment. The
  session id is a long-lived bearer credential, so an id that was known to a third party *before*
  authentication remains valid and fully authenticated *after* it.
- **Why it matters / attack path:** Standard session-fixation shape. An attacker who can plant a chosen
  value in the victim's `FusionAuth.SessionId` cookie before the victim signs in — via a
  cookie-injection foothold on a sibling subdomain, a cookie-tossing bug, an app feature that accepts a
  session id (note `RpcPeerOptionsExt.SessionParameterName = "session"` at
  `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:14` lets a session id travel in a query string),
  or physical/kiosk access — retains a session that is authenticated as the victim once the victim signs
  in. The default `Settings.AllowSignIn = AllowAnywhere` (`ServerAuthHelper.cs:25`) makes the binding
  happen on *any* request, not only on the dedicated sign-in callback, widening the window.
  Mitigating factors: the cookie is `HttpOnly` + `SameSite=Lax`, so this needs a same-site injection
  primitive; that is why this is MEDIUM rather than HIGH.
- **Evidence:**
  ```csharp
  // ServerAuthHelper.cs:120-134 — session is used as-is
  if (httpIsSignedIn) {
      if (isSignedIn && IsSameUser(user, httpUser, httpAuthenticationSchema)) return;
      …
      await SignIn(session, sessionInfo, user, httpUser, httpAuthenticationSchema, cancellationToken)…;
  }
  ```
  ```csharp
  // DbAuthService.Backend.cs:65-70 — the same session.Id keeps its row, now authenticated
  sessionInfo = sessionInfo with { …, AuthenticatedIdentity = authenticatedIdentity, UserId = … };
  await Sessions.Upsert(dbContext, session.Id, sessionInfo, cancellationToken)…;
  ```
- **Fix:** Rotate on privilege change: in `ServerAuthHelper.SignIn`, mint `Session.New()`, run
  `AuthBackend_SignIn` against the new session, force-sign-out the old one, and have the caller write the
  new id to the session cookie (the middleware already re-issues the cookie whenever
  `session != originalSession`, `Middlewares/SessionMiddleware.cs:108-112`). If a hard rotation is too
  disruptive for existing apps, at minimum make it opt-in via `ServerAuthHelper.Options` and document the
  fixation exposure.

---

### F8. `Session` ids have no maximum length or charset constraint, while `DbSessionInfo.Id` is `varchar(256)`

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** dos / logic
- **Location:** `src/ActualLab.Fusion/Session/Session.cs:39`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbSessionInfo.cs:21`,
  `src/ActualLab.Fusion.Ext.Services/Extensions/Services/SandboxedKeyValueStore.cs:113`
- **What:** The `Session` constructor validates only a *minimum* length of 8 (and the special `"~"`).
  There is no upper bound and no character restriction, even though generated ids are always 20
  characters from a 64-symbol alphabet. Meanwhile the persisted key is declared
  `[Key, StringLength(256)]`.
- **Why it matters / attack path:**
  - A remote peer can pass a multi-megabyte session id to any session-taking compute method. Each distinct
    value becomes a distinct `Computed` key held by the registry, a distinct
    `DbEntityResolver`/`BatchProcessor` input, and a distinct SQL parameter. Cheap to send, expensive to
    hold.
  - Ids longer than 256 characters make the `_Sessions` insert fail on providers that enforce
    `varchar(256)` (PostgreSQL, SQL Server, MySQL), turning `AuthBackend_SetupSession` /
    `IAuth.SignOut` into a guaranteed server-side error path rather than a clean rejection.
  - `SandboxedKeyValueStore` builds key prefixes by `string.Format("@session/{0}", session.Id)`, so an
    attacker-chosen id also controls the shape of stored keys (including embedding `/` delimiters, which
    `KeyChecker.MatchesPrefix` treats specially — `SandboxedKeyValueStore.KeyChecker.cs:55-65`). I could
    not construct a cross-session read from this (a victim's generated id never contains `/`), but the
    prefix machinery is being fed unvalidated attacker input.
  - Related, and lower confidence because it depends on the app calling it: every HTTP request that runs
    `ServerAuthHelper.UpdateAuthState` with an unknown session cookie creates a `_Sessions` row plus an
    `_Operations` row (`ServerAuthHelper.cs:109-116` → `AuthBackend_SetupSession`), so an unauthenticated
    request loop with fresh random cookies has the same durable-storage cost described in F2.
- **Evidence:**
  ```csharp
  // Session.cs:36-43
  public Session(string id) {
      // The check is here to prevent use of sessions with empty or other special Ids,
      // which could be a source of security problems later.
      if (id.IsNullOrEmpty() || (id.Length < 8 && id is not ['~']))
          throw Errors.InvalidSessionId(id);
      Id = id;
  }
  ```
  ```csharp
  // DbSessionInfo.cs:21-22
  [Key, StringLength(256)]
  public string Id { get; set; } = "";
  ```
- **Fix:** Enforce an upper bound and a conservative charset in the `Session` constructor — e.g. length
  ≤ 256 and characters restricted to `RandomStringGenerator.DefaultAlphabet` plus the tag separators
  `&` and `=` (and `/` only if session shard/tag syntax needs it). Make the limit a
  `public static int MaxIdLength` so apps with custom id formats can widen it deliberately.

---

### F9. `signIn/{scheme}` and `signOut` pass an unvalidated route/query value to `ChallengeAsync`/`SignOutAsync`

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** dos / error handling
- **Location:** `src/ActualLab.Fusion.Ext.Services/Authentication/Endpoints/AuthEndpoints.cs:42`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/Endpoints/AuthEndpoints.cs:48`,
  `src/ActualLab.Fusion.Ext.Services/Authentication/Endpoints/AuthEndpoints.cs:59`
- **What:** The `scheme` value comes straight from the route (`MapGet("/signIn/{scheme}", …)`,
  `EndpointRouteBuilderExt.cs:21`) or the query string and is handed to
  `httpContext.ChallengeAsync(scheme, …)` / `SignOutAsync(scheme, …)` with no check that the scheme
  exists. ASP.NET Core throws `InvalidOperationException: No authentication handler is registered for the
  scheme '…'`, producing an unhandled 500 (and, in `DetailedErrors` configurations, a stack trace) for a
  trivially craftable URL.
- **Why it matters / attack path:** `GET /signIn/whatever` → 500 on every request; a cheap way to
  generate error-log volume and, on misconfigured hosts, to surface internal type/stack information. The
  `returnUrl` parameter is already correctly validated with `RedirectUrlChecker` (which uses
  `IUrlHelper.IsLocalUrl`), so there is no open-redirect here — this is only the scheme path.
- **Evidence:**
  ```csharp
  // AuthEndpoints.cs:37-48
  public virtual Task SignIn(HttpContext httpContext, string? scheme, string? returnUrl) {
      scheme = scheme.NullIfEmpty() ?? Settings.DefaultSignInScheme;
      returnUrl ??= "/";
      if (!RedirectUrlChecker.Invoke(returnUrl)) returnUrl = "/";   // returnUrl is checked
      …
      return httpContext.ChallengeAsync(scheme, properties);        // scheme is not
  }
  ```
- **Fix:** Call `httpContext.IsAuthenticationSchemeSupported(scheme)`
  (already available at `Authentication/HttpContextExt.cs:28`) and fall back to
  `Settings.DefaultSignInScheme` / return `400` when the scheme is unknown.

---

## Out-of-partition findings

### OP1. Forced sign-out permanently wedges a browser in an infinite redirect loop (`SessionMiddleware`)

- **Severity:** HIGH
- **Confidence:** CONFIRMED (by code reading; not executed end-to-end)
- **Category:** logic / dos
- **Location:** `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:98-105`,
  `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:32-40`
  (P4 file; reported here because the trigger and the validator live in P6)
- **What:** When `ISessionValidator.IsValidSession` returns `false`, `GetOrCreateSession` invokes
  `InvalidSessionHandler` (which signs out the ASP.NET auth cookie and issues a 302 to the *same* URL),
  sets `MustShortCircuitFeature` and `return Session.New()` — **returning before the code that writes the
  session cookie**. The `FusionAuth.SessionId` cookie therefore still holds the invalid id on the next
  request, which repeats the whole sequence.
- **Why it matters / attack path:** `DbAuthService.IsValidSession` is
  `session.IsValid() && !await IsSignOutForced(session, …)`
  (`src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.Base.cs:40-41`). A *forced*
  sign-out sets `IsSignOutForced = true` on the `_Sessions` row permanently (until the 60-day trimmer
  removes it). `ClientAuthHelper.SignOutEverywhere(bool force = true)`
  (`src/ActualLab.Fusion.Blazor.Authentication/ClientAuthHelper.cs:46-47`) defaults to `force: true`, so a
  user clicking "sign out everywhere" — a first-class framework feature — leaves every one of their
  browsers permanently redirect-looping (`ERR_TOO_MANY_REDIRECTS`) on that origin, with **no in-app
  recovery path**: the only fix is manually clearing cookies.
- **Evidence:**
  ```csharp
  // SessionMiddleware.cs:98-112
  if (isInvalid) {
      var mustShortCircuit = await Settings.InvalidSessionHandler(this, httpContext)…;
      if (mustShortCircuit) {
          httpContext.Features.Set(MustShortCircuitFeature.Instance);
          return Session.New();          // <- returns; the cookie below is never written
      }
      session = null;
  }
  session ??= Session.New();
  session = Settings.TagProvider?.Invoke(session, httpContext) ?? session;
  if (Settings.AlwaysUpdateCookie || session != originalSession) {
      …
      responseCookies.Append(cookieName, session.Id, Settings.Cookie.Build(httpContext));
  }
  ```
  ```csharp
  // SessionMiddleware.cs:32-40 — redirects to the same URL, does not touch the Fusion cookie
  await httpContext.SignOutAsync()…;
  var url = httpContext.Request.GetEncodedPathAndQuery();
  httpContext.Response.Redirect(url);
  return true;
  ```
- **Fix:** Write the freshly minted session id to the response cookie *before* short-circuiting, e.g.
  hoist the cookie-append block above the `return Session.New()` (or have
  `DefaultInvalidSessionHandler` delete/replace the `FusionAuth.SessionId` cookie). A guard that refuses
  to redirect more than once per request chain (e.g. a marker query parameter or a one-shot cookie) would
  make the failure mode non-fatal even if a custom handler forgets.

---

## Areas examined

Read in full:

- `src/ActualLab.Fusion/Session/` — `Session.cs`, `SessionExt.cs`, `SessionResolver.cs`,
  `SessionFactory.cs`, `DefaultSessionFactory.cs`, `SessionValidator.cs`, `ISessionValidator.cs`,
  `SessionCommandExt.cs`.
- `src/ActualLab.Fusion.Ext.Contracts/` — all of `Authentication/` (`IAuth`, `User`, `UserIdentity`,
  `SessionInfo`, `SessionAuthInfo`, `UserExt`, `PresenceReporter`, `FusionBuilderExt`) and all of
  `Extensions/` (`ISandboxedKeyValueStore`, `SandboxedKeyValueStoreExt`, `PageRef`, `QueryableExt`,
  `EnumerableExt`, `SortDirection`).
- `src/ActualLab.Fusion.Ext.Services/` — all of `Authentication/` (builders, `ServerAuthHelper`,
  `AuthEndpoints`, `AuthController`, `HttpContextExt`, `IAuthBackend`, and every file under
  `Authentication/Services/`: `DbAuthService.{Base,,Backend}`, `DbUser*`, `DbSessionInfo*`,
  `InMemoryAuthService{,.Backend}`, `DbAuthIsolationLevelSelector`) and all of `Extensions/`
  (`IKeyValueStore`, `KeyValueStoreExt`, `DbKeyValueStore`, `InMemoryKeyValueStore`, `DbKeyValueTrimmer`,
  `DbKeyValue`, `SandboxedKeyValueStore{,.KeyChecker}`, `FusionBuilderExt`, `Internal/Errors`).
- `src/ActualLab.Fusion.EntityFramework/` — project-root files (`DbHub`, `DbEntityResolver`,
  `DbEntityConverter`, `DbContextBuilder`, `DbContextBase`, `DbContextExt`, `DbSetExt`,
  `DbContextOptionsBuilderExt`, `DbOperationsBuilder`, `DbWorkerBase`, `ServiceCollectionExt`),
  `Sharding/` (all), `Operations/` (all), `LogProcessing/` (all), `Internal/` (all),
  `Compatibility/` (skimmed).
- `src/ActualLab.Fusion.EntityFramework.Npgsql/` — all files (`NpgsqlDbLogWatcher`,
  `NpgsqlDbHintFormatter`, `NpgsqlHintQuerySqlGenerator{,Factory}`, builders).
- `src/ActualLab.Fusion.EntityFramework.Redis/` — all files.
- `src/ActualLab.Redis/` — all files (`RedisConnector`, `RedisComponent`, `RedisDb`, `RedisPub`,
  `RedisSubBase`, `RedisActionSub`, `RedisChannelSub`, `RedisTaskSub`, `RedisQueue`, `RedisHash`,
  `RedisSequenceSet`, `RedisStreamer`, `RedisSubKey`, `ServiceCollectionExt`, `Internal/`).
- `src/ActualLab.Fusion.Blazor/` and `src/ActualLab.Fusion.Blazor.Authentication/` — all files.

Read as supporting context (outside my partition):

- `src/ActualLab.Rpc/IBackendService.cs`, `Infrastructure/RpcInboundContext.cs` (backend guard),
  `Configuration/RpcMethodDef.cs`, `Configuration/RpcServiceDef.cs`,
  `Configuration/Options/RpcRegistryOptions.cs` — to establish what is and isn't client-callable.
- `src/ActualLab.Fusion/FusionBuilder.cs`, `Configuration/ComputedOptions.cs`,
  `Interception/ComputedOptionsProvider.cs`, `Interception/ComputeMethodDef.cs` — service-mode and
  compute-method detection semantics.
- `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs`,
  `Rpc/RpcPeerOptionsExt.cs`, `Rpc/RpcDefaultSessionReplacer.cs`, `Rpc/SessionBoundRpcConnection.cs`,
  `Endpoints/RedirectUrlChecker.cs`, `Endpoints/RenderModeEndpoint.cs` — session plumbing.
- `src/ActualLab.Core/Generators/*` (entropy of session/user id generation),
  `src/ActualLab.Core/Api/ApiMap.cs`, `src/ActualLab.Core/Async/BatchProcessor.cs`,
  `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs`.
- `tests/ActualLab.Fusion.Tests/FusionTestBase.cs`,
  `D:\Projects\ActualLab.Fusion.Samples\src\Blazor\Server\Program.cs` — to confirm the real-world
  registration modes used for F1.

Experiment run: `tmp/review-r2/repro-p6` — a standalone console project referencing the published
`ActualLab.Fusion.Ext.Contracts` NuGet package, used to confirm F3. The main working tree was not
modified or built.

Checks performed that produced **no** finding (recorded so the next pass can skip them):

- **Session id entropy** — `DefaultSessionFactory.New()` uses `RandomStringGenerator(20)` over a 64-symbol
  alphabet backed by `RandomNumberGenerator.Create()` (~120 bits, unbiased for power-of-two alphabets).
  Sound.
- **`Session.Hash`** — non-cryptographic XxHash3 truncated to 32 bits, but it is only used to identify one
  of a single user's own sessions (`Auth_SignOut.KickUserSessionHash`), and the kick loop is already
  restricted to that user's session list. Not exploitable.
- **SQL injection** — the only raw SQL in the partition is `NpgsqlDbLogWatcher`'s `LISTEN`/`NOTIFY`
  (`NpgsqlDbLogWatcher.cs:62-63`); the channel name goes through
  `NpgsqlCommandBuilder.QuoteIdentifier` and the payload is the host id with `'` doubled. Every other
  query is LINQ/parameterised. `RedisSequenceSet`'s Lua script passes the key via `ARGV`, not string
  interpolation.
- **`SandboxedKeyValueStore` prefix checking** — I tried to construct a cross-session/cross-user prefix
  escape through `KeyChecker.MatchesPrefix` (delimiter-boundary handling, attacker-chosen session ids
  containing `/`, user-id prefix collisions such as `@user/1` vs `@user/12`). The boundary check is
  correct in all cases I could construct; the sandbox is bypassed via F1 instead, not via the checker.
- **`IAuthBackend`** — correctly marked `IBackendService`, and `AuthBackend_SignIn` /
  `AuthBackend_SetupSession` are `IBackendCommand`, so both the service and the commands are unreachable
  from a client peer. (`AuthBackend_SetSessionOptions` is *not* marked `IBackendCommand`, but its only
  handler lives on the backend service, so the service-level gate still covers it. Worth adding the marker
  for consistency, but not a finding.)
- **Blazor** — `AuthStateProvider` is scoped per circuit, resolves the session through the scoped
  `ISessionResolver`, and disposes its `ComputedState`; `CircuitHub` is scoped; `ComponentInfo` /
  `ParameterComparerProvider` caches are keyed by `Type`, not by user input. No cross-circuit state
  sharing found. `ClientAuthHelper.GetSchemas` uses `JSRuntime.InvokeAsync("eval", …)` but only with a
  static, developer-controlled expression.
- **Redis** — key prefixes come from DI configuration and type names, not user input; pub/sub payloads in
  the log watchers are host ids compared with `string.Equals`. `RedisStreamer<T>.Read` deserializes an
  `ExceptionInfo` out of the stream and calls `ToException()`, which would matter only if the Redis
  instance were attacker-controlled — the type-resolution behaviour of `ExceptionInfo` belongs to P3.

## Areas NOT examined

- `src/ActualLab.Fusion.EntityFramework/Compatibility/EntityFrameworkServiceCollectionExtensions.cs`
  and `Compatibility/IndexAttribute.cs` — only skimmed; they are shims for older EF/TFMs with no
  attacker-reachable input.
- Generated files under `*/obj/**` (MemoryPack / MessagePack formatters) — out of scope for this
  partition; formatter correctness on hostile input is P3.
- `ActualLab.Fusion.Ext.Services` NETSTANDARD/.NET Framework conditional branches
  (`#if NETSTANDARD2_0` paths in `DbSessionInfoRepo.Trim`, `InMemoryAuthService.UpsertSessionInfo`,
  `RandomStringGenerator`) — read but not analysed as deeply as the modern paths.
- The **RPC transport, framing, handshake and argument deserialization** that carry `Session` values to
  these services (P1/P2/P3). I assumed the RPC layer delivers a well-formed `Session` object and only
  verified the backend-service gate at `RpcInboundContext.cs:47`.
- `src/ActualLab.Fusion/Operations/` (operation scope/completion in Fusion core) and
  `src/ActualLab.CommandR/` pipeline internals — P5/P8. I read only enough of `DbOperationScope` and
  `DbEventProcessor` to establish the operation/event-log write and replay paths for F2 and F5.
- No end-to-end execution of a multi-shard deployment (F4) or a forced-sign-out browser session (OP1);
  both were established by code path tracing rather than by running the scenario. A worktree-based
  integration test would be the natural way to confirm them before shipping fixes.
