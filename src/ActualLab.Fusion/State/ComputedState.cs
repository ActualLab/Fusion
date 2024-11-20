using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static class ComputedState
{
    public static class DefaultOptions
    {
        public static bool TryComputeSynchronously { get; set; } = true;
        public static bool FlowExecutionContext { get; set; } = false;
        public static TimeSpan GracefulDisposeDelay { get; set; } = TimeSpan.FromSeconds(10);
    }
}

public interface IComputedState : IState, IDisposable, IHasWhenDisposed
{
    public new interface IOptions : IState.IOptions
    {
        public IUpdateDelayer? UpdateDelayer { get; init; }
        public bool TryComputeSynchronously { get; init; }
        public bool FlowExecutionContext { get; init; }
        public TimeSpan GracefulDisposeDelay { get; init; }
    }

    public IUpdateDelayer UpdateDelayer { get; set; }
    public Task UpdateCycleTask { get; }
    public CancellationToken DisposeToken { get; }
    public CancellationToken GracefulDisposeToken { get; }
}

public interface IComputedState<T> : IState<T>, IComputedState;

public abstract class ComputedState<T> : State<T>, IComputedState<T>, IGenericTimeoutHandler
{
    public new record Options : State<T>.Options, IComputedState.IOptions
    {
        public IUpdateDelayer? UpdateDelayer { get; init; }
        public bool TryComputeSynchronously { get; init; } = ComputedState.DefaultOptions.TryComputeSynchronously;
        public bool FlowExecutionContext { get; init; } = ComputedState.DefaultOptions.FlowExecutionContext;
        public TimeSpan GracefulDisposeDelay { get; init; } = ComputedState.DefaultOptions.GracefulDisposeDelay;
    }

    private volatile Computed<T>? _computingComputed;
    private volatile IUpdateDelayer _updateDelayer = null!;
    private volatile Task? _whenDisposed;
    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly CancellationTokenSource? _gracefulDisposeTokenSource;
    private readonly TimeSpan _gracefulDisposeDelay;

    public IUpdateDelayer UpdateDelayer {
        get => _updateDelayer;
        set => _updateDelayer = value;
    }

    public CancellationToken DisposeToken { get; }
    public CancellationToken GracefulDisposeToken { get; }
    public Task UpdateCycleTask { get; private set; } = null!;
    public Task? WhenDisposed => _whenDisposed;
    public override bool IsDisposed => _whenDisposed != null;

    protected ComputedState(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, false)
    {
        _disposeTokenSource = new CancellationTokenSource();
        DisposeToken = _disposeTokenSource.Token;

        _gracefulDisposeDelay = settings.GracefulDisposeDelay;
        _gracefulDisposeTokenSource = _gracefulDisposeDelay > TimeSpan.Zero
            ? new CancellationTokenSource()
            : _disposeTokenSource;
        GracefulDisposeToken = _gracefulDisposeTokenSource.Token;

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
        // Double-check locking
        if (_whenDisposed != null)
            return;
        lock (Lock) {
            if (_whenDisposed != null)
                return;

            // UpdateCycleTask == null if Initialize wasn't called somehow
            _whenDisposed = UpdateCycleTask ?? Task.CompletedTask;
        }
        GC.SuppressFinalize(this);
        _disposeTokenSource.CancelAndDisposeSilently();
        if (!ReferenceEquals(_gracefulDisposeTokenSource, _disposeTokenSource))
            Timeouts.Generic.AddOrUpdateToEarlier(this, Timeouts.Clock.Now + _gracefulDisposeDelay);
    }

    // Handles the rest of Dispose
    void IGenericTimeoutHandler.OnTimeout()
        => _gracefulDisposeTokenSource.CancelAndDisposeSilently();

    protected virtual async Task UpdateCycle()
    {
        var cancellationToken = DisposeToken;
        try {
            await Computed.Update(cancellationToken).ConfigureAwait(false);
            while (true) {
                var snapshot = Snapshot;
                var computed = snapshot.Computed;
                if (!computed.IsInvalidated())
                    await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);
                await UpdateDelayer.Delay(snapshot.RetryCount, cancellationToken).ConfigureAwait(false);
                if (!snapshot.WhenUpdated().IsCompleted)
                    await computed.Update(GracefulDisposeToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) {
            if (!e.IsCancellationOf(cancellationToken))
                Log.LogError(e, "UpdateCycle() failed and stopped for {Category}", Category);
        }
        finally {
            _gracefulDisposeTokenSource.CancelAndDisposeSilently();
        }
    }

    public override Computed? GetExistingComputed()
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
