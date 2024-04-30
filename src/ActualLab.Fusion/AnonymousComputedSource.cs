using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

public interface IAnonymousComputedSource : IFunction
{
    ComputedOptions ComputedOptions { get; init; }
}

public class AnonymousComputedSource<T> : ComputedInput,
    IFunction<T>, IAnonymousComputedSource,
    IEquatable<AnonymousComputedSource<T>>,
    IDisposable
{
    private volatile AnonymousComputed<T>? _computed;
    private volatile Func<AnonymousComputedSource<T>, CancellationToken, ValueTask<T>>? _computer;
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
    public Func<AnonymousComputedSource<T>, CancellationToken, ValueTask<T>> Computer
        => _computer ?? throw new ObjectDisposedException(ToString());
    public event Action<AnonymousComputed<T>>? Invalidated;
    public event Action<AnonymousComputed<T>>? Updated;

    public bool IsComputed => _computed != null;
    public override bool IsDisposed => _computer == null;
    public AnonymousComputed<T> Computed {
        get => _computed ?? throw Errors.AnonymousComputedSourceIsNotComputedYet();
        private set {
            lock (Lock) {
                var computed = _computed;
                if (computed == value)
                    return;

                computed?.Invalidate();
                _computed = value;
                Updated?.Invoke(value);
            }
        }
    }

    public AnonymousComputedSource(
        IServiceProvider services,
        Func<AnonymousComputedSource<T>, CancellationToken, ValueTask<T>> computer,
        string? category = null)
    {
        Services = services;
        _computer = computer;
        _category = category;

        ComputedOptions = ComputedOptions.Default;
        AsyncLock = new AsyncLock(LockReentryMode.CheckedFail);
        Initialize(this, RuntimeHelpers.GetHashCode(this));
    }

    public void Dispose()
    {
        if (_computer == null)
            return;

        Computed<T>? computed;
        lock (Lock) {
            if (_computer == null)
                return;

            computed = _computed;
            _computer = null;
        }
        computed?.Invalidate();
    }

    // ComputedInput

    public override IComputed? GetExistingComputed()
        => _computed;

    // Update & Use

    public async ValueTask<Computed<T>> Update(CancellationToken cancellationToken = default)
    {
        using var scope = Fusion.Computed.BeginIsolation();
        return await Invoke(null, scope.Context, cancellationToken).ConfigureAwait(false);
    }

    public virtual async ValueTask<T> Use(CancellationToken cancellationToken = default)
    {
        var usedBy = ActualLab.Fusion.Computed.Current;
        var context = ComputeContext.Current;
        if ((context.CallOptions & CallOptions.GetExisting) != 0) // Both GetExisting & Invalidate
            throw Errors.InvalidContextCallOptions(context.CallOptions);

        var computed = _computed;
        if (computed?.IsConsistent() == true && computed.TryUseExistingFromLock(context, usedBy))
            return computed.Value;

        computed = (AnonymousComputed<T>) await Invoke(usedBy, context, cancellationToken).ConfigureAwait(false);
        return computed.Value;
    }

    // Equality

    public bool Equals(AnonymousComputedSource<T>? other)
        => ReferenceEquals(this, other);
    public override bool Equals(ComputedInput? other)
        => ReferenceEquals(this, other);
    public override bool Equals(object? other)
        => ReferenceEquals(this, other);
    public override int GetHashCode()
        => HashCode;

    // Private methods

    private async ValueTask<Computed<T>> Invoke(
        IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        context ??= ComputeContext.Current;

        var computed = _computed;
        if (computed.TryUseExisting(context, usedBy))
            return computed!;

        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        computed = _computed;
        if (computed.TryUseExistingFromLock(context, usedBy))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy);
        return computed;
    }

    private Task<T> InvokeAndStrip(
        IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        context ??= ComputeContext.Current;

        var result = _computed;
        return result.TryUseExisting(context, usedBy)
            ? result.StripToTask(context)
            : TryRecompute(usedBy, context, cancellationToken);
    }

    private async Task<T> TryRecompute(
        IComputed? usedBy, ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = _computed;
        if (computed.TryUseExistingFromLock(context, usedBy))
            return computed.Strip(context);

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        computed.UseNew(context, usedBy);
        return computed.Value;
    }

    private async ValueTask<AnonymousComputed<T>> GetComputed(CancellationToken cancellationToken)
    {
        AnonymousComputed<T> computed;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            Computed = computed = new AnonymousComputed<T>(ComputedOptions, this);
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

    internal void OnInvalidated(AnonymousComputed<T> computed)
    {
        try {
            Invalidated?.Invoke(computed);
        }
        catch (Exception e) {
            Log.LogError(e, "Invalidated handler failed for {Category}", Category);
        }
    }

    // IFunction<T> & IFunction

    ValueTask<Computed<T>> IFunction<T>.Invoke(
        ComputedInput input, IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return Invoke(usedBy, context, cancellationToken);
    }

    async ValueTask<IComputed> IFunction.Invoke(
        ComputedInput input, IComputed? usedBy, ComputeContext? context,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(input, this))
            // This "Function" supports just a single input == this
            throw new ArgumentOutOfRangeException(nameof(input));

        return await Invoke(usedBy, context, cancellationToken).ConfigureAwait(false);
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
}
