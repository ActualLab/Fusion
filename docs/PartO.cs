using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using ActualLab.CommandR.Internal;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Redis;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartO;

// Sample DbContext for Part 6
public class AppDbContext(DbContextOptions options) : DbContextBase(options)
{
    // ActualLab.Fusion.EntityFramework.Operations tables
    #region PartO_DbSet
    public DbSet<DbOperation> Operations { get; protected set; } = null!;
    public DbSet<DbEvent> Events { get; protected set; } = null!;
    #endregion
}

#region PartO_PostMessageCommand
public record PostMessageCommand(Session Session, string Text) : ICommand<ChatMessage>;
#endregion

// Placeholder for ChatMessage type (actual type would be in your domain)
public record ChatMessage(long Id, string Text);

// Example: Pre-OF handler (old pattern, before Operations Framework)
public class PreOfChatService(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartO_PreOfHandler
    public async Task<ChatMessage> PostMessage(
        Session session, string text, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateDbContext(cancellationToken).ConfigureAwait(false);
        // Actual code...
        var message = await PostMessageImpl(dbContext, session, text, cancellationToken);

        // Invalidation
        using (Invalidation.Begin())
            _ = PseudoGetAnyChatTail();
        return message;
    }
    #endregion

    [ComputeMethod]
    public virtual Task<ChatMessage[]> PseudoGetAnyChatTail() => Task.FromResult(Array.Empty<ChatMessage>());

    // Fake implementation placeholder
    private Task<ChatMessage> PostMessageImpl(AppDbContext db, Session session, string text, CancellationToken ct)
        => Task.FromResult(new ChatMessage(0, text));
}

// Sample service demonstrating command handler pattern
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class ChatService(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartO_PostOfHandler
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task<ChatMessage> PostMessage(
        PostMessageCommand command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        // Actual code...
        var message = await PostMessageImpl(dbContext, command, cancellationToken);

        Invalidation.Defer(() => _ = PseudoGetAnyChatTail());
        return message;
    }
    #endregion

    // Placeholder compute method for invalidation
    [ComputeMethod]
    public virtual Task<ChatMessage[]> PseudoGetAnyChatTail() => Task.FromResult(Array.Empty<ChatMessage>());

    // Fake implementation placeholder
    private Task<ChatMessage> PostMessageImpl(AppDbContext db, PostMessageCommand command, CancellationToken ct)
        => Task.FromResult(new ChatMessage(0, command.Text));
}

// Example: testing that a mutating command invalidates both an entity-specific
// and an aggregate query -- see "Testing Invalidation" in PartO.md
#region PartO_TestInvalidation_Command
public record KeyValueService_Set(string Key, string Value) : ICommand<Unit>;
#endregion

#region PartO_TestInvalidation_Service
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class KeyValueService : IComputeService
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    // Entity-specific query
    [ComputeMethod]
    public virtual Task<string?> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    // Aggregate query
    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [CommandHandler]
    public virtual Task<Unit> Set(KeyValueService_Set command, CancellationToken cancellationToken = default)
    {
        // Requests an operation scope, so this in-memory command commits like a stored
        // one does -- see TransientOperationScopeProvider
        TransientOperationScope.Require();
        _values[command.Key] = command.Value;

        // Every mutating command handler must invalidate BOTH the entity-specific
        // query it directly affects AND every aggregate query whose result may change --
        // dependency tracking alone won't discover an omitted root call.
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
        });
        return Task.FromResult(Unit.Default);
    }
}
#endregion

public class PartO : DocPart
{
    public override async Task Run()
    {
        {
            StartSnippetOutput("Testing Invalidation");
            #region PartO_TestInvalidation_Demo
            var services = new ServiceCollection();
            services.AddFusion().AddComputeService<KeyValueService>();
            var sp = services.BuildServiceProvider();
            var commander = sp.Commander();
            var kv = sp.GetRequiredService<KeyValueService>();

            // Capture both queries BEFORE the mutating command runs
            var cGet = await Computed.Capture(() => kv.Get("a"));
            var cCount = await Computed.Capture(() => kv.Count());
            WriteLine($"Before Set: Get.IsConsistent={cGet.IsConsistent()}, Count.IsConsistent={cCount.IsConsistent()}");

            await commander.Call(new KeyValueService_Set("a", "1"));

            // A test would assert both are False here -- if Set() only invalidated
            // Get(), this would (correctly) fail and catch the missing Count() invalidation
            WriteLine($"After Set:  Get.IsConsistent={cGet.IsConsistent()}, Count.IsConsistent={cCount.IsConsistent()}");
            #endregion
        }

        StartSnippetOutput("Reference verification");

        // 1. DbOperation - entity for operation log
        _ = typeof(DbOperation); // "DbOperation" from docs

        // 2. DbServiceBase - base class for services using EF
        _ = typeof(DbServiceBase<>); // "DbServiceBase" from docs

        // 3. Invalidation - for checking/starting invalidation mode
        _ = typeof(Invalidation); // "Invalidation" from docs
        // Still public, but an Operations Framework handler no longer branches on it:
        // Invalidation.Defer(...) is what declares an invalidation now
        _ = Invalidation.IsActive;

        // 4. CommandContext - context for current command
        _ = typeof(CommandContext); // "CommandContext" from docs
        // CommandContext.GetCurrent() - for getting current context
        // 5. ICommand<TResult> - command interface
        _ = typeof(ICommand<Unit>); // "ICommand<TResult>" from docs

        // 6. Command handler priorities
        // CommanderCommandHandlerPriority (ActualLab.CommandR)
        _ = CommanderCommandHandlerPriority.PreparedCommandHandler; // Priority: 1_000_000_000
        _ = CommanderCommandHandlerPriority.CommandTracer; // Priority: 998_000_000
        _ = CommanderCommandHandlerPriority.LocalCommandRunner; // Priority: 900_000_000
        _ = CommanderCommandHandlerPriority.RpcRoutingCommandHandler; // Priority: 800_000_000

        // FusionOperationsCommandHandlerPriority (ActualLab.Fusion)
        _ = FusionOperationsCommandHandlerPriority.InvalidationGuard; // Priority: 999_999_000
        _ = FusionOperationsCommandHandlerPriority.OperationReprocessor; // Priority: 100_000
        _ = FusionOperationsCommandHandlerPriority.TransientOperationScopeProvider; // Priority: 10_000
        _ = FusionOperationsCommandHandlerPriority.DeferredInvalidationScopeProvider; // Priority: 9_000
        _ = FusionOperationsCommandHandlerPriority.CompletionTerminator; // Priority: -1_000_000_000

        // FusionEntityFrameworkCommandHandlerPriority (ActualLab.Fusion.EntityFramework)
        _ = FusionEntityFrameworkCommandHandlerPriority.DbOperationScopeProvider; // Priority: 9900

        // 7. PreparedCommandHandler - validates IPreparedCommand
        _ = typeof(PreparedCommandHandler); // "PreparedCommandHandler" from docs
        _ = typeof(IPreparedCommand); // "IPreparedCommand" from docs

        // 8. TransientOperationScopeProvider - catch-all operation scope
        _ = typeof(TransientOperationScopeProvider); // "TransientOperationScopeProvider" from docs

        // 9. DeferredInvalidationScopeProvider - opens the deferred invalidation scope
        _ = typeof(DeferredInvalidationScopeProvider);

        // 10. OperationCompletionNotifier - notifies completion listeners
        _ = typeof(OperationCompletionNotifier); // "OperationCompletionNotifier" from docs

        // 11. IOperationCompletionListener - listens for operation completion
        _ = typeof(IOperationCompletionListener); // "IOperationCompletionListener" from docs

        // 12. CompletionProducer - produces completion commands
        _ = typeof(CompletionProducer); // "CompletionProducer" from docs

        // 13. Completion<T> / ICompletion<T> - completion command wrapper
        _ = typeof(Completion<>); // "Completion<TCommand>" from docs
        _ = typeof(ICompletion<>); // "ICompletion<TCommand>" from docs

        // 14. OperationCompletionHandler - applies recorded invalidation calls
        _ = typeof(OperationCompletionHandler);

        // 15. DbOperationLogReader - reads operation log
        _ = typeof(DbOperationLogReader<>); // "DbOperationLogReader" from docs

        // 16. DbHub - modern way to get operation DbContext
        _ = typeof(DbHub<>); // "DbHub" - modern API, docs mention "CreateOperationDbContext"

        // 17. Operation log watchers - for multi-host invalidation
        _ = typeof(DbOperationsBuilder<>); // Builder for configuring operation log watchers
        _ = typeof(FileSystemDbLogWatcher<,>); // AddFileSystemOperationLogWatcher
        _ = typeof(NpgsqlDbLogWatcher<,>); // AddNpgsqlOperationLogWatcher
        _ = typeof(RedisDbLogWatcher<,>); // AddRedisOperationLogWatcher

        // 18. HostId - identifies the host/process (was "AgentInfo" in docs)
        _ = typeof(HostId); // "AgentInfo" from docs - RENAMED to HostId

        WriteLine("All identifier references verified successfully!");
        WriteLine();

        StartSnippetOutput("Name Changes from Documentation");
        WriteLine("- InMemoryOperationScopeProvider -> TransientOperationScopeProvider");
        WriteLine("- InMemoryOperationScope -> TransientOperationScope");
        WriteLine("- Operation.Items / nested operations -> Operation.InvalidationCalls");
        WriteLine("- DbServiceBase.CreateOperationDbContext() -> DbHub.CreateOperationDbContext()");
        WriteLine("- AgentInfo -> HostId (moved to ActualLab.Core)");

        StartSnippetOutput("Command Handler Priorities");
        WriteLine($"- PreparedCommandHandler: {CommanderCommandHandlerPriority.PreparedCommandHandler:N0}");
        WriteLine($"- InvalidationGuard: {FusionOperationsCommandHandlerPriority.InvalidationGuard:N0}");
        WriteLine($"- TransientOperationScopeProvider: {FusionOperationsCommandHandlerPriority.TransientOperationScopeProvider:N0}");
        WriteLine($"- DeferredInvalidationScopeProvider: {FusionOperationsCommandHandlerPriority.DeferredInvalidationScopeProvider:N0}");
        WriteLine($"- CompletionTerminator: {FusionOperationsCommandHandlerPriority.CompletionTerminator:N0}");

        await Task.CompletedTask;
    }

    // Example: AddDbContextServices configuration
    #region PartO_AddDbContextServices
    public static void ConfigureServices(IServiceCollection services, IHostEnvironment Env)
    {
        services.AddDbContextServices<AppDbContext>(db => {
            // Uncomment if you'll be using AddRedisOperationLogWatcher
            // db.AddRedisDb("127.0.0.1", "FusionDocumentation.PartO");

            db.AddOperations(operations => {
                // This call enabled Operations Framework (OF) for AppDbContext.
                operations.ConfigureOperationLogReader(_ => new() {
                    // We use AddFileSystemOperationLogWatcher, so unconditional wake up period
                    // can be arbitrary long – all depends on the reliability of Notifier-Monitor chain.
                    // See what .ToRandom does – most of timeouts in Fusion settings are RandomTimeSpan-s,
                    // but you can provide a normal one too – there is an implicit conversion from it.
                    CheckPeriod = TimeSpan.FromSeconds(Env.IsDevelopment() ? 60 : 5).ToRandom(0.05),
                });
                // Optionally enable file-based operation log watcher
                operations.AddFileSystemOperationLogWatcher();

                // Or, if you use PostgreSQL, use this instead of above line
                // operations.AddNpgsqlOperationLogWatcher();

                // Or, if you use Redis, use this instead of above line
                // operations.AddRedisOperationLogWatcher();
            });
        });
    }
    #endregion
}

// Example: a handler whose invalidation depends on what the mutation discovered
#region PartO_SignOutCommand
public record SignOutCommand(Session Session, bool Force = false) : ICommand<Unit>;
#endregion

// Example: Service demonstrating conditional invalidation
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class AuthServiceExample(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartO_SignOutHandler
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task SignOut(
        SignOutCommand command, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);

        var dbSessionInfo = await Sessions.FindOrCreate(dbContext, command.Session, cancellationToken).ConfigureAwait(false);
        var sessionInfo = dbSessionInfo.ToModel();
        if (sessionInfo.IsSignOutForced)
            return;

        // ...

        // What to invalidate depends on what the mutation found, so it's an ordinary closure
        Invalidation.Defer(() => {
            _ = GetUser(sessionInfo.UserId, default);
            _ = GetUserSessions(sessionInfo.UserId, default);
        });
    }
    #endregion

    // Placeholder types and methods for the example
    protected DbSessionInfoRepo Sessions { get; } = null!;
    [ComputeMethod] public virtual Task<User?> GetUser(string userId, CancellationToken ct) => Task.FromResult<User?>(null);
    [ComputeMethod] public virtual Task<SessionInfo[]> GetUserSessions(string userId, CancellationToken ct) => Task.FromResult(Array.Empty<SessionInfo>());
}

// Placeholder types for SignOut example
public record SessionInfo(string UserId, bool IsSignOutForced);
public record User(string Id, string Name);
public class DbSessionInfoRepo
{
    public Task<DbSessionInfo> FindOrCreate(AppDbContext db, Session session, CancellationToken ct) => Task.FromResult(new DbSessionInfo());
}
public record DbSessionInfo
{
    public SessionInfo ToModel() => new("", false);
}

// Example: Completion command invocation
public static class CompletionExample
{
    #region PartO_CompletionCall
    public static async Task InvokeCompletion(ICommander Commander, Operation operation)
    {
        await Commander.Call(Completion.New(operation), true).ConfigureAwait(false);
    }
    #endregion
}

