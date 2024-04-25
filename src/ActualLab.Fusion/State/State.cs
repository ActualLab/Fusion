using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IState : IResult, IHasServices
{
    public interface IOptions
    {
        ComputedOptions ComputedOptions { get; init; }
        Action<IState>? EventConfigurator { get; init; }
        string? Category { get; init; }
    }

    IStateSnapshot Snapshot { get; }
    IComputed Computed { get; }
    object? LastNonErrorValue { get; }

    event Action<IState, StateEventKind>? Invalidated;
    event Action<IState, StateEventKind>? Updating;
    event Action<IState, StateEventKind>? Updated;
}

public interface IState<T> : IState, IResult<T>
{
    new StateSnapshot<T> Snapshot { get; }
    new Computed<T> Computed { get; }
    new T LastNonErrorValue { get; }

    new event Action<IState<T>, StateEventKind>? Invalidated;
    new event Action<IState<T>, StateEventKind>? Updating;
    new event Action<IState<T>, StateEventKind>? Updated;
}

public abstract class State<T> : ComputedInput,
    IState<T>,
    IEquatable<State<T>>,
    IFunction<T>
{
    public record Options : IState.IOptions
    {
        public ComputedOptions ComputedOptions { get; init; } = ComputedOptions.Default;
        public Result<T> InitialOutput { get; init; }
        public string? Category { get; init; }

        public T InitialValue {
            get => InitialOutput.ValueOrDefault!;
            init => InitialOutput = new Result<T>(value, null);
        }

        public Action<IState<T>>? EventConfigurator { get; init; }
        Action<IState>? IState.IOptions.EventConfigurator { get; init; }
    }

    private volatile StateSnapshot<T>? _snapshot;
    private string? _category;
    private ILogger? _log;

    protected ComputedOptions ComputedOptions { get; private set; } = null!;
    protected AsyncLock AsyncLock { get; } = new(LockReentryMode.CheckedFail);
    protected object Lock => AsyncLock;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; }
    public StateSnapshot<T> Snapshot => _snapshot!;

    public override string Category {
        get => _category ??= GetType().GetName();
        init => _category = value;
    }

#pragma warning disable CA1721
    public Computed<T> Computed {
#pragma warning restore CA1721
        get => Snapshot.Computed;
        protected set {
            value.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            lock (Lock) {
                var prevSnapshot = _snapshot;
                if (prevSnapshot != null) {
                    if (prevSnapshot.Computed == value)
                        return;

                    prevSnapshot.Computed.Invalidate();
                    _snapshot = new StateSnapshot<T>(prevSnapshot, value);
                }
                else
                    _snapshot = new StateSnapshot<T>(this, value);
                OnSetSnapshot(_snapshot, prevSnapshot);
            }
        }
    }

    public T? ValueOrDefault => Computed.ValueOrDefault;
    public T Value => Computed.Value;
    public Exception? Error => Computed.Error;
    public bool HasValue => Computed.HasValue;
    public bool HasError => Computed.HasError;
    public T LastNonErrorValue => Snapshot.LastNonErrorComputed.Value;

    IStateSnapshot IState.Snapshot => Snapshot;
    Computed<T> IState<T>.Computed => Computed;
    IComputed IState.Computed => Computed;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    object? IState.LastNonErrorValue => LastNonErrorValue;
    // ReSharper disable once HeapView.PossibleBoxingAllocation
    object? IResult.UntypedValue => Computed.Value;

    public event Action<IState<T>, StateEventKind>? Invalidated;
    public event Action<IState<T>, StateEventKind>? Updating;
    public event Action<IState<T>, StateEventKind>? Updated;

    event Action<IState, StateEventKind>? IState.Invalidated {
        add => UntypedInvalidated += value;
        remove => UntypedInvalidated -= value;
    }
    event Action<IState, StateEventKind>? IState.Updating {
        add => UntypedUpdating += value;
        remove => UntypedUpdating -= value;
    }
    event Action<IState, StateEventKind>? IState.Updated {
        add => UntypedUpdated += value;
        remove => UntypedUpdated -= value;
    }

    protected event Action<IState<T>, StateEventKind>? UntypedInvalidated;
    protected event Action<IState<T>, StateEventKind>? UntypedUpdating;
    protected event Action<IState<T>, StateEventKind>? UntypedUpdated;

    protected State(Options settings, IServiceProvider services, bool initialize = true)
    {
        Initialize(this, RuntimeHelpers.GetHashCode(this));
        Services = services;

        // ReSharper disable once VirtualMemberCallInConstructor
        if (initialize)
#pragma warning disable CA2214
            Initialize(settings);
#pragma warning restore CA2214
    }

    public void Deconstruct(out T value, out Exception? error)
        => Computed.Deconstruct(out value, out error);

    public bool IsValue(out T value)
        => Computed.IsValue(out value!);
    public bool IsValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out Exception error)
        => Computed.IsValue(out value, out error);

    public Result<T> AsResult()
        => Computed.AsResult();
    public Result<TOther> Cast<TOther>()
        => Computed.Cast<TOther>();
    T IConvertibleTo<T>.Convert() => Value;
    Result<T> IConvertibleTo<Result<T>>.Convert() => AsResult();

    // Equality

    public bool Equals(State<T>? other)
        => ReferenceEquals(this, other);
    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj);
    public override int GetHashCode()
        => base.GetHashCode();

    // Protected methods

    protected virtual void Initialize(Options settings)
    {
        _category = settings.Category;
        ComputedOptions = settings.ComputedOptions;
        settings.EventConfigurator?.Invoke(this);
        var untypedOptions = (IState.IOptions) settings;
        untypedOptions.EventConfigurator?.Invoke(this);

        var computed = CreateComputed();
        if (_snapshot != null)
            return; // CreateComputed sets Computed, if overriden (e.g. in MutableState)

        computed.TrySetOutput(settings.InitialOutput);
        Computed = computed;
        computed.Invalidate();
    }

    protected internal virtual void OnInvalidated(Computed<T> computed)
    {
        var snapshot = Snapshot;
        if (computed != snapshot.Computed)
            return;

        try {
            Invalidated?.Invoke(this, StateEventKind.Invalidated);
            UntypedInvalidated?.Invoke(this, StateEventKind.Invalidated);
        }
        catch (Exception e) {
            Log.LogError(e, "Invalidated / UntypedInvalidated handler failed for {Category}", Category);
        }
    }

    protected virtual void OnUpdating(Computed<T> computed)
    {
        var snapshot = Snapshot;
        if (computed != snapshot.Computed)
            return;

        try {
            snapshot.OnUpdating();
            Updating?.Invoke(this, StateEventKind.Updating);
            UntypedUpdating?.Invoke(this, StateEventKind.Updating);
        }
        catch (Exception e) {
            Log.LogError(e, "Updating / UntypedUpdating handler failed for {Category}", Category);
        }
    }

    protected virtual void OnSetSnapshot(StateSnapshot<T> snapshot, StateSnapshot<T>? prevSnapshot)
    {
        if (prevSnapshot == null)
            // First assignment / initialization
            return;

        try {
            prevSnapshot.OnUpdated();
            Updated?.Invoke(this, StateEventKind.Updated);
            UntypedUpdated?.Invoke(this, StateEventKind.Updated);
        }
        catch (Exception e) {
            Log.LogError(e, "Updated / UntypedUpdated handler failed for {Category}", Category);
        }
    }

    // ComputedInput

    public override IComputed? GetExistingComputed()
        => _snapshot?.Computed;

    // IFunction<T> & IFunction

    ValueTask<Computed<T>> IFunction<T>.Invoke(ComputedInput input, IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return Invoke(usedBy, context, cancellationToken);
    }

    async ValueTask<IComputed> IFunction.Invoke(ComputedInput input, IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return await Invoke(usedBy, context, cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask<Computed<T>> Invoke(
        IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        context ??= ComputeContext.Current;

        var computed = Computed;
        if (computed.TryUseExisting(context, usedBy))
            return computed;

        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        computed = Computed;
        if (computed.TryUseExistingFromLock(context, usedBy))
            return computed;

        releaser.MarkLockedLocally();
        OnUpdating(computed);
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy);
        return computed;
    }

    Task<T> IFunction<T>.InvokeAndStrip(
        ComputedInput input, IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return InvokeAndStrip(usedBy, context, cancellationToken);
    }

    protected virtual Task<T> InvokeAndStrip(
        IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        context ??= ComputeContext.Current;

        var result = Computed;
        return result.TryUseExisting(context, usedBy)
            ? result.StripToTask(context)
            : TryRecompute(usedBy, context, cancellationToken);
    }

    protected async Task<T> TryRecompute(
        IComputed? usedBy, ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = Computed;
        if (computed.TryUseExistingFromLock(context, usedBy))
            return computed.Strip(context);

        releaser.MarkLockedLocally();
        OnUpdating(computed);
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy);
        return computed.Value;
    }

    protected async ValueTask<StateBoundComputed<T>> GetComputed(CancellationToken cancellationToken)
    {
        var tryIndex = 0;
        StateBoundComputed<T> computed;
        while (true) {
            computed = CreateComputed();
            using (Fusion.Computed.BeginCompute(computed)) {
                try {
                    var value = await Compute(cancellationToken).ConfigureAwait(false);
                    computed.TrySetOutput(Result.New(value));
                    break;
                }
                catch (Exception e) {
                    if (cancellationToken.IsCancellationRequested) {
                        computed.Invalidate(true); // Instant invalidation on cancellation
                        computed.TrySetOutput(Result.Error<T>(e));
                        if (e is OperationCanceledException)
                            throw;

                        cancellationToken.ThrowIfCancellationRequested(); // Always throws here
                    }

                    if (e is not OperationCanceledException || ++tryIndex > Fusion.Computed.StrangeCancellationReprocessingLimit) {
                        computed.TrySetOutput(Result.Error<T>(e));
                        break;
                    }

                    computed.Invalidate(true); // Instant invalidation on cancellation
                    computed.TrySetOutput(Result.Error<T>(e));
                    var delay = Fusion.Computed.StrangeCancellationReprocessingDelays[tryIndex];
                    Log.LogWarning(e,
                        "GetComputed #{TryIndex} for {Category} was cancelled internally, retry in {Delay}",
                        tryIndex, Category, delay.ToShortString());
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // It's super important to make "Computed = computed" assignment after "using" block -
        // otherwise all State events will be triggered while Computed.Current still points on
        // computed (which is already computed), so if any compute method runs inside
        // the event handler, it will fail on attempt to add a dependency.
        Computed = computed;
        return computed;
    }

    protected abstract Task<T> Compute(CancellationToken cancellationToken);

    protected virtual StateBoundComputed<T> CreateComputed()
        => new(ComputedOptions, this);
}
