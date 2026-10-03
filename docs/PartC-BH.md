# Built-in Command Handlers

CommandR and Fusion include several built-in command handlers that form the execution pipeline. Understanding these handlers helps you design your own handlers and debug issues.

## Handler Execution Order

Handlers execute in **descending priority order** (highest priority runs first). Filter handlers wrap lower-priority handlers, creating a middleware-like pipeline.

## CommandR Handlers

These handlers are registered automatically when you call `AddCommander()`.

**Assembly:** `ActualLab.CommandR`

| Handler | Priority | Type | Command Type |
|---------|----------|------|--------------|
| `PreparedCommandHandler` | 1,000,000,000 | Filter | `IPreparedCommand` |
| `CommandTracer` | 998,000,000 | Filter | `ICommand` |
| `LocalCommandRunner` | 900,000,000 | Final | `ILocalCommand` |
| `RpcCommandHandler` | 800,000,000 | Filter | `ICommand` |

### PreparedCommandHandler

**Priority:** 1,000,000,000 (runs first)

Handles commands implementing `IPreparedCommand` by calling their `Prepare` method before invoking remaining handlers.

<!-- snippet: PartCBH_PreparedCommandHandlerReg -->
```cs
// Registration (automatic in AddCommander)
services.AddSingleton(_ => new PreparedCommandHandler());
commander.AddHandlers<PreparedCommandHandler>();
```
<!-- endSnippet -->

### CommandTracer

**Priority:** 998,000,000

Provides diagnostic tracing and activity tracking for command execution. Creates OpenTelemetry activities for observability and logs errors.

<!-- snippet: PartCBH_CommandTracerReg -->
```cs
// Registration (automatic in AddCommander)
services.AddSingleton(_ => CommandTracer.Options.Default);
services.AddSingleton(c => new CommandTracer(c.GetRequiredService<CommandTracer.Options>(), c));
commander.AddHandlers<CommandTracer>();
```
<!-- endSnippet -->

### LocalCommandRunner

**Priority:** 900,000,000

Executes commands implementing `ILocalCommand` by calling their `Run` method. Unlike filter handlers, `LocalCommandRunner` doesn't call `InvokeRemainingHandlers` — it fully handles `ILocalCommand` by executing its `Run()` method and returns.

<!-- snippet: PartCBH_LocalCommandRunnerReg -->
```cs
// Registration (automatic in AddCommander)
services.AddSingleton(_ => new LocalCommandRunner());
commander.AddHandlers<LocalCommandRunner>();
```
<!-- endSnippet -->

### RpcCommandHandler

**Priority:** 800,000,000

Routes commands to RPC services when appropriate. Handles distributed command execution and automatic rerouting to the correct server.

<!-- snippet: PartCBH_RpcCommandHandlerReg -->
```cs
// Registration (automatic in AddCommander)
services.AddSingleton(c => new RpcCommandHandler(c));
commander.AddHandlers<RpcCommandHandler>();
```
<!-- endSnippet -->

## Fusion Operations Framework Handlers

These handlers are registered when you call `AddFusion()`. They implement multi-host invalidation and operation logging.

**Assembly:** `ActualLab.Fusion`

| Handler | Priority | Type | Command Type |
|---------|----------|------|--------------|
| `InvalidationGuard` | 999,999,000 | Filter | `ICommand` |
| `OperationReprocessor` | 100,000 | Filter | `ICommand` |
| `TransientOperationScopeProvider` | 10,000 | Filter | `ICommand` |
| `DeferredInvalidationScopeProvider` | 9,000 | Filter | `ICommand` |
| `FusionOperationCompletionHandler` | 0 | Handler | `OperationCompletion` |
| `CompletionTerminator` | -1,000,000,000 | Final | `ICompletion` |

### InvalidationGuard

**Priority:** 999,999,000

Throws if a command is started while an invalidation pass is active.

Deferred invalidation re-invokes the compute methods a handler declared &ndash; it never replays the
command handler &ndash; so a command running inside an invalidation pass is a bug. Left alone it
would be a quiet one: every operation scope provider down the chain opts out while
`Invalidation.IsActive`, so such a command would mutate without an operation, without events and
without invalidations.

Only a *nested* command can reach the guard: `Commander` suppresses the ExecutionContext flow of an
outermost command and runs its pipeline on a fresh one, so an outermost command never sees its
caller's `ComputeContext` &ndash; and therefore runs with its Operations Framework intact.

<!-- snippet: PartCBH_InvalidationGuardReg -->
```cs
// Registration (automatic in AddFusion)
services.AddSingleton(_ => new InvalidationGuard());
commander.AddHandlers<InvalidationGuard>();
```
<!-- endSnippet -->

### OperationReprocessor

**Priority:** 100,000

Retries failed commands with transient errors using configurable retry policies.

<!-- snippet: PartCBH_OperationReprocessorReg -->
```cs
// Registration (optional, via AddOperationReprocessor)
fusion.AddOperationReprocessor();
```
<!-- endSnippet -->

See [Part 5: Operations Framework](PartO.md) for details.

### TransientOperationScopeProvider

**Priority:** 10,000

Provides in-memory operation scopes for commands that don't use database-backed operation scopes. Also triggers operation completion notifications.

<!-- snippet: PartCBH_TransientOperationScopeProviderReg -->
```cs
// Registration (automatic in AddFusion)
services.AddSingleton(c => new TransientOperationScopeProvider(c));
commander.AddHandlers<TransientOperationScopeProvider>();
```
<!-- endSnippet -->

See [Part 5: Operations Framework](PartO.md) for details.

### DeferredInvalidationScopeProvider

**Priority:** 9,000

Activates the `DeferredInvalidationContext` that `TransientOperationScopeProvider` created for the
command, and closes it when the handler returns. `Invalidation.Defer(...)` works from a handler's
first statement because of this scope, and the mode of its first block becomes the operation's.

It sits below every operation scope provider, including `DbOperationScopeProvider` (9,000 &lt; 9,900),
so a scope is already open by the time anything can be deferred &ndash; `Replicated` and
`Distributed` need one to carry their calls to other hosts. Closing the context before those
providers commit is also what lets the commit read a block list that's final.

See [Part 5: Operations Framework](PartO.md) for details.

### FusionOperationCompletionHandler

**Priority:** 0 (handler for `OperationCompletion`)

Applies the invalidation calls a handler recorded &ndash; locally right after the commit, and on
every other host once the operation log delivers the operation. It also handles
`OperationCompletion`, which is how a `Distributed` invalidation is recovered from its
event row.

<!-- snippet: PartCBH_FusionOperationCompletionHandlerReg -->
```cs
// Registration (automatic in AddFusion) - replaces CommandR's OperationCompletionHandler
commander.AddOperationCompletionHandler(c => new FusionOperationCompletionHandler(c));
```
<!-- endSnippet -->

See [Invalidation Modes](PartO-IM.md) for details.

### CompletionTerminator

**Priority:** -1,000,000,000 (runs last)

Terminal handler for completion commands. Ensures the completion pipeline has a final handler.

<!-- snippet: PartCBH_CompletionTerminatorReg -->
```cs
// Registration (automatic in AddFusion)
services.AddSingleton(_ => new CompletionTerminator());
commander.AddHandlers<CompletionTerminator>();
```
<!-- endSnippet -->

## Entity Framework Handlers

These handlers are registered when you call `AddOperations()` on a `DbContextBuilder`.

**Assembly:** `ActualLab.Fusion.EntityFramework`

| Handler | Priority | Type | Command Type |
|---------|----------|------|--------------|
| `DbOperationScopeProvider` | 9,900 | Filter | `ICommand` |

### DbOperationScopeProvider

**Priority:** 9,900

Provides database operation scopes for database-backed operations. Manages transactions and ensures operations are logged to the database.

<!-- snippet: PartCBH_DbOperationScopeProviderReg -->
```cs
// Registration (via AddOperations on DbContextBuilder)
services.AddDbContextServices<AppDbContext>(db => {
    db.AddOperations(operations => {
        // DbOperationScopeProvider is registered here
    });
});
```
<!-- endSnippet -->

See [Part 5: Operations Framework](PartO.md) for details.

## Complete Pipeline Visualization

When all handlers are registered, the pipeline looks like this (in execution order):

```
Command Received
    │
    ▼
┌──────────────────────────────────────────┐
│ PreparedCommandHandler (1,000,000,000)   │ ← Calls IPreparedCommand.Prepare()
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ InvalidationGuard (999,999,000)          │ ← Rejects commands during invalidation
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ CommandTracer (998,000,000)              │ ← Creates activity, logs errors
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ LocalCommandRunner (900,000,000)         │ ← Runs ILocalCommand.Run() if applicable
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ RpcCommandHandler (800,000,000)          │ ← Routes to RPC if needed
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ OperationReprocessor (100,000)           │ ← Retries on transient errors
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ TransientOperationScopeProvider (10,000) │ ← Provides operation scope
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ DbOperationScopeProvider (9,900)         │ ← Provides DB operation scope
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ DeferredInvalidationScopeProvider (9,000)│ ← Opens the invalidation scope
└──────────────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────────────┐
│ Your Handlers (default priority: 0)      │ ← Your command handlers
└──────────────────────────────────────────┘
```

For completion commands (`ICompletion<T>`):

```
Completion Command
    │
    ▼
┌──────────────────────────────────────────┐
│ CompletionTerminator (-1,000,000,000)    │ ← Terminal handler
└──────────────────────────────────────────┘
```

`CompletionTerminator` is the terminal handler of every completion, so a handler of your own for
`ICompletion<T>` has to be a **filter** &ndash; a second non-filter handler for the same completion
is an error.

## Adding Custom Handlers

### Filter Handler

A filter handler wraps subsequent handlers (like middleware):

<!-- snippet: PartCBH_FilterHandlerExample -->
```cs
public class LoggingHandler : ICommandHandler<ICommand>
{
    [CommandHandler(Priority = 500_000, IsFilter = true)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken ct)
    {
        Console.WriteLine($"Before: {command.GetType().Name}");
        try {
            await context.InvokeRemainingHandlers(ct);
        }
        finally {
            Console.WriteLine($"After: {command.GetType().Name}");
        }
    }
}

// Registration:
// services.AddSingleton<LoggingHandler>();
// commander.AddHandlers<LoggingHandler>();
```
<!-- endSnippet -->

### Final Handler

A final handler doesn't call `InvokeRemainingHandlers`:

<!-- snippet: PartCBH_FinalHandlerExample -->
```cs
public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public async Task OnCommand(MyCommand command, CommandContext context, CancellationToken ct)
    {
        // Handle the command - don't call InvokeRemainingHandlers
        await DoWork(command, ct);
    }

    private Task DoWork(MyCommand command, CancellationToken ct) => Task.CompletedTask;
}
```
<!-- endSnippet -->

## Priority Guidelines

When choosing priorities for custom handlers:

| Range | Purpose |
|-------|---------|
| > 100,000 | Infrastructure handlers (validation, tracing, RPC routing) |
| 10,000 - 100,000 | Cross-cutting concerns (logging, caching, retry logic) |
| 1,000 - 10,000 | Database/transaction management |
| 0 - 1,000 | Business logic handlers (default: 0) |
| < 0 | Post-processing, cleanup handlers |

Your custom handlers typically use the default priority (0) unless they need to run before/after specific built-in handlers.
