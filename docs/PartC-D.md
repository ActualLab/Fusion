# CommandR: Diagrams

Diagrams for the CommandR concepts introduced in [Part 4](PartC.md).


## Command Handler Pipeline

```mermaid
flowchart TD
    Call["Commander.Call(command)"] --> Context["CommandContext&nbsp;Created<br/>•&nbsp;New&nbsp;ServiceScope&nbsp;(if&nbsp;outermost)<br/>•&nbsp;ExecutionState&nbsp;initialized"]

    subgraph Pipeline ["&nbsp;Handler&nbsp;Pipeline&nbsp;(Descending&nbsp;Priority)&nbsp;"]
        direction TB
        Filters["Filtering&nbsp;Handlers<br/>PreparedCommandHandler,&nbsp;CommandTracer,<br/>LocalCommandRunner,&nbsp;RpcCommandHandler"]
        Filters --> Ops["Operations&nbsp;Framework&nbsp;Handlers<br/>(See&nbsp;PartO)"]
        Ops --> Final["Your&nbsp;Command&nbsp;Handler&nbsp;[Final]<br/>Priority:&nbsp;0"]
    end

    Context --> Pipeline
    Pipeline --> Result["Result&nbsp;Returned<br/>•&nbsp;context.TryComplete<br/>•&nbsp;context.DisposeAsync"]
```

| Handler | Priority | Type | Purpose |
|---------|----------|------|---------|
| `PreparedCommandHandler` | 1,000,000,000 | Filter | Calls `IPreparedCommand.Prepare()` if implemented |
| `CommandTracer` | 998,000,000 | Filter | Creates Activity for tracing, logs errors |
| `LocalCommandRunner` | 900,000,000 | Filter | Runs `ILocalCommand.Run()` if implemented |
| `RpcCommandHandler` | 800,000,000 | Filter | Routes to RPC if command should be handled remotely |
| `OperationReprocessor` | 100,000 | Filter | Operations Framework |
| `NestedOperationLogger` | 11,000 | Filter | Operations Framework |
| `OperationScopeProvider` | 10,000 | Filter | Operations Framework |
| `DbOperationScopeProvider` | 1,000 | Filter | Operations Framework |
| Your Handler | 0 | Final | Your business logic |


## CommandContext Hierarchy for Nested Commands

```mermaid
flowchart TD
    Call["Commander.Call(OuterCommand)"]
    Call --> C1

    subgraph C1 ["CommandContext&nbsp;#1&nbsp;(Outermost)"]
        direction TB
        Info1["Command:&nbsp;OuterCommand<br/>ServiceScope:&nbsp;New&nbsp;scope&nbsp;created<br/>OuterContext:&nbsp;null<br/>OutermostContext:&nbsp;this"]
        Info1 --> CallInner["Handler&nbsp;calls&nbsp;Commander.Call(InnerCommand)"]

        subgraph C2 ["CommandContext&nbsp;#2&nbsp;(Nested)"]
            direction TB
            Info2["Command:&nbsp;InnerCommand<br/>ServiceScope:&nbsp;Shared&nbsp;with&nbsp;#1<br/>OuterContext:&nbsp;Context&nbsp;#1<br/>OutermostContext:&nbsp;Context&nbsp;#1"]
            Info2 --> CallDeep["Handler&nbsp;calls&nbsp;Commander.Call(DeepCommand)"]

            subgraph C3 ["CommandContext&nbsp;#3&nbsp;(Deeply&nbsp;Nested)"]
                Info3["Command:&nbsp;DeepCommand<br/>ServiceScope:&nbsp;Shared&nbsp;with&nbsp;#1<br/>OuterContext:&nbsp;Context&nbsp;#2<br/>OutermostContext:&nbsp;Context&nbsp;#1"]
            end
            CallDeep --> C3
        end
        CallInner --> C2
    end
```

| Property | Behavior |
|----------|----------|
| `ServiceScope` | Shared across all nested contexts (same `ICommander`) |
| `Items` | Each context has its own dictionary |
| `OutermostContext` | Always points to the root context |
| Data sharing | Use `context.OutermostContext.Items` to share across contexts |


## IOutermostCommand Behavior

```mermaid
flowchart LR
    subgraph Outermost ["IOutermostCommand&nbsp;(forced&nbsp;isolation)"]
        direction LR
        O1["Context #1"] --> O2["Call(OutermostCmd)"]
        O2 --> O3["Context #2 (NEW SCOPE!)<br/>OuterContext: null<br/>ServiceScope: NEW<br/>OutermostContext: #2"]
    end

    subgraph Regular ["Regular&nbsp;Command&nbsp;(nested)"]
        direction LR
        R1["Context #1"] --> R2["Call(RegularCmd)"]
        R2 --> R3["Context #2<br/>OuterContext: #1<br/>ServiceScope: shared"]
    end
```

### IDelegatingCommand Behavior

```mermaid
flowchart TD
    Call["Commander.Call(BatchProcessCommand)<br/>// IDelegatingCommand"] --> C1["Context&nbsp;#1&nbsp;(Delegating&nbsp;command&nbsp;context)<br/>•&nbsp;Operations&nbsp;Framework&nbsp;handlers&nbsp;filtered&nbsp;out<br/>•&nbsp;No&nbsp;operation&nbsp;logging&nbsp;for&nbsp;this&nbsp;command"]

    C1 --> Item1["Commander.Call(ProcessItemCommand)"]
    C1 --> Item2["Commander.Call(ProcessItemCommand)"]

    Item1 --> C2["Context&nbsp;#2&nbsp;(Forced&nbsp;outermost)<br/>•&nbsp;NEW&nbsp;ServiceScope<br/>•&nbsp;Full&nbsp;Operations&nbsp;Framework&nbsp;pipeline"]
    Item2 --> C3["Context&nbsp;#3&nbsp;(Also&nbsp;forced&nbsp;outermost)<br/>•&nbsp;NEW&nbsp;ServiceScope<br/>•&nbsp;Independent&nbsp;from&nbsp;Context&nbsp;#2"]
```

**Code in `CommandContext.New()`:**
```csharp
if (!isOutermost && (command is IOutermostCommand ||
                     Current?.UntypedCommand is IDelegatingCommand))
    isOutermost = true;
```


## IEventCommand Parallel Execution

```mermaid
flowchart TD
    Call["Commander.Call(OrderCreatedEvent { ChainId = #quot;#quot; })"] --> Detect["Commander.Run() detects:<br/>IEventCommand with empty ChainId"]
    Detect --> Build["RunEvent() builds handler chains"]

    Build --> Clone1["Clone #1<br/>ChainId = #quot;NotificationHandlers.SendEmail#quot;"]
    Build --> Clone2["Clone #2<br/>ChainId = #quot;NotificationHandlers.UpdateAnalytics#quot;"]
    Build --> Clone3["Clone #3<br/>ChainId = #quot;NotificationHandlers.NotifyWarehouse#quot;"]

    Clone1 --> H1["Filters + SendEmail handler"]
    Clone2 --> H2["Filters + UpdateAnalytics handler"]
    Clone3 --> H3["Filters + NotifyWarehouse handler"]

    H1 --> WhenAll["Task.WhenAll()<br/>All complete"]
    H2 --> WhenAll
    H3 --> WhenAll
```

| Scenario | Behavior |
|----------|----------|
| `ChainId` is empty | `RunEvent()` builds chains, clones command for each, runs in parallel |
| `ChainId` is set | Goes through `RunCommand()`, executes only that specific handler chain |

**ChainId format:** `{ServiceType.GetName()}.{Method.Name}`


## ISessionCommand Processing (RpcDefaultSessionReplacer)

```mermaid
flowchart TD
    subgraph Client
        Cmd["var&nbsp;cmd&nbsp;=&nbsp;new&nbsp;UpdateProfileCommand&nbsp;{&nbsp;Session&nbsp;=&nbsp;Session.Default&nbsp;}"]
        Call["Commander.Call(cmd)"]
        Cmd --> Call
    end

    Call -->|RPC Call| Inbound

    subgraph Server
        Inbound["RPC&nbsp;Inbound&nbsp;Pipeline"]
        Replacer["RpcDefaultSessionReplacer&nbsp;(IRpcMiddleware)"]
        Handler["Command&nbsp;Handler&nbsp;—&nbsp;cmd.Session&nbsp;is&nbsp;now&nbsp;valid!"]

        Inbound --> Replacer
        Replacer -->|"Session.Default&nbsp;→&nbsp;actual&#8209;session&#8209;id"| Handler
    end
```

### RpcDefaultSessionReplacer Logic

| Step | Action |
|------|--------|
| 1 | Check if first param is `ISessionCommand` |
| 2 | Get `SessionBoundRpcConnection` from peer |
| 3 | If `session.IsDefault()`: `command.SetSession(connection.Session)` |
| 4 | Else: `session.RequireValid()` |

### Session Resolution Flow

```mermaid
flowchart LR
    RpcCall["RpcInboundCall<br/>call.Context.Peer.ConnectionState"] --> Connection["SessionBoundRpcConnection"]
    Connection --> Session["Session<br/>(from cookie/auth token)"]
```


## Handler Registration and Resolution

### Registration (at startup)

```mermaid
flowchart TD
    Add["services.AddCommander()"]
    Add --> Handlers[".AddHandlers&lt;OrderHandlers&gt;()"]
    Add --> Service[".AddService&lt;OrderService&gt;()"]

    Handlers --> Scan1["Scan&nbsp;for:<br/>•&nbsp;ICommandHandler&lt;T&gt;&nbsp;interfaces<br/>•&nbsp;Methods&nbsp;with&nbsp;[CommandHandler]"]
    Scan1 --> Desc["Create&nbsp;CommandHandler&nbsp;descriptors:<br/>ServiceType,&nbsp;CommandType,&nbsp;Priority,&nbsp;IsFilter"]

    Service --> Scan2["1.&nbsp;Register&nbsp;with&nbsp;proxy<br/>2.&nbsp;Scan&nbsp;for&nbsp;[CommandHandler]&nbsp;methods<br/>3.&nbsp;Proxy&nbsp;prevents&nbsp;direct&nbsp;calls&nbsp;(throws)"]
```

### Resolution (at runtime)

```mermaid
flowchart TD
    Call["Commander.Call(CreateOrderCommand)"] --> Resolve["CommandHandlerResolver.GetCommandHandlers(commandType)"]
    Resolve --> Steps["1.&nbsp;Get&nbsp;all&nbsp;base&nbsp;types&nbsp;of&nbsp;command<br/>2.&nbsp;Find&nbsp;registered&nbsp;handlers<br/>3.&nbsp;Apply&nbsp;handler&nbsp;filters<br/>4.&nbsp;Sort&nbsp;by&nbsp;Priority&nbsp;DESC<br/>5.&nbsp;Verify:&nbsp;only&nbsp;ONE&nbsp;non-filter&nbsp;handler"]
    Steps --> Chain["CommandHandlerChain"]
```

| Index | Handler | Priority | Type |
|-------|---------|----------|------|
| [0] | `PreparedCommandHandler` | 1,000,000,000 | Filter |
| [1] | `CommandTracer` | 998,000,000 | Filter |
| [2] | `LocalCommandRunner` | 900,000,000 | Filter |
| [3] | `RpcCommandHandler` | 800,000,000 | Filter |
| [4] | `OperationReprocessor` | 100,000 | Filter |
| [5] | `NestedOperationLogger` | 11,000 | Filter |
| [6] | `InMemoryOperationScope` | 10,000 | Filter |
| [7] | `DbOperationScopeProvider` | 1,000 | Filter |
| [8] | `YourHandler` | 0 | Final |


## Command Service Proxy (AOP)

```mermaid
flowchart TD
    subgraph Registration
        direction TB
        Reg["commander.AddService&lt;OrderService&gt;()"]
        Reg --> Gen["Runtime&nbsp;generates:<br/>OrderServiceProxy&nbsp;:&nbsp;OrderService"]
    end

    subgraph CallFlow ["Call&nbsp;Flow"]
        direction TB
        Direct["orderService.CreateOrder(cmd,&nbsp;ct)"] --> Proxy["OrderServiceProxy.CreateOrder()"]
        Proxy --> Check{"CommandContext<br/>exists&nbsp;and&nbsp;matches?"}
        Check -->|No| Commander["Commander.Call(CreateOrderCommand)"]
        Check -->|Yes| Original["OrderService.CreateOrder()&nbsp;executes"]
        Commander --> Context["Creates&nbsp;CommandContext<br/>Runs&nbsp;handler&nbsp;pipeline"]
        Context --> Proxy2["Pipeline&nbsp;invokes&nbsp;OrderServiceProxy"]
        Proxy2 --> Original
    end
```

| Call Style | Result |
|------------|--------|
| `commander.Call(new CreateOrderCommand(...), ct)` | Full pipeline |
| `orderService.CreateOrder(new CreateOrderCommand(...), ct)` | Full pipeline (via proxy) |

Both paths go through: `PreparedCommandHandler` → `CommandTracer` → ... → `Handler`
