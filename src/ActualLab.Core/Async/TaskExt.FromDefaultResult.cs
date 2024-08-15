namespace ActualLab.Async;

public static partial class TaskExt
{
    private static readonly ConcurrentDictionary<Type, Task> FromDefaultResultCache = new();

    // FromDefaultResult

    public static Task FromDefaultResult(Type resultType)
        => FromDefaultResultCache.GetOrAdd(resultType,
            static t => {
                // ReSharper disable once UseCollectionExpression
                var type = typeof(Task<>).MakeGenericType(t);
                var ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [t], null);
                return (Task)ctor!.Invoke([t.GetDefaultValue()]);
            });
}
