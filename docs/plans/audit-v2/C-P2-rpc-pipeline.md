### F1. Remote API version sets grow the method-resolver cache without bound

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:15`
- **What:** Every distinct `VersionSet` supplied in a peer handshake is retained forever as a key in `_legacyServerMethodResolvers`, and every value is a resolver containing method dictionaries. There is no normalization to configured service scopes, size/count limit, eviction, or cache lifetime.
- **Why it matters / failure scenario:** A remote client connects repeatedly (or reconnects the same logical peer) while varying an unused scope/version pair in `RemoteApiVersionSet`. `RpcPeer` installs a resolver for every handshake, `GetServerMethodResolver` adds every distinct set to the hub-wide cache, and the attacker permanently grows both the cache keys and per-entry method maps until the server runs out of memory; resolver construction also repeatedly walks all services and methods.
- **Evidence:** `RpcPeer` refreshes `_serverMethodResolver` from every new handshake (`src/ActualLab.Rpc/RpcPeer.cs:614`), and `GetServerMethodResolver` performs `_legacyServerMethodResolvers.GetOrAdd(versions, ... new RpcMethodResolver(...))` with no bound (`src/ActualLab.Rpc/Configuration/RpcServiceRegistry.cs:110`). `VersionSet` preserves arbitrary scope names in its parsed `Items` dictionary (`src/ActualLab.Core/Collections/VersionSet.cs:117`).
- **Fix:** Project the remote version set onto the finite set of configured service scopes before using it as a key, reject excessive scope/value sizes, and bound or evict the resulting resolver cache. The canonical empty/current set should reuse the base resolver.

### F2. A peer can create unlimited concurrent inbound calls

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`
- **What:** The inbound call table is an unbounded `ConcurrentDictionary`, and `GetOrRegister` accepts every new remote-supplied call ID. No peer-wide or method-wide in-flight limit exists, and no-wait calls bypass the table while still starting arbitrary asynchronous work.
- **Why it matters / failure scenario:** After establishing an RPC connection, a remote peer sends calls with unique IDs to any method that waits, streams, blocks on I/O, or otherwise completes slowly. The read loop starts each call without awaiting it (`src/ActualLab.Rpc/RpcPeer.cs:419`), so the dictionary, linked cancellation sources, invocation tasks, arguments, application resources, and continuations accumulate until memory or another server resource is exhausted. Replacing the target with a no-wait method avoids even the dictionary accounting while retaining the task fan-out.
- **Evidence:** `Calls` has no capacity control (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`); `GetOrRegister` unconditionally `TryAdd`s each new ID (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:59`); and `RpcInboundCall.Process` invokes the server and returns its task without imposing concurrency or duration limits (`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:89`, `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:136`). `RpcInboundCallOptions` exposes only a context factory, not an inbound-call limit (`src/ActualLab.Rpc/Configuration/Options/RpcInboundCallOptions.cs:10`).
- **Fix:** Add configurable per-peer and optionally per-method limits covering both regular and no-wait calls. Reserve capacity before constructing/invoking the call, release it on every completion/error path, and reject or disconnect peers that exceed it. Give no-wait work bounded scheduling rather than fire-and-forget fan-out.

### F3. Duplicate call IDs leak linked cancellation sources

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** leak
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:114`
- **What:** Every non-no-wait inbound call creates a linked `CancellationTokenSource` before registration. When a duplicate call ID resolves to an existing call, `Process` returns the existing call's task without disposing the newly created call's linked source.
- **Why it matters / failure scenario:** A remote peer starts one long-running call with ID X and floods duplicate messages using ID X. Each duplicate constructs a fresh linked source registered on the peer-change token, loses the call instance on the duplicate branch, and retains the source/registration until the peer changes or stops. The inbound dictionary remains at one entry, so a future call-count limit alone would not stop this memory-exhaustion path.
- **Evidence:** The constructor creates `CallCancelSource = peerChangedToken.CreateLinkedTokenSource()` for every regular call (`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:48`). The duplicate branch at lines 114-119 returns without cleanup, while the only normal disposal is in `UnregisterFromLock` at lines 294-300.
- **Fix:** Dispose the losing call instance's linked source before returning from the duplicate branch. Also validate that a duplicate's method reference and call type match the registered call before treating it as a retry; otherwise reject it as a protocol violation.

### F4. A remote stream sender can grow the receiver's item queue without bound

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/RpcStream.cs:194`
- **What:** Enumerating a remote `RpcStream<T>` creates an unbounded channel. Incoming `$sys.I` and `$sys.B` messages enqueue every correctly indexed item without enforcing `AckAdvance`, a maximum buffered-item count, or any other receive window.
- **Why it matters / failure scenario:** A client starts enumerating a valid stream returned by a malicious or buggy server and then consumes slowly or stops consuming. The server ignores acknowledgements and continuously sends sequential item/batch frames; `RpcSystemCalls.I/B` dispatches them to `OnItem`/`OnBatch`, which permanently queue them until consumption, allowing the server to exhaust client memory.
- **Evidence:** `_remoteChannel = Channel.CreateUnbounded<T>(RemoteChannelOptions)` is created at enumeration (`src/ActualLab.Rpc/RpcStream.cs:183`). `OnItem` writes unconditionally at line 285, and `OnBatch` writes every element at lines 304-308. The wire handlers directly call these methods for a resolved stream (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:164`, `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:172`).
- **Fix:** Use a bounded receive buffer derived from a separately capped protocol window, track the last acknowledged/allowed index, validate batches before enqueueing, and disconnect or fail the stream if the sender exceeds the advertised window. Do not block the peer read loop while waiting for consumer capacity.

### F5. Remotely induced shared streams have no per-peer object limit

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** resource-exhaustion
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:193`
- **What:** `RpcSharedObjectTracker` strongly retains every shared object in an unbounded dictionary. Serializing each locally returned `RpcStream<T>` registers a new `RpcSharedStream<T>`, and an unresponsive consumer can leave it retained until the 125-second default release timeout.
- **Why it matters / failure scenario:** A remote caller repeatedly invokes an exposed stream-returning method but never acknowledges, enumerates, or disconnects the returned streams. Each successful response allocates a new ID and strongly stores the stream and its source; the attacker can create objects faster than periodic expiration removes them, exhausting server memory even though the RPC calls themselves finish quickly.
- **Evidence:** `Register` always adds to `_objects` without a limit (`src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:214`), and maintenance only expires objects after `ObjectReleaseTimeout` (`src/ActualLab.Rpc/Infrastructure/RpcObjectTrackers.cs:231`). The serialized stream ID getter creates and registers a `RpcSharedStream<T>` on every previously unregistered local stream (`src/ActualLab.Rpc/RpcStream.cs:116`).
- **Fix:** Add a configurable maximum shared-object/stream count and retained-byte budget per peer, reserve a slot before response serialization, and roll back registration on serialization/send failure. Reject additional stream creation or close the offending peer when the limit is reached.

### F6. A pre-cancelled outbound call is still transmitted

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** race
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:94`
- **What:** `Invoke` registers the outbound call and its cancellation callback, but unconditionally calls `SendRegistered` afterward when the peer is connected. If the token is already cancelled, registration synchronously cancels and unregisters the call, yet the cancelled request is then sent.
- **Why it matters / failure scenario:** A caller invokes a side-effecting RPC method with an already-cancelled token, or cancellation wins between registration and send. The local task completes as cancelled and a `$sys.Cancel` may be sent before the request, where it finds no inbound call; the subsequent request can then execute remotely despite the caller having cancelled before dispatch.
- **Evidence:** `Invoke` calls `Register()` and then `SendRegistered()` without checking the token or `ResultTask` (`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:81`). `Register` adds the call before registering the callback at lines 116-123, and cancellation unregisters/notifies from `CompleteAndUnregister` (`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:316`, `src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:348`).
- **Fix:** Fail fast on an already-cancelled token before registration/routing, and synchronize the post-registration send with cancellation so a call whose result source was cancelled/unregistered cannot be transmitted. Apply the same check after awaiting a connection.

### F7. An empty `$sys.Error` payload leaves the outbound call pending

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:100`
- **What:** `RpcSystemCalls.Error` null-forgives `ExceptionInfo.ToException()`, although `ToException()` explicitly returns null for `ExceptionInfo.None`. Passing that null to `SetError` throws instead of completing the referenced outbound call.
- **Why it matters / failure scenario:** A buggy or malicious server replies to an active client call with `$sys.Error` and a default/empty `ExceptionInfo`. The no-wait system handler faults while processing the frame, the exception is only logged by the peer's message-processing wrapper, and the original call remains registered until its run timeout or connection teardown instead of failing promptly with a protocol error.
- **Evidence:** `var exception = error.ToException()!` is passed directly to `SetError` (`src/ActualLab.Rpc/Infrastructure/RpcSystemCalls.cs:95`). `ExceptionInfo.ToException` returns null when `IsNone` (`src/ActualLab.Core/Serialization/ExceptionInfo.cs:57`), while `SetError` passes its argument to `ResultSource.TrySetException(error)` (`src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs:297`).
- **Fix:** Treat `ExceptionInfo.None` as an invalid error response and complete the referenced call with a non-null `RpcException`/protocol exception (and optionally close the peer). Never pass a null exception to `SetError`.

### F8. Unexpected server exceptions expose their concrete type and message to remote callers

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:146`
- **What:** Every non-cancellation exception escaping an RPC method is converted directly to `ExceptionInfo` and sent to the caller, with no server-side mapping or sanitization hook. `ExceptionInfo` includes the exception's assembly-qualified type reference and raw `Message`.
- **Why it matters / failure scenario:** A public RPC method throws a database/provider/configuration exception whose message contains SQL, paths, host names, identifiers, or other operational detail. The RPC pipeline returns those details to an untrusted client even though only a generic public failure should cross the boundary.
- **Evidence:** Both error-send paths call `ArgumentList.New(error.ToExceptionInfo())` (`src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:116`, `src/ActualLab.Rpc/Infrastructure/RpcSystemCallSender.cs:136`). `ExceptionInfo(Exception)` records `new TypeRef(exception.GetType())` and `exception.Message` verbatim (`src/ActualLab.Core/Serialization/ExceptionInfo.cs:41`).
- **Fix:** Add a configurable server-side exception-to-wire mapper and make the safe default expose only allow-listed public exception contracts/messages; map all unexpected exceptions to an opaque remote error with a correlation ID while logging full details server-side.

### F9. Invalid-call-type errors report the replacement method's expected type

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs:60`
- **What:** On a call-type mismatch, the code replaces `MethodDef` with the system `NotFound` definition before reading the expected call type. The resulting error therefore reports the replacement method's call type rather than the addressed method's required type.
- **Why it matters / failure scenario:** A malformed peer invokes a method with an unsupported call type. The rejection itself works, but the returned diagnostic names the wrong expected type, obscuring protocol incompatibilities and sending misleading data to clients and logs.
- **Evidence:** Lines 60-62 assign `MethodDef = ...NotFoundMethodDef` and then construct `RpcInboundInvalidCallTypeCall` using `MethodDef.CallType.Id`. The original method definition is no longer available at that point.
- **Fix:** Capture `var expectedCallTypeId = MethodDef.CallType.Id` before replacing `MethodDef`, and pass the captured value to `RpcInboundInvalidCallTypeCall`.

## Areas examined

- RPC method/service definition and resolution, including legacy-name/hash lookup, backend gating, call-type selection, and the versioned resolver cache.
- Inbound and outbound call construction, registration, duplicate/reprocess behavior, cancellation, completion, rerouting, timeout maintenance, and result/error system calls.
- RPC streams and shared/remote object trackers, including item/batch/end dispatch, acknowledgements, reconnect behavior, queueing, registration, keep-alive, and expiration.
- Middleware construction and filtering, route/nullability validation, headers, cache capture/match handling, call diagnostics/tracing, attributes, and relevant internal helpers.
- Supporting entry paths in `RpcPeer`, `RpcLimits`, `RpcStream`, `VersionSet`, `ExceptionInfo`, service registration, and RPC tests/samples needed to prove or disprove candidates.

## Areas NOT examined

- P1-owned transport framing, WebSocket implementation, client connection establishment, handshake identity/version validation beyond the resolver-cache call path, keep-alive transport behavior, and peer teardown internals.
- P3-owned serializers and low-level wire/buffer parsing beyond the already-deserialized values consumed by P2.
- Fusion compute/state/session/auth/EF code, TypeScript packages, and other partitions except for narrow caller/type context.
- No tests or experiments were run: the findings above follow deterministic source paths, and the main working tree was not built or modified.
