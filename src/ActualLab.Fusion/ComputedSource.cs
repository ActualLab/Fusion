using System.Runtime.ExceptionServices;
using ActualLab.Fusion.Internal;
using ActualLab.Locking;

namespace ActualLab.Fusion;

#pragma warning disable CA1721

public interface IComputedSource : IComputeFunction
{
    ComputedOptions ComputedOptions { get; init; }
    ComputedBase Computed { get; }
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

    ComputedBase IComputedSource.Computed => Computed;
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

    public override ComputedBase? GetExistingComputed()
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

    FusionInternalHub IComputeFunction.Hub => Services.GetRequiredService<FusionInternalHub>();

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
        if (ComputedHelpers.TryUseExisting(computed, context))
            return computed!;

        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        computed = Computed;
        if (ComputedHelpers.TryUseExistingFromLock(computed, context))
            return computed!;

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        ComputedHelpers.UseNew(computed, context);
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
        return ComputedHelpers.TryUseExisting(result, context)
            ? ComputedHelpers.StripToTask(result, context)
            : TryRecompute(context, cancellationToken);
    }

    // Private methods

    private async Task<T> TryRecompute(
        ComputeContext context,
        CancellationToken cancellationToken)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);

        var computed = Computed;
        if (ComputedHelpers.TryUseExistingFromLock(computed, context))
            return ComputedHelpers.Strip(computed, context);

        releaser.MarkLockedLocally();
        computed = await GetComputed(cancellationToken).ConfigureAwait(false);
        ComputedHelpers.UseNew(computed, context);
        return computed.Value;
    }

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
                computed.TrySetOutput(Result.New(value));
                break;
            }
            catch (Exception e) {
                var delayTask = ComputedHelpers.TryReprocessInternalCancellation(
                    nameof(GetComputed), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                if (delayTask == SpecialTasks.MustThrow)
                    throw;
                if (delayTask == SpecialTasks.MustReturn)
                    return computed;
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
