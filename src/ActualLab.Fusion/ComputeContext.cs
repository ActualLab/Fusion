using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public sealed class ComputeContext
{
    private static readonly AsyncLocal<ComputeContext?> CurrentLocal = new();
    private volatile Computed? _captured;

    public static readonly ComputeContext None = new(default(CallOptions));
    public static readonly ComputeContext Invalidating = new(CallOptions.Invalidate);

    public static ComputeContext Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value ?? None;
        internal set => CurrentLocal.Value = ReferenceEquals(value, None) ? null : value;
    }

    public readonly CallOptions CallOptions;
    public readonly Computed? Computed;

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(CallOptions callOptions)
        => CallOptions = callOptions;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(Computed computed)
        => Computed = computed;

    // Conversion

    public override string ToString()
        => $"{GetType().GetName()}({CallOptions})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContextScope Activate()
        => new(this);

    // (Try)GetCaptured

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed GetCaptured()
        => _captured ?? throw Errors.NoComputedCaptured();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed<T> GetCaptured<T>()
        => (Computed<T>)(_captured ?? throw Errors.NoComputedCaptured());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed? TryGetCaptured()
        => _captured;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed<T>? TryGetCaptured<T>()
        => _captured as Computed<T> ?? default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TryCapture(Computed computed)
    {
        if ((CallOptions & CallOptions.Capture) == 0)
            return;

        // The logic below always "overwrites" captured computed - we assume that:
        // - ComputedHelpers.TryUseExisting & Use are the only methods capturing the computed,
        //   and they're called at the end of computation, i.e. when we effectively know the
        //   exact IComputed we want to capture. They're never called for temporary computed instances.
        // - Computed.BeginCompute(computed) wraps any Computed computation, and it is responsible
        //   for creating a new ComputeContext, so dependencies cannot be captured by subsequent calls
        //   of TryCompute happening in chains like "ComputeX -> ComputeDependencyOfX".
        _captured = computed;
    }
}
