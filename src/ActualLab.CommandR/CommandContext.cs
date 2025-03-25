using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using ActualLab.CommandR.Internal;
using ActualLab.CommandR.Operations;
using ActualLab.OS;

namespace ActualLab.CommandR;

public abstract class CommandContext(ICommander commander) : IHasServices, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<Type, Type> CommandContextTypeCache = new(HardwareInfo.ProcessorCountPo2, 131);

    protected static readonly AsyncLocal<CommandContext?> CurrentLocal = new();

    private readonly MutablePropertyBag _items = null!;
    private Operation? _operation;

#pragma warning disable CA1721
    public static CommandContext? Current => CurrentLocal.Value;
#pragma warning restore CA1721

    protected internal IServiceScope ServiceScope { get; protected init; } = null!;
    public ICommander Commander { get; } = commander;
#pragma warning disable CA2119
    public abstract ICommand UntypedCommand { get; }
    public abstract Task UntypedResultTask { get; }
    public abstract Result UntypedResult { get; }
    public abstract bool IsCompleted { get; }
#pragma warning restore CA2119

    public CommandContext? OuterContext { get; protected init; }
    public CommandContext OutermostContext { get; protected init; } = null!;
    public bool IsOutermost => ReferenceEquals(OutermostContext, this);
    public CommandExecutionState ExecutionState { get; set; }
    public IServiceProvider Services => ServiceScope.ServiceProvider;

    public MutablePropertyBag Items {
        get => OutermostContext._items;
        protected init => _items = value;
    }

    public Operation Operation
        => OutermostContext._operation ?? throw Errors.CommandContextHasNoOperation();

    // Static methods

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume all command handling code is preserved")]
    public static CommandContext New(
        ICommander commander, ICommand command, bool isOutermost)
    {
        if (!isOutermost && (command is IOutermostCommand || Current?.UntypedCommand is IApiCommand))
            isOutermost = true;

        var tCommandResult = command.GetResultType();
        var tContext = CommandContextTypeCache.GetOrAdd(
            tCommandResult,
            static t => typeof(CommandContext<>).MakeGenericType(t));
        return (CommandContext)tContext.CreateInstance(commander, command, isOutermost);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandContext GetCurrent()
        => Current ?? throw Errors.NoCurrentCommandContext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandContext<TResult> GetCurrent<TResult>()
        => GetCurrent().Cast<TResult>();

    public static ClosedDisposable<CommandContext> Suppress()
    {
        var oldCurrent = Current;
        CurrentLocal.Value = null;
        return Disposable.NewClosed(oldCurrent!, oldCurrent1 => CurrentLocal.Value = oldCurrent1);
    }

    public ClosedDisposable<CommandContext> Activate()
    {
        var oldCurrent = Current;
        CurrentLocal.Value = this;
        return Disposable.NewClosed(oldCurrent!, oldCurrent1 => CurrentLocal.Value = oldCurrent1);
    }

    // Constructors

    // IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (!IsOutermost)
            return;

        if (ServiceScope is IAsyncDisposable ad)
            await ad.DisposeAsync().ConfigureAwait(false);
        ServiceScope.Dispose();
    }

    // Instance methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandContext<TResult> Cast<TResult>()
        => (CommandContext<TResult>)this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Operation? TryGetOperation()
        => OutermostContext._operation;

    public void ChangeOperation(Operation? operation)
    {
        var outermostContext = OutermostContext;
        if (!ReferenceEquals(outermostContext._operation, operation))
            outermostContext._operation = operation;
    }

    public abstract Task InvokeRemainingHandlers(CancellationToken cancellationToken = default);

    public abstract void ResetResult();
    public abstract void SetResult(object value);
    public abstract void SetResult(Exception exception);

    public abstract bool TryComplete(CancellationToken cancellationToken);
}

public sealed class CommandContext<TResult> : CommandContext
{
    public ICommand<TResult> Command { get; }
    public Task<TResult> ResultTask => ResultSource.Task;
    public readonly TaskCompletionSource<TResult> ResultSource; // Set at the very end of the pipeline (via Complete)

    // Result may change while the pipeline runs
    public Result<TResult> Result {
        get;
        set {
            if (IsCompleted)
                return;

            field = value;
        }
    }

    public override bool IsCompleted => ResultTask.IsCompleted;

    public override ICommand UntypedCommand => Command;
    public override Task UntypedResultTask => ResultTask;
    public override Result UntypedResult => Result.AsUntyped();

    public CommandContext(ICommander commander, ICommand command, bool isOutermost)
        : base(commander)
    {
        var tResult = typeof(TResult);
        var tCommandResult = command.GetResultType();
        if (tCommandResult != tResult)
            throw Errors.CommandResultTypeMismatch(tResult, tCommandResult);

        Command = (ICommand<TResult>) command;
        ResultSource = new TaskCompletionSource<TResult>();

        var outerContext = isOutermost ? null : Current;
        if (outerContext != null && outerContext.Commander != commander)
            outerContext = null;

        if (outerContext == null) {
            OuterContext = null;
            OutermostContext = this;
            ServiceScope = Commander.Services.CreateScope();
            Items = new MutablePropertyBag();
        }
        else {
            OuterContext = outerContext;
            OutermostContext = outerContext.OutermostContext;
            ServiceScope = OutermostContext.ServiceScope;
        }
    }

    public override async Task InvokeRemainingHandlers(CancellationToken cancellationToken = default)
    {
        try {
            if (ExecutionState.IsFinal)
                throw Errors.NoFinalHandlerFound(UntypedCommand.GetType());

            var handler = ExecutionState.NextHandler;
            ExecutionState = ExecutionState.NextState;
            var handlerTask = handler.Invoke(UntypedCommand, this, cancellationToken);
            if (handlerTask is Task<TResult> typedHandlerTask)
                Result = await typedHandlerTask.ConfigureAwait(false);
            else
                await handlerTask.ConfigureAwait(false);
        }
        catch (Exception ex) {
            SetResult(ex);
            throw;
        }
        // We want to ensure we re-throw any exception even if
        // it wasn't explicitly thrown (i.e. set via SetResult)
        if (Result.Error is { } error)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    public override void ResetResult()
        => Result = default;

    public override void SetResult(Exception exception)
        => Result = new Result<TResult>(default!, exception);
    public override void SetResult(object value)
        => Result = new Result<TResult>((TResult) value, null);
    public void SetResult(TResult result)
        => Result = new Result<TResult>(result, null);

    public override bool TryComplete(CancellationToken cancellationToken)
        => ResultSource.TrySetFromResult(Result, cancellationToken);
}
