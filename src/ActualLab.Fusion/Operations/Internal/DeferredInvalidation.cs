using ActualLab.CommandR.Configuration;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// The Operations Framework side of deferred invalidation: it builds the capture scope
/// for a command and freezes its recorded <see cref="InvalidationCall"/>s at commit time.
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
    public static async Task Harvest(Operation operation)
    {
        if (DeferInvalidationScope.Current is not { } scope)
            return;

        operation.InvalidationCalls = await scope.Harvest().ConfigureAwait(false);
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
}
