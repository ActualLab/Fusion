---
title: ActualLab.Fusion MCP Introduction
description: A compact introduction to ActualLab.Fusion for AI coding assistants using the documentation MCP server.
---

# ActualLab.Fusion in Brief

ActualLab.Fusion is an end-to-end reactivity framework for .NET and TypeScript applications. It combines automatic
caching, dependency tracking, precise invalidation, RPC, and reactive client state so an application can propagate
changes from its database and backend services to every affected UI without hand-written subscription protocols.

## The Mental Model

A Fusion **compute service** exposes intercepted methods marked with `[ComputeMethod]`. A unique service-method-argument
call produces a cached, immutable `Computed<T>`. While computing, Fusion records every other computed value it reads and
forms a dependency graph automatically. Calling the same input reuses its current consistent result.

When application data changes, command handlers enter invalidation mode and call the affected compute methods with the
same arguments. Fusion marks those computed values invalid and immediately propagates invalidation to their live
dependants. Values are normally recomputed lazily when requested again, so only data that is both affected and still in
use consumes work.

`ComputedState<T>` and Fusion's UI components turn invalidation into reactive updates. Fusion RPC carries compute calls,
results, and invalidation messages across process and network boundaries, extending the same dependency graph to
Blazor, MAUI, browser, and TypeScript clients. The Operations Framework can record completed commands transactionally
and replay their invalidation phase on every backend host.

## What Is Available

- **Compute services and computed values** — caching, dependency graphs, invalidation, consistency, update delays, and
  memory management.
- **Reactive state and UI** — mutable and computed states, snapshots, Blazor components, loading, and error handling.
- **ActualLab.Rpc** — reconnectable RPC, routing, streaming, serialization, peer references, and remote compute services.
- **CommandR and Operations Framework** — command pipelines, transactional operation logs, distributed invalidation,
  events, and transient-error reprocessing.
- **Authentication and Entity Framework integration** — session-aware computed APIs, database hubs, batching, sharding,
  and operation scopes.
- **ActualLab.Core and serialization** — reusable concurrency, caching, collections, identifiers, results, workers, and
  serializer abstractions.
- **TypeScript packages** — ports of the core, Fusion, RPC, Fusion RPC, and React integration layers.
- **API and terminology references** — the full public-type index and Fusion glossary.

Use `search` when you need candidate documentation anchors, `get` for the immediate text at a known anchor, and
`search_expanded` when you need complete matched sections including their nested subsections.
