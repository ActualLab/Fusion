# Changelog

All notable changes to **ActualLab.Fusion** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

`+HexNumber` after version number is the commit hash of this version.
It isn't included into the NuGet package version.

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
- Added [api-index.md](api-index.md) &ndash; condensed type reference (~300 lines) alongside [api-index-full.md](api-index-full.md) (full API index)


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

## Earlier Versions

For changes in earlier versions, please refer to the git commit history
or see "Fusion/🎉Releases" on Voxt.ai: https://voxt.ai/chat/s-1KCdcYy9z2-uJVPKZsbEo
