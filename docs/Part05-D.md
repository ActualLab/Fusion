# Operations Framework: Diagrams

This page contains visual diagrams explaining how Operations Framework works.

## High-Level Architecture

```mermaid
flowchart TD
    subgraph Pipeline ["Command&nbsp;Execution&nbsp;Pipeline"]
        direction LR
        NOL["NestedOperationLogger<br/>(11000)"]
        IOSP["InMemory/DbOperation<br/>ScopeProvider<br/>(10000/1000)"]
        ICH["InvalidatingCompletionHandler<br/>(100)"]
    end

    subgraph Background ["Background&nbsp;Services"]
        direction LR
        OLR["Operation Log Reader"] ~~~ OLT["Operation Log Trimmer"]
        ELR["Event Log Reader"] ~~~ ELT["Event Log Trimmer"]
    end

    subgraph Notification ["Notification&nbsp;System"]
        direction LR
        CN["Completion Notifier"] ~~~ LW["Log Watcher<br/>(PG/Redis/FS)"]
    end
```

**Command Execution Pipeline:**

| Class | Description |
|-------|-------------|
| `NestedOperationLogger` | Captures nested command calls |
| `InMemoryOperationScopeProvider` | Provides transient operation scope |
| `DbOperationScopeProvider` | Provides DB-backed operation scope |
| `InvalidatingCompletionHandler` | Runs invalidation for completed operations |

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
| `DbOperationCompletionNotifier` | Triggers invalidation for replayed operations |
| `PostgreSqlDbLogWatcher` | Listens for NOTIFY signals |
| `RedisDbLogWatcher` | Subscribes to Redis pub/sub |
| `FileSystemDbLogWatcher` | Watches for file changes |


## Command Execution Flow

```mermaid
flowchart TD
    Client["Client Request<br/>CreateOrderCommand(...)"] --> P1

    subgraph Handlers ["Command&nbsp;Handlers"]
        P1["1. NestedOperationLogger (11000)<br/>Captures nested command calls<br/>Stores in Operation.NestedOperations"]
        P1 --> P2["2. InMemoryOperationScopeProvider (10000)<br/>Provides transient operation scope<br/>Runs operation completion"]
        P2 --> P3["3. DbOperationScopeProvider (1000)<br/>Creates DbOperationScope<br/>Manages DB transaction"]
        P3 --> Your["YOUR COMMAND HANDLER<br/>Business logic + invalidation block"]
        Your --> P4["4. InvalidatingCommandCompletionHandler (100)<br/>Reacts to ICompletion<br/>Runs invalidation mode"]
    end
```

| Handler | Priority | Responsibility |
|---------|----------|----------------|
| `NestedOperationLogger` | 11,000 | Captures nested commands, isolates `Operation.Items` |
| `InMemoryOperationScopeProvider` | 10,000 | Transient scope, completion handling |
| `DbOperationScopeProvider` | 1,000 | DB transaction, operation persistence |
| `InvalidatingCommandCompletionHandler` | 100 | Runs invalidation pass |


## Operation Scope Lifecycle

```mermaid
flowchart TD
    Start["Command&nbsp;Starts"] --> Provider["InMemoryOperationScopeProvider&nbsp;Creates&nbsp;Scope"]

    Provider --> NoDb["No&nbsp;DB&nbsp;Access<br/>InMemoryScope<br/>IsTransient:&nbsp;true<br/>UUID:&nbsp;xxx-local"]
    Provider --> WithDb["DB&nbsp;Access&nbsp;Used<br/>DbOperationScope<br/>IsTransient:&nbsp;false<br/>UUID:&nbsp;xxx"]

    NoDb --> ExecNoDb["Command&nbsp;Executes<br/>In-memory&nbsp;only<br/>No&nbsp;persistence"]
    WithDb --> ExecDb["BEGIN&nbsp;TRANSACTION<br/>Business&nbsp;Logic<br/>Operation&nbsp;Stored"]

    ExecNoDb --> DisposeNoDb["Scope&nbsp;Disposed<br/>Local&nbsp;invalidation&nbsp;only"]
    ExecDb --> CommitDb["COMMIT<br/>Verify&nbsp;commit&nbsp;if&nbsp;error"]

    DisposeNoDb --> Completion["Operation&nbsp;Completion<br/>•&nbsp;Deduplication<br/>•&nbsp;Listener&nbsp;invocation<br/>•&nbsp;Invalidation&nbsp;trigger"]
    CommitDb --> Completion
```


## Operation Items vs Context Items

| Property | `CommandContext.Items` | `Operation.Items` |
|----------|------------------------|-------------------|
| **Scope** | Single command execution | Operation + invalidation across all hosts |
| **Lifetime** | Command start → end | Command start → invalidation on all hosts |
| **Persistence** | In-memory only | Stored in database (JSON) |
| **Cross-host** | No | Yes |
| **Usage** | `context.Items.Set(...)` / `Get<T>()` | `operation.Items.KeylessSet(x)` / `KeylessGet<T>()` |

```mermaid
flowchart TD
    subgraph ContextItems ["CommandContext.Items&nbsp;(Local&nbsp;Only)"]
        direction TB
        CI1["Host A: Command Execution"]
        CI2["context.Items.Set(...)<br/>context.Items.Get&lt;T&gt;()"]
        CI1 --> CI2
    end

    subgraph OpItems ["Operation.Items&nbsp;(Cross-Host)"]
        direction TB
        OI1["Host A: Command Execution"]
        OI2["operation.Items.KeylessSet(x)"]
        OI3["Serialized to _Operations.ItemsJson"]
        OI4["Host A: Invalidation Block<br/>operation.Items.KeylessGet&lt;T&gt;()"]
        OI5["Host B: Invalidation Block<br/>Same data available!<br/>(deserialized from DB)"]

        OI1 --> OI2
        OI2 --> OI3
        OI3 --> OI4
        OI3 --> OI5
    end
```


## Multi-Host Invalidation Flow

```mermaid
sequenceDiagram
    participant A as Host A (Originator)
    participant DB as Database
    participant PS as Pub/Sub (Redis/PG/FS)
    participant B as Host B (Peer)

    A->>A: 1. Execute Command
    A->>A: 2. BEGIN TRANSACTION
    A->>A: 3. Business Logic
    A->>A: 4. Store Operation
    A->>DB: 5. COMMIT
    A->>PS: 6. Notify
    PS->>B: Notification
    A->>A: 7. Local Invalidation
    B->>DB: 8. Read Operation Log
    DB-->>B: Operation data
    B->>B: 9. Completion Notifier
    B->>B: 10. Run Invalidation
```


## Event Processing Flow

```mermaid
flowchart TD
    Handler["Command&nbsp;Handler<br/>context.Operation.AddEvent(new&nbsp;OrderCreatedEvent(id))"]
    Handler --> Commit["Commit&nbsp;Transaction<br/>_Operations:&nbsp;{&nbsp;Command,&nbsp;Items,&nbsp;Events[]&nbsp;}<br/>_Events:&nbsp;{&nbsp;Uuid,&nbsp;Value,&nbsp;State:&nbsp;New&nbsp;}"]

    Commit -->|"Async&nbsp;(background)"| Reader["DbEventLogReader<br/>SELECT&nbsp;*&nbsp;FROM&nbsp;_Events<br/>WHERE&nbsp;State&nbsp;=&nbsp;'New'"]

    Reader --> Processor["DbEventProcessor<br/>if&nbsp;(event.Value&nbsp;is&nbsp;ICommand)<br/>await&nbsp;Commander.Call(command)"]

    Processor --> Success["Success<br/>State&nbsp;=&nbsp;Processed"]
    Processor --> Failure["Failure<br/>Retry&nbsp;(up&nbsp;to&nbsp;5x)"]

    Failure --> Discarded["If&nbsp;exhausted:<br/>State&nbsp;=&nbsp;Discarded"]
    Discarded --> Trimmer["DbEventLogTrimmer<br/>DELETE&nbsp;FROM&nbsp;_Events<br/>WHERE&nbsp;State&nbsp;!=&nbsp;'New'"]
```

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
