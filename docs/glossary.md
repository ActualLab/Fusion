---
title: Glossary
description: Definitions of the core concepts and terminology used by ActualLab.Fusion and its companion libraries.
---

# Glossary

This glossary explains terms that have a specific meaning in Fusion and its companion libraries. For concrete types
and members, see the [API Index](api-index-full.md).

## Compute Services and Computed Values

###### Automatic caching

Reuse of a compute method result for the same service, method, and arguments while its
`Computed<T>` remains consistent and alive. Fusion uses weak references by default, so “cached” does not necessarily
mean “retained indefinitely.” See [Automatic Caching](PartF.md#automatic-caching) and
[Memory Management](PartF-MM.md).

###### Cache-aware API design

Structuring an API as small, independently cacheable compute methods, often fetching IDs
before individual items, so Fusion can batch reads, reuse results, and invalidate only affected data. See
[Cache-Aware API Design](PartAC.md).

###### Cascading invalidation

Automatic propagation of invalidation from a computed dependency to every live computed
value that depends on it. Propagation is immediate; recomputation is normally deferred until the value is requested
again. See [Automatic Dependency Tracking and Cascading Invalidation](PartF.md#automatic-dependency-tracking-and-cascading-invalidation).

###### Compute method

An intercepted service method marked with `[ComputeMethod]`. Each unique call can produce a
cached `Computed<T>`, participate in dependency tracking, and be invalidated independently. See
[Compute Services and Compute Methods](PartF.md).

###### Compute service

A service implementing `IComputeService` whose virtual or interface methods can be compute
methods. Compute services compose like ordinary services while Fusion adds caching, dependency tracking, and
invalidation. See [Compute Services and Compute Methods](PartF.md).

###### Computed input

The identity of a compute method call: its service, method, and arguments. Fusion uses this identity
to find the one current computed value for that call. See [`Computed<T>` Metadata](PartF-C.md#metadata).

###### Computed value (`Computed<T>`)

An immutable result-or-error produced by a computation. It records its input,
version, consistency state, dependencies, and dependants; invalidation creates the need for a later version rather than
mutating the old result. See [`Computed<T>`: The Core Abstraction](PartF-C.md).

###### Computed registry

Fusion's process-wide weak registry that maps computed inputs to their current computed values
and exposes graph metrics and diagnostic events. It provides uniqueness without keeping unused computations alive
forever. See [ComputedRegistry](PartF-C.md#computedregistry).

###### Computing / Consistent / Invalidated

The three lifecycle states of a computed value. A value is mutable only while
`Computing`, becomes immutable when `Consistent`, and can no longer serve as the current result after it is
`Invalidated`. See [ConsistencyState](PartF-C.md#consistencystate).

###### Consolidation delay

A compute-method option that suppresses a propagated invalidation when recomputation shows
that the value did not actually change. It can reduce false invalidations at the cost of delayed propagation. See
[ConsolidationDelay](PartF-CO.md#consolidationdelay).

###### Dependency / dependant

If computation A uses computed value B, B is A's dependency and A is B's dependant.
Dependencies are retained strongly by their live dependants so invalidation can reliably flow in the opposite
direction. See [Dependency Chains Keep Values Alive](PartF-MM.md#dependency-chains-keep-values-alive).

###### Dependency graph

The runtime directed acyclic graph formed by computed values and their dependencies. Fusion
updates it automatically as computations run and uses it to propagate precise invalidations. See
[Automatic Dependency Tracking and Cascading Invalidation](PartF.md#automatic-dependency-tracking-and-cascading-invalidation).

###### Invalidation

Marking a computed value as outdated. The old immutable value remains readable through existing
references, but the next normal call for the same input must obtain a newer value. See
[Invalidation](PartF.md#invalidation).

###### Invalidation block

An `Invalidation.Begin()` scope in which compute method calls invalidate matching computed
inputs instead of evaluating the methods. In Operations Framework handlers, the equivalent branch is usually selected
with `Invalidation.IsActive`. See [Invalidation Block Behavior](PartF-D.md#invalidation-block-behavior).

###### Invalidation source

Optional diagnostic information recording why a computed value was invalidated. Whole-chain
tracking can preserve the path back to the original cause. See
[Invalidation Source Tracking](PartF-C.md#invalidation-source-tracking).

###### Pseudo-dependency

A compute method used only as an invalidation signal, allowing many otherwise unrelated
computed values to depend on a shared logical key or hierarchy. See
[Pseudo-Dependencies for Batch Invalidation](PartAC-PM.md).

###### Recomputation

Producing a new computed value after the previous one was invalidated. Fusion does this lazily when
the input is used again and reuses every dependency that is still consistent. See
[Computed Value Lifecycle](PartF-D.md).

## State and Reactive UI

###### Circuit hub

A scoped Blazor service that represents one interactive circuit and coordinates circuit lifetime,
render mode, JavaScript availability, and other circuit-bound Fusion services. See [CircuitHub](PartB-Services.md#circuithub).

###### Computed state (`ComputedState<T>`)

A state backed by a computation and an update loop. It recomputes after its
dependencies invalidate and publishes a new snapshot, making it the standard bridge from computed values to reactive
UI. See [ComputedState](PartF-ST.md).

###### Computed state component

A Blazor component base that owns a `ComputedState<T>`, calls its `ComputeState` method,
and rerenders when the state changes. See
[ComputedStateComponent](PartB.md).

###### Last non-error value

The latest successful state value retained across a later failed update. UI can display it
as stale data while also presenting the current error. See [Handling Loading and Errors](PartB-CS.md#handling-loading-and-errors).

###### Mixed state component

A Blazor component that combines a computed source state with a mutable editing state,
typically for forms that must refresh from the server without discarding in-progress user input. See
[MixedStateComponent](PartB.md).

###### Mutable state (`MutableState<T>`)

A state whose value or error is assigned directly. Compute methods can depend on
it, so changing it invalidates derived computed values. See [MutableState](PartF-ST.md).

###### Snapshot

A state's immutable view of one update, including the current computed value and useful prior state.
Reading one snapshot keeps related value/error information consistent during rendering. See
[`IState<T>` Snapshot](PartF-ST.md#snapshot-property).

###### State (`State<T>`)

A long-lived holder that tracks successive computed values for a logical piece of data. Unlike
one `Computed<T>`, a state represents change over time. See [States](PartF-ST.md).

###### Update delayer

A policy that spaces or batches state recomputations after invalidation, preventing rapid changes
from causing excessive work or UI renders. See [Update Delayers](PartF-ST.md#update-delayers).

###### Update loop

The lifecycle inside a computed state that waits for invalidation, applies its update-delayer policy,
recomputes, and publishes a new snapshot until disposed. See
[ComputedState Lifecycle](PartF-ST.md#computedstate-lifecycle).

## Distributed Fusion and RPC

###### ActualLab.Rpc

The RPC layer used by Fusion for remote service calls, streams, routing, reconnection, and
server-to-client calls. It can be used independently of compute services. See [ActualLab.Rpc](PartR.md#what-is-actuallabrpc).

###### Call routing

Selection of the `RpcPeer` that should execute a call, potentially from the service, method, and
arguments. A route can change while a call is in progress. See [Call Routing](PartR-CallRouting.md).

###### Compute service client

A client-side proxy for a remote compute service. It mirrors server computed results
locally and propagates server invalidations into the client's dependency graph. See
[What is Compute Service Client?](PartR.md#what-is-compute-service-client).

###### Persistent computed cache

Client-side storage of remote computed results across application restarts. It enables
fast startup and offline reads while RPC later verifies and refreshes cached entries. See
[Persistent Cache Implementation](PartAC-PC.md).

###### Remote computed value

The client-side computed replica associated with a server compute method call. It has a
client lifetime and cache policy while tracking the consistency of its server counterpart. See
[RemoteComputed Lifetime](PartR.md#remotecomputed-lifetime).

###### Rerouting

Retrying a call through a newly selected peer when topology or route state changes. This is distinct
from retrying an arbitrary application failure. See [Rerouting Flow](PartR-CallRouting.md#rerouting-flow).

###### Reverse RPC

An RPC call initiated by a server toward a service implemented by a connected client. The caller
selects the client peer through outbound call routing. See [Server-to-Client Calls](PartR-ReverseRpc.md).

###### RPC hub

The root service that owns RPC configuration, service definitions, peer references, and active peers in
one process. See [`RpcHub`](PartR-CC.md#rpchub).

###### RPC peer

One logical local or remote RPC endpoint and its connection lifecycle. Client and server peers send,
receive, and track calls over reconnectable connections. See [`RpcPeer`](PartR-CC.md#rpcpeer).

###### RPC peer reference

A stable description of which peer a call targets and how that peer is routed or resolved.
It can outlive an individual connection or concrete peer instance. See [`RpcPeerRef`](PartR-CC.md#rpcpeerref).

###### `RpcNoWait` call

A fire-and-forget RPC call whose caller does not wait for a remote result. It is appropriate only
when completion and errors do not need to be observed by the caller. See [Fire-and-Forget Calls](PartR-RpcNoWait.md).

###### `RpcStream<T>`

Fusion's reconnect-aware, batched RPC stream abstraction. It supports streams in either direction
and optional real-time behavior that may skip obsolete items under pressure. See [Streaming with RpcStream](PartR-RpcStream.md).

## CommandR and Operations Framework

###### Backend command

A command marked for execution on a backend peer rather than directly on the API host. Routing is
handled by the command pipeline and RPC integration. See [Backend Commands](PartO.md#backend-commands).

###### Command

A message describing an action and, optionally, its result type. `ICommander` sends it through one shared
handler pipeline. See [Command Interfaces](PartC-CI.md).

###### Command context

Ambient state for one command execution, including the command, result, services, operation, and
position in the handler pipeline. Nested commands form a hierarchy of contexts. See
[CommandContext](PartC.md#commandcontext).

###### Command handler

Code that participates in command execution. Filter handlers wrap the remainder of the pipeline;
the final handler performs the command's core action. See [Built-in Command Handlers](PartC-BH.md).

###### Command pipeline

The ordered chain of filter handlers followed by a final handler. CommandR uses the same
pipeline for tracing, routing, operation scopes, retries, invalidation, and application logic. See
[Command Handler Pipeline](PartC-D.md#command-handler-pipeline).

###### Command service

A proxied service whose handler methods look like normal calls but are redirected through
`ICommander` and its pipeline. See [Command Services](PartC.md#command-services).

###### Event command

A command dispatched to multiple independent handler chains instead of one final handler. It is
CommandR's broadcast-style command model. See [`IEventCommand`](PartC-CI.md#ieventcommand).

###### Invalidation mode

The Operations Framework replay phase in which a command handler skips its business logic and
runs only its invalidation branch. This reproduces the originating host's invalidations on other hosts. See
[Invalidation Mode](PartO.md#invalidation-mode).

###### Log watcher

A notification mechanism that wakes an operation or event log reader when new records may be
available. PostgreSQL, Redis, file-system, local, and polling-oriented implementations have different deployment
tradeoffs. See [Operations Framework: Log Watchers](PartO-PR.md).

###### Nested command

A command executed from another command's handler. It receives its own command context and is
captured as a child of the parent operation. See [Nested Commands](PartO.md#nested-commands).

###### Operation

The durable description of a completed command execution, including its command, host, items, nested
operations, and emitted events. Other hosts consume it to reproduce invalidation. See
[Operation](PartO.md#operation).

###### Operation event

An event recorded as part of an operation and transferred to the event log for eventual
processing. Delivery is designed around durable, at-least-once workflows. See
[Operations Framework: Events](PartO-EV.md).

###### Operation log

The ordered durable record of operations consumed by every host. It carries invalidation work
across the cluster without making cache invalidation part of the original database transaction. See
[Operations Framework](PartO.md).

###### Operation items

Serializable data attached to an operation to carry information from its execution phase to the
later invalidation phase on every host. Nested operations have independent item bags. See
[Passing Data to Invalidation Block](PartO.md#passing-data-to-invalidation-block).

###### Operation reprocessing

Re-execution of a command after a transient failure, with a fresh command context and a
configured retry delay. See [Operations Framework: Reprocessing](PartO-RP.md).

###### Operation scope

The execution context that collects an operation and controls its completion. Database scopes
coordinate the business-data transaction with operation-log storage; in-memory scopes produce transient operations.
See [Operation Scope](PartO.md#operation-scope).

###### Operations Framework (OF)

Fusion's CommandR-based infrastructure for transactional operation logging,
multi-host invalidation, durable events, and transient-error reprocessing. See
[Operations Framework](PartO.md).

###### Outbox pattern

Writing an operation or event record in the same database transaction as business data, then
processing it asynchronously. This avoids losing the notification after a successful data commit and provides
at-least-once delivery. See [The Outbox Pattern](PartO.md#the-outbox-pattern).

###### Transient operation

An operation completed and invalidated in process without durable operation-log storage.
It is suitable when replay after restart and cross-host delivery are unnecessary. See
[Transient Operations](PartO-TR.md).

## Authentication and Data Access

###### Authentication backend

The trusted server-side half of Fusion authentication. It changes session and user data;
the frontend authentication service exposes session-aware computed queries and user-facing commands. See
[`IAuth` vs `IAuthBackend`](PartAA.md#iauth-vs-iauthbackend).

###### Default session

The special session with ID `"~"`, used when a client cannot know the real server session ID.
The server replaces it with the connection's authenticated session before invoking session-aware APIs. See
[Default Session](PartAA-Interfaces.md#default-session).

###### Fusion session

Fusion's serializable identifier and metadata carrier for a logical user session. It is passed to
session-aware compute methods so authentication and authorization participate in caching and invalidation. See
[Session](PartAA.md#session).

###### Presence reporting

Periodic client activity reporting that lets authentication services expose whether a user is
currently online. See [PresenceReporter](PartB-Auth.md#presencereporter).

###### Database hub (`DbHub`)

The entry point for creating correctly configured Entity Framework contexts, including
read-only, read-write, operation-scoped, and sharded contexts. See [DbHub](PartEF.md#dbhub).

###### Entity resolver

A batched entity loader that combines concurrent key lookups into fewer database queries and can
apply sharding and transient-failure retries. See [DbEntityResolver](PartEF.md#dbentityresolver).

###### Sharding

Partitioning application data across named database shards and resolving each operation to the correct
shard, often from a typed ID, session, or command. See [Sharding](PartEF.md#sharding).

## Interception, Serialization, and Cross-Runtime Support

###### Async context

The TypeScript mechanism used to carry the currently executing computed value across `await`
boundaries, filling the role that .NET `ExecutionContext` provides for Fusion's dependency tracking. See
[AsyncContext: Why It Matters](PartTS.md#asynccontext-why-it-matters).

###### Interceptor

A reusable call handler attached to a generated proxy. Fusion, RPC, and CommandR use interceptors to
add behavior around ordinary-looking service methods. See [Interceptors and Proxies](PartAP.md).

###### Pass-through proxy

A proxy that intercepts a call and can delegate it to a real underlying implementation. A
virtual proxy has no required target and can produce the result entirely in its interceptor. See
[Creating Pass-Through Proxies](PartAP.md#creating-pass-through-proxies).

###### Proxy

A compile-time-generated implementation or subclass that redirects eligible service calls to an
interceptor. This is the common mechanism behind compute services, RPC services, and command services. See
[Proxy Generation](PartAP-PG.md).

###### Serialized wrapper

A value holder such as `ByteSerialized<T>` or `TextSerialized<T>` that defers serialization or
deserialization until its data or value is accessed. See [Serialized Wrappers](PartS.md#serializedt-wrappers).

###### Type-decorated serialization

Serialization that embeds concrete type identity alongside a payload so a value
declared as a base type or `object` can be reconstructed polymorphically. See
[Type-Decorated Serialization](PartS.md#type-decorated-serialization).

###### Transiency resolver

A policy that classifies failures as transient, terminal, or unknown for a specific context.
Fusion uses that classification to choose error invalidation delays, reconnection behavior, and operation retries. See
[Transiency Resolvers](PartCore-Transiency.md).

###### Unified serialization

ActualLab's common text and byte serializer abstractions and wrappers over System.Text.Json,
Newtonsoft.Json, MemoryPack, and MessagePack. See [Unified Serialization](PartS.md).
