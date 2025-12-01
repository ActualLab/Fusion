using ActualLab.Caching;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

#pragma warning disable CA1721
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

// Interfaces

public interface IState : IResult, IComputeFunction
{
    public StateSnapshot Snapshot { get; }
    public Computed Computed { get; }
    public object? LastNonErrorValue { get; }
    public string Category { get; init; }

    public event Action<State, StateEventKind>? Invalidated;
    public event Action<State, StateEventKind>? Updating;
    public event Action<State, StateEventKind>? Updated;
}

public interface IState<T> : IState, IResult<T>
{
    public new Computed<T> Computed { get; }
    public new T LastNonErrorValue { get; }

    public bool IsInitial(out T value);
    public bool IsInitial(out T value, out Exception? error);
}

// Classes

public abstract class State : ComputedInput, IState
{
    private volatile StateSnapshot? _snapshot;
    private string? _category;

    // Protected properties

    protected ComputedOptions ComputedOptions { get; private set; } = null!;

    protected AsyncLock AsyncLock { get; } = new(LockReentryMode.CheckedFail);
    protected object Lock => AsyncLock;

    protected Func<Task, object?> GetTaskResultAsObjectSynchronously
        => field ??= GenericInstanceCache.GetUnsafe<Func<Task, object?>>(
            typeof(TaskExt.GetResultAsObjectSynchronouslyFactory<>), OutputType);
    protected ILogger Log => field ??= Services.LogFor(GetType());

    // Public properties

    public IServiceProvider Services { get; }
    public abstract Type OutputType { get; }

    public override string Category {
        get => _category ??= GetType().GetName();
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member
        init => _category = value;
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member
    }

    protected StateSnapshot UntypedSnapshot {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!;
    }

    protected Computed UntypedComputed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!.Computed;
    }

    public StateSnapshot Snapshot {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!;
    }

    public Computed Computed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!.Computed;
    }

    public object? Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!.Computed.Value;
    }

    public object? LastNonErrorValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot!.LastNonErrorComputed;
    }

    public Exception? Error {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedComputed.Error;
    }

    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedComputed.HasValue;
    }

    public bool HasError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedComputed.HasError;
    }

    public event Action<State, StateEventKind>? Invalidated;
    public event Action<State, StateEventKind>? Updating;
    public event Action<State, StateEventKind>? Updated;

    protected State(IStateOptions options, IServiceProvider services, bool initialize = true)
    {
        if (options.ComputedOptions.IsConsolidating)
            throw new ArgumentOutOfRangeException(nameof(options));

        Services = services;
        Initialize(this, RuntimeHelpers.GetHashCode(this)); // ComputedInput.Initialize

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
#pragma warning disable CA2214
            Initialize(options);
#pragma warning restore CA2214
    }

    // IResult implementation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out object? untypedValue, out Exception? error)
        => ((IResult)UntypedComputed).Deconstruct(out untypedValue, out error);
    public object? GetUntypedValueOrErrorBox()
        => UntypedComputed.GetUntypedValueOrErrorBox();

    // Equality
    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj);

    // ComputedInput

    public override ComputedOptions GetComputedOptions()
        => ComputedOptions;

    public override Computed? GetExistingComputed()
        => _snapshot?.Computed;

    // IComputedFunction implementation

    FusionHub IComputeFunction.Hub => Services.GetRequiredService<FusionHub>();

    Task<Computed> IComputeFunction.ProduceComputed(ComputedInput input, ComputeContext context, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return ProduceComputed(context, cancellationToken);
    }

    protected virtual async Task<Computed> ProduceComputed(ComputeContext context, CancellationToken cancellationToken = default)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = UntypedComputed;
        if (ComputedImpl.TryUseExistingFromLock(computed, context))
            return computed;

        releaser.MarkLockedLocally(unmarkOnRelease: false);
        OnUpdating(computed);
        computed = await ProduceComputedFromLock(cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed;
    }

    protected async ValueTask<Computed> ProduceComputedFromLock(CancellationToken cancellationToken)
    {
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        Computed computed;
        while (true) {
            computed = CreateComputed();
            try {
                using var _ = Computed.BeginCompute(computed);
                var computeTask = Compute(cancellationToken);
                await computeTask.ConfigureAwait(false);
                var value = GetTaskResultAsObjectSynchronously.Invoke(computeTask);
                computed.TrySetValue(value);
                break;
            }
            catch (Exception e) {
                var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                    nameof(ProduceComputedFromLock), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                if (delayTask == SpecialTasks.MustThrow)
                    throw;
                if (delayTask == SpecialTasks.MustReturn)
                    break;
                await delayTask.ConfigureAwait(false);
            }
        }

        // It's super important to make SetComputed after "using" block -
        // otherwise all State events will be triggered while Computed.Current still points on
        // computed (which is already computed), so if any compute method runs inside
        // the event handler, it will fail on an attempt to add a dependency.
        SetComputed(computed, InvalidationSource.StateProduce);
        return computed;
    }

    protected abstract Computed CreateComputed();
    protected abstract Task Compute(CancellationToken cancellationToken);

    // Life cycle / OnXxx methods

    protected virtual void Initialize(IStateOptions options)
    {
        _category = options.Category;
        ComputedOptions = options.ComputedOptions;
        options.EventConfigurator?.Invoke(this);

        var computed = CreateComputed();
        if (_snapshot is not null)
            return; // CreateComputed sets Computed, if overriden (e.g. in MutableState)

        computed.TrySetOutput(options.InitialOutput);
        SetComputed(computed, InvalidationSource.StateInitialize);
        computed.Invalidate(immediately: true, InvalidationSource.InitialState);
    }

    protected void SetComputed(Computed computed, InvalidationSource source)
    {
        computed.AssertConsistencyStateIsNot(ConsistencyState.Computing);
        lock (Lock) {
            var prevSnapshot = _snapshot;
            if (prevSnapshot is not null) {
                if (prevSnapshot.Computed == computed)
                    return;

                prevSnapshot.Computed.Invalidate(immediately: true, source);
                _snapshot = new StateSnapshot(this, prevSnapshot, computed);
            }
            else
                _snapshot = new StateSnapshot(this, null, computed);
            OnSetSnapshot(_snapshot, prevSnapshot);
        }
    }

    protected internal virtual void OnInvalidated(Computed computed)
    {
        var snapshot = Snapshot;
        if (computed != snapshot.Computed)
            return;

        try {
            Invalidated?.Invoke(this, StateEventKind.Invalidated);
        }
        catch (Exception e) {
            Log.LogError(e, "Invalidated event handler failed for {Category}", Category);
        }
    }

    protected virtual void OnUpdating(Computed computed)
    {
        var snapshot = Snapshot;
        if (computed != snapshot.Computed)
            return;

        try {
            snapshot.OnUpdating();
            Updating?.Invoke(this, StateEventKind.Updating);
        }
        catch (Exception e) {
            Log.LogError(e, "Updating event handler failed for {Category}", Category);
        }
    }

    protected virtual void OnSetSnapshot(StateSnapshot snapshot, StateSnapshot? prevSnapshot)
    {
        if (prevSnapshot is null)
            // First assignment / initialization
            return;

        try {
            prevSnapshot.OnUpdated();
            Updated?.Invoke(this, StateEventKind.Updated);
        }
        catch (Exception e) {
            Log.LogError(e, "Updated event handler failed for {Category}", Category);
        }
    }
}
