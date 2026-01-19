# Operations Framework: Diagrams

This page contains visual diagrams explaining how Operations Framework works.

## High-Level Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Operations Framework                            │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                   Command Execution Pipeline                     │  │
│  │                                                                  │  │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────────────────────┐  │  │
│  │  │ Prepared   │  │ Nested     │  │ InMemory / DbOperation     │  │  │
│  │  │ Command    │─>│ Operation  │─>│ ScopeProvider              │  │  │
│  │  │ Handler    │  │ Logger     │  │                            │  │  │
│  │  │ (1B)       │  │ (11K)      │  │ (10K / 1K)                 │  │  │
│  │  └────────────┘  └────────────┘  └─────────────┬──────────────┘  │  │
│  │                                                │                 │  │
│  │                                                ▼                 │  │
│  │                                       ┌────────────────┐         │  │
│  │                                       │ Invalidating   │         │  │
│  │                                       │ Completion     │         │  │
│  │                                       │ Handler (100)  │         │  │
│  │                                       └────────────────┘         │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                      Background Services                         │  │
│  │                                                                  │  │
│  │  ┌──────────────────┐          ┌──────────────────┐              │  │
│  │  │ Operation Log    │          │ Operation Log    │              │  │
│  │  │ Reader           │          │ Trimmer          │              │  │
│  │  │ (Hosted Service) │          │ (Hosted Service) │              │  │
│  │  └──────────────────┘          └──────────────────┘              │  │
│  │                                                                  │  │
│  │  ┌──────────────────┐          ┌──────────────────┐              │  │
│  │  │ Event Log        │          │ Event Log        │              │  │
│  │  │ Reader           │          │ Trimmer          │              │  │
│  │  │ (Hosted Service) │          │ (Hosted Service) │              │  │
│  │  └──────────────────┘          └──────────────────┘              │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                      Notification System                         │  │
│  │                                                                  │  │
│  │  ┌──────────────────┐          ┌──────────────────┐              │  │
│  │  │ Log Watcher      │<─────────│ Completion       │              │  │
│  │  │ (PG/Redis/FS)    │          │ Notifier         │              │  │
│  │  └──────────────────┘          └──────────────────┘              │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

## Command Execution Flow

```
                         Command Execution Flow
                         ══════════════════════

    ┌────────────────────────────────────────────────────────────┐
    │                      Client Request                        │
    │                  CreateOrderCommand(...)                   │
    └────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌────────────────────────────────────────────────────────────────────┐
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                 1. PreparedCommandHandler (1B)               │  │
│  │                                                              │  │
│  │  - Validates IPreparedCommand implementations                │  │
│  │  - Calls command.Prepare() if applicable                     │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                  │                                 │
│                                  ▼                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                2. NestedOperationLogger (11K)                │  │
│  │                                                              │  │
│  │  - Captures nested command calls                             │  │
│  │  - Stores them in Operation.NestedOperations                 │  │
│  │  - Isolates their Operation.Items                            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                  │                                 │
│                                  ▼                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │            3. InMemoryOperationScopeProvider (10K)           │  │
│  │                                                              │  │
│  │  - Provides transient operation scope                        │  │
│  │  - Runs operation completion after execution                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                  │                                 │
│                                  ▼                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │             4. DbOperationScopeProvider<T> (1K)              │  │
│  │                                                              │  │
│  │  - Creates DbOperationScope<TDbContext>                      │  │
│  │  - Manages database transaction                              │  │
│  │  - Stores operation in same transaction                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                  │                                 │
│                                  ▼                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    YOUR COMMAND HANDLER                      │  │
│  │                                                              │  │
│  │  if (Invalidation.IsActive) {                                │  │
│  │      _ = GetOrder(command.OrderId, default);                 │  │
│  │      return default!;                                        │  │
│  │  }                                                           │  │
│  │                                                              │  │
│  │  var db = await DbHub.CreateOperationDbContext(ct);          │  │
│  │  // ... business logic ...                                   │  │
│  │  await db.SaveChangesAsync(ct);                              │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                  │                                 │
│                                  ▼                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │         5. InvalidatingCommandCompletionHandler (100)        │  │
│  │                                                              │  │
│  │  - Reacts to ICompletion<TCommand>                           │  │
│  │  - Runs command in Invalidation.Begin() block                │  │
│  │  - Also runs nested commands in invalidation mode            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Multi-Host Invalidation Flow

```
                     Multi-Host Invalidation Flow
                     ════════════════════════════

   Host A (Originator)                     Host B (Peer)
   ═══════════════════                     ═════════════

 ┌─────────────────────┐
 │ 1. Execute Command  │
 │                     │
 │ CreateOrderCommand  │
 └──────────┬──────────┘
            │
            ▼
 ┌─────────────────────┐
 │ 2. Create DbContext │
 │                     │
 │ BEGIN TRANSACTION   │
 └──────────┬──────────┘
            │
            ▼
 ┌─────────────────────┐
 │ 3. Business Logic   │
 │                     │
 │ INSERT INTO Orders  │
 └──────────┬──────────┘
            │
            ▼
 ┌─────────────────────┐
 │ 4. Store Operation  │
 │                     │
 │ INSERT INTO         │
 │ _Operations         │
 └──────────┬──────────┘
            │
            ▼
 ┌─────────────────────┐
 │ 5. Commit           │
 │                     │
 │ COMMIT TRANSACTION  │─────────────────────────────┐
 └──────────┬──────────┘                             │
            │                                        │
            ▼                                        │
 ┌─────────────────────┐                             │
 │ 6. Notify Watcher   │                             │
 │                     │                             │
 │ NOTIFY / PUBLISH    │──────────────────────┐      │
 └──────────┬──────────┘                      │      │
            │                                 │      │
            ▼                                 │      │
 ┌─────────────────────┐                      │      │
 │ 7. Local Completion │                      │      │
 │                     │                      │      │
 │ Run Invalidation    │                      │      │
 │ _ = GetOrder(...)   │                      │      │
 └─────────────────────┘                      │      │
                                              ▼      ▼
                                  ┌─────────────────────┐
                                  │ 8. Receive          │
                                  │    Notification     │
                                  │                     │
                                  │ LISTEN / SUBSCRIBE  │
                                  └──────────┬──────────┘
                                             │
                                             ▼
                                  ┌─────────────────────┐
                                  │ 9. Read Operation   │
                                  │    Log              │
                                  │                     │
                                  │ SELECT FROM         │
                                  │ _Operations         │
                                  │ WHERE LoggedAt > ?  │
                                  └──────────┬──────────┘
                                             │
                                             ▼
                                  ┌─────────────────────┐
                                  │ 10. Completion      │
                                  │     Notifier        │
                                  │                     │
                                  │ NotifyCompleted()   │
                                  └──────────┬──────────┘
                                             │
                                             ▼
                                  ┌─────────────────────┐
                                  │ 11. Run Invalidation│
                                  │                     │
                                  │ Invalidation.Begin()│
                                  │ _ = GetOrder(...)   │
                                  └─────────────────────┘
```

## Operation Scope Lifecycle

```
                     Operation Scope Lifecycle
                     ═════════════════════════

    ┌─────────────────────────────────────────────────────────┐
    │                     Command Starts                      │
    └─────────────────────────────────────────────────────────┘
                                │
                                ▼
                ┌────────────────────────────────┐
                │ InMemoryOperationScopeProvider │
                │         Creates Scope          │
                └───────────────┬────────────────┘
                                │
          ┌─────────────────────┴─────────────────────┐
          │                                           │
          ▼                                           ▼
┌───────────────────────┐               ┌───────────────────────┐
│    No DB Access       │               │    DB Access Used     │
│                       │               │                       │
│    InMemoryScope      │               │    DbOperationScope   │
│    IsTransient: true  │               │    IsTransient: false │
│    UUID: xxx-local    │               │    UUID: xxx          │
└───────────┬───────────┘               └───────────┬───────────┘
            │                                       │
            ▼                                       ▼
┌───────────────────────┐               ┌───────────────────────┐
│   Command Executes    │               │   BEGIN TRANSACTION   │
│                       │               │                       │
│   In-memory only      │               │   Business Logic      │
│   No persistence      │               │   Operation Stored    │
└───────────┬───────────┘               └───────────┬───────────┘
            │                                       │
            ▼                                       ▼
┌───────────────────────┐               ┌───────────────────────┐
│   Scope Disposed      │               │        COMMIT         │
│                       │               │                       │
│   Local invalidation  │               │   Verify commit       │
│   only                │               │   if error            │
└───────────┬───────────┘               └───────────┬───────────┘
            │                                       │
            └───────────────────┬───────────────────┘
                                │
                                ▼
                ┌────────────────────────────────┐
                │     Operation Completion       │
                │                                │
                │   - Deduplication              │
                │   - Listener invocation        │
                │   - Invalidation trigger       │
                └────────────────────────────────┘
```

## Event Processing Flow

```
                         Event Processing Flow
                         ═════════════════════

    ┌────────────────────────────────────────────────────────────┐
    │                     Command Handler                        │
    │                                                            │
    │  context.Operation.AddEvent(new OrderCreatedEvent(id))     │
    └─────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
    ┌────────────────────────────────────────────────────────────┐
    │                    Commit Transaction                      │
    │                                                            │
    │  _Operations: { Command, Items, Events[] }                 │
    │  _Events: { Uuid, Value, DelayUntil, State: New }          │
    └─────────────────────────────┬──────────────────────────────┘
                                  │
                                  │ (Async - background service)
                                  ▼
    ┌────────────────────────────────────────────────────────────┐
    │                    DbEventLogReader                        │
    │                                                            │
    │  SELECT * FROM _Events                                     │
    │  WHERE State = 'New' AND DelayUntil <= NOW()               │
    │  ORDER BY DelayUntil LIMIT 64                              │
    └─────────────────────────────┬──────────────────────────────┘
                                  │
                                  ▼
    ┌────────────────────────────────────────────────────────────┐
    │                    DbEventProcessor                        │
    │                                                            │
    │  if (event.Value is ICommand command)                      │
    │      await Commander.Call(command, true, ct)               │
    └─────────────────────────────┬──────────────────────────────┘
                                  │
                ┌─────────────────┴─────────────────┐
                │                                   │
           Success                              Failure
                │                                   │
                ▼                                   ▼
    ┌───────────────────────┐         ┌───────────────────────┐
    │  State = Processed    │         │  Retry (up to 5x)     │
    │                       │         │                       │
    │  UPDATE _Events       │         │  If exhausted:        │
    │  SET State = 2        │         │  State = Discarded    │
    └───────────────────────┘         └───────────┬───────────┘
                                                  │
                                                  ▼
                                      ┌───────────────────────┐
                                      │  DbEventLogTrimmer    │
                                      │                       │
                                      │  DELETE FROM _Events  │
                                      │  WHERE DelayUntil < ? │
                                      │  AND State != 'New'   │
                                      └───────────────────────┘
```

## Log Watcher Comparison

```
                      Log Watcher Comparison
                      ══════════════════════

┌───────────────────────────────────────────────────────────────────┐
│                      PostgreSQL NOTIFY                            │
│                                                                   │
│  Host A                                          Host B           │
│  ┌──────────┐                                 ┌──────────┐        │
│  │ Command  │                                 │ LISTEN   │        │
│  │ Executes │                                 │ channel  │<──┐    │
│  └────┬─────┘                                 └──────────┘   │    │
│       │                                                      │    │
│       ▼                                                      │    │
│  ┌──────────┐       ┌────────────────┐                       │    │
│  │ NOTIFY   │──────>│   PostgreSQL   │───────────────────────┘    │
│  │ channel  │       │   (built-in)   │                            │
│  └──────────┘       └────────────────┘                            │
│                                                                   │
│  Latency: < 10ms    Infrastructure: None (uses DB)                │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                        Redis Pub/Sub                              │
│                                                                   │
│  Host A                                          Host B           │
│  ┌──────────┐                                 ┌──────────┐        │
│  │ Command  │                                 │SUBSCRIBE │        │
│  │ Executes │                                 │ channel  │<──┐    │
│  └────┬─────┘                                 └──────────┘   │    │
│       │                                                      │    │
│       ▼                                                      │    │
│  ┌──────────┐       ┌────────────────┐                       │    │
│  │ PUBLISH  │──────>│     Redis      │───────────────────────┘    │
│  │ channel  │       │   (external)   │                            │
│  └──────────┘       └────────────────┘                            │
│                                                                   │
│  Latency: < 1ms     Infrastructure: Redis server                  │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                      FileSystem Watcher                           │
│                                                                   │
│  Process A                                       Process B        │
│  ┌──────────┐                                 ┌──────────┐        │
│  │ Command  │                                 │FileSystem│        │
│  │ Executes │                                 │ Watcher  │<──┐    │
│  └────┬─────┘                                 └──────────┘   │    │
│       │                                                      │    │
│       ▼                                                      │    │
│  ┌──────────┐       ┌────────────────┐                       │    │
│  │ Touch    │──────>│  Shared File   │───────────────────────┘    │
│  │ file     │       │  (temp dir)    │                            │
│  └──────────┘       └────────────────┘                            │
│                                                                   │
│  Latency: < 100ms   Infrastructure: Shared filesystem             │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                      No Watcher (Polling)                         │
│                                                                   │
│  Host A                                          Host B           │
│  ┌──────────┐                                 ┌──────────┐        │
│  │ Command  │                                 │  Poll    │        │
│  │ Executes │                                 │  every   │        │
│  └────┬─────┘                                 │  5 sec   │        │
│       │                                       └────┬─────┘        │
│       ▼                                            │              │
│  ┌──────────┐       ┌────────────────┐             │              │
│  │ Write to │──────>│    Database    │<────────────┘              │
│  │ _Ops     │       │                │                            │
│  └──────────┘       └────────────────┘                            │
│                                                                   │
│  Latency: 0-5s      Infrastructure: None                          │
└───────────────────────────────────────────────────────────────────┘
```

## Operation Items vs Context Items

```
                 Operation.Items vs CommandContext.Items
                 ═══════════════════════════════════════

┌───────────────────────────────────────────────────────────────────┐
│                    CommandContext.Items                           │
│                                                                   │
│  Scope: Single command execution on originating host              │
│  Lifetime: From command start to command end                      │
│  Persistence: None (in-memory only)                               │
│  Cross-host: No                                                   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  Host A                                                     │  │
│  │  ┌───────────────────────────────────────────────────────┐  │  │
│  │  │  Command Execution                                    │  │  │
│  │  │  ┌─────────────────┐                                  │  │  │
│  │  │  │ context.Items   │ <── Available here               │  │  │
│  │  │  │   .Set(...)     │                                  │  │  │
│  │  │  │   .Get<T>()     │                                  │  │  │
│  │  │  └─────────────────┘                                  │  │  │
│  │  └───────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│                      Operation.Items                              │
│                                                                   │
│  Scope: Operation and its invalidation across all hosts           │
│  Lifetime: From command start through invalidation on all hosts   │
│  Persistence: Stored in database with operation                   │
│  Cross-host: Yes                                                  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  Host A (Originator)                                        │  │
│  │  ┌───────────────────────────────────────────────────────┐  │  │
│  │  │  Command Execution                                    │  │  │
│  │  │  ┌───────────────────┐                                │  │  │
│  │  │  │ operation.Items   │ <── Store data here            │  │  │
│  │  │  │ .KeylessSet(x)    │                                │  │  │
│  │  │  └─────────┬─────────┘                                │  │  │
│  │  │            │ Serialized to JSON                       │  │  │
│  │  │            ▼                                          │  │  │
│  │  │  ┌───────────────────┐                                │  │  │
│  │  │  │ _Operations       │                                │  │  │
│  │  │  │ ItemsJson: {...}  │                                │  │  │
│  │  │  └───────────────────┘                                │  │  │
│  │  └───────────────────────────────────────────────────────┘  │  │
│  │                                                             │  │
│  │  ┌───────────────────────────────────────────────────────┐  │  │
│  │  │  Invalidation Block                                   │  │  │
│  │  │  ┌───────────────────┐                                │  │  │
│  │  │  │ operation.Items   │ <── Available here too         │  │  │
│  │  │  │ .KeylessGet<T>()  │                                │  │  │
│  │  │  └───────────────────┘                                │  │  │
│  │  └───────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  Host B (Peer)                                              │  │
│  │  ┌───────────────────────────────────────────────────────┐  │  │
│  │  │  Invalidation Block (replayed)                        │  │  │
│  │  │  ┌───────────────────┐                                │  │  │
│  │  │  │ operation.Items   │ <── Same data available!       │  │  │
│  │  │  │ .KeylessGet<T>()  │     (deserialized from DB)     │  │  │
│  │  │  └───────────────────┘                                │  │  │
│  │  └───────────────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────┘
```
