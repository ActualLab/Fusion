namespace ActualLab.Internal;

[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public abstract class Boxed
{
    private static readonly ConcurrentDictionary<Type, Func<object, Boxed>> FactoryCache = new();
    private static readonly MethodInfo FactoryMethod =
        typeof(Boxed).GetMethod(nameof(Factory), BindingFlags.Static | BindingFlags.NonPublic)!;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public abstract object UntypedValue { get; }

    public static Boxed<T> New<T>(T value) => new(value);
    public static Func<object, Boxed> GetFactory(Type type)
        => FactoryCache.GetOrAdd(type,
            t => (Func<object, Boxed>)FactoryMethod
                .MakeGenericMethod(t)
                .CreateDelegate(typeof(Func<object, Boxed>)));

    private static Boxed Factory<T>(object value)
        => new Boxed<T>((T) value);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public sealed partial class Boxed<T>(T value) : Boxed
{
    [DataMember(Order = 0), MemoryPackOrder(0)]
    public T Value { get; } = value;

#pragma warning disable CS8603
    public override object UntypedValue => Value;
#pragma warning restore CS8603

    public override string ToString()
        => $"{GetType().Name}({Value})";

    public static implicit operator T(Boxed<T> source) => source.Value;
    public static implicit operator Boxed<T>(T value) => new(value);
}
