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
    public event Action<Computed> Invalidated;

    public Task GetValuePromise();
    public ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default);
    public Task UseUntyped(CancellationToken cancellationToken = default);
    public void Invalidate(bool immediately = false);
}

public abstract partial class Computed(ComputedOptions options, ComputedInput input, Result output)
    : IComputed, IGenericTimeoutHandler
{
    private volatile int _state;
    private volatile ComputedFlags _flags;
    private volatile int _lastKeepAliveSlot;
    private Result _output = output;
    private RefHashSetSlim3<Computed> _dependencies;
    private HashSetSlim3<(ComputedInput Input, ulong Version)> _dependants;
    // ReSharper disable once InconsistentNaming
    private InvalidatedHandlerSet _invalidated;

    protected object Lock {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this;
    }

    protected ComputedFlags Flags {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _flags;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _flags = value;
    }

    public readonly ComputedOptions Options = options;
    public readonly ComputedInput Input = input;
    public readonly ulong Version = ComputedVersion.Next();
    private Task? _untypedValuePromise;

    // IComputed properties

    public Type OutputType => Input.Function.OutputType;

    public ConsistencyState ConsistencyState {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (ConsistencyState)_state;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal set => _state = (int)value;
    }

    public Result Output {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            return _output;
        }
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
                    value(this);
                    return;
                }
                _invalidated.Add(value);
            }
        }
        remove {
            lock (Lock) {
                if (ConsistencyState == ConsistencyState.Invalidated)
                    return;
                _invalidated.Remove(value);
            }
        }
    }

    // IResult implementation

    public object? Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Output.Value;
    }

    public Exception? Error {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Output.Error;
    }

    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Output.HasValue;
    }

    public bool HasError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Output.HasError;
    }

    void IResult.Deconstruct(out object? untypedValue, out Exception? error)
    {
        untypedValue = Value;
        error = Error;
    }

    public object? GetUntypedValueOrErrorBox()
        => Error != null ? new ErrorBox(Error) : Value;

    // ToString & GetHashCode

    public override string ToString()
        => $"{GetType().GetName()}({Input} v.{Version.FormatVersion()}, State: {ConsistencyState})";

    public override int GetHashCode()
        => unchecked((int)Version);

    // GetValuePromise, UpdateUntyped & UseUntyped

    public Task GetValuePromise()
    {
        if (_untypedValuePromise != null)
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

    public Task UseUntyped(CancellationToken cancellationToken = default)
    {
        var context = ComputeContext.Current;
        if ((context.CallOptions & CallOptions.GetExisting) != 0) // Neither GetExisting nor Invalidate can be used here
            throw Errors.InvalidContextCallOptions(context.CallOptions);

        // Slightly faster version of this.TryUseExistingFromLock(context)
        if (this.IsConsistent()) {
            // It can become inconsistent here, but we don't care, since...
            ComputedImpl.UseNew(this, context);
            // it can also become inconsistent here & later, and UseNew handles this.
            // So overall, Use(...) guarantees the dependency chain will be there even
            // if computed is invalidated right after above "if".
            return GetValuePromise();
        }

        return Input.GetOrProduceValuePromise(context, cancellationToken);
    }

    // Invalidate

    void IGenericTimeoutHandler.OnTimeout()
        => Invalidate(true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Invalidate(bool immediately = false)
    {
        if (ConsistencyState == ConsistencyState.Invalidated)
            return;

        // Debug.WriteLine($"{nameof(Invalidate)}: {this}");
        lock (Lock) {
            var flags = _flags;
            switch (ConsistencyState) {
            case ConsistencyState.Invalidated:
                return;
            case ConsistencyState.Computing:
                flags |= ComputedFlags.InvalidateOnSetOutput;
                if (immediately)
                    flags |= ComputedFlags.InvalidateOnSetOutputImmediately;
                _flags = flags;
                return;
            default: // == ConsistencyState.Computed
                immediately |= Options.InvalidationDelay <= TimeSpan.Zero;
                if (immediately) {
                    ConsistencyState = ConsistencyState.Invalidated;
                    break;
                }

                if ((flags & ComputedFlags.DelayedInvalidationStarted) != 0)
                    return; // Already started

                _flags = flags | ComputedFlags.DelayedInvalidationStarted;
                break;
            }
        }

        if (!immediately) {
            // Delayed invalidation
            this.Invalidate(Options.InvalidationDelay);
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
                _dependants.Apply(default(Unit), static (_, usedByEntry) => {
                    var c = usedByEntry.Input.GetExistingComputed();
                    if (c != null && c.Version == usedByEntry.Version)
                        c.Invalidate(); // Invalidate doesn't throw - ever
                });
                _dependants.Clear();
            }
        }
        catch (Exception e) {
            // We should never throw errors during the invalidation
            try {
                var log = Input.Function.Services.LogFor(GetType());
                log.LogError(e, "Error while invalidating {Category}", Input.Category);
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
        ComputedFlags flags;
        lock (Lock) {
            if (ConsistencyState != ConsistencyState.Computing)
                return false;

            ConsistencyState = ConsistencyState.Consistent;
            _output = output;
            flags = Flags;
        }

        if ((flags & ComputedFlags.InvalidateOnSetOutput) != 0) {
            Invalidate((flags & ComputedFlags.InvalidateOnSetOutputImmediately) != 0);
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
        if (error == null) {
            timeout = Options.AutoInvalidationDelay;
            if (timeout != TimeSpan.MaxValue)
                this.Invalidate(timeout);
            return;
        }

        if (error is OperationCanceledException) {
            // This error requires instant invalidation
            Invalidate(true);
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
        var result = new Computed[_dependencies.Count];
        lock (Lock) {
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
            if (_lastKeepAliveSlot != keepAliveSlot) { // Fast check
                if (Interlocked.Exchange(ref _lastKeepAliveSlot, keepAliveSlot) != keepAliveSlot) // Slow check
                    Timeouts.KeepAlive.AddOrUpdateToLater(this, keepAliveSlot);
            }
        }
        ComputedRegistry.Instance.ReportAccess(this, isNew);
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
                if (c != null && c.Version == entry.Version)
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
        return transiencyResolver?.Invoke(error).IsTransient()
            ?? TransiencyResolvers.PreferTransient.Invoke(error).IsTransient();
    }
}
