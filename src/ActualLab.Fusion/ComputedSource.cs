using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

#pragma warning disable CA1721
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

public interface IComputedSource : IComputeFunction
{
    public ComputedOptions ComputedOptions { get; }
    public Computed Computed { get; }
}

public class ComputedSource<T> : ComputedInput, IComputedSource, IEquatable<ComputedSource<T>>
{
    private volatile ComputedSourceComputed<T> _computed;
    private volatile Func<ComputedSource<T>, CancellationToken, ValueTask<T>>? _computer;
    private string? _category;

    protected AsyncLock AsyncLock { get; }
    protected object Lock => AsyncLock;
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; }

    public override string Category {
        get => _category ??= GetType().GetName();
        init => _category = value;
    }

    public ComputedOptions ComputedOptions { get; init; }
    public Func<ComputedSource<T>, CancellationToken, ValueTask<T>> Computer
        => _computer ?? throw new ObjectDisposedException(ToString());
    public event Action<ComputedSourceComputed<T>>? Invalidated;
    public event Action<ComputedSourceComputed<T>>? Updated;

    Computed IComputedSource.Computed => Computed;
    public ComputedSourceComputed<T> Computed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _computed;
        private set {
            lock (Lock) {
                var computed = _computed;
                if (computed == value)
                    return;

                computed.Invalidate();
                _computed = value;
                Updated?.Invoke(value);
            }
        }
    }

    public ComputedSource(
        IServiceProvider services,
        Func<ComputedSource<T>, CancellationToken, ValueTask<T>> computer,
        string? category = null)
        : this(services, default, computer, category)
    { }

    public ComputedSource(
        IServiceProvider services,
        Result<T> initialOutput,
        Func<ComputedSource<T>, CancellationToken, ValueTask<T>> computer,
        string? category = null)
    {
        Services = services;
        _computer = computer;
        _category = category;

        ComputedOptions = ComputedOptions.Default;
        AsyncLock = new AsyncLock(LockReentryMode.CheckedFail);
        Initialize(this, RuntimeHelpers.GetHashCode(this));
        lock (Lock)
            _computed = new ComputedSourceComputed<T>(ComputedOptions, this, initialOutput, false);
    }

    // ComputedInput

    public override ComputedOptions GetComputedOptions()
        => ComputedOptions;

    public override Computed? GetExistingComputed()
        => Computed;

    // Equality

    public bool Equals(ComputedSource<T>? other)
        => ReferenceEquals(this, other);
    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? other)
        => ReferenceEquals(this, other);

    // IComputedFunction

    FusionHub IComputeFunction.Hub => Services.GetRequiredService<FusionHub>();
    public Type OutputType => typeof(T);

    Task<Computed> IComputeFunction.ProduceComputed(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return Invoke(context, cancellationToken);
    }

    private async Task<Computed> Invoke(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = Computed;
        if (ComputedImpl.TryUseExistingFromLock(computed, context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        ComputedImpl.UseNew(computed, context);
        return computed;
    }

    // Private methods

    private async ValueTask<ComputedSourceComputed<T>> GetComputed(CancellationToken cancellationToken)
    {
        ComputedSourceComputed<T> computed;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            Computed = computed = new ComputedSourceComputed<T>(ComputedOptions, this);
            try {
                using var _ = Fusion.Computed.BeginCompute(computed);
                var value = await Computer.Invoke(this, cancellationToken).ConfigureAwait(false);
                computed.TrySetOutput(Result.NewUntyped(value));
                break;
            }
            catch (Exception e) {
                var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                    nameof(GetComputed), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                if (delayTask == SpecialTasks.MustThrow)
                    throw;
                if (delayTask == SpecialTasks.MustReturn)
                    break;
                await delayTask.ConfigureAwait(false);
            }
        }
        return computed;
    }

    internal void OnInvalidated(ComputedSourceComputed<T> computed)
    {
        try {
            Invalidated?.Invoke(computed);
        }
        catch (Exception e) {
            Log.LogError(e, "Invalidated handler failed for {Category}", Category);
        }
    }
}
