using ActualLab.Internal;

namespace ActualLab.DependencyInjection;

public static class HasWhenDisposedExt
{
    public static void ThrowIfDisposedOrDisposing(this IHasWhenDisposed target)
    {
        if (target.WhenDisposed is not null)
            throw Errors.AlreadyDisposedOrDisposing(target.GetType());
    }
}
