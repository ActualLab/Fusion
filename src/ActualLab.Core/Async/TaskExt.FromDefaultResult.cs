namespace ActualLab.Async;

public static partial class TaskExt
{
    private static readonly ConcurrentDictionary<Type, Task> FromDefaultResultCache = new();
    private static readonly MethodInfo FromDefaultResultInternalMethod =
        typeof(TaskExt).GetMethod(nameof(FromDefaultResultInternal), BindingFlags.Static | BindingFlags.NonPublic)!;

    // FromDefaultResult

    public static Task FromDefaultResult(Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => (Task)FromDefaultResultInternalMethod
                .MakeGenericMethod(t)
                .Invoke(null, [])!);

    // Private methods

    private static Task FromDefaultResultInternal<T>()
        => Task.FromResult(default(T));
}
