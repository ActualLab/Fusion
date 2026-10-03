using ActualLab.Fusion.Internal;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion;

/// <summary>
/// Tracks the current compute call context, including call options and captured computed instances.
/// </summary>
public sealed class ComputeContext
{
    public static readonly ComputeContext None = new(default(CallOptions));

    private static readonly AsyncLocal<ComputeContext?> CurrentLocal = new();

    private Computed? _captured;
    // Owned by whoever created this context: that's how the captured calls are read back
    private readonly List<ServiceCall>? _capturedInvalidations;

    public readonly InvalidationSource InvalidationSource;

    public static ComputeContext Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value ?? None;
        internal set => CurrentLocal.Value = ReferenceEquals(value, None) ? null : value;
    }

    public readonly CallOptions CallOptions;
    public readonly Computed? Computed;

    // Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(Computed computed)
        => Computed = computed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(CallOptions callOptions)
        => CallOptions = callOptions;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(InvalidationSource invalidationSource)
        : this(CallOptions.Invalidate, invalidationSource)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(CallOptions callOptions, InvalidationSource invalidationSource)
    {
        CallOptions = callOptions;
        InvalidationSource = invalidationSource;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContext(List<ServiceCall> capturedInvalidations)
    {
        CallOptions = CallOptions.CaptureInvalidation;
        _capturedInvalidations = capturedInvalidations;
    }

    // Conversion

    public override string ToString()
        => $"{GetType().GetName()}({CallOptions})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputeContextScope Activate()
        => new(this);

    // CaptureInvalidation

    public void CaptureInvalidation(ServiceCall invalidation)
    {
        if (_capturedInvalidations is not { } capturedCalls)
            throw Errors.ComputeContextDoesNotCaptureInvalidation(CallOptions);

        lock (capturedCalls)
            capturedCalls.Add(invalidation);
    }

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
        if (!CallOptions.HasFlag(CallOptions.Capture))
            return;

        // The logic below always "overwrites" captured computed - we assume that:
        // - ComputedHelpers.TryUseExisting & Use are the only methods capturing the computed,
        //   and they're called at the end of computation, i.e. when we effectively know the
        //   exact IComputed we want to capture. They're never called for temporary computed instances.
        // - Computed.BeginCompute(computed) wraps any Computed computation, and it is responsible
        //   for creating a new ComputeContext, so dependencies cannot be captured by subsequent calls
        //   of TryCompute happening in chains like "ComputeX -> ComputeDependencyOfX".
        // Release: the (Try)GetCaptured methods above read _captured plainly
        Volatile.Write(ref _captured, computed);
    }
}
