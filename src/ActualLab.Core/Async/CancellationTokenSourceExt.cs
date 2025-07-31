using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Async;

public static class CancellationTokenSourceExt
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_disposed")]
    private static extern ref bool IsDisposedGetter(CancellationTokenSource @this);
#else
    private static readonly Func<CancellationTokenSource, bool> IsDisposedGetter;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "See DynamicDependency below")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "See DynamicDependency below")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CancellationTokenSource))]
    static CancellationTokenSourceExt()
    {
        var tCts = typeof(CancellationTokenSource);
        var fIsDisposed =
            tCts.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? tCts.GetField("m_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
        IsDisposedGetter = fIsDisposed!.GetGetter<CancellationTokenSource, bool>();
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDisposed(this CancellationTokenSource cancellationTokenSource)
        => IsDisposedGetter(cancellationTokenSource);

    public static void CancelAndDisposeSilently(this CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
            return;

        try {
            if (cancellationTokenSource.IsCancellationRequested || IsDisposedGetter(cancellationTokenSource))
                return;

            cancellationTokenSource.Cancel();
        }
        catch {
            // Intended
        }
        finally {
            cancellationTokenSource.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeSilently(this CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
            return;

        try {
            cancellationTokenSource.Dispose();
        }
        catch {
            // Intended
        }
    }
}
