# Operations Framework: Diagrams

This page contains visual diagrams explaining how Operations Framework works.

## High-Level Architecture

<img src="/img/diagrams/PartO-D-1.svg" alt="High-Level Architecture" style="width: 100%; max-width: 800px;" />

**Command Execution Pipeline:**

| Class | Description |
|-------|-------------|
| `OperationReprocessor` | Retries commands that fail with transient errors |
| `TransientOperationScopeProvider` | Provides transient operation scope |
| `DbOperationScopeProvider` | Provides DB-backed operation scope |
| `DeferredInvalidationScopeProvider` | Opens the deferred invalidation scope |
| `OperationCompletionHandler` | Applies the invalidation calls an operation recorded |

**Background Services:**

| Class | Description |
|-------|-------------|
| `DbOperationLogReader` | Reads operations from other hosts |
| `DbOperationLogTrimmer` | Removes old operations from log |
| `DbEventLogReader` | Processes pending events |
| `DbEventLogTrimmer` | Removes processed/discarded events |

**Notification System:**

| Class | Description |
|-------|-------------|
| `DbOperationCompletionListener<TDbContext>` | Notifies log watchers when an operation completes locally |
| `NpgsqlDbLogWatcher<TDbContext, TDbEntry>` | Listens for NOTIFY signals |
| `RedisDbLogWatcher` | Subscribes to Redis pub/sub |
| `FileSystemDbLogWatcher` | Watches for file changes |


## Command Execution Flow

<img src="/img/diagrams/PartO-D-2.svg" alt="Command Execution Flow" style="width: 100%; max-width: 800px;" />

| Handler | Priority | Responsibility |
|---------|----------|----------------|
| `InvalidationGuard` | 999,999,000 | Rejects a nested command run inside an invalidation pass |
| `OperationReprocessor` | 100,000 | Retries commands that fail with transient errors |
| `TransientOperationScopeProvider` | 10,000 | Transient scope, completion handling |
| `DbOperationScopeProvider` | 9,900 | DB transaction, operation persistence |
| `DeferredInvalidationScopeProvider` | 9,000 | Deferred invalidation scope |
| `CompletionTerminator` | -1,000,000,000 | Terminal handler for `ICompletion` |


## Operation Scope Lifecycle

<img src="/img/diagrams/PartO-D-3.svg" alt="Operation Scope Lifecycle" style="width: 100%; max-width: 800px;" />


## Multi-Host Invalidation Flow

<img src="/img/diagrams/PartO-D-5.svg" alt="Multi-Host Invalidation Flow" style="width: 100%; max-width: 800px;" />


## Event Processing Flow

<img src="/img/diagrams/PartO-D-6.svg" alt="Event Processing Flow" style="width: 100%; max-width: 800px;" />

| Event State | Description |
|-------------|-------------|
| `New` | Freshly added, awaiting processing |
| `Processed` | Successfully executed |
| `Discarded` | Failed after max retries |


## Log Watcher Comparison

| Watcher Type | Mechanism | Latency | Infrastructure |
|--------------|-----------|---------|----------------|
| **PostgreSQL NOTIFY** | `NOTIFY` / `LISTEN` on channel | < 10ms | None (uses DB) |
| **Redis Pub/Sub** | `PUBLISH` / `SUBSCRIBE` on channel | < 1ms | Redis server |
| **FileSystem Watcher** | Touch file / Watch directory | < 100ms | Shared filesystem |
| **No Watcher (Polling)** | Poll `_Operations` table | 0-5s | None |
