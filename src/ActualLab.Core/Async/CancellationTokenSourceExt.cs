using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Async;

public static class CancellationTokenSourceExt
{
#if USE_UNSAFE_ACCESSORS && NET8_0_OR_GREATER
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_disposed")]
    private static extern ref bool IsDisposedGetter(CancellationTokenSource @this);
#else
    private static readonly Func<CancellationTokenSource, bool> IsDisposedGetter;

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CancellationTokenSource))]
    static CancellationTokenSourceExt()
    {
        var tCts = typeof(CancellationTokenSource);
#pragma warning disable IL2026
        var fIsDisposed =
            tCts.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? tCts.GetField("m_disposed", BindingFlags.Instance | BindingFlags.NonPublic);
        IsDisposedGetter = fIsDisposed!.GetGetter<CancellationTokenSource, bool>();
#pragma warning restore IL2026
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDisposed(this CancellationTokenSource cancellationTokenSource)
        => IsDisposedGetter(cancellationTokenSource);

    public static void CancelAndDisposeSilently(this CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource == null)
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
        if (cancellationTokenSource == null)
            return;

        try {
            cancellationTokenSource.Dispose();
        }
        catch {
            // Intended
        }
    }
}
