namespace ActualLab.Interception;

abstract partial record ArgumentList
{
    protected static readonly ConcurrentDictionary<
        (Type, MethodInfo),
        LazySlim<(Type, MethodInfo), Func<object?, ArgumentList, object?>>> InvokerCache = new();

    public static readonly ArgumentList Empty = new ArgumentList0();

    public static Type FindType(params Type[] argumentTypes)
    {
        var listType = Types[argumentTypes.Length];
        return argumentTypes.Length == 0 ? listType : listType.MakeGenericType(argumentTypes);
    }
}
