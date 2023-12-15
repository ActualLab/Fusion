using ActualLab.Internal;

namespace ActualLab.DependencyInjection;

public static class HasWhenDisposedExt
{
    public static void ThrowIfDisposedOrDisposing(this IHasWhenDisposed target)
    {
        if (target.WhenDisposed != null)
            throw Errors.AlreadyDisposedOrDisposing(target.GetType());
    }
}
