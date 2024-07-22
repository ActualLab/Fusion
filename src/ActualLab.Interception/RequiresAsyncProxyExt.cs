using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public static class RequiresAsyncProxyExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TProxy RequireProxy<TProxy>(this IRequiresAsyncProxy? source)
        => source is TProxy expected
            ? expected
            : throw Errors.InvalidProxyType(source?.GetType(), typeof(TProxy));
}
