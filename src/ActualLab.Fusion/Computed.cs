using ActualLab.Collections.Slim;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Internal;
using ActualLab.Resilience;
using ActualLab.Versioning;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public interface IComputed : IResult, IHasVersion<ulong>
{
    public ComputedOptions Options { get; }
    public ComputedInput Input { get; }
    public Type OutputType { get; }
    public ConsistencyState ConsistencyState { get; }
    public Result Output { get; }
    public InvalidationSource InvalidationSource { get; }
    public event Action<Computed> Invalidated;

    public Task GetValuePromise();
    public ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default);
    public Task UseUntyped(CancellationToken cancellationToken = default);
    public Task UseUntyped(bool allowInconsistent, CancellationToken cancellationToken = default);
    public void Invalidate(bool immediately, InvalidationSource source);
}

public abstract partial class Computed : IComputed, IGenericTimeoutHandler
{
    protected const int ConsistencyStateMask = 0xFF;

    // ReSharper disable once InconsistentNaming
    private int _state;
    private int _lastKeepAliveSlot;
    private Result _output;
    private RefHashSetSlim3<Computed> _dependencies;
    private HashSetSlim3<(ComputedInput Input, ulong Version)> _dependants;
    // ReSharper disable once InconsistentNaming
    private InvalidatedHandlerSet _invalidated;
    private object? _invalidationSource; // Object type makes atomic updates and volatile reads possible

    protected object Lock {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this;
    }

    protected internal Result UntypedOutput {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            return _output;
        }
    }

    public readonly ComputedOptions Options;
    public readonly ComputedInput Input;
    public readonly ulong Version = ComputedVersion.Next();
    private Task? _untypedValuePromise;

    // IComputed properties

    public Type OutputType => Input.Function.OutputType;

    public ConsistencyState ConsistencyState {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ConsistencyState)(_state & ConsistencyStateMask);
    }

    public Result Output {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            return _output;
        }
    }

    public InvalidationSource InvalidationSource {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_invalidationSource);
    }

    // IComputed implementation

    ComputedOptions IComputed.Options => Options;
    ComputedInput IComputed.Input => Input;
    ulong IHasVersion<ulong>.Version => Version;

    public event Action<Computed> Invalidated {
        add {
            if (ConsistencyState == ConsistencyState.Invalidated) {
                value.Invoke(this);
                return;
            }
            lock (Lock) {
                if (ConsistencyState == ConsistencyState.Invalidated) {
                    value.Invoke(this);
                    return;
                }
                _invalidated.Add(value);
            }
        }
        remove {
            if (ConsistencyState == ConsistencyState.Invalidated) return;
            lock (Lock) {
                if (ConsistencyState == ConsistencyState.Invalidated) return;
                _invalidated.Remove(value);
            }
        }
    }

    // IResult implementation

    public object? Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedOutput.Value;
    }

    public Exception? Error {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedOutput.Error;
    }

    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedOutput.HasValue;
    }

    public bool HasError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => UntypedOutput.HasError;
    }

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result output)
    {
        _output = output;
        Options = options;
        Input = input;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result output, bool isConsistent)
    {
        _output = output;
        Options = options;
        Input = input;
        _state = isConsistent ? (int)ConsistencyState.Consistent : (int)ConsistencyState.Invalidated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IResult.Deconstruct(out object? untypedValue, out Exception? error)
    {
        untypedValue = Value;
        error = Error;
    }

    public object? GetUntypedValueOrErrorBox()
        => Error is not null ? new ErrorBox(Error) : Value;

    // ToString & GetHashCode

    public override string ToString()
        => $"{GetType().GetName()}({Input} v.{Version.FormatVersion()}, State: {ConsistencyState})";

    public override int GetHashCode()
        => unchecked((int)Version);

    // GetValuePromise, UpdateUntyped & UseUntyped

    public Task GetValuePromise()
    {
        if (_untypedValuePromise is not null)
            return _untypedValuePromise;

        lock (Lock)
            return _untypedValuePromise ??= CreateValuePromise();
    }

    public async ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default)
    {
        if (this.IsConsistent())
            return this;

        using var scope = BeginIsolation();
        var computed = await Input.GetOrProduceComputed(scope.Context, cancellationToken).ConfigureAwait(false);
        return computed!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UseUntyped(CancellationToken cancellationToken = default)
        => UseUntyped(allowInconsistent: false, cancellationToken);

    public Task UseUntyped(bool allowInconsistent, CancellationToken cancellationToken = default)
    {
        var context = ComputeContext.Current;
        if ((context.CallOptions & CallOptions.GetExisting) != 0) // Neither GetExisting nor Invalidate can be used here
            throw Errors.InvalidContextCallOptions(context.CallOptions);

        // Slightly faster version of this.TryUseExistingFromLock(context)
        if (allowInconsistent || this.IsConsistent()) {
            // It can become inconsistent here, but we don't care, since...
            ComputedImpl.UseNew(this, context);
            // it can also become inconsistent here & later, and UseNew handles this.
            // So overall, Use(...) guarantees the dependency chain will be there even
            // if computed is invalidated right after the above "if".
            return GetValuePromise();
        }

        return Input.GetOrProduceValuePromise(context, cancellationToken);
    }

    // Invalidate

    void IGenericTimeoutHandler.OnTimeout(object? invalidationSource)
        => Invalidate(true, new InvalidationSource(invalidationSource).OrUnknown());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Invalidate(bool immediately, InvalidationSource source)
    {
        if (ConsistencyState == ConsistencyState.Invalidated)
            return;

        lock (Lock) {
            // The original _invalidationReason always takes precedence over the current one
            if (_invalidationSource is null)
                _invalidationSource = source;
            else
                source = new(_invalidationSource);

            switch (_state & ConsistencyStateMask) {
            case (int)ConsistencyState.Invalidated:
                return;
            case (int)ConsistencyState.Computing:
                _state |= (int)InvalidationFlags.InvalidateOnSetOutput;
                if (immediately)
                    _state |= (int)InvalidationFlags.InvalidateOnSetOutputImmediately;
                return;
            default: // == ConsistencyState.Computed
                immediately |= Options.InvalidationDelay <= TimeSpan.Zero;
                if (immediately) {
                    _state = (int)ConsistencyState.Invalidated;
                    break;
                }

                if ((_state & (int)InvalidationFlags.DelayedInvalidationStarted) != 0)
                    return; // Already started

                _state |= (int)InvalidationFlags.DelayedInvalidationStarted;
                break;
            }
        }

        if (!immediately) {
            // Delayed invalidation
            this.Invalidate(Options.InvalidationDelay, source);
            return;
        }

        // Instant invalidation - it may happen just once,
        // so we don't need a lock here.
        try {
            try {
                // StaticLog.For<IComputed>().LogWarning("Invalidating: {Computed}", this);
                OnInvalidated();
                _invalidated.Invoke(this);
                _invalidated = default;
            }
            finally {
                // Any code called here may not throw
                _dependencies.Apply(this, (self, c) => c.RemoveDependant(self));
                _dependencies.Clear();
                _dependants.Apply(new InvalidationSource(this), static (invalidationSource, usedByEntry) => {
                    var c = usedByEntry.Input.GetExistingComputed();
                    if (c is not null && c.Version == usedByEntry.Version)
                        c.Invalidate(immediately: false, invalidationSource); // Invalidate doesn't throw - ever
                });
                _dependants.Clear();
            }
        }
        catch (Exception e) {
            // We should never throw errors during the invalidation
            try {
                var log = Input.Function.Services.LogFor(GetType());
                log.LogError(e, "Error while invalidating {Category} by {Source}", Input.Category, source);
            }
            catch {
                // Intended: Invalidate doesn't throw!
            }
        }
    }

    // Protected methods

    protected virtual void OnInvalidated()
        => CancelTimeouts();

    protected abstract Task CreateValuePromise();

    // Protected internal methods - you can call them via ComputedImpl

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal bool TrySetValue(object? output)
        => TrySetOutput(Result.NewUntyped(output));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal bool TrySetError(Exception exception)
        => TrySetOutput(Result.NewUntypedError(exception));

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected internal bool TrySetOutput(Result output)
    {
        int state;
        lock (Lock) {
            if ((_state & ConsistencyStateMask) != 0) // != ComputedState.Computing
                return false;

            state = _state |= (int)ConsistencyState.Consistent; // Fine to do that, this part of the state was 0
            _output = output;
        }

        if ((state & (int)InvalidationFlags.InvalidateOnSetOutput) != 0) {
            const string reason =
                $"{nameof(Computed)}.{nameof(TrySetOutput)}: {nameof(InvalidationFlags.InvalidateOnSetOutput)} (no {nameof(InvalidationSource)})";
            Invalidate((state & (int)InvalidationFlags.InvalidateOnSetOutputImmediately) != 0, new InvalidationSource(reason));
            return true;
        }

        StartAutoInvalidation();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected internal void StartAutoInvalidation()
    {
        if (!this.IsConsistent())
            return;

        TimeSpan timeout;
        var error = Error;
        if (error is null) {
            timeout = Options.AutoInvalidationDelay;
            if (timeout != TimeSpan.MaxValue)
                this.Invalidate(timeout);
            return;
        }

        if (error is OperationCanceledException) {
            // This error requires instant invalidation
            const string reason =
                $"{nameof(Computed)}.{nameof(StartAutoInvalidation)}: {nameof(Error)} is {nameof(OperationCanceledException)}";
            Invalidate(immediately: true, new InvalidationSource(reason));
            return;
        }

        timeout = IsTransientError(error)
            ? Options.TransientErrorInvalidationDelay
            : Options.AutoInvalidationDelay;
        if (timeout != TimeSpan.MaxValue)
            this.Invalidate(timeout);
    }

    protected internal Computed[] GetDependencies()
    {
        lock (Lock) {
            var result = new Computed[_dependencies.Count];
            _dependencies.CopyTo(result);
            return result;
        }
    }

    protected internal (ComputedInput Input, ulong Version)[] GetDependants()
    {
        var result = new (ComputedInput Input, ulong Version)[_dependants.Count];
        lock (Lock) {
            _dependants.CopyTo(result);
            return result;
        }
    }

#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    protected internal void RenewTimeouts(bool isNew)
    {
        if (ConsistencyState == ConsistencyState.Invalidated)
            return; // We shouldn't register miss here, since it's going to be counted later anyway

        var minCacheDuration = Options.MinCacheDuration;
        if (minCacheDuration != default) {
            var keepAliveSlot = Timeouts.GetKeepAliveSlot(Timeouts.Clock.Now + minCacheDuration);
            if (Volatile.Read(ref _lastKeepAliveSlot) != keepAliveSlot) { // Fast check
                if (Interlocked.Exchange(ref _lastKeepAliveSlot, keepAliveSlot) != keepAliveSlot) // Slow check
                    Timeouts.KeepAlive.AddOrUpdateToLater(this, keepAliveSlot);
            }
        }
        ComputedRegistry.ReportAccess(this, isNew);
    }

    protected internal void CancelTimeouts()
    {
        var options = Options;
        if (options.MinCacheDuration != default) {
            Interlocked.Exchange(ref _lastKeepAliveSlot, 0);
            Timeouts.KeepAlive.Remove(this);
        }
    }

    protected internal void AddDependency(Computed dependency)
    {
        // Debug.WriteLine($"{nameof(AddUsed)}: {this} <- {used}");
        lock (Lock) {
            if (ConsistencyState != ConsistencyState.Computing) {
                // The current computed is either:
                // - Invalidated: nothing to do in this case.
                //   Deps are meaningless for whatever is already invalidated.
                // - Consistent: this means the dependency computation hasn't been completed
                //   while the dependant was computing, which literally means it is actually unused.
                //   This happens e.g. when N tasks to compute dependencies start during the computation,
                //   but only some of them are awaited. Other results might be ignored e.g. because
                //   an exception was thrown in one of early "awaits". And if you "linearize" such a
                //   method, it becomes clear that dependencies that didn't finish by the end of computation
                //   actually aren't used, coz in the "linear" flow they would be requested at some
                //   later point.
                return;
            }
            if (dependency.AddDependant(this))
                _dependencies.Add(dependency);
        }
    }

    // Should be called only from AddUsed
    private bool AddDependant(Computed dependant)
    {
        lock (Lock) {
            switch (ConsistencyState) {
            case ConsistencyState.Computing:
                throw Errors.WrongComputedState(ConsistencyState);
            case ConsistencyState.Invalidated:
                dependant.Invalidate();
                return false;
            }

            var usedByRef = (dependant.Input, dependant.Version);
            _dependants.Add(usedByRef);
            return true;
        }
    }

    protected internal void RemoveDependant(Computed usedBy)
    {
        lock (Lock) {
            if (ConsistencyState == ConsistencyState.Invalidated)
                // _usedBy is already empty or going to be empty soon;
                // moreover, only Invalidated code can modify
                // _used/_usedBy once invalidation flag is set
                return;

            _dependants.Remove((usedBy.Input, usedBy.Version));
        }
    }

    protected internal (int OldCount, int NewCount) PruneDependants()
    {
        lock (Lock) {
            if (ConsistencyState != ConsistencyState.Consistent)
                // _usedBy is already empty or going to be empty soon;
                // moreover, only Invalidated code can modify
                // _used/_usedBy once invalidation flag is set
                return (0, 0);

            var replacement = new HashSetSlim3<(ComputedInput Input, ulong Version)>();
            var oldCount = _dependants.Count;
            foreach (var entry in _dependants.Items) {
                var c = entry.Input.GetExistingComputed();
                if (c is not null && c.Version == entry.Version)
                    replacement.Add(entry);
            }
            _dependants = replacement;
            return (oldCount, _dependants.Count);
        }
    }

    protected internal void CopyDependenciesTo(ref ArrayBuffer<Computed> buffer)
    {
        lock (Lock) {
            var count = buffer.Count;
            buffer.EnsureCapacity(count + _dependencies.Count);
            _dependencies.CopyTo(buffer.Buffer.AsSpan(count));
        }
    }

    protected internal bool IsTransientError(Exception error)
    {
        if (error is OperationCanceledException)
            return true; // Must be transient under any circumstances in IComputed

        TransiencyResolver<Computed>? transiencyResolver = null;
        try {
            var services = Input.Function.Services;
            transiencyResolver = services.GetService<TransiencyResolver<Computed>>();
        }
        catch (ObjectDisposedException) {
            // We want to handle IServiceProvider disposal gracefully
        }
        return transiencyResolver?.Invoke(error).IsAnyTransient()
            ?? TransiencyResolvers.PreferTransient.Invoke(error).IsAnyTransient();
    }
}
