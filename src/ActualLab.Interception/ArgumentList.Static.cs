using ActualLab.Collections.Internal;

namespace ActualLab.Interception;

abstract partial record ArgumentList
{
    private static readonly MethodInfo InsertUntypedImplMethod =
        typeof(ArgumentList).GetMethod(nameof(InsertUntypedImpl), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<SequenceEqualityBox.ForArray<Type>, Type> ListTypeCache = new();
    private static readonly ConcurrentDictionary<Type, ImmutableArray<Type>> ItemTypesCache = new();
    private static readonly ConcurrentDictionary<(Type, int, Type), Type> LongerListTypeCache = new();
    private static readonly ConcurrentDictionary<(Type, int), Type> ShorterListTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Func<ArgumentList, int, object?, ArgumentList>> InsertUntypedCache = new();

    protected static readonly ConcurrentDictionary<
        (Type, MethodInfo),
        LazySlim<(Type, MethodInfo), Func<object?, ArgumentList, object?>>> InvokerCache = new();

    public static readonly ArgumentList Empty = new ArgumentList0();

    public static Type GetListType(params Type[] itemTypes)
        => ListTypeCache.GetOrAdd(new(itemTypes), static key => {
            var argumentTypes = key.Source;
            if (argumentTypes.Length < NativeTypes.Length) {
                // Native type
                var listType = NativeTypes[argumentTypes.Length];
                return argumentTypes.Length == 0
                    ? listType
                    : listType.MakeGenericType(argumentTypes);
            }

            var span = argumentTypes.AsSpan();
            var t0 = GetListType(span[..NativeTypeCount].ToArray()); // Definitely native
            var t1 = GetListType(span[NativeTypeCount..].ToArray()); // Native or pair
            return typeof(ArgumentListPair<,>).MakeGenericType(t0, t1);
        });

    public static ImmutableArray<Type> GetItemTypes(Type listType)
        => ItemTypesCache.GetOrAdd(listType, static t => {
            if (typeof(ArgumentListNative).IsAssignableFrom(t))
                return t.IsGenericType
                    // ReSharper disable once UseCollectionExpression
                    ? ImmutableArray.Create(t.GetGenericArguments())
                    : ImmutableArray<Type>.Empty;

            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(ArgumentListPair<,>))
                throw new ArgumentOutOfRangeException(nameof(t));

            // It's ArgumentListPair<,>
            var gParameters = t.GetGenericArguments();
            var prefix = GetItemTypes(gParameters[0]);
            var suffix = GetItemTypes(gParameters[1]);
            // ReSharper disable once UseCollectionExpression
            return ImmutableArray.Create(prefix.Concat(suffix).ToArray());
        });

    public static Type GetLongerListType(Type listType, int index, Type itemType)
        => LongerListTypeCache.GetOrAdd((listType, index, itemType), static key => {
            var (listType, index, itemType) = key;
            var itemTypes = GetItemTypes(listType);
            itemTypes = itemTypes.Insert(index, itemType);
            return GetListType(itemTypes.ToArray());
        });

    public static Type GetShorterListType(Type listType, int index)
        => ShorterListTypeCache.GetOrAdd((listType, index), static key => {
            var (listType, index) = key;
            var itemTypes = GetItemTypes(listType);
            itemTypes = itemTypes.RemoveAt(index);
            return GetListType(itemTypes.ToArray());
        });

    // Private methods

    private static ArgumentList InsertUntypedImpl<T>(ArgumentList list, int index, object? item)
        => list.Insert<T>(index, (T)item!);
}
