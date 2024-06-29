using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1721

public partial class Computed
{
    public static TimeSpan PreciseInvalidationDelayThreshold { get; set; } = TimeSpan.FromSeconds(1);

    // Current & GetCurrent

    public static Computed? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComputeContext.Current.Computed;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Computed GetCurrent()
        => Current ?? throw Errors.CurrentComputedIsNull();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Computed<T> GetCurrent<T>()
        => (Computed<T>)(Current ?? throw Errors.CurrentComputedIsNull());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope BeginCompute(Computed computed)
        => new(new ComputeContext(computed));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope BeginIsolation()
        => new(ComputeContext.None);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope BeginCapture()
        => new(new ComputeContext(CallOptions.Capture));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope BeginCaptureExisting()
        => new(new ComputeContext(CallOptions.Capture | CallOptions.GetExisting));

    // TryCapture

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Option<Computed>> TryCapture(
        Func<Task> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured();
            if (result.IsSome(out var computed) && computed.HasError)
                return result; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Option<Computed<T>>> TryCapture<T>(
        Func<Task<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured<T>();
            if (result.IsSome(out var computed) && computed.HasError)
                return result; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Option<Computed>> TryCapture(
        Func<ValueTask> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured();
            if (result.IsSome(out var computed) && computed.HasError)
                return result; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Option<Computed<T>>> TryCapture<T>(
        Func<ValueTask<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured<T>();
            if (result.IsSome(out var computed) && computed.HasError)
                return result; // Return the original error, if possible
            throw;
        }
    }

    // Capture

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed> Capture(
        Func<Task> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.GetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured();
            if (result.IsSome(out var computed) && computed.HasError)
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed<T>> Capture<T>(
        Func<Task<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.GetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured<T>();
            if (result.IsSome(out var computed) && computed.HasError)
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed> Capture(
        Func<ValueTask> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.GetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured();
            if (result.IsSome(out var computed) && computed.HasError)
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed<T>> Capture<T>(
        Func<ValueTask<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.GetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var result = ccs.Context.TryGetCaptured<T>();
            if (result.IsSome(out var computed) && computed.HasError)
                return computed; // Return the original error, if possible
            throw;
        }
    }

    // GetExisting

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Computed<T>? GetExisting<T>(Func<Task<T>> producer)
    {
        using var ccs = BeginCaptureExisting();
        var task = producer.Invoke();
        _ = task.AssertCompleted(); // The must be always synchronous in this case
        return ccs.Context.TryGetCaptured<T>().ValueOrDefault;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Computed<T>? GetExisting<T>(Func<ValueTask<T>> producer)
    {
        using var ccs = BeginCaptureExisting();
        var task = producer.Invoke();
        _ = task.AssertCompleted(); // The must be always synchronous in this case
        return ccs.Context.TryGetCaptured<T>().ValueOrDefault;
    }
}
