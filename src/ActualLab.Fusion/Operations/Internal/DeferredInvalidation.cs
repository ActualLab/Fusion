using ActualLab.CommandR.Configuration;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// The Operations Framework side of deferred invalidation: it builds the capture scope
/// for a command, freezes its recorded <see cref="InvalidationCall"/>s at commit time,
/// and resolves whether the operation has to be stored at all.
/// </summary>
public static class DeferredInvalidation
{
    // The returned delegate is meant to be created once per host and reused: it resolves the mode
    // of whatever command handler is running when Defer(...) is called.
    public static Func<InvalidationMode> NewModeResolver(IServiceProvider services)
    {
        var modeResolver = services.GetRequiredService<InvalidationModeResolver>();
        var handlerResolver = services.GetRequiredService<CommandHandlerResolver>();
        return () => ResolveCurrentMode(handlerResolver, modeResolver);
    }

    // An operation's invalidation calls are frozen at commit time: DbOperationScope.Commit adds
    // the DbOperation row inside its transaction, so anything harvested later never reaches it.
    public static async Task OnCommit(IOperationScope scope)
    {
        if (DeferInvalidationScope.Current is { } deferScope
            && deferScope.HasEntries(InvalidationMode.Replicated)) {
            // Replicated invalidation is only as reliable as its carrier. The operation log row is
            // written inside the same transaction as the mutation and is read with gap detection;
            // a scope that stores nothing has no way to replicate the invalidation at all.
            if (scope.IsTransient || scope.MustStoreOperation == false)
                throw Fusion.Internal.Errors.ReplicatedInvalidationRequiresStoredOperation(scope.GetType());

            scope.Operation.InvalidationCalls = await deferScope.Harvest().ConfigureAwait(false);
        }
        scope.MustStoreOperation ??= MustStoreOperation(scope);
    }

    // Private methods

    private static InvalidationMode ResolveCurrentMode(
        CommandHandlerResolver handlerResolver,
        InvalidationModeResolver modeResolver)
    {
        // The innermost command context wins: a nested command may declare a different mode than
        // the outermost one, and its Defer(...) blocks belong to the mode it declares.
        if (CommandContext.Current?.UntypedCommand is not { } command)
            return InvalidationMode.Local;

        return handlerResolver.GetCommandHandlerChain(command).FinalHandler is IMethodCommandHandler finalHandler
            ? modeResolver.Resolve(finalHandler)
            : InvalidationMode.Local;
    }

    private static bool MustStoreOperation(IOperationScope scope)
    {
        var operation = scope.Operation;
        if (operation.Events.Count != 0 || !operation.InvalidationCalls.IsEmpty)
            return true;

        var services = scope.CommandContext.Services;
        var handlerResolver = services.GetRequiredService<CommandHandlerResolver>();
        var modeResolver = services.GetRequiredService<InvalidationModeResolver>();
        if (IsReplayed(handlerResolver, modeResolver, operation.Command))
            return true;

        foreach (var (command, _) in operation.NestedOperations)
            if (IsReplayed(handlerResolver, modeResolver, command))
                return true;

        return false;
    }

    private static bool IsReplayed(
        CommandHandlerResolver handlerResolver,
        InvalidationModeResolver modeResolver,
        ICommand? command)
    {
        // Anything this can't resolve keeps the row: a Legacy handler that isn't replayed leaves
        // stale caches behind, while an operation row nobody reads only costs storage.
        if (command is null)
            return true;

        try {
            return handlerResolver.GetCommandHandlerChain(command).FinalHandler is not IMethodCommandHandler handler
                || modeResolver.Resolve(handler) is InvalidationMode.Legacy;
        }
        catch (Exception) {
            return true;
        }
    }
}
