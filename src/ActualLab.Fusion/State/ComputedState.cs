using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using ActualLab.Trimming;

namespace ActualLab.Fusion;

// Interfaces

public interface IComputedStateOptions : IStateOptions
{
    public IUpdateDelayer? UpdateDelayer { get; init; }
    public bool TryComputeSynchronously { get; init; }
    public bool FlowExecutionContext { get; init; }
    public TimeSpan GracefulDisposeDelay { get; init; }
}

public interface IComputedState : IState, IDisposable, IHasWhenDisposed
{
    public IUpdateDelayer UpdateDelayer { get; set; }
    public Task UpdateCycleTask { get; }
    public CancellationToken DisposeToken { get; }
    public CancellationToken GracefulDisposeToken { get; }
}

public interface IComputedState<T> : IState<T>, IComputedState;

// Classes

public abstract class ComputedState : State, IComputedState, IGenericTimeoutHandler
{
    public static class DefaultOptions
    {
        public static bool TryComputeSynchronously { get; set; } = true;
        public static bool FlowExecutionContext { get; set; } = false;
        public static TimeSpan GracefulDisposeDelay { get; set; } = TimeSpan.FromSeconds(10);
    }

    private const string GenericTimeoutReason = nameof(ComputedState) + "." + nameof(Dispose);
    private volatile Task? _whenDisposed;
    private volatile IUpdateDelayer _updateDelayer = null!;

    protected volatile Computed? ComputingComputed;
    protected readonly CancellationTokenSource DisposeTokenSource;
    protected readonly CancellationTokenSource? GracefulDisposeTokenSource;
    protected readonly TimeSpan GracefulDisposeDelay;

    public IUpdateDelayer UpdateDelayer {
        get => _updateDelayer;
        set => _updateDelayer = value;
    }

    public CancellationToken DisposeToken { get; }
    public CancellationToken GracefulDisposeToken { get; }
    public Task UpdateCycleTask { get; private set; } = null!;
    public Task? WhenDisposed => _whenDisposed;
    public override bool IsDisposed => _whenDisposed is not null;

    protected ComputedState(IComputedStateOptions options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize: false)
    {
        DisposeTokenSource = new CancellationTokenSource();
        DisposeToken = DisposeTokenSource.Token;

        GracefulDisposeDelay = options.GracefulDisposeDelay;
        GracefulDisposeTokenSource = GracefulDisposeDelay > TimeSpan.Zero
            ? new CancellationTokenSource()
            : DisposeTokenSource;
        GracefulDisposeToken = GracefulDisposeTokenSource.Token;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
            Initialize(options);
    }

    protected override void Initialize(IStateOptions settings)
    {
        base.Initialize(settings);
        var computedStateOptions = (IComputedStateOptions)settings;
        _updateDelayer = computedStateOptions.UpdateDelayer ?? Services.GetRequiredService<IUpdateDelayer>();

        // Ideally, we want to suppress execution context flow here,
        // because the Update is ~ a worker-style task.
        if (computedStateOptions.TryComputeSynchronously)
            UpdateCycleTask = computedStateOptions.FlowExecutionContext
                ? UpdateCycle()
                : ExecutionContextExt.Start(ExecutionContextExt.Default, UpdateCycle);
        else if (computedStateOptions.FlowExecutionContext || ExecutionContext.IsFlowSuppressed())
            UpdateCycleTask = Task.Run(UpdateCycle, DisposeToken);
        else {
            using var _ = ExecutionContext.SuppressFlow();
            UpdateCycleTask = Task.Run(UpdateCycle, DisposeToken);
        }
    }

    // ~ComputedState() => Dispose();

    public virtual void Dispose()
    {
        // Double-check locking
        if (_whenDisposed is not null)
            return;
        lock (Lock) {
            if (_whenDisposed is not null)
                return;

            // UpdateCycleTask is null if Initialize wasn't called somehow
            _whenDisposed = UpdateCycleTask ?? Task.CompletedTask;
        }
        GC.SuppressFinalize(this);
        DisposeTokenSource.CancelAndDisposeSilently();
        if (!ReferenceEquals(GracefulDisposeTokenSource, DisposeTokenSource))
            Timeouts.Generic.AddOrUpdateToEarlier(
                new GenericTimeoutSlot(this, GenericTimeoutReason),
                Timeouts.Clock.Now + GracefulDisposeDelay);
    }

    // Handles the rest of Dispose
    void IGenericTimeoutHandler.OnTimeout(object? reason)
        => GracefulDisposeTokenSource.CancelAndDisposeSilently();

    protected virtual async Task UpdateCycle()
    {
        var cancellationToken = DisposeToken;
        try {
            await Computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            while (true) {
                var snapshot = Snapshot;
                var computed = snapshot.Computed;
                if (!computed.IsInvalidated())
                    await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);

                await UpdateDelayer.Delay(snapshot.RetryCount, cancellationToken).ConfigureAwait(false);

                if (!snapshot.WhenUpdated().IsCompleted)
                    // GracefulDisposeToken here allows Update to take some extra after DisposeToken cancellation.
                    // This, in particular, lets RPC calls to complete, cache entries to populate, etc.
                    await computed.UpdateUntyped(GracefulDisposeToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) {
            if (!e.IsCancellationOf(cancellationToken))
                Log.LogError(e, "UpdateCycle() failed and stopped for {Category}", Category);
        }
        finally {
            GracefulDisposeTokenSource.CancelAndDisposeSilently();
        }
    }

    public override Computed? GetExistingComputed()
    {
        lock (Lock)
            return ComputingComputed ?? base.GetExistingComputed();
    }

    protected Task? GetComputeTaskIfDisposed()
        => ComputedStateImpl.GetComputeTaskIfDisposed(this);

    protected override void OnSetSnapshot(StateSnapshot snapshot, StateSnapshot? prevSnapshot)
    {
        // This method is called inside lock (Lock)
        ComputingComputed = null;
        base.OnSetSnapshot(snapshot, prevSnapshot);
    }
}

public abstract class ComputedState<T> : ComputedState, IState<T>
{
    public record Options : StateOptions<T>, IComputedStateOptions
    {
        public IUpdateDelayer? UpdateDelayer { get; init; }
        public bool TryComputeSynchronously { get; init; } = DefaultOptions.TryComputeSynchronously;
        public bool FlowExecutionContext { get; init; } = DefaultOptions.FlowExecutionContext;
        public TimeSpan GracefulDisposeDelay { get; init; } = DefaultOptions.GracefulDisposeDelay;
    }

    public override Type OutputType => typeof(T);

    // IState<T> implementation
    public new Computed<T> Computed => Unsafe.As<Computed<T>>(UntypedComputed);
    public T? ValueOrDefault => Computed.ValueOrDefault;
    public new T Value => Computed.Value;
    public new T LastNonErrorValue => Unsafe.As<Computed<T>>(Snapshot.LastNonErrorComputed).Value;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected ComputedState(Options options, IServiceProvider services, bool initialize = true)
        : base(options, services, initialize)
    {
        if (CodeKeeper.AlwaysFalse)
            CodeKeeper.Keep<ComputedStateImpl.GetComputeTaskIfDisposedFactory<T>>();
    }

    // IResult<T> implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);
    T IConvertibleTo<T>.Convert() => Value;

    // Useful helpers

    public bool IsInitial(out T value)
    {
        var snapshot = Snapshot;
        value = Unsafe.As<Computed<T>>(snapshot.Computed).Value;
        return snapshot.IsInitial;
    }

    public bool IsInitial(out T value, out Exception? error)
    {
        var snapshot = Snapshot;
        (value, error) = Unsafe.As<Computed<T>>(snapshot.Computed);
        return snapshot.IsInitial;
    }

    // Protected methods

    protected override Computed CreateComputed()
    {
        var computed = new StateBoundComputed<T>(ComputedOptions, this);
        lock (Lock)
            ComputingComputed = computed;
        return computed;
    }
}
