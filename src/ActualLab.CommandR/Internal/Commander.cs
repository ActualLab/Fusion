using System.Diagnostics;

namespace ActualLab.CommandR.Internal;

public class Commander : ICommander
{
    private static readonly PropertyInfo ChainIdSetterProperty =
        typeof(IEventCommand).GetProperty(nameof(IEventCommand.ChainId))!;

    private Action<IEventCommand, Symbol>? _chainIdSetter;
    private ILogger? _log;

    public IServiceProvider Services { get; }
    public CommanderHub Hub { get; }

    protected Action<IEventCommand, Symbol> ChainIdSetter => _chainIdSetter ??= ChainIdSetterProperty.GetSetter<Symbol>();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public Commander(IServiceProvider services)
    {
        Services = services;
        Hub = new CommanderHub(this, services);
    }

    public Task Run(CommandContext context, CancellationToken cancellationToken = default)
    {
        if (context.UntypedCommand is IEventCommand { ChainId.IsEmpty: true } eventCommand)
            return RunEvent(eventCommand, (CommandContext<Unit>)context, cancellationToken);

        // Task.Run is used to call RunInternal to make sure parent
        // task's ExecutionContext won't be "polluted" by temp.
        // change of CommandContext.Current (via AsyncLocal).
        var currentActivity = Activity.Current;
        using var _ = context.IsOutermost ? ExecutionContextExt.TrySuppressFlow() : default;
        return Task.Run(() => {
            if (currentActivity != null)
                Activity.Current = currentActivity; // We want to restore it even though we suppress the flow here
            return RunCommand(context, cancellationToken);
        }, CancellationToken.None);
    }

    protected virtual async Task RunCommand(
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
    }

    protected virtual async Task RunEvent(
        IEventCommand command, CommandContext<Unit> context, CancellationToken cancellationToken = default)
    {
        try {
            if (!command.ChainId.IsEmpty)
                throw new ArgumentOutOfRangeException(nameof(command));

            var handlers = Hub.HandlerResolver.GetCommandHandlers(command);
            var handlerChains = handlers.HandlerChains;
            if (handlerChains.Count == 0) {
                await OnUnhandledEvent(command, context, cancellationToken).ConfigureAwait(false);
                return;
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
