using ActualLab.Caching;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

#pragma warning disable CA1721
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

public interface IComputedSource : IComputeFunction
{
    public ComputedOptions ComputedOptions { get; }
    public Func<ComputedSource, CancellationToken, Task> Computer { get; }
    public Computed Computed { get; }

    public event Action<Computed>? Invalidated;
    public event Action<Computed>? Updated;
}

public interface IComputedSource<T> : IComputedSource
{
    public new ComputedSourceComputed<T> Computed { get; }
}

public abstract class ComputedSource : ComputedInput, IComputedSource
{
    private volatile Func<ComputedSource, CancellationToken, Task> _computer;
    private volatile Computed _computed;

    protected Func<Task, object?> GetTaskResultAsObjectSynchronously
        => field ??= GenericInstanceCache.GetUnsafe<Func<Task, object?>>(
            typeof(TaskExt.GetResultAsObjectSynchronouslyFactory<>), OutputType);

    protected AsyncLock AsyncLock { get; }
    protected object Lock => AsyncLock;
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; }

    public override string Category {
        get => field ??= GetType().GetName();
        init;
    }

    public ComputedOptions ComputedOptions {
        get;
        init => field = value.IsConsolidating
            ? throw new ArgumentOutOfRangeException(nameof(value))
            : value;
    }

    public abstract Type OutputType { get; }

    public Func<ComputedSource, CancellationToken, Task> Computer
        => _computer ?? throw new ObjectDisposedException(ToString());

    public Computed Computed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _computed;
    }

    public event Action<Computed>? Invalidated;
    public event Action<Computed>? Updated;

    protected ComputedSource(
        IServiceProvider services,
        Func<ComputedSource, CancellationToken, Task> computer,
        Result initialOutput,
        string? category = null)
    {
        Services = services;
        _computer = computer;
        // ReSharper disable once VirtualMemberCallInConstructor
        Category = category!;

        ComputedOptions = ComputedOptions.Default;
        AsyncLock = new AsyncLock(LockReentryMode.CheckedFail);
        Initialize(this, RuntimeHelpers.GetHashCode(this));
        lock (Lock)
#pragma warning disable CA2214 // Do not call overridable methods in constructors
            // ReSharper disable once VirtualMemberCallInConstructor
            _computed = CreateComputed(initialOutput);
#pragma warning restore CA2214
    }

    // ComputedInput

    public override ComputedOptions GetComputedOptions()
        => ComputedOptions;

    public override Computed? GetExistingComputed()
        => Computed;

    // Equality

    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? other)
        => ReferenceEquals(this, other);

    // IComputedFunction

    FusionHub IComputeFunction.Hub => Services.GetRequiredService<FusionHub>();

    Task<Computed> IComputeFunction.ProduceComputed(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return ProduceComputed(context, cancellationToken);
    }

    // Protected methods

    protected abstract Computed CreateComputed(Result? initialOutput = null);

    private void SetComputed(Computed computed, InvalidationSource source)
    {
        lock (Lock) {
            var oldComputed = _computed;
            if (oldComputed == computed)
                return;

            oldComputed.Invalidate(immediately: true, source);
            _computed = computed;
            Updated?.Invoke(computed);
        }
    }

    protected async Task<Computed> ProduceComputed(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = Computed;
        if (ComputedImpl.TryUseExistingFromLock(computed, context))
            return computed!;

        releaser.MarkLockedLocally(unmarkOnRelease: false);
        computed = await ProduceComputedFromLock(cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed;
    }

    protected async Task<Computed> ProduceComputedFromLock(CancellationToken cancellationToken)
    {
        Computed computed;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            SetComputed(computed = CreateComputed(), InvalidationSource.ComputedSourceProduce);
            try {
                using var _ = Computed.BeginCompute(computed);
                var computeTask = Computer.Invoke(this, cancellationToken);
                await computeTask.ConfigureAwait(false);
                var value = GetTaskResultAsObjectSynchronously(computeTask);
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
        return computed;
    }

    internal void OnInvalidated(Computed computed)
    {
        try {
            Invalidated?.Invoke(computed);
        }
        catch (Exception e) {
            Log.LogError(e, "Invalidated handler failed for {Category}", Category);
        }
    }
}

public sealed class ComputedSource<T> : ComputedSource, IComputedSource<T>
{
    public override Type OutputType => typeof(T);

    public new ComputedSourceComputed<T> Computed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ComputedSourceComputed<T>)base.Computed;
    }

    public ComputedSource(
        IServiceProvider services,
        Func<ComputedSource, CancellationToken, Task<T>> computer,
        string? category = null)
        : this(services, default, computer, category)
    { }

    // ReSharper disable once ConvertToPrimaryConstructor
    public ComputedSource(
        IServiceProvider services,
        Result<T> initialOutput,
        Func<ComputedSource, CancellationToken, Task<T>> computer,
        string? category = null)
        : base(services, computer, initialOutput.ToUntypedResult(), category)
    { }

    protected override Computed CreateComputed(Result? initialOutput = null)
        => initialOutput.HasValue
            ? new ComputedSourceComputed<T>(ComputedOptions, this, initialOutput.GetValueOrDefault(), false)
            : new ComputedSourceComputed<T>(ComputedOptions, this);
}
