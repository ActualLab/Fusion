using ActualLab.Collections.Slim;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Resilience;
using ActualLab.Versioning;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion;

public interface IComputed : IResult, IHasVersion<ulong>
{
    ComputedOptions Options { get; }
    ComputedInput Input { get; }
    ConsistencyState ConsistencyState { get; }
    IResult Output { get; }
    Type OutputType { get; }
    event Action<Computed> Invalidated;

    void Invalidate(bool immediately = false);

    ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default);
    ValueTask UseUntyped(CancellationToken cancellationToken = default);

    TResult Apply<TArg, TResult>(IComputedApplyHandler<TArg, TResult> handler, TArg arg);
}

public abstract partial class Computed(ComputedOptions options, ComputedInput input)
    : IComputed, IGenericTimeoutHandler
{
    private volatile int _state;
    private volatile ComputedFlags _flags;
    private volatile int _lastKeepAliveSlot;
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

    public ConsistencyState ConsistencyState {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ConsistencyState)_state;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal set => _state = (int)value;
    }

    public IResult Output {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this;
    }

    public abstract Type OutputType { get; }

    // IComputed implementation
    ComputedOptions IComputed.Options => Options;
    ComputedInput IComputed.Input => Input;
    ulong IHasVersion<ulong>.Version => Version;

    // IResult implementation
    public abstract bool HasValue { get; }
    public abstract object? UntypedValue { get; }
    public abstract bool HasError { get; }
    public abstract Exception? Error { get; }
    public abstract Result<TOther> Cast<TOther>();

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

    // Invalidation

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

    protected virtual void OnInvalidated()
        => CancelTimeouts();

    // Update & Use

    public abstract ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default);
    public abstract ValueTask UseUntyped(CancellationToken cancellationToken = default);

    // Handy helper: Apply

    public abstract TResult Apply<TArg, TResult>(IComputedApplyHandler<TArg, TResult> handler, TArg arg);

    // Protected internal methods - you can call them via ComputedImpl

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal virtual void InvalidateFromCall()
        => Invalidate();

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
