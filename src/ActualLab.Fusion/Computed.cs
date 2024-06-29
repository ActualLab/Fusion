using System.Diagnostics.CodeAnalysis;
using ActualLab.Collections.Slim;
using ActualLab.Conversion;
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
    Task OutputAsTask { get; }
    event Action<ComputedBase> Invalidated;

    void Invalidate(bool immediately = false);

    ValueTask<ComputedBase> UpdateUntyped(CancellationToken cancellationToken = default);
    ValueTask UseUntyped(CancellationToken cancellationToken = default);

    TResult Apply<TArg, TResult>(IComputedApplyHandler<TArg, TResult> handler, TArg arg);
}

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public abstract partial class ComputedBase(ComputedOptions options, ComputedInput input) : IComputed, IGenericTimeoutHandler
{
    private volatile int _state;
    private volatile ComputedFlags _flags;
    private long _lastKeepAliveSlot;
    private RefHashSetSlim3<ComputedBase> _used;
    private HashSetSlim3<(ComputedInput Input, ulong Version)> _usedBy;
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

    public IResult Output => this;
    public abstract Type OutputType { get; }
    public abstract Task OutputAsTask { get; }

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

    public event Action<ComputedBase> Invalidated {
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
                _used.Apply(this, (self, c) => c.RemoveUsedBy(self));
                _used.Clear();
                _usedBy.Apply(default(Unit), static (_, usedByEntry) => {
                    var c = usedByEntry.Input.GetExistingComputed();
                    if (c != null && c.Version == usedByEntry.Version)
                        c.Invalidate(); // Invalidate doesn't throw - ever
                });
                _usedBy.Clear();
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

    public abstract ValueTask<ComputedBase> UpdateUntyped(CancellationToken cancellationToken = default);
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

    protected internal ComputedBase[] GetUsed()
    {
        var result = new ComputedBase[_used.Count];
        lock (Lock) {
            _used.CopyTo(result);
            return result;
        }
    }

    protected internal (ComputedInput Input, ulong Version)[] GetUsedBy()
    {
        var result = new (ComputedInput Input, ulong Version)[_usedBy.Count];
        lock (Lock) {
            _usedBy.CopyTo(result);
            return result;
        }
    }

    protected internal void RenewTimeouts(bool isNew)
    {
        if (ConsistencyState == ConsistencyState.Invalidated)
            return; // We shouldn't register miss here, since it's going to be counted later anyway

        var minCacheDuration = Options.MinCacheDuration;
        if (minCacheDuration != default) {
            var keepAliveSlot = Timeouts.GetKeepAliveSlot(Timeouts.Clock.Now + minCacheDuration);
            var lastKeepAliveSlot = Interlocked.Exchange(ref _lastKeepAliveSlot, keepAliveSlot);
            if (lastKeepAliveSlot != keepAliveSlot)
                Timeouts.KeepAlive.AddOrUpdateToLater(this, keepAliveSlot);
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

    protected internal void AddUsed(ComputedBase used)
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
            if (used.AddUsedBy(this))
                _used.Add(used);
        }
    }

    // Should be called only from AddUsed
    private bool AddUsedBy(ComputedBase usedBy)
    {
        lock (Lock) {
            switch (ConsistencyState) {
            case ConsistencyState.Computing:
                throw Errors.WrongComputedState(ConsistencyState);
            case ConsistencyState.Invalidated:
                usedBy.Invalidate();
                return false;
            }

            var usedByRef = (usedBy.Input, usedBy.Version);
            _usedBy.Add(usedByRef);
            return true;
        }
    }

    protected internal void RemoveUsedBy(ComputedBase usedBy)
    {
        lock (Lock) {
            if (ConsistencyState == ConsistencyState.Invalidated)
                // _usedBy is already empty or going to be empty soon;
                // moreover, only Invalidated code can modify
                // _used/_usedBy once invalidation flag is set
                return;

            _usedBy.Remove((usedBy.Input, usedBy.Version));
        }
    }

    protected internal (int OldCount, int NewCount) PruneUsedBy()
    {
        lock (Lock) {
            if (ConsistencyState != ConsistencyState.Consistent)
                // _usedBy is already empty or going to be empty soon;
                // moreover, only Invalidated code can modify
                // _used/_usedBy once invalidation flag is set
                return (0, 0);

            var replacement = new HashSetSlim3<(ComputedInput Input, ulong Version)>();
            var oldCount = _usedBy.Count;
            foreach (var entry in _usedBy.Items) {
                var c = entry.Input.GetExistingComputed();
                if (c != null && c.Version == entry.Version)
                    replacement.Add(entry);
            }
            _usedBy = replacement;
            return (oldCount, _usedBy.Count);
        }
    }

    protected internal void CopyUsedTo(ref ArrayBuffer<ComputedBase> buffer)
    {
        lock (Lock) {
            var count = buffer.Count;
            buffer.EnsureCapacity(count + _used.Count);
            _used.CopyTo(buffer.Buffer.AsSpan(count));
        }
    }

    protected internal bool IsTransientError(Exception error)
    {
        if (error is OperationCanceledException)
            return true; // Must be transient under any circumstances in IComputed

        TransiencyResolver<ComputedBase>? transiencyResolver = null;
        try {
            var services = Input.Function.Services;
            transiencyResolver = services.GetService<TransiencyResolver<ComputedBase>>();
        }
        catch (ObjectDisposedException) {
            // We want to handle IServiceProvider disposal gracefully
        }
        return transiencyResolver?.Invoke(error).IsTransient()
            ?? TransiencyResolvers.PreferTransient.Invoke(error).IsTransient();
    }
}

public abstract class Computed<T> : ComputedBase, IResult<T>
{
    private Result<T> _output;
    private Task<T>? _outputAsTask;

    // IComputed properties

    public IComputeFunction<T> Function => (IComputeFunction<T>)Input.Function;

    public sealed override Type OutputType {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => typeof(T);
    }

    public new Result<T> Output {
        get {
            this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
            return _output;
        }
    }

    public sealed override Task<T> OutputAsTask {
        get {
            if (_outputAsTask != null)
                return _outputAsTask;

            lock (Lock) {
                this.AssertConsistencyStateIsNot(ConsistencyState.Computing);
                return _outputAsTask ??= _output.AsTask();
            }
        }
    }

    // IResult<T> properties
    public T? ValueOrDefault => Output.ValueOrDefault;
    public T Value => Output.Value;
    public sealed override Exception? Error => Output.Error;
    public sealed override bool HasValue => Output.HasValue;
    public sealed override bool HasError => Output.HasError;
    public sealed override object? UntypedValue => Output.Value;
    public sealed override Result<TOther> Cast<TOther>()
        => Output.Cast<TOther>();

    // IResult<T> methods

    public bool IsValue([MaybeNullWhen(false)] out T value)
        => Output.IsValue(out value);
    public bool IsValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out Exception error)
        => Output.IsValue(out value, out error!);
    public Result<T> AsResult()
        => Output.AsResult();
    T IConvertibleTo<T>.Convert() => Value;
    Result<T> IConvertibleTo<Result<T>>.Convert() => AsResult();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input)
        : base(options, input)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected Computed(ComputedOptions options, ComputedInput input, Result<T> output, bool isConsistent)
        : base(options, input)
    {
        ConsistencyState = isConsistent ? ConsistencyState.Consistent : ConsistencyState.Invalidated;
        _output = output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Output.Deconstruct(out value, out error);

    public void Deconstruct(out T value, out Exception? error, out ulong version)
    {
        Output.Deconstruct(out value, out error);
        version = Version;
    }

    public override string ToString()
        => $"{GetType().GetName()}({Input} v.{Version.FormatVersion()}, State: {ConsistencyState})";

    // GetHashCode

    public override int GetHashCode() => (int)Version;

    // Update & use

    public sealed override async ValueTask<ComputedBase> UpdateUntyped(CancellationToken cancellationToken = default)
        => await Update(cancellationToken).ConfigureAwait(false);

    public async ValueTask<Computed<T>> Update(CancellationToken cancellationToken = default)
    {
        if (this.IsConsistent())
            return this;

        using var scope = Computed.BeginIsolation();
        return await Function.Invoke(Input, scope.Context, cancellationToken).ConfigureAwait(false);
    }

    public sealed override async ValueTask UseUntyped(CancellationToken cancellationToken = default)
        => await Use(cancellationToken).ConfigureAwait(false);

    public async ValueTask<T> Use(CancellationToken cancellationToken = default)
    {
        var context = ComputeContext.Current;
        if ((context.CallOptions & CallOptions.GetExisting) != 0) // Both GetExisting & Invalidate
            throw Errors.InvalidContextCallOptions(context.CallOptions);

        // Slightly faster version of this.TryUseExistingFromLock(context)
        if (this.IsConsistent()) {
            // It can become inconsistent here, but we don't care, since...
            ComputedHelpers.UseNew(this, context);
            // it can also become inconsistent here & later, and UseNew handles this.
            // So overall, Use(...) guarantees the dependency chain will be there even
            // if computed is invalidated right after above "if".
            return Value;
        }

        var computed = await Function.Invoke(Input, context, cancellationToken).ConfigureAwait(false);
        return computed.Value;
    }

    // Apply

    public override TResult Apply<TArg, TResult>(IComputedApplyHandler<TArg, TResult> handler, TArg arg)
        => handler.Apply(this, arg);

    // Protected internal methods - you can call them via ComputedImpl

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected internal bool TrySetOutput(Result<T> output)
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
}
