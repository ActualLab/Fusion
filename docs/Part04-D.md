# CommandR: Diagrams

Text-based diagrams for the CommandR concepts introduced in [Part 4](Part04.md).


## Command Handler Pipeline

```
┌───────────────────────────────────────────────────────────────────────┐
│                    Command Handler Pipeline                           │
└───────────────────────────────────────────────────────────────────────┘

  Commander.Call(command)
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │                    CommandContext Created                           │
  │  • New ServiceScope (if outermost)                                  │
  │  • ExecutionState initialized with handler chain                    │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
  ╔════════════════════════════════╧════════════════════════════════════╗
  ║                       HANDLER PIPELINE                              ║
  ║                    (Descending Priority)                            ║
  ╠═════════════════════════════════════════════════════════════════════╣
  ║                                                                     ║
  ║  Priority: 1,000,000,000                                            ║
  ║  ┌─────────────────────────────────────────────────────────────┐    ║
  ║  │              PreparedCommandHandler [Filter]                │    ║
  ║  │  if (command is IPreparedCommand pc)                        │    ║
  ║  │      await pc.Prepare(context, ct);                         │    ║
  ║  │  await context.InvokeRemainingHandlers(ct);                 │    ║
  ║  └─────────────────────────────────────────────────────────────┘    ║
  ║                              │                                      ║
  ║  Priority: 998,000,000       ▼                                      ║
  ║  ┌─────────────────────────────────────────────────────────────┐    ║
  ║  │                  CommandTracer [Filter]                     │    ║
  ║  │  Creates Activity for tracing, logs errors                  │    ║
  ║  │  await context.InvokeRemainingHandlers(ct);                 │    ║
  ║  └─────────────────────────────────────────────────────────────┘    ║
  ║                              │                                      ║
  ║  Priority: 900,000,000       ▼                                      ║
  ║  ┌─────────────────────────────────────────────────────────────┐    ║
  ║  │               LocalCommandRunner [Filter]                   │    ║
  ║  │  if (command is ILocalCommand lc)                           │    ║
  ║  │      await lc.Run(context, ct);                             │    ║
  ║  │  else                                                       │    ║
  ║  │      await context.InvokeRemainingHandlers(ct);             │    ║
  ║  └─────────────────────────────────────────────────────────────┘    ║
  ║                              │                                      ║
  ║  Priority: 800,000,000       ▼                                      ║
  ║  ┌─────────────────────────────────────────────────────────────┐    ║
  ║  │                RpcCommandHandler [Filter]                   │    ║
  ║  │  Routes to RPC if command should be handled remotely        │    ║
  ║  │  await context.InvokeRemainingHandlers(ct);                 │    ║
  ║  └─────────────────────────────────────────────────────────────┘    ║
  ║                              │                                      ║
  ║                              ▼                                      ║
  ║          ┌─────────────────────────────────────┐                    ║
  ║          │   Operations Framework Handlers     │                    ║
  ║          │   (See Part 5 for details)          │                    ║
  ║          │   • OperationReprocessor (100,000)  │                    ║
  ║          │   • NestedOperationLogger (11,000)  │                    ║
  ║          │   • OperationScopeProvider (10,000) │                    ║
  ║          │   • DbOperationScopeProvider (1,000)│                    ║
  ║          └─────────────────────────────────────┘                    ║
  ║                              │                                      ║
  ║  Priority: 0 (default)       ▼                                      ║
  ║  ┌─────────────────────────────────────────────────────────────┐    ║
  ║  │              Your Command Handler [Final]                   │    ║
  ║  │  [CommandHandler]                                           │    ║
  ║  │  public async Task<TResult> Handle(TCommand cmd, ct)        │    ║
  ║  │  {                                                          │    ║
  ║  │      // Your business logic                                 │    ║
  ║  │      return result;                                         │    ║
  ║  │  }                                                          │    ║
  ║  └─────────────────────────────────────────────────────────────┘    ║
  ║                                                                     ║
  ╚═════════════════════════════════════════════════════════════════════╝
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │                        Result Returned                              │
  │  • context.TryComplete(ct)                                          │
  │  • context.DisposeAsync()                                           │
  └─────────────────────────────────────────────────────────────────────┘
```


## CommandContext Hierarchy (Nested Commands)

```
┌───────────────────────────────────────────────────────────────────────┐
│                  CommandContext Hierarchy                             │
└───────────────────────────────────────────────────────────────────────┘


  Commander.Call(OuterCommand)
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  CommandContext #1 (Outermost)                                      │
  │  ┌───────────────────────────────────────────────────────────────┐  │
  │  │  Command: OuterCommand                                        │  │
  │  │  ServiceScope: New scope created                              │  │
  │  │  OuterContext: null                                           │  │
  │  │  OutermostContext: this (self)                                │  │
  │  │  Items: { } (own dictionary)                                  │  │
  │  └───────────────────────────────────────────────────────────────┘  │
  │                              │                                      │
  │          Handler calls Commander.Call(InnerCommand)                 │
  │                              │                                      │
  │                              ▼                                      │
  │  ┌───────────────────────────────────────────────────────────────┐  │
  │  │  CommandContext #2 (Nested)                                   │  │
  │  │  ┌─────────────────────────────────────────────────────────┐  │  │
  │  │  │  Command: InnerCommand                                  │  │  │
  │  │  │  ServiceScope: Shared with #1                           │  │  │
  │  │  │  OuterContext: Context #1                               │  │  │
  │  │  │  OutermostContext: Context #1                           │  │  │
  │  │  │  Items: { } (own dictionary)                            │  │  │
  │  │  └─────────────────────────────────────────────────────────┘  │  │
  │  │                           │                                   │  │
  │  │       Handler calls Commander.Call(DeepCommand)               │  │
  │  │                           │                                   │  │
  │  │                           ▼                                   │  │
  │  │  ┌─────────────────────────────────────────────────────────┐  │  │
  │  │  │  CommandContext #3 (Deeply Nested)                      │  │  │
  │  │  │  ┌───────────────────────────────────────────────────┐  │  │  │
  │  │  │  │  Command: DeepCommand                             │  │  │  │
  │  │  │  │  ServiceScope: Shared with #1                     │  │  │  │
  │  │  │  │  OuterContext: Context #2                         │  │  │  │
  │  │  │  │  OutermostContext: Context #1                     │  │  │  │
  │  │  │  │  Items: { } (own dictionary)                      │  │  │  │
  │  │  │  └───────────────────────────────────────────────────┘  │  │  │
  │  │  └─────────────────────────────────────────────────────────┘  │  │
  │  └───────────────────────────────────────────────────────────────┘  │
  └─────────────────────────────────────────────────────────────────────┘


  Key Points:
  ───────────────────────────────────────────────────────────────────────
  • ServiceScope is shared across all nested contexts (same ICommander)
  • Each context has its own Items dictionary
  • To share data across contexts, use: context.OutermostContext.Items
  • OutermostContext always points to the root context
```


## IOutermostCommand / IDelegatingCommand Behavior

```
┌───────────────────────────────────────────────────────────────────────┐
│            IOutermostCommand / IDelegatingCommand                     │
└───────────────────────────────────────────────────────────────────────┘


  Regular Command (nested):              IOutermostCommand (forced isolation):
  ─────────────────────────              ────────────────────────────────────

  Context #1                             Context #1
  │                                      │
  ├─► Call(RegularCmd)                   ├─► Call(OutermostCmd)
  │   │                                  │   │
  │   └─► Context #2                     │   └─► Context #2 (NEW SCOPE!)
  │       OuterContext: #1               │       OuterContext: null
  │       ServiceScope: shared           │       ServiceScope: NEW
  │                                      │       OutermostContext: #2 (self)


  IDelegatingCommand Flow:
  ───────────────────────────────────────────────────────────────────────

  Commander.Call(BatchProcessCommand)    // IDelegatingCommand
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Context #1 (Delegating command context)                            │
  │  • Operations Framework handlers filtered out                       │
  │  • No operation logging for this command                            │
  │  • No invalidation pass needed                                      │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
          Handler calls Commander.Call(ProcessItemCommand)
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Context #2 (Forced outermost because parent is IDelegatingCommand) │
  │  • NEW ServiceScope                                                 │
  │  • Full Operations Framework pipeline                               │
  │  • Own operation, transaction, invalidation                         │
  └─────────────────────────────────────────────────────────────────────┘
                                   │
          Handler calls Commander.Call(ProcessItemCommand)
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Context #3 (Also forced outermost)                                 │
  │  • NEW ServiceScope                                                 │
  │  • Independent from Context #2                                      │
  └─────────────────────────────────────────────────────────────────────┘


  Code in CommandContext.New():
  ───────────────────────────────────────────────────────────────────────
  if (!isOutermost && (command is IOutermostCommand ||
                       Current?.UntypedCommand is IDelegatingCommand))
      isOutermost = true;
```


## IEventCommand Parallel Execution

```
┌───────────────────────────────────────────────────────────────────────┐
│                  IEventCommand Parallel Execution                     │
└───────────────────────────────────────────────────────────────────────┘


  Commander.Call(OrderCreatedEvent { ChainId = "" })
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Commander.Run() detects: IEventCommand with empty ChainId          │
  │  ──────────────────────────────────────────────────────────────     │
  │  if (command is IEventCommand evt && evt.ChainId.IsNullOrEmpty())   │
  │      return RunEvent(evt, context, ct);                             │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  RunEvent() builds handler chains                                   │
  │  ───────────────────────────────────────────────────────────────    │
  │  HandlerChains = {                                                  │
  │    "NotificationHandlers.SendEmail"      → [Filters + SendEmail],   │
  │    "NotificationHandlers.UpdateAnalytics"→ [Filters + Analytics],   │
  │    "NotificationHandlers.NotifyWarehouse"→ [Filters + Warehouse]    │
  │  }                                                                  │
  │                                                                     │
  │  ChainId format: "{ServiceType.GetName()}.{Method.Name}"            │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Clone command for each chain, set ChainId, run in parallel         │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
  │ Clone #1         │    │ Clone #2         │    │ Clone #3         │
  │ ChainId =        │    │ ChainId =        │    │ ChainId =        │
  │ "Notification    │    │ "Notification    │    │ "Notification    │
  │  Handlers.       │    │  Handlers.       │    │  Handlers.       │
  │  SendEmail"      │    │  UpdateAnalytics"│    │  NotifyWarehouse"│
  └────────┬─────────┘    └────────┬─────────┘    └────────┬─────────┘
         │                         │                         │
         ▼                         ▼                         ▼
  ┌─────────────┐           ┌─────────────┐           ┌─────────────┐
  │Commander    │           │Commander    │           │Commander    │
  │.Call()      │           │.Call()      │           │.Call()      │
  └──────┬──────┘           └──────┬──────┘           └──────┬──────┘
         │                         │                         │
         ▼                         ▼                         ▼
  ┌─────────────┐           ┌─────────────┐           ┌─────────────┐
  │ Filters +   │           │ Filters +   │           │ Filters +   │
  │ SendEmail   │           │ Update      │           │ Notify      │
  │ handler     │           │ Analytics   │           │ Warehouse   │
  └─────────────┘           └─────────────┘           └─────────────┘
         │                         │                         │
         └─────────────────────────┼─────────────────────────┘
                                   │
                                   ▼
                         ┌─────────────────┐
                         │ Task.WhenAll()  │
                         │ All complete    │
                         └─────────────────┘


  When ChainId is set (re-entry):
  ───────────────────────────────────────────────────────────────────────
  Commander.Call(OrderCreatedEvent { ChainId = "NotificationHandlers.SendEmail" })
         │
         ▼
  ChainId is NOT empty → goes through RunCommand(), not RunEvent()
         │
         ▼
  GetHandlerChain(command) looks up chain by ChainId
         │
         ▼
  Only that specific handler chain executes
```


## ISessionCommand Processing (RpcDefaultSessionReplacer)

```
┌───────────────────────────────────────────────────────────────────────┐
│            ISessionCommand Processing via RPC                         │
└───────────────────────────────────────────────────────────────────────┘


  CLIENT                                          SERVER
  ──────                                          ──────

  var cmd = new UpdateProfileCommand("John") {
      Session = Session.Default    // Empty session
  };
  await Commander.Call(cmd);
         │
         │  RPC Call
         │
         └──────────────────────────────────────────►┐
                                                     │
                                                     ▼
                                    ┌────────────────────────────────────┐
                                    │      RPC Inbound Pipeline          │
                                    └────────────────┬───────────────────┘
                                                     │
                                                     ▼
                                    ┌────────────────────────────────────┐
                                    │    RpcDefaultSessionReplacer       │
                                    │    (IRpcMiddleware)                │
                                    │    Priority: ArgumentValidation-1  │
                                    │                                    │
                                    │  ┌──────────────────────────────┐  │
                                    │  │ 1. Check if first param is   │  │
                                    │  │    ISessionCommand           │  │
                                    │  │                              │  │
                                    │  │ 2. Get SessionBoundRpc       │  │
                                    │  │    Connection from peer      │  │
                                    │  │                              │  │
                                    │  │ 3. If session.IsDefault():   │  │
                                    │  │    command.SetSession(       │  │
                                    │  │      connection.Session)     │  │
                                    │  │                              │  │
                                    │  │ 4. Else: session.RequireValid│  │
                                    │  └──────────────────────────────┘  │
                                    └────────────────┬───────────────────┘
                                                     │
                                                     │  Session replaced:
                                                     │  Session.Default → 
                                                     │  "actual-session-id"
                                                     ▼
                                    ┌────────────────────────────────────┐
                                    │      Command Handler               │
                                    │                                    │
                                    │  [CommandHandler]                  │
                                    │  Task<Unit> UpdateProfile(         │
                                    │      UpdateProfileCommand cmd,     │
                                    │      CancellationToken ct)         │
                                    │  {                                 │
                                    │    // cmd.Session is now valid!    │
                                    │    var user = await Auth.GetUser(  │
                                    │        cmd.Session, ct);           │
                                    │  }                                 │
                                    └────────────────────────────────────┘


  Session Resolution Flow:
  ───────────────────────────────────────────────────────────────────────

  ┌───────────────────┐     ┌─────────────────────┐     ┌─────────────────┐
  │  RpcInboundCall   │────►│ SessionBoundRpc     │────►│    Session      │
  │                   │     │ Connection          │     │  (from cookie/  │
  │  call.Context     │     │                     │     │   auth token)   │
  │  .Peer            │     │ connection.Session  │     │                 │
  │  .ConnectionState │     │                     │     │                 │
  └───────────────────┘     └─────────────────────┘     └─────────────────┘
```


## Handler Registration and Resolution

```
┌───────────────────────────────────────────────────────────────────────┐
│                Handler Registration and Resolution                    │
└───────────────────────────────────────────────────────────────────────┘


  Registration (at startup):
  ───────────────────────────────────────────────────────────────────────

  services.AddCommander()
      │
      ├──► .AddHandlers<OrderHandlers>()
      │         │
      │         ▼
      │    ┌─────────────────────────────────────────────────────────┐
      │    │  Scan OrderHandlers for:                                │
      │    │  • ICommandHandler<T> interfaces                        │
      │    │  • Methods with [CommandHandler] attribute              │
      │    │                                                         │
      │    │  Create CommandHandler descriptors:                     │
      │    │  • ServiceType, CommandType, Priority, IsFilter         │
      │    └─────────────────────────────────────────────────────────┘
      │
      └──► .AddService<OrderService>()
                │
                ▼
           ┌─────────────────────────────────────────────────────────┐
           │  1. Register OrderService with proxy                    │
           │  2. Scan for [CommandHandler] methods                   │
           │  3. Proxy ensures direct calls go through pipeline      │
           └─────────────────────────────────────────────────────────┘


  Resolution (at runtime):
  ───────────────────────────────────────────────────────────────────────

  Commander.Call(CreateOrderCommand)
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  CommandHandlerResolver.GetCommandHandlers(commandType)             │
  │                                                                     │
  │  1. Get all base types of command (including interfaces)            │
  │  2. Find all registered handlers matching those types               │
  │  3. Apply handler filters (CommandHandlerFilter)                    │
  │  4. Sort by: Priority DESC, then Type specificity DESC              │
  │  5. Verify: only ONE non-filter handler (unless IEventCommand)      │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  CommandHandlerChain                                                │
  │  ─────────────────────────────────────────────────────────────────  │
  │  [0] PreparedCommandHandler     (Priority: 1,000,000,000, Filter)   │
  │  [1] CommandTracer              (Priority: 998,000,000, Filter)     │
  │  [2] LocalCommandRunner         (Priority: 900,000,000, Filter)     │
  │  [3] RpcCommandHandler          (Priority: 800,000,000, Filter)     │
  │  [4] OperationReprocessor       (Priority: 100,000, Filter)         │
  │  [5] NestedOperationLogger      (Priority: 11,000, Filter)          │
  │  [6] InMemoryOperationScope     (Priority: 10,000, Filter)          │
  │  [7] DbOperationScopeProvider   (Priority: 1,000, Filter)           │
  │  [8] YourHandler                (Priority: 0, Final)                │
  └─────────────────────────────────────────────────────────────────────┘


  Handler Invocation:
  ───────────────────────────────────────────────────────────────────────

  context.ExecutionState = new CommandExecutionState(handlers)
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  context.InvokeRemainingHandlers(ct)                                │
  │                                                                     │
  │  var handler = ExecutionState.NextHandler;                          │
  │  ExecutionState = ExecutionState.NextState;                         │
  │  await handler.Invoke(command, context, ct);                        │
  │                                                                     │
  │  // Each filter handler calls InvokeRemainingHandlers()             │
  │  // to continue the chain                                           │
  └─────────────────────────────────────────────────────────────────────┘
```


## Command Service Proxy (AOP)

```
┌───────────────────────────────────────────────────────────────────────┐
│                    Command Service Proxy                              │
└───────────────────────────────────────────────────────────────────────┘


  Registration:
  ───────────────────────────────────────────────────────────────────────

  commander.AddService<OrderService>()
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Runtime generates: OrderServiceProxy : OrderService                │
  │                                                                     │
  │  Original:                      Proxy:                              │
  │  ─────────                      ──────                              │
  │  public class OrderService      public class OrderServiceProxy      │
  │  {                              {                                   │
  │    [CommandHandler]               public override Task<Order>       │
  │    public virtual Task<Order>       CreateOrder(cmd, ct)            │
  │      CreateOrder(cmd, ct)         {                                 │
  │    { ... }                          // Intercepts call              │
  │  }                                  // Routes through Commander     │
  │                                   }                                 │
  │                                 }                                   │
  └─────────────────────────────────────────────────────────────────────┘


  Call Flow:
  ───────────────────────────────────────────────────────────────────────

  // Direct call to service method
  var order = await orderService.CreateOrder(cmd, ct);
         │
         ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  OrderServiceProxy.CreateOrder()                                    │
  │  │                                                                  │
  │  │  CommandServiceInterceptor checks:                               │
  │  │  1. Is there a current CommandContext?                           │
  │  │  2. Does context.Command match invocation command?               │
  │  │                                                                  │
  │  │  If checks pass → invoke original method                         │
  │  │  If no context → throw DirectCommandHandlerCallsAreNotAllowed    │
  └────────────────────────────────┬────────────────────────────────────┘
                                   │
                                   │  First call (no context):
                                   │  Goes through Commander.Call()
                                   │
                                   ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │  Commander.Call(CreateOrderCommand)                                 │
  │  │                                                                  │
  │  │  Creates CommandContext                                          │
  │  │  Runs handler pipeline                                           │
  │  │  Pipeline invokes OrderServiceProxy.CreateOrder()                │
  │  │  │                                                               │
  │  │  │  Now CommandContext exists and matches                        │
  │  │  │  → Interceptor allows call to original method                 │
  │  │  │                                                               │
  │  │  └──► OrderService.CreateOrder() executes                        │
  └─────────────────────────────────────────────────────────────────────┘


  Result: Both paths invoke full pipeline
  ───────────────────────────────────────────────────────────────────────

  // These are equivalent:
  await commander.Call(new CreateOrderCommand(...), ct);
  await orderService.CreateOrder(new CreateOrderCommand(...), ct);

  // Both go through: PreparedCommandHandler → CommandTracer → ... → Handler
```
