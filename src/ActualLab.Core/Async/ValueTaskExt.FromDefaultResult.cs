namespace ActualLab.Async;

public static partial class ValueTaskExt
{
    private static readonly ConcurrentDictionary<Type, object> FromDefaultResultCache = new();
    private static readonly MethodInfo FromDefaultResultInternalMethod =
        typeof(ValueTaskExt).GetMethod(nameof(FromDefaultResultInternal), BindingFlags.Static | BindingFlags.NonPublic)!;

    // FromDefaultResult

    public static object FromDefaultResult(Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => FromDefaultResultInternalMethod
                .MakeGenericMethod(t)
                .Invoke(null, [])!);

    // Private methods

    private static object FromDefaultResultInternal<T>()
        => default(ValueTask<T>);
}
