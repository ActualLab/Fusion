# P8 — ActualLab.Core (rest), Interception, Generators, CommandR, Plugins

Round-2 security & severe-bug review. Partition scope per `PARTITIONS.md` §P8.

Findings are ordered most-severe first.

---

### F1. Unbounded, never-evicted `TypeRef.ResolveCache` lets any remote peer permanently exhaust server memory via wire-supplied type names

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** dos
- **Location:** `src/ActualLab.Core/Reflection/TypeRef.cs:31` (cache declaration),
  `src/ActualLab.Core/Reflection/TypeRef.cs:97` (`Resolve`);
  reachable via `src/ActualLab.Core/Serialization/ExceptionInfo.cs:96` (`TryCreateException`)
  and `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:100` (`$sys.Error`)
- **What:** `TypeRef.Resolve(string)` funnels an arbitrary, attacker-supplied
  assembly-qualified type name into `Type.GetType(...)` and memoizes every
  *successful* resolution in a static, unbounded, never-evicted
  `ConcurrentDictionary<string, Type?>`. Because .NET materializes a brand-new,
  permanently-loaded runtime type for every distinct array shape / generic
  instantiation, a peer can force an unbounded number of distinct types to be
  created and pinned, in both the managed cache and the (never-reclaimable)
  runtime type-loader heap.
- **Why it matters / attack path:**
  1. Attacker opens an RPC WebSocket connection to the server (this is the normal
     `ActualLab.Rpc.Server` endpoint; no application-level authentication is
     required to send system calls).
  2. Attacker sends a `$sys.Error` message. `RpcSystemCalls.Error` runs
     `error.ToException()` **unconditionally**, before it even looks up whether a
     matching outbound call exists:
     ```csharp
     var outboundCallId = context.Message.RelatedId;
     var exception = error.ToException()!;              // RpcSystemCalls.cs:100
     peer.OutboundCalls.Get(outboundCallId)?.SetError(exception, context);
     ```
  3. `ExceptionInfo.ToException()` → `TryCreateException` →
     `exceptionInfo.TypeRef.TryResolve()` → `TypeRef.Resolve(aqn)`.
  4. The attacker sets `TypeRef` to e.g. `System.Int32[,][,,][][,,,,]…` (a fresh
     array shape each time) or a nested `List\`1[[List\`1[[…]]]]`. Each resolves
     successfully, so the `if (result is null) ResolveCache.TryRemove(...)`
     cleanup does **not** fire; the entry and the runtime type stay forever.
     `typeof(Exception).IsAssignableFrom(type)` then fails, the call returns a
     generic `RemoteException`, no exception is thrown, nothing is rate-limited,
     and the peer stays connected — so the loop can be repeated indefinitely.
  5. The leaked memory lives in the type-loader heap, so it is invisible to GC
     pressure heuristics and is only reclaimable by restarting the process.

  I measured the growth in an out-of-repo scratch harness (plain BCL, no Fusion),
  resolving distinct array shapes through the identical `GetOrAdd` +
  `TryRemove-on-null` pattern:

  ```
  start priv=7MB
    n=5000  ok=5000  priv=14MB (+7MB)
    n=10000 ok=10000 priv=21MB (+13MB)
    n=15000 ok=15000 priv=27MB (+20MB)
    n=20000 ok=20000 priv=34MB (+27MB)
  ```

  ≈1.4 KB permanently leaked per tiny message, linear and unbounded; deep nested
  generics cost far more (250 distinct deep `List<…>` instantiations cost 35 MB).
  A few million small frames therefore drives the server to OOM.

  Two secondary consequences of the same call site:
  - `Type.GetType` on a full assembly-qualified name will **probe for and load
    assemblies by name** from the app's probing path, running their module
    initializers — a remote peer chooses which assembly names the server tries to
    load.
  - The value that survives the `Exception` assignability check is then passed to
    `ActivatorExt.CreateInstance` (`src/ActualLab.Core/Reflection/ActivatorExt.cs:131`),
    which emits and permanently caches a `DynamicMethod` constructor delegate for
    every distinct `Exception`-derived type the peer names.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Reflection/TypeRef.cs:31
  private static readonly ConcurrentDictionary<string, Type?> ResolveCache
      = new(HardwareInfo.ProcessorCountPo2, 131, StringComparer.Ordinal);
  ...
  // src/ActualLab.Core/Reflection/TypeRef.cs:97
  public static Type? Resolve(string assemblyQualifiedName)
  {
      var result = ResolveCache.GetOrAdd(assemblyQualifiedName,
          static aqn => Type.GetType(aqn, false, false));
      if (result is null)
          ResolveCache.TryRemove(assemblyQualifiedName, out _); // Potential memory lead / attack vector
      return result;
  }
  ```
  The in-source comment (`// Potential memory lead / attack vector`) confirms the
  hazard was known; the mitigation only removes *failed* lookups, which is exactly
  the case an attacker does not need.
- **Fix:**
  1. Bound `ResolveCache` (size-capped LRU, e.g. a few thousand entries) instead
     of an unbounded `ConcurrentDictionary`, so a hostile key set cannot pin
     memory indefinitely.
  2. Reject pathological names *before* calling `Type.GetType`: cap the AQN
     length, and reject names containing `[` / `` ` `` nesting beyond a small
     depth (legitimate RPC exception/argument type names are shallow).
  3. Better: introduce an explicit allow-list / resolver hook for
     wire-originated `TypeRef`s (`ExceptionInfo.UnknownExceptionTypeResolver`
     already hints at this design) and make `ExceptionInfo.ToException()` and
     `ByteTypeSerializer.FromBytes` use *only* that restricted resolver, never the
     unrestricted `Type.GetType` path.
  4. Independently, `RpcSystemCalls.Error` should look up the outbound call first
     and skip `ToException()` entirely when `RelatedId` matches nothing.

---

### F2. `ActualLab.Plugins` deserializes its plugin cache from a world-writable temp directory with `TypeNameHandling.Auto`, then instantiates the type names it finds

- **Severity:** HIGH
- **Confidence:** CONFIRMED (code path); requires local write access to the app's temp directory
- **Category:** deserialization
- **Location:** `src/ActualLab.Plugins/Internal/CachingPluginFinderBase.cs:73`,
  `src/ActualLab.Plugins/FileSystemPluginFinder.cs:33`,
  `src/ActualLab.Plugins/Internal/PluginHandle.cs:86`,
  `src/ActualLab.Plugins/Internal/PluginCache.cs:23`,
  `src/ActualLab.Plugins/Internal/PluginFactory.cs:21`,
  `src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:29`
- **What:** `FileSystemPluginFinder` caches its discovered `PluginSetInfo` as JSON
  in `FilePath.GetApplicationTempDirectory()` (a predictable subdirectory of the
  system temp dir) and deserializes it back with `NewtonsoftJsonSerializer.Default`,
  whose settings enable `TypeNameHandling.Auto` with no `SerializationBinder`. The
  deserialized `PluginSetInfo` is then treated as trusted: its `TypeRef`s are
  resolved and instantiated through the DI container.
- **Why it matters / attack path:**
  1. `Options.CacheDir` defaults to `FilePath.GetApplicationTempDirectory()`
     (`FileSystemPluginFinder.cs:33`), which is
     `Path.GetTempPath() & GetHashedName($"{appId}_{appDir}")`
     (`src/ActualLab.Core/IO/FilePath.Extras.cs:59`). The name is a deterministic
     function of the entry-assembly name and its directory — both readable by any
     local user — and `Directory.CreateDirectory` is a no-op if the directory
     already exists, so a lower-privileged local user can pre-create it (on Linux,
     `/tmp` is world-writable by default; on Windows, `%TEMP%` is per-user but a
     service running as `LocalSystem`/`NETWORK SERVICE` can share a temp root).
  2. The same user computes the cache key
     (`"v1:{detectIndirect}:{(path, lastWriteFileTime), …}"`,
     `FileSystemPluginFinder.cs:61-70`) from the readable plugin directory and its
     file timestamps, hashes it into the file name
     (`FileSystemCache<TKey,TValue>.GetFileName`,
     `src/ActualLab.Core/Caching/FileSystemCache.cs:154`) and plants the file.
  3. On startup `CachingPluginFinderBase.FindOrGetCachedPlugins` reads it and
     calls
     ```csharp
     protected virtual PluginSetInfo Deserialize(string source)
         => NewtonsoftJsonSerialized.New<PluginSetInfo?>(source).Value ?? PluginSetInfo.Empty;
     ```
     which uses `NewtonsoftJsonSerializer.Default`, i.e.
     `TypeNameHandling = TypeNameHandling.Auto` with the stock
     `DefaultSerializationBinder` (`NewtonsoftJsonSerializer.cs:27-34`). Json.NET
     honours `$type` metadata on read for every member whose declared type is
     assignable from the named type, giving the attacker constructor/`ISerializable`
     gadget surface inside the object graph.
  4. Even without a Json.NET gadget, the plugin path alone is code execution:
     `PluginHandle<TPlugin>.GetInstances` takes the attacker-supplied
     `TypesByBaseTypeOrderedByDependency` map and does
     ```csharp
     // src/ActualLab.Plugins/Internal/PluginHandle.cs:86
     .Select(pi => PluginCache.GetOrCreate(pi.Type.Resolve()).Instance);
     ```
     `pi.Type.Resolve()` is `Type.GetType` on an attacker string,
     `PluginCache.GetOrCreate` resolves `IPluginInstanceHandle<thatType>` and
     `PluginFactory.Create` calls `ActivatorUtilities.CreateInstance(services, type)`
     — running an arbitrary constructor of an arbitrary loadable type inside the
     host process, with DI-resolved arguments. `PluginInstanceHandle.Dispose` then
     also calls `IDisposable.Dispose()` on it.
  5. Note the deserialization failure is swallowed and merely logged
     (`CachingPluginFinderBase.cs:58-60`), so a gadget that throws after firing is
     invisible.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Serialization/NewtonsoftJsonSerializer.cs:27
  public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
      TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
      TypeNameHandling = TypeNameHandling.Auto,     // no SerializationBinder
      ...
  ```
  ```csharp
  // src/ActualLab.Plugins/FileSystemPluginFinder.cs:31-33
  public bool UseCache { get; init; } = true;
  ...
  public FilePath CacheDir { get; init; } = FilePath.GetApplicationTempDirectory();
  ```
  `PluginHostBuilder` registers `FileSystemPluginFinder` as the default
  `IPluginFinder` (`src/ActualLab.Plugins/PluginHostBuilder.cs:35-40`), so this is
  the out-of-the-box configuration for anyone using `ActualLab.Plugins`.
- **Fix:**
  - Serialize/deserialize the plugin cache with a serializer that has
    `TypeNameHandling.None` (the model is a closed, concrete graph — it does not
    need `$type` at all). At minimum, pass an explicit
    `SerializationBinder` restricted to `PluginSetInfo`/`PluginInfo`/`TypeRef`.
  - Do not treat cached metadata as authoritative for *which types to load*:
    re-validate every cached `TypeRef` against the set of types actually
    discovered by scanning (`type.GetCustomAttribute<PluginAttribute>()?.IsEnabled == true`)
    before instantiating anything. The cache should be an optimization, not a
    trust boundary.
  - Default `CacheDir` to a directory owned by the app (next to the app, or a
    per-user location created with restrictive ACLs) rather than the shared temp
    root, and refuse to read a cache file whose directory is group/other-writable.

---

### F3. Command payloads (including raw `Session` ids) are written to logs on failure, ignoring the `INotLogged` marker

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:** `src/ActualLab.CommandR/Diagnostics/CommandTracer.cs:54`,
  `src/ActualLab.CommandR/Rpc/RpcCommandHandler.cs:91`,
  `src/ActualLab.CommandR/Internal/Commander.cs:113`
- **What:** Three CommandR log statements format the whole command object into the
  log message. `Session.ToString()` returns the raw session id, and most
  Fusion commands are `ISessionCommand`s that carry a `Session`. None of the three
  sites checks the `INotLogged` marker interface, even though that marker exists
  precisely for this and *is* honoured elsewhere in the codebase.
- **Why it matters / attack path:** Session ids are bearer credentials in Fusion —
  `Session.Validator` only rejects the default session, so anyone holding the id
  acts as that user. Any client can cause a command to fail (bad arguments, a
  version conflict, an authorization error inside the handler); the failure is
  logged at `Error`/`Warning` level with the full command, so the victim's session
  id lands in the application log, which is typically shipped to a log aggregator,
  a third-party APM, or support tooling that has a much wider audience than the
  session itself.

  For `CommandTracer` the log line fires when an `ActivityListener` is subscribed
  to `CommanderInstruments.ActivitySource` (i.e. whenever OpenTelemetry tracing is
  enabled — otherwise `StartActivity` returns `null`). `RpcCommandHandler` and
  `Commander.OnUnhandledEvent` have no such precondition.

  `CommandTracer` is registered as a default handler for every commander
  (`src/ActualLab.CommandR/CommanderBuilder.cs:62-64`).
- **Evidence:**
  ```csharp
  // src/ActualLab.CommandR/Diagnostics/CommandTracer.cs:49-54
  var message = context.IsOutermost ?
      "Outermost command failed: {Command}" :
      "Nested command failed: {Command}";
  var level = activity.Status is ActivityStatusCode.Error ? LogLevel.Error : LogLevel.Warning;
  Log.IfEnabled(level)?.Log(level, e, message, command);
  ```
  ```csharp
  // src/ActualLab.CommandR/Rpc/RpcCommandHandler.cs:91
  Log.LogWarning(e, "Rerouting command #{RerouteCount}: {Command}",
      rerouteCount, context.UntypedCommand);
  ```
  ```csharp
  // src/ActualLab.CommandR/Internal/Commander.cs:113
  Log.LogWarning("Unhandled event: {Event}", command);
  ```
  The marker exists and is honoured in Fusion:
  ```csharp
  // src/ActualLab.Fusion/Operations/Internal/CompletionProducer.cs:42
  if (command is not INotLogged || Settings.IgnoreNotLogged)
      Log.IfEnabled(...)?.Log(..., "…Command: {Command}", …, command);
  ```
  and `Session.ToString()` is the raw id:
  ```csharp
  // src/ActualLab.Fusion/Session/Session.cs:110
  public override string ToString() => Id;
  ```
  `AuthBackend_SetupSession` is explicitly marked `INotLogged`
  (`src/ActualLab.Fusion.Ext.Services/Authentication/IAuthBackend.cs:39`) — the
  intent is clear, the enforcement is just missing in CommandR.
- **Fix:** Gate all three sites on `command is not INotLogged` (mirroring
  `CompletionProducer`), and prefer logging `command.GetType().GetName()` plus
  `Session.Hash` rather than the full payload. Optionally make `Session` redact
  itself in `ToString()` and expose the raw id only via `.Id`, so the marker is a
  second line of defence rather than the only one.

---

### F4. `RpcInboundCommandHandler` dispatches on the command's runtime type, so an abstract/interface command parameter defeats the `IsBackend` gate

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (no shipped contract in this repo has the required shape; it is reachable in user contracts)
- **Category:** auth-bypass
- **Location:** `src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:37`,
  supporting: `src/ActualLab.Rpc/Configuration/RpcMethodDef.cs:105`,
  `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:47`,
  `src/ActualLab.Rpc/Serialization/RpcArgumentSerializer.cs:40`
- **What:** The backend-only check happens once, statically, against the *declared*
  first-parameter type of the RPC method
  (`RpcMethodDef.IsBackend = service.IsBackend || isBackend`, where `isBackend`
  comes from `IsCommandType(ParameterTypes[0], out isBackendCommand)`), and is
  enforced in `RpcInboundContext` as `MethodDef.IsBackend && !Peer.Ref.IsBackend`.
  But `RpcInboundCommandHandler` then hands the *deserialized instance* to the
  commander, and `CommandHandlerResolver` selects handlers by
  `command.GetType()`. When the declared parameter type is abstract or an
  interface, `RpcArgumentSerializer.IsPolymorphic` is true and the wire chooses the
  concrete type, so the type that was security-checked and the type that is
  dispatched are different.
- **Why it matters / attack path:** Suppose an application exposes a
  client-reachable RPC method whose command parameter is an interface or abstract
  base, e.g. `Task<Unit> Run(IMyCommand command, CancellationToken ct)`.
  `typeof(IMyCommand).IsAbstract` is `true`, so `IsPolymorphic` returns true and
  `RpcByteArgumentSerializerV4.PolyDeserialize` resolves the concrete type from the
  wire, subject only to `expectedType.IsAssignableFrom(itemType)`
  (`src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:88`). A
  non-backend client can therefore send a derived command that implements
  `IBackendCommand`; `RpcMethodDef.IsBackend` was computed from the *base* type and
  is `false`, so `RpcInboundContext.cs:47` does not reject the call, and
  `RpcInboundCommandHandler` runs the derived command through the full commander
  pipeline, reaching the backend-only handler.
  Two things make this worth fixing rather than dismissing:
  - `Errors.BackendCommandRequiresBackendPeer()`
    (`src/ActualLab.CommandR/Internal/Errors.cs:16`) exists but is **never thrown
    anywhere in the repo**, and the `IBackendCommand` doc comment claims
    "`CommandServiceInterceptor` is responsible for checks associated with this
    interface" — `CommandServiceInterceptor.CreateTypedHandler`
    (`src/ActualLab.CommandR/Interception/CommandServiceInterceptor.cs:31-45`)
    contains no such check. The documented second line of defence does not exist.
  - The middleware deliberately removes `RpcRouteValidator` from the pipeline
    (`RpcInboundCommandHandler.cs:28`), so it is the only remaining gate.
- **Evidence:**
  ```csharp
  // src/ActualLab.CommandR/Rpc/RpcInboundCommandHandler.cs:35-47
  return call => {
      commander ??= call.Hub.Services.Commander();
      var args = call.Arguments!;
      var command = (ICommand<T>?)args.Get0Untyped()!;   // runtime type is wire-chosen
      ...
      var commandContext = CommandContext.New(commander, command, isOutermost: true);
      commandContext.Items.KeylessSet(call);
      return commandContext.Call(cancellationToken);
  };
  ```
  ```csharp
  // src/ActualLab.CommandR/Configuration/CommandHandlerResolverExt.cs:12
  public static CommandHandlerChain GetCommandHandlerChain(this CommandHandlerResolver resolver, ICommand command)
      => resolver.GetCommandHandlers(command.GetType()).GetHandlerChain(command);
  ```
  ```csharp
  // src/ActualLab.Rpc/Serialization/RpcArgumentSerializer.cs:40
  public static bool IsPolymorphic(Type type)
      => (type.IsAbstract || type == typeof(object)) && RpcSerializableAttribute.Get(type) is null;
  ```
- **Fix:** Re-check the *actual* command instance in
  `RpcInboundCommandHandler.Create`, before `CommandContext.New`:
  if the deserialized command's runtime type implements `IBackendCommand` and
  `call.Context.Peer.Ref.IsBackend` is false, throw
  `Errors.BackendCommandRequiresBackendPeer()` (the already-written, currently dead
  error). This makes the check type-accurate regardless of the declared parameter
  type, and makes the `IBackendCommand` XML doc true.

---

### F5. `LazySlim<…>` uses double-checked locking without a volatile read or memory barrier

- **Severity:** MEDIUM
- **Confidence:** PLAUSIBLE (weak-memory-model architectures only: ARM64 / Apple Silicon / Graviton / Ampere)
- **Category:** race
- **Location:** `src/ActualLab.Core/LazySlim.cs:60`, `src/ActualLab.Core/LazySlim.cs:115`,
  `src/ActualLab.Core/LazySlim.cs:175`
- **What:** All three `LazySlim` variants publish two independent, non-volatile
  fields from inside a lock (`field = f.Invoke(); _factory = null;`) and then read
  them on the fast path *without* the lock and without any acquire barrier:
  ```csharp
  if (_factory is null) return field;
  ```
  `field` is not reached through `_factory`, so there is no address dependency to
  order the two loads. On a weakly-ordered CPU a reader can observe
  `_factory == null` while still seeing the pre-initialization value of `field`.
- **Why it matters / attack path:** `LazySlim` is used on hot, widely-shared paths:
  `MethodDef._defaultResultLazy` / `_defaultUnwrappedResultLazy`
  (`src/ActualLab.Interception/MethodDef.cs:124-125`),
  `RuntimeCodegen.DefaultModeLazy` (`src/ActualLab.Core/Reflection/RuntimeCodegen.cs:21`),
  `ArgumentList.InvokerCache` values (`src/ActualLab.Interception/ArgumentList.cs:19`),
  `PluginInfoProvider._pluginCache` (`src/ActualLab.Plugins/PluginInfoProvider.cs:35`).
  A stale read returns `default(TValue)` — `null` for the reference cases — which
  surfaces as a sporadic `NullReferenceException` or a silently wrong default
  result deep inside interception/argument-list machinery, on exactly the kind of
  machine (ARM64 cloud instance) that is now common in production. It is
  non-deterministic and effectively undiagnosable from a stack trace.
  The rest of the codebase is aware of this class of hazard —
  `Sampler.ToConcurrent`/`EveryNth`/`Random` all end with an explicit
  `Thread.MemoryBarrier()` before publishing
  (`src/ActualLab.Core/Diagnostics/Samplers.cs:52,82,104`) — `LazySlim` just misses it.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/LazySlim.cs:57-77
  public TValue Value {
      get {
          // Double-check locking
          if (_factory is null) return field;     // plain (non-volatile) loads, no acquire
          lock (this) {
              switch (_factory) {
              case null: return field;
              case Func<TValue> f: field = f.Invoke(); break;
              ...
              }
              _factory = null;                     // plain store
          }
          return field;
      }
  }
  ```
- **Fix:** Make `_factory` `volatile` (a volatile read on the fast path is an
  acquire and orders the subsequent `field` read), or read it via
  `Volatile.Read(ref _factory)`. Note the `field` keyword prevents adding
  `volatile` to the backing field directly, so the cleanest change is an explicit
  `private volatile Delegate? _factory;` (already the declaration) plus
  `Volatile.Read`/`Volatile.Write` on the fast path, or an explicit backing field
  for the value that is itself `volatile`.

---

### F6. `RandomInt32Generator` / `RandomInt64Generator` read the shared buffer outside the lock, so concurrent callers can get duplicate values

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED (by inspection)
- **Category:** race
- **Location:** `src/ActualLab.Core/Generators/RandomInt32Generator.cs:16`,
  `src/ActualLab.Core/Generators/RandomInt64Generator.cs:16`
- **What:** Both classes are documented `// Thread-safe!` and advertise
  "cryptographically random" values, but only the *fill* of the shared instance
  field `_buffer` is inside the lock; the *read* of the buffer happens after the
  lock is released. Two threads can therefore interleave
  `fill(T1) → fill(T2) → read(T1) → read(T2)` and both return the value T2
  generated — i.e. the generator hands out duplicates.
- **Why it matters / attack path:** In this repo the only shipped consumer is a
  clock seed (`src/ActualLab.Core/Time/Internal/CoarseClockHelper.cs:17`), so the
  present impact is limited. But these are public API surface in `ActualLab.Core`
  with an explicit thread-safety and CSPRNG contract; any downstream code that uses
  them to mint identifiers, nonces, or tokens under concurrency silently gets
  colliding "random" values. The bug is invisible until it isn't.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Generators/RandomInt64Generator.cs:14-21
  public override long Next()
  {
      lock (_rng) {
          _rng.GetBytes(_buffer);          // shared instance field, filled under lock
      }
      var bufferSpan = MemoryMarshal.Cast<byte, long>(_buffer.AsSpan());
      return bufferSpan![0];               // ...but read outside the lock
  }
  ```
  (`RandomInt32Generator.Next` is identical with `int`.) Contrast with
  `RandomStringGenerator.Next`, which correctly rents a **per-call** buffer
  (`src/ActualLab.Core/Generators/RandomStringGenerator.cs:116`).
- **Fix:** Move the read inside the lock, or (better) drop the shared field and use
  a stack buffer:
  ```csharp
  public override long Next() {
      Span<byte> buffer = stackalloc byte[sizeof(long)];
      lock (_rng) _rng.GetBytes(buffer);
      return BinaryPrimitives.ReadInt64LittleEndian(buffer);
  }
  ```
  (`RandomNumberGenerator.GetBytes` is itself thread-safe, so with a stack buffer
  the lock can be dropped entirely.)

---

### F7. Session-id entropy is returned to the shared `ArrayPool` without being cleared

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:** `src/ActualLab.Core/Generators/RandomStringGenerator.cs:116`
- **What:** `RandomStringGenerator.Next(int, string?)` rents its randomness buffer
  from `ArrayPools.SharedBytePool` with `mustClear: false` and returns it
  unscrubbed. For the default power-of-two alphabet, buffer byte *i* maps
  deterministically to output character *i* (`FillPowerOfTwoCharSpan` masks with
  `alphabet.Length - 1`), so the residual bytes are a direct pre-image of the
  generated string.
- **Why it matters / attack path:** This generator is the default session-id
  factory (`src/ActualLab.Fusion/Session/DefaultSessionFactory.cs:12`) and is also
  used to derive user ids
  (`src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbUserIdHandler.cs:43`).
  The freed array goes back into a process-wide pool, so an unrelated component
  that rents a same-sized array and inspects uninitialized content — or a crash
  dump / core file / heap snapshot taken later — can recover recently minted
  session ids that would otherwise not be in memory. Not remotely exploitable on
  its own; it weakens the blast radius of any other memory-disclosure issue.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/Generators/RandomStringGenerator.cs:116
  var buffer = new RefArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, Math.Max(length, sizeof(uint)), mustClear: false);
  ...
  finally {
      buffer.Release();   // returned to the shared pool with the raw entropy intact
  }
  ```
- **Fix:** Pass `mustClear: true` for this generator (or explicitly
  `CryptographicOperations.ZeroMemory(buffer.Array.AsSpan(0, length))` before
  `Release()`). The cost is negligible for 16–32 byte buffers.

---

### F8. `StaticLog`'s single cache mixes `ILogger` and `ILogger<T>` values under the same key type

- **Severity:** LOW
- **Confidence:** CONFIRMED (latent — no shipped call site currently triggers it)
- **Category:** logic
- **Location:** `src/ActualLab.Core/StaticLog.cs:32`, `src/ActualLab.Core/StaticLog.cs:36`
- **What:** `For<T>()` and `For(Type)` share one
  `ConcurrentDictionary<object, ILogger>` and use the same key for the same type
  (`typeof(T)` vs `type.NonProxyType()`, identical for non-proxy types), but store
  incompatible values: `For<T>()` stores a `Logger<T>` and unconditionally casts
  the retrieved value to `ILogger<T>`, while `For(Type)` stores whatever
  `ILoggerFactory.CreateLogger(Type)` returns (a plain `ILogger`).
- **Why it matters / attack path:** If any code calls `StaticLog.For(typeof(Foo))`
  before `StaticLog.For<Foo>()`, the generic call retrieves the existing
  non-generic logger and throws `InvalidCastException` at
  `(ILogger<T>)Cache.GetOrAdd(...)`. Because `StaticLog` is used for
  static-context logging in framework internals (e.g.
  `src/ActualLab.Fusion/ComputedRegistry.cs:57`,
  `src/ActualLab.Fusion/Internal/ComputedGraphPruner.cs:33`), such a crash would
  surface in a place with no obvious cause. Today the two forms happen never to
  overlap on the same type, so this is a trap rather than a live bug — but it is
  cheap to close.
- **Evidence:**
  ```csharp
  // src/ActualLab.Core/StaticLog.cs:31-41
  public static ILogger<T> For<T>()
      => (ILogger<T>)Cache.GetOrAdd(typeof(T), static _ => new Logger<T>(Factory));
  public static ILogger For(Type type)
      => Cache.GetOrAdd(type.NonProxyType(), static key => Factory.CreateLogger((Type)key));
  ```
- **Fix:** Use distinct key spaces (e.g. wrap the generic key, or key the generic
  overload by `(typeof(T), generic: true)`), or make `For(Type)` return the same
  `Logger<T>`-shaped instance by routing both through one factory. A `TryGet` +
  type-check fallback would also work.

---

## Out-of-partition findings

These sit outside P8 but are directly implicated by the F1 attack path; flagging
for whoever owns P2/P3.

- **`ByteTypeSerializer.FromBytesCache` never evicts failed lookups** —
  `src/ActualLab.Rpc/Serialization/Internal/ByteTypeSerializer.cs:50-64`. Unlike
  `TypeRef.Resolve`, `FromBytes` caches the result of
  `typeRef.Resolve()` including `null`, keyed by the raw wire `ByteString`. Every
  distinct garbage blob a peer sends on a polymorphic-argument path therefore
  permanently occupies a dictionary entry — an even cheaper unbounded-growth
  vector than F1 (no valid type needed at all), bounded only by the 64 KiB
  per-name limit. It should be size-bounded and should not memoize failures.
  (Reachability: `$sys.I` / `$sys.B` on a stream with a polymorphic item type, or
  `$sys.Ok` for a call with a polymorphic result.) *(P3)*

- **`RpcSystemCalls.Error` resolves the remote exception type before validating
  `RelatedId`** — `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:100`. Moving
  `peer.OutboundCalls.Get(outboundCallId)` ahead of `error.ToException()` and
  returning early on `null` removes the cheapest route to F1 at essentially zero
  cost. *(P2)*

- **`Completion.New` calls `MakeGenericType(operation.Command.GetType())`** —
  `src/ActualLab.Fusion/Operations/Completion.cs:41-43`. `operation.Command` is
  rehydrated from the operation log; per the shared threat model, stored payloads a
  client influenced are untrusted. Worth confirming that the operation-log
  deserializer constrains the command type. *(P5/P6)*

---

## Areas examined

**`src/ActualLab.Core` (P8 folders):**
- `Reflection/` — `TypeRef.cs`, `TypeNameHelpers.cs`, `ActivatorExt.cs`,
  `RuntimeCodegen.cs`, `MemberwiseCloner.cs`, `TypeExt.cs` (proxy/non-proxy
  resolution, `GetTaskOrValueTaskType`), `FuncExt.cs`, `MemberInfoExt.cs`
  (skimmed), `Internal/` converters (enumerated, not line-by-line).
- `Generators/` — all files: `RandomStringGenerator.cs` (bias/rejection-sampling
  math verified for both the ≤256 and >256 alphabet branches),
  `RandomInt32Generator.cs`, `RandomInt64Generator.cs`, `RandomShared.cs`,
  `ConcurrentGenerator.cs`, `ConcurrentInt32/64Generator.cs`,
  `Internal/ConcurrentFuncBasedGenerator.cs`, `UuidGenerator.cs`.
- `Versioning/` — `ClockBasedVersionGenerator.cs`,
  `CpuTimestampBasedVersionGenerator.cs` (monotonicity), plus the folder listing.
- `Requirements/` + `Requirement.cs` — `MustExistRequirement`, `JointRequirement`,
  `ExceptionBuilder` (`string.Format` template/target/value handling),
  `Requirement<T>.MustExist` static reflection lookup.
- `DependencyInjection/` — `ServiceProviderExt.cs` (`CreateInstance`,
  `GetServiceOrCreateInstance`), `ServiceResolver.cs`, `ServiceDescriptorExt.cs`
  (reflective `GetImplementationType`).
- `Api/` — `ApiArray.cs` in full (index/range/`With*`/`Without`/`ToTrimmed` math),
  `Internal/ApiArrayMessagePackFormatter.cs` (confirmed `ReadArrayHeader`
  length-vs-remaining validation makes `new T[len]` safe), other `Api/Internal`
  converters enumerated.
- `Conversion/` — `DefaultConverterProvider.cs`, `DefaultSourceConverterProvider.cs`
  (reflective `TryParse`/`Parse` discovery), folder listing.
- `Diagnostics/` — `Samplers.cs` in full, `DiagnosticsExt.cs` (metric-name regex),
  `INotAnError.cs`; `ActivityExt.cs` partially.
- `Internal/` — `Errors.cs` in full, `CoreModuleInitializer.cs` (skimmed).
- `Trimming/CodeKeeper.cs`, `Comparison/`, `UnitOptions/`, `Compatibility/`,
  `Rpc/RpcRerouteException.cs` — read or enumerated.
- Project-root: `HostId.cs`, `StaticLog.cs`, `ExceptionExt.cs`,
  `ServiceException.cs`, `LazySlim.cs`, `INotLogged.cs`.

**`src/ActualLab.Interception`:** `Interceptor.cs`, `InterceptorBinding.cs`,
`Invocation.cs`, `MethodDef.cs`, `MethodDef.NestedTypes.cs` (all six invoker
factories + the universal result converter), `ProxyMethodTable.cs`,
`ProxyMethodRef.cs`, `Internal/ProxyHelper.cs`, `Proxies.cs`, `ArgumentList.cs`,
`ArgumentListType.cs`, `ArgumentListReader.cs`, the `ArgumentListG1`/`ArgumentList0`
sections of `ArgumentList-Generated.cs` (incl. the emitted-IL invoker paths and the
`Get`/`Set`/`SetFrom` type-check patterns), `Interceptors/{Scoped,TypedFactory,Scheduling}*.cs`,
`Trimming/`.

**`src/ActualLab.CommandR`:** `Rpc/RpcInboundCommandHandler.cs`,
`Rpc/RpcCommandHandler.cs`, `Internal/Commander.cs`, `CommandContext.cs`,
`CommanderBuilder.cs`, `Configuration/{CommandHandlerResolver,CommandHandlerRegistry,
CommandHandlerSet,CommandHandlerChain,MethodCommandHandler,InterfaceCommandHandler,
CommandHandlerResolverExt}.cs`, `Interception/{CommandServiceInterceptor,
CommandHandlerMethodDef}.cs`, `Diagnostics/{CommandTracer,CommandActivityExt,
CommanderInstruments}.cs`, `Internal/{Errors,PreparedCommandHandler,LocalCommandRunner}.cs`,
`Commands/{LocalCommand,IBackendCommand}.cs`, `Operations/{Operation,OperationEvent}.cs`,
`IEventCommand.cs`.

**`src/ActualLab.Plugins`:** every non-`obj` file — `FileSystemPluginFinder.cs`,
`Internal/{CachingPluginFinderBase,PluginFactory,PluginCache,PluginHandle,
PluginInstanceHandle}.cs`, `Metadata/{PluginSetInfo,PluginInfo}.cs`,
`PluginHostBuilder.cs`, `PluginInfoProvider.cs`.

**Supporting context read outside the partition (to prove/disprove reachability):**
`ExceptionInfo.cs`, `NewtonsoftJsonSerializer.cs`, `FileSystemCache.cs`,
`FilePath.Extras.cs`, `RpcSystemCalls.cs`, `RpcInboundContext.cs`, `RpcMethodDef.cs`
(+`.Static.cs`), `RpcArgumentSerializer.cs`, `RpcByteArgumentSerializerV4.cs`,
`ByteTypeSerializer.cs`, `Session.cs`, `IAuthBackend.cs`, `CompletionProducer.cs`,
`Completion.cs`.

**Experiments:** one throwaway .NET console harness in the session scratchpad
(outside the repo, BCL-only) to quantify F1 — measured private working-set growth
against distinct `Type.GetType` inputs and confirmed nested-array / nested-generic
names resolve without assembly qualification. No repository file was modified,
staged, or built.

---

## Areas NOT examined

- **`src/ActualLab.Generators` (the Roslyn source generator)** —
  `ProxyGenerator.cs`, `ProxyTypeGenerator.cs` and `Internal/*` were only sized and
  spot-checked, not reviewed line-by-line. Rationale: it is build-time code with no
  runtime attack surface under this threat model. It is, however, the producer of
  every proxy's method table and slot indices, so a *correctness* review of
  emitted slot ordering (which `ProxyMethodTable`/`InterceptorBinding` index into
  with `Unsafe.Add`, deliberately unchecked) would be worthwhile in a pass focused
  on codegen. The checked-in `obj/**/generated/*.g.cs` artifacts are stale and
  were ignored.
- **The bulk of `ArgumentList-Generated.cs` (~12k lines)** — I read the
  `ArgumentList0` and `ArgumentListG1<T0>` families in full and pattern-matched the
  emitted-IL / expression-tree invokers and `Get`/`Set`/`SetFrom`/`Equals`
  implementations across the other arities via targeted greps, rather than reading
  all 21 generated types line-by-line. The code is machine-generated from one
  template, so the risk of a divergent arity is low but nonzero (particularly the
  `Gn`-with-simple-tail hybrids, arities 5–10, where `GenericItemCount` is clamped
  to `MaxGenericItemCount = 4`).
- **`src/ActualLab.Core/Compatibility/`** — enumerated and skimmed only. These are
  thin polyfills for `netstandard2.0`/`net472`, which are not the primary server
  target; `Interop.cs` and the `MemoryPack.*` shims would deserve attention in a
  pass that cares about the legacy TFMs.
- **`Api/ApiList`, `ApiMap`, `ApiSet`, `ApiOption`, `ApiNullable`/`ApiNullable8`
  and their MessagePack/Newtonsoft formatters** — I read `ApiArray` and its
  MessagePack formatter closely and confirmed the length-validation pattern; the
  sibling types and the remaining `Api/Internal` formatters were enumerated but not
  read. Since these are wire-facing collection types, a P3-style pass over their
  `Deserialize` methods (length handling, `DepthStep`, duplicate-key behaviour in
  `ApiMap`/`ApiSet`) is the main gap I would close first.
- **`Core/Reflection/MemberInfoExt.cs`, `MethodInfoExt.cs`, `ExpressionExt.cs`,
  `ILGeneratorExt.cs`, `DelegateExt.cs`, `MemberwiseCopier.cs`** — skimmed for
  string-driven member lookup (none found beyond compile-time-constant names), not
  read exhaustively.
- **`Core/Diagnostics/{LoggerExt,InstrumentExt,ActivitySourceExt,AssemblyExt,
  CodeLocation}.cs`** — enumerated only; no evidence of attacker-controlled format
  strings was found in the CommandR/Interception call sites I did read, but a
  dedicated log-injection sweep over `LoggerExt` was not performed.
- **`Core/Result.cs`, `Option.cs`, `Disposable.cs`, `StringExt.cs`,
  `RequireExt.cs`, `KeyValuePairExt.cs`** — not read. `Result`/`Option` are used on
  RPC result paths and would be worth a look for exception-capture/rethrow
  semantics, though `Result` is largely P3/P5 territory in practice.
- **Any dynamic verification of F4 or F5.** F4 needs a purpose-built RPC contract
  with an abstract command parameter (none exists in this repo), and F5 needs an
  ARM64 host under contention; both are reported at the confidence level the static
  evidence supports rather than upgraded on assumption.
