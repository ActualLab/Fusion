using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IComputedSource : IComputeFunction
{
    ComputedOptions ComputedOptions { get; init; }
    IComputed Computed { get; }
}

public class ComputedSource<T> : ComputedInput,
    IComputeFunction<T>, IComputedSource,
    IEquatable<ComputedSource<T>>
{
    private volatile ComputedSourceComputed<T> _computed;
    private volatile Func<ComputedSource<T>, CancellationToken, ValueTask<T>>? _computer;
    private string? _category;
    private ILogger? _log;

    protected AsyncLock AsyncLock { get; }
    protected object Lock => AsyncLock;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

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

    IComputed IComputedSource.Computed => Computed;
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

    public override IComputed? GetExistingComputed()
        => Computed;

    // Equality

    public bool Equals(ComputedSource<T>? other)
        => ReferenceEquals(this, other);
    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? other)
        => ReferenceEquals(this, other);
    public override int GetHashCode()
        => HashCode;

    // IFunction<T> & IFunction

    ValueTask<Computed<T>> IComputeFunction<T>.Invoke(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return Invoke(context, cancellationToken);
    }

    private async ValueTask<Computed<T>> Invoke(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        var computed = Computed;
        if (computed.TryUseExisting(context))
            return computed!;

        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        computed = Computed;
        if (computed.TryUseExistingFromLock(context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed;
    }

    Task<T> IComputeFunction<T>.InvokeAndStrip(
        ComputedInput input,
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return InvokeAndStrip(context, cancellationToken);
    }

    private Task<T> InvokeAndStrip(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        var result = Computed;
        return result.TryUseExisting(context)
            ? result.StripToTask(context)
            : TryRecompute(context, cancellationToken);
    }

    // Private methods

    private async Task<T> TryRecompute(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = Computed;
        if (computed.TryUseExistingFromLock(context))
            return computed.Strip(context);

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context);
        return computed.Value;
    }

    private async ValueTask<ComputedSourceComputed<T>> GetComputed(CancellationToken cancellationToken)
    {
        ComputedSourceComputed<T> computed;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            Computed = computed = new ComputedSourceComputed<T>(ComputedOptions, this);
            using var _ = Fusion.Computed.BeginCompute(computed);
            try {
                var value = await Computer.Invoke(this, cancellationToken).ConfigureAwait(false);
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

                var cancellationReprocessingOptions = ComputedOptions.CancellationReprocessing;
                if (e is not OperationCanceledException
                    || ++tryIndex > cancellationReprocessingOptions.MaxTryCount
                    || startedAt.Elapsed > cancellationReprocessingOptions.MaxDuration) {
                    computed.TrySetOutput(Result.Error<T>(e));
                    break;
                }

                computed.Invalidate(true); // Instant invalidation on cancellation
                computed.TrySetOutput(Result.Error<T>(e));
                var delay = cancellationReprocessingOptions.RetryDelays[tryIndex];
                Log.LogWarning(e,
                    "GetComputed #{TryIndex} for {Category} was cancelled internally, retry in {Delay}",
                    tryIndex, Category, delay.ToShortString());
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
