# API Index (Condensed)

A condensed reference (~300 lines) of the most important types in ActualLab.Fusion.
Use this to find existing abstractions before writing new code.
See also: [Full API Index](api-index-full.md) (~1000 lines).


## Core (`ActualLab.Core`) — [PartCore.md](PartCore.md)

### Result, Option, Requirement
- `Result<T>` (struct) — computation result holding value or error
- `Option<T>` (struct) — optional value (Some/None)
- `Requirement<T>` (record) — validates values and produces errors on failure

### Time — [PartCore-Time.md](PartCore-Time.md)
- `Moment` (struct) — UTC timestamp (wraps `DateTime`)
- `MomentClock` — abstract clock producing `Moment` timestamps
- `CpuClock` — monotonic high-resolution clock (Stopwatch-based)
- `ServerClock` — clock with offset from base clock (for client-side server time)
- `SystemClock` — real UTC clock via `DateTime.UtcNow`
- `CoarseSystemClock` — low-overhead periodically-updated clock
- `MomentClockSet` — set of related clocks (system, CPU, server, coarse)
- `TimerSet<T>` — priority-based timer set backed by radix heap
- `ConcurrentTimerSet<T>` — sharded concurrent version of `TimerSet`
- `TickSource` — shared coalesced timer tick for multiple consumers
- `RetryDelaySeq` (record) — retry delay sequence (fixed/exponential backoff with jitter)

### Collections — [PartCore-PropertyBag.md](PartCore-PropertyBag.md)
- `PropertyBag` (struct) — immutable typed property bag
- `MutablePropertyBag` — thread-safe mutable property bag with change notifications
- `OptionSet` — thread-safe mutable set of named options
- `RingBuffer<T>` (struct) — fixed-capacity circular buffer (power-of-2 capacity)
- `ArrayBuffer<T>` (struct) — `ArrayPool`-backed list-like buffer (zero alloc)
- `ArrayOwner<T>` — pooled array as `IMemoryOwner<T>`
- `FixedArray0..16<T>` (struct) — fixed-size inline stack arrays
- `HashSetSlim1..4<T>` (struct) — compact hash sets, inline before fallback to `HashSet`
- `BinaryHeap<TPriority, TValue>` — min-heap
- `FenwickTree<T>` — binary indexed tree for prefix sums
- `ApiList<T>`, `ApiMap<TKey,TValue>`, `ApiSet<T>`, `ApiNullable<T>` — serializable API contract collections

### Async Primitives — [PartCore-AsyncLock.md](PartCore-AsyncLock.md), [PartCore-Worker.md](PartCore-Worker.md)
- `AsyncLock` — semaphore-based async lock with optional reentry detection
- `AsyncLockSet<TKey>` — keyed async lock set
- `AsyncChain` (record struct) — composable named async operation (retry, log, cancel)
- `WorkerBase` / `IWorker` — long-running background worker (start/stop)
- `BatchProcessor<T, TResult>` — dynamic-scale batched processing
- `AsyncState<T>` — linked-list async state transitions

### Networking & Resilience — [PartCore-Transiency.md](PartCore-Transiency.md)
- `Transiency` (enum) — error transiency classification
- `TransiencyResolver` — classifies exceptions as transient/terminal/non-transient
- `Connector<T>` — persistent connection with auto-reconnect
- `RetryDelayer` — configurable retry delay with limits
- `RetryPolicy` (record) — retry with try count, per-try timeout, delay sequence
- `ChaosMaker` (record) — chaos engineering fault injection

### Serialization
- `IByteSerializer<T>` / `ITextSerializer<T>` — core serialization interfaces
- `ByteSerialized<T>`, `TextSerialized<T>` — auto-serializing wrappers
- `TypeDecoratingSerializer` (byte & text) — prefixes data with type info for polymorphism
- `ExceptionInfo` — serializable exception snapshot

### Specific serializers
- `MessagePackByteSerializer` — MessagePack binary serializer
- `MemoryPackByteSerializer` — MemoryPack binary serializer
- `NerdbankMessagePackByteSerializer` — Nerdbank.MessagePack binary serializer (in `ActualLab.Serialization.NerdbankMessagePack`)
- `SystemJsonSerializer` — `System.Text.Json` implementation
- `NewtonsoftJsonSerializer` — JSON.NET implementation

### DI Helpers
- `ServiceResolver` — encapsulates service type + optional custom resolver
- `HostedServiceSet` — manages group of `IHostedService` as a whole
- `ServiceCollectionExt` / `ServiceProviderExt` — various DI extension methods

### Text & IO — [PartCore-Symbol.md](PartCore-Symbol.md)
- `Symbol` (struct) — interned string with fast equality
- `FilePath` (struct) — cross-platform file path abstraction
- `ListFormat` (struct) — delimiter-based list serialization/parsing
- `Base64UrlEncoder` — URL-safe Base64 encoding (RFC 4648)
- `JsonString` — raw JSON value wrapper

### Identifiers & Versioning
- `HostId` (record) — unique host identifier
- `VersionGenerator<T>` — abstract version generator
- `ClockBasedVersionGenerator` — monotonic `long` versions from clock ticks
- `CpuTimestampBasedVersionGenerator` — monotonic `long` versions from CPU timestamp ticks

### Reflection & Codegen
- `MemberwiseCopier` — reflection-based property/field copier

### Hashing & Sharding Helpers
- `ShardMap<TNode>` — maps shards to nodes using a configurable builder
- `ShardMapBuilder` — selects the strategy used to build shard-to-node maps
- `HashRing<T>` — consistent hash ring


## RPC (`ActualLab.Rpc`) — [PartR.md](PartR.md)

- `IRpcService` — marker for RPC-invocable services
- `RpcStream<T>` — typed RPC stream with batched delivery
- `RpcHub` — central hub managing peers, services, configuration
- `RpcPeer` — one side of an RPC channel (connection, serialization, call tracking)
- `RpcClientPeer` / `RpcServerPeer` — client/server peer specializations
- `RpcConnection` — wraps transport + properties for a single connection
- `RpcClient` — establishes RPC connections to remote peers
- `RpcAlternatingClient` — alternates connection attempts between multiple RPC clients
- `RpcHttpClient` — client establishing full-duplex HTTP/2 connections
- `RpcWebSocketClient` — client establishing connections via WebSocket

### Service & Method Descriptors
- `RpcServiceRegistry` — registry of all RPC service definitions
- `RpcServiceDef` — describes a registered RPC service (type, mode, methods)
- `RpcMethodDef` — describes an RPC method (name, kind, serialization, timeouts)

### Attributes
- `RpcMethodAttribute` — configures RPC method properties (name, timeouts, delayed calls, execution modes)
- `RpcSerializableAttribute` — controls RPC-specific polymorphic serialization behavior
- `LegacyNameAttribute` — backward-compatible RPC name mapping with max version

### Configuration
- `RpcBuilder` (struct) — fluent builder for registering RPC services in DI
- `RpcConfiguration` — registered service builders + default service mode
- `RpcLimits` (record) — timeout/periodic limits for connections, keep-alive
- `RpcCallTimeouts` (record) — connect/run/delay timeouts for outbound calls
- `RpcServiceMode` (enum) — local, server, client, or distributed

### Transports
- `RpcFrameBasedTransport` — batches outbound RPC messages into frames
- `RpcPipeTransport` / `RpcStreamTransport` — length-prefixed framed transports over pipes or streams
- `RpcPeerConnectionStateKind` (enum) — disconnected, connecting, connected, or terminal peer state

## RPC Server — [`ActualLab.Rpc.Server`]
- `RpcHttpServer` — accepts full-duplex HTTP/2 connections for ASP.NET Core
- `RpcWebSocketServer` — accepts WebSocket connections for ASP.NET Core


## CommandR (`ActualLab.CommandR`) — [PartC.md](PartC.md)

- `ICommand<T>` — command producing result `T`
- `ICommander` — main entry point for executing commands through handler pipeline
- `CommandContext` / `CommandContext<T>` — tracks execution state within pipeline
- `CommandHandler` (record) — handler descriptor in execution pipeline

### Attributes
- `CommandHandlerAttribute` — marks method as command handler
- `CommandFilterAttribute` — marks method as command filter

### Pipeline
- `CommandHandlerChain` — ordered chain of filters + final handler
- `CommandHandlerFilter` — filters which handlers are used
- `CommandHandlerRegistry` — registry of all handlers, resolved from DI

### Operations (used in Operations Framework)
- `Operation` — recorded operation (completed command execution)
- `OperationEvent` — event recorded during an operation (for eventual consistency)
- `IOperationScope` — manages operation lifecycle within command pipeline
- `NestedOperation` (record) — nested command within parent operation

### Configuration
- `CommanderBuilder` (struct) — fluent builder for commander registration
- `ICommandService` — tagging interface for command service proxies
- `IEventCommand` — event dispatched to multiple handler chains


## Fusion (`ActualLab.Fusion`) — [PartF.md](PartF.md)

- `IComputeService` — tagging interface for compute service proxies
- `IComputed` / `Computed<T>` — cached computation result with dependency tracking and invalidation support
- `ComputedOptions` (record) — configuration for compute method behavior
- `ComputedRegistry` — global registry of all `Computed` instances (weak refs, auto-prune)
- `ComputeContext` — tracks current compute call context

### Attributes
- `ComputeMethodAttribute` — marks method as compute method (auto-cache + invalidation)
- `RemoteComputeMethodAttribute` — extends `ComputeMethodAttribute` with remote computed caching config

### States — [PartF-ST.md](PartF-ST.md)
- `IState<T>` / `State` — reactive state with computed value
- `IComputedState<T>` / `ComputedState<T>` — state backed by a computation and automatic update loop
- `IMutableState<T>` / `MutableState<T>` — manually settable reactive state
- `StateFactory` — creates state instances
- `StateSnapshot` — immutable snapshot of state lifecycle

### Invalidation
- `Invalidation` — static helpers to check/begin invalidation scopes
- `InvalidationSource` (struct) — describes source of invalidation
- `UpdateDelayer` (record) — integrates with `UIActionTracker` for instant UI updates
- `FixedDelayer` (record) — fixed update delay with configurable retry delays

### Sessions
- `Session` — authenticated user session (unique string Id)
- `SessionFactory` (delegate) — creates session instances
- `SessionResolver` — resolves current session
- `ISessionCommand<T>` — command scoped to a session

### Remote/Client
- `IRemoteComputed` / `RemoteComputed<T>` — computed value populated by a remote RPC call
- `RemoteComputedCache` — abstract base for remote computed caches

### Builder
- `FusionBuilder` (struct) — registers Fusion services in DI


## Blazor (`ActualLab.Fusion.Blazor`) — [PartB.md](PartB.md)

### Components
- `ComputedStateComponent<T>` — Blazor component backed by `ComputedState<T>`
- `MixedStateComponent<T, TMutableState>` — computed + mutable state component
- `FusionComponentBase` — base component with custom parameter comparison
- `StatefulComponentBase<T>` — component owning a typed `IState<T>`
- `CircuitHubComponentBase` — base component accessing `CircuitHub` services

### Attributes
- `FusionComponentAttribute` — enables custom parameter comparison and event handling on a component
- `ParameterComparerAttribute` — assigns a custom `ParameterComparer` to a component parameter

### UI Services — [PartB-UICommander.md](PartB-UICommander.md)
- `UICommander` — executes commands wrapped in tracked `UIAction`
- `UIActionTracker` — tracks running/completed `UIAction` instances
- `UIAction<T>` — strongly-typed tracked UI action
- `CircuitHub` — scoped service caching Blazor & Fusion services (dispatcher, session, etc.)

### Server-Side Services & Helpers
- `SessionMiddleware` — resolves/creates `Session` from cookies
- `FusionWebServerBuilder` (struct) — configures RPC, session middleware, render mode
- `ServerAuthHelper` — syncs ASP.NET Core auth state with Fusion `IAuth`


## Entity Framework (`ActualLab.Fusion.EntityFramework`) — [PartEF.md](PartEF.md)

- `DbHub<TDbContext>` — creates `DbContext` with execution strategy and operation scope
- `DbServiceBase<TDbContext>` — base for DB services with `DbHub` access
- `DbContextBase` — `DbContext` base solving EF Core pooled disposal issues
- `DbEntityResolver<TDbContext, TKey, TDbEntity>` — batched entity resolution via `BatchProcessor`
- `DbEntityConverter<TDbContext, TDbEntity, TModel>` — entity-to-model conversion

### Operations — [PartO.md](PartO.md)
- `DbOperationScope` / `DbOperationScope<TDbContext>` — manages transaction, operation/event persistence, commit
- `DbOperation` — persisted operation entity for cross-host replication
- `DbEvent` — persisted operation event entity with delayed processing

### Sharding
- `DbShard` — identifies a database shard
- `DbShardResolver<TDbContext>` — resolves shards from `Session`, `IHasShard`, `ISessionCommand`
- `DbShardRegistry<TContext>` — maintains shard sets, tracks used shards
- `ShardDbContextFactory<TDbContext>` — per-shard `DbContext` factory

### Log Processing
- `DbLogReader<TDbContext, TKey, TDbEntry, TOptions>` — shard-aware batched log reader
- `DbLogWatcher<TDbContext, TDbEntry>` — detects log changes per shard
- `DbLogTrimmer` — trims old log entries by age
- `DbOperationLogReader<TDbContext>` — reads operation logs for cache invalidation
- `DbEventLogReader<TDbContext>` — reads event logs for command dispatch

### Log Watcher Implementations
- `NpgsqlDbLogWatcher` — PostgreSQL LISTEN/NOTIFY
- `RedisDbLogWatcher` — Redis pub/sub
- `FileSystemDbLogWatcher` — file system watchers
- `LocalDbLogWatcher` — in-process notifications


## Authentication — [PartAA.md](PartAA.md)

### Contracts
- `IAuth` — primary auth service (sign-out, edit user, presence, session/user queries)
- `IAuthBackend` — backend auth (sign-in, session setup, options management)
- `User` (record) — authenticated/guest user with claims, identities, version
- `SessionInfo` (record) — session details (version, timestamps, IP, user agent)
- `SessionAuthInfo` (record) — auth info for session (identity, user ID, forced sign-out)

### Commands
- `AuthBackend_SignIn` (record) — sign in with identity
- `Auth_SignOut` (record) — sign out session
- `Auth_EditUser` (record) — edit user profile

### Blazor Auth
- `AuthStateProvider` — Fusion-aware `AuthenticationStateProvider`
- `ClientAuthHelper` — client-side sign-in/out via JS interop


## Interceptors & Proxies (`ActualLab.Interception`) — [PartAP.md](PartAP.md)

- `IProxy` — proxy object with assignable `Interceptor`
- `Interceptor` — base class for method interceptors
- `MethodDef` — describes an intercepted method (return type, params, async detection)
- `Invocation` (struct) — single intercepted method invocation (proxy, method, args, delegate)
- `ArgumentList` (record) — immutable argument list for intercepted calls (0..10 args)
- `Proxies` — factory for creating proxy instances
- `TypedFactoryInterceptor` — resolves instances via DI (`ActivatorUtilities`)
- `ScopedServiceInterceptor` — resolves scoped service per call
- `SchedulingInterceptor` — schedules async invocations via `TaskFactory`


## Redis Helpers (`ActualLab.Redis`)

- `RedisConnector` — resilient Redis connection with auto-reconnect
- `RedisDb<T>` — typed Redis DB scoped by context
- `RedisPub<T>` — typed pub/sub publisher
- `RedisQueue<T>` — Redis-backed FIFO queue
- `RedisStreamer<T>` — streaming read/write over Redis Streams
- `RedisHash` — Redis hash operations (get, set, remove, increment)
- `RedisSubBase` — abstract Redis pub/sub subscriber base
