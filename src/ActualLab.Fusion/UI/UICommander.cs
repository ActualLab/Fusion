using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Fusion.UI;

public class UICommander(IServiceProvider services) : IHasServices
{
    private static readonly ConcurrentDictionary<Type, Func<UICommander, ICommand, CancellationToken, UIAction>>
        CreateUIActionInvokers = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly MethodInfo CreateUIActionTypedMethod = typeof(UICommander)
        .GetMethod(nameof(CreateUIActionTyped), BindingFlags.Static | BindingFlags.NonPublic)!;

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public ICommander Commander => field ??= Services.Commander();
    [field: AllowNull, MaybeNull]
    public UIActionTracker UIActionTracker => field ??= Services.GetRequiredService<UIActionTracker>();
    public MomentClock Clock => UIActionTracker.Clock;

    public async Task<TResult> Call<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var result = await Run(command, cancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    public async Task<object?> Call(ICommand command, CancellationToken cancellationToken = default)
    {
        var result = await Run(command, cancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    public Task<UIActionResult<TResult>> Run<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var action = Start(command, cancellationToken);
        return action.ResultTask;
    }

    public async Task<IUIActionResult> Run(ICommand command, CancellationToken cancellationToken = default)
    {
        var action = Start(command, cancellationToken);
        await action.WhenCompleted().ConfigureAwait(false);
        return action.UntypedResult!;
    }

    public UIAction<TResult> Start<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var action = CreateUIAction<TResult>(command, cancellationToken);
        UIActionTracker.Register(action);
        return action;
    }

    public UIAction Start(ICommand command, CancellationToken cancellationToken = default)
    {
        var action = CreateUIAction(this, command, cancellationToken);
        UIActionTracker.Register(action);
        return action;
    }

    // Protected methods

    protected virtual UIAction<TResult> CreateUIAction<TResult>(
        ICommand command,
        CancellationToken cancellationToken)
    {
        var typedCommand = (ICommand<TResult>)command;
        var resultTask = Commander.Call(typedCommand, isOutermost: true, cancellationToken);
        var action = new UIAction<TResult>(typedCommand, Clock, resultTask, cancellationToken);
        return action;
    }

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume CreateUIActionTyped method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume CreateUIActionTyped method is preserved")]
    private static UIAction CreateUIAction(UICommander uiCommander, ICommand command, CancellationToken cancellationToken)
        => CreateUIActionInvokers.GetOrAdd(
            command.GetResultType(),
            static tCommand => (Func<UICommander, ICommand, CancellationToken, UIAction>)CreateUIActionTypedMethod
                .MakeGenericMethod(tCommand)
                .CreateDelegate(typeof(Func<UICommander, ICommand, CancellationToken, UIAction>))
        ).Invoke(uiCommander, command, cancellationToken);

    private static UIAction CreateUIActionTyped<TResult>(
        UICommander uiCommander,
        ICommand command,
        CancellationToken cancellationToken)
        => uiCommander.CreateUIAction<TResult>(command, cancellationToken);
}
