namespace ActualLab.Fusion;

public interface IComputedState : IState, IDisposable, IHasWhenDisposed
{
    public static class DefaultOptions
    {
        public static bool TryComputeSynchronously { get; set; } = true;
        public static bool FlowExecutionContext { get; set; } = false;
    }

    public new interface IOptions : IState.IOptions
    {
        IUpdateDelayer? UpdateDelayer { get; init; }
        public bool TryComputeSynchronously { get; init; }
        public bool FlowExecutionContext { get; init; }
    }

    IUpdateDelayer UpdateDelayer { get; set; }
    Task UpdateCycleTask { get; }
    CancellationToken DisposeToken { get; }
}

public interface IComputedState<T> : IState<T>, IComputedState;

public abstract class ComputedState<T> : State<T>, IComputedState<T>
{
    public new record Options : State<T>.Options, IComputedState.IOptions
    {
        public IUpdateDelayer? UpdateDelayer { get; init; }
        public bool TryComputeSynchronously { get; init; } = IComputedState.DefaultOptions.TryComputeSynchronously;
        public bool FlowExecutionContext { get; init; } = IComputedState.DefaultOptions.FlowExecutionContext;
    }

    private volatile Computed<T>? _computingComputed;
    private volatile IUpdateDelayer _updateDelayer = null!;
    private volatile Task? _whenDisposed;
    private readonly CancellationTokenSource _disposeTokenSource;

    public IUpdateDelayer UpdateDelayer {
        get => _updateDelayer;
        set => _updateDelayer = value;
    }

    public CancellationToken DisposeToken { get; }
    public Task UpdateCycleTask { get; private set; } = null!;
    public Task? WhenDisposed => _whenDisposed;
    public override bool IsDisposed => _whenDisposed != null;

    protected ComputedState(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, false)
    {
        _disposeTokenSource = new CancellationTokenSource();
        DisposeToken = _disposeTokenSource.Token;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(settings);
    }

    protected override void Initialize(State<T>.Options settings)
    {
        base.Initialize(settings);
        var computedStateOptions = (Options)settings;
        _updateDelayer = computedStateOptions.UpdateDelayer ?? Services.GetRequiredService<IUpdateDelayer>();

        // Ideally we want to suppress execution context flow here,
        // because the Update is ~ a worker-style task.
        if (computedStateOptions.TryComputeSynchronously)
            UpdateCycleTask = computedStateOptions.FlowExecutionContext
                ? UpdateCycle()
                : ExecutionContextExt.Start(ExecutionContextExt.Default, UpdateCycle);
        else if (computedStateOptions.FlowExecutionContext)
            UpdateCycleTask = Task.Run(UpdateCycle, DisposeToken);
        else {
            using var _ = ExecutionContextExt.TrySuppressFlow();
            UpdateCycleTask = Task.Run(UpdateCycle, DisposeToken);
        }
    }

    // ~ComputedState() => Dispose();

    public virtual void Dispose()
    {
        if (_whenDisposed != null)
            return;
        lock (Lock) {
            if (_whenDisposed != null)
                return;

            _whenDisposed = UpdateCycleTask ?? Task.CompletedTask;
        }
        GC.SuppressFinalize(this);
        _disposeTokenSource.CancelAndDisposeSilently();
    }

    protected virtual async Task UpdateCycle()
    {
        var cancellationToken = DisposeToken;
        try {
            await Computed.Update(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            if (e.IsCancellationOf(cancellationToken)) {
                Computed.Invalidate();
                return;
            }

            Log.LogError(e, "Failure inside UpdateCycle()");
        }

        while (!cancellationToken.IsCancellationRequested) {
            try {
                var snapshot = Snapshot;
                var computed = snapshot.Computed;
                if (!computed.IsInvalidated())
                    await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
                await UpdateDelayer.Delay(snapshot.RetryCount, cancellationToken).ConfigureAwait(false);
                if (!snapshot.WhenUpdated().IsCompleted)
                    await computed.Update(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) {
                if (e.IsCancellationOf(cancellationToken))
                    break;

                Log.LogError(e, "Failure inside UpdateCycle()");
            }
        }
        Computed.Invalidate();
    }

    public override IComputed? GetExistingComputed()
    {
        lock (Lock)
            return _computingComputed ?? base.GetExistingComputed();
    }

    protected override StateBoundComputed<T> CreateComputed()
    {
        var computed = new StateBoundComputed<T>(ComputedOptions, this);
        lock (Lock)
            _computingComputed = computed;
        return computed;
    }

    protected override void OnSetSnapshot(StateSnapshot<T> snapshot, StateSnapshot<T>? prevSnapshot)
    {
        // This method is called inside lock (Lock)
        _computingComputed = null;
        base.OnSetSnapshot(snapshot, prevSnapshot);
    }
}
