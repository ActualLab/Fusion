using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public sealed class ComputeContext
{
    private static readonly AsyncLocal<ComputeContext?> CurrentLocal = new();
    private volatile IComputed? _captured;

    public static readonly ComputeContext None = new(default(CallOptions));
    public static readonly ComputeContext Invalidation = new(CallOptions.Invalidate);

    public static ComputeContext Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value ?? None;
        internal set {
            if (value == None)
                value = null!;
            CurrentLocal.Value = value;
        }
    }

    public readonly CallOptions CallOptions;
    public readonly IComputed? Computed;

    public bool IsCapturing {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (CallOptions & CallOptions.Capture) == CallOptions.Capture;
    }

    public bool IsInvalidating {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate;
    }

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(CallOptions callOptions)
        => CallOptions = callOptions;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(IComputed computed)
        => Computed = computed;

    // Conversion

    public override string ToString()
        => $"{GetType().GetName()}({CallOptions})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContextScope Activate()
        => new(this);

    // (Try)GetCaptured

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IComputed GetCaptured()
        => _captured ?? throw Errors.NoComputedCaptured();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed<T> GetCaptured<T>()
        => (Computed<T>)(_captured ?? throw Errors.NoComputedCaptured());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Option<IComputed> TryGetCaptured()
        => _captured is { } result ? Option.Some(result) : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Option<Computed<T>> TryGetCaptured<T>()
        => _captured is Computed<T> result ? Option.Some(result) : default;

    // Internal methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void TryCapture(IComputed computed)
    {
        if ((CallOptions & CallOptions.Capture) == 0)
            return;

        _captured = computed;
    }
}
