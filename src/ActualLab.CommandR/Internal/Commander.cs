using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ActualLab.CommandR.Internal;

public class Commander : ICommander
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume all command handling code is preserved")]
    [field: AllowNull, MaybeNull]
    protected static Action<IEventCommand, string> ChainIdSetter
        => field ??= typeof(IEventCommand).GetProperty(nameof(IEventCommand.ChainId))!.GetSetter<string>();

    public IServiceProvider Services { get; }
    public CommanderHub Hub { get; }

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public Commander(IServiceProvider services)
    {
        Services = services;
        Hub = new CommanderHub(this, services);
    }

    public Task<CommandContext> Run(CommandContext context, CancellationToken cancellationToken = default)
    {
#pragma warning disable MA0100 // Do not use generic Task.Run
        if (context.UntypedCommand is IEventCommand eventCommand && eventCommand.ChainId.IsNullOrEmpty())
            return RunEvent(eventCommand, (CommandContext<Unit>)context, cancellationToken);

        // Task.Run is used to call RunInternal to make sure parent
        // task's ExecutionContext won't be "polluted" by temp.
        // change of CommandContext.Current (via AsyncLocal).
        var currentActivity = Activity.Current;
        using var _ = context.IsOutermost ? ExecutionContextExt.TrySuppressFlow() : default;
        return Task.Run(() => {
            if (currentActivity is not null)
                Activity.Current = currentActivity; // We want to restore it even though we suppress the flow here
            return RunCommand(context, cancellationToken);
        }, CancellationToken.None);
#pragma warning restore MA0100
    }

    // Protected methods

    protected virtual async Task<CommandContext> RunCommand(
        CommandContext context, CancellationToken cancellationToken = default)
    {
        try {
            var command = context.UntypedCommand;
            var handlers = Hub.HandlerResolver.GetCommandHandlerChain(command);
            context.ExecutionState = new CommandExecutionState(handlers);
            if (handlers.Length == 0)
                await OnUnhandledCommand(command, context, cancellationToken).ConfigureAwait(false);

            using var _ = context.Activate();
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            context.SetResult(e);
        }
        finally {
            context.TryComplete(cancellationToken);
            await context.DisposeAsync().ConfigureAwait(false);
        }

        return context;
    }

    protected virtual async Task<CommandContext> RunEvent(
        IEventCommand command, CommandContext<Unit> context, CancellationToken cancellationToken = default)
    {
        try {
            if (!command.ChainId.IsNullOrEmpty())
                throw new ArgumentOutOfRangeException(nameof(command));

            var handlers = Hub.HandlerResolver.GetCommandHandlers(command);
            var handlerChains = handlers.HandlerChains;
            if (handlerChains.Count == 0) {
                await OnUnhandledEvent(command, context, cancellationToken).ConfigureAwait(false);
                return context;
            }
            var callTasks = new Task[handlerChains.Count];
            var i = 0;
            foreach (var (chainId, _) in handlerChains) {
                var chainCommand = MemberwiseCloner.Invoke(command);
                ChainIdSetter.Invoke(chainCommand, chainId);
                callTasks[i++] = this.Call(chainCommand, context.IsOutermost, cancellationToken);
            }
            await Task.WhenAll(callTasks).ConfigureAwait(false);
        }
        catch (Exception e) {
            context.SetResult(e);
        }
        finally {
            context.TryComplete(cancellationToken);
            await context.DisposeAsync().ConfigureAwait(false);
        }

        return context;
    }

    protected virtual Task OnUnhandledCommand(
        ICommand command, CommandContext context,
        CancellationToken cancellationToken)
        => throw Errors.NoHandlerFound(command.GetType());

    protected virtual Task OnUnhandledEvent(
        IEventCommand command, CommandContext<Unit> context,
        CancellationToken cancellationToken)
    {
        Log.LogWarning("Unhandled event: {Event}", command);
        return Task.CompletedTask;
    }
}
