using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

#pragma warning disable CA1721

public partial class Computed
{
    private static LazySlim<IServiceProvider> _defaultServices = new(
        () => new ServiceCollection().AddFusion().AddFusionTime().Services.BuildServiceProvider());

    public static TimeSpan PreciseInvalidationDelayThreshold { get; set; } = TimeSpan.FromSeconds(1);

    public static IServiceProvider DefaultServices {
        get => _defaultServices.Value;
        set => _defaultServices = new LazySlim<IServiceProvider>(value);
    }

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

    // New

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Computed<T> New<T>(
        Func<CancellationToken, Task<T>> compute)
        => new ComputedSource<T>(DefaultServices, (_, ct) => compute.Invoke(ct)).Computed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Computed<T> New<T>(
        Result<T> initialOutput,
        Func<CancellationToken, Task<T>> compute)
        => new ComputedSource<T>(DefaultServices, initialOutput, (_, ct) => compute.Invoke(ct)).Computed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Computed<T> New<T>(
        IServiceProvider services,
        Func<CancellationToken, Task<T>> compute)
        => new ComputedSource<T>(services, (_, ct) => compute.Invoke(ct)).Computed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Computed<T> New<T>(
        IServiceProvider services,
        Result<T> initialOutput,
        Func<CancellationToken, Task<T>> compute)
        => new ComputedSource<T>(services, initialOutput, (_, ct) => compute.Invoke(ct)).Computed;

    // TryCapture

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed?> TryCapture(
        Func<Task> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var computed = ccs.Context.TryGetCaptured();
            if (computed is { HasError: true })
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed<T>?> TryCapture<T>(
        Func<Task<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var computed = ccs.Context.TryGetCaptured<T>();
            if (computed is { HasError: true })
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed?> TryCapture(
        Func<ValueTask> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var computed = ccs.Context.TryGetCaptured();
            if (computed is { HasError: true })
                return computed; // Return the original error, if possible
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async ValueTask<Computed<T>?> TryCapture<T>(
        Func<ValueTask<T>> producer,
        CancellationToken cancellationToken = default)
    {
        using var ccs = BeginCapture();
        try {
            await producer.Invoke().ConfigureAwait(false);
            return ccs.Context.TryGetCaptured<T>();
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            var computed = ccs.Context.TryGetCaptured<T>();
            if (computed is { HasError: true })
                return computed; // Return the original error, if possible
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
            var computed = ccs.Context.TryGetCaptured();
            if (computed is { HasError: true })
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
            var computed = ccs.Context.TryGetCaptured<T>();
            if (computed is { HasError: true })
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
            var computed = ccs.Context.TryGetCaptured();
            if (computed is { HasError: true })
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
            var computed = ccs.Context.TryGetCaptured<T>();
            if (computed is { HasError: true })
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
#pragma warning disable CA2025 // Ensure tasks using 'IDisposable' instances complete before the instances are disposed
        _ = task.AssertCompleted(); // Compute method calls are always synchronous in this case
#pragma warning restore CA2025
        return ccs.Context.TryGetCaptured<T>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Computed<T>? GetExisting<T>(Func<ValueTask<T>> producer)
    {
        using var ccs = BeginCaptureExisting();
        var task = producer.Invoke();
#pragma warning disable CA2025 // Ensure tasks using 'IDisposable' instances complete before the instances are disposed
        _ = task.AssertCompleted(); // Compute method calls are always synchronous in this case
#pragma warning restore CA2025
        return ccs.Context.TryGetCaptured<T>();
    }
}
