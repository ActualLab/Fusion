using ActualLab.Interception;
using ActualLab.Interception.Serialization;
using MessagePack;

namespace ActualLab.CommandR.Operations;

#pragma warning disable IL2080, MsgPack017

/// <summary>
/// A recorded call to a service method: everything needed to reproduce it on this or another host.
/// Deferred invalidation records what it invalidates as these.
/// </summary>
/// <remarks>
/// Its serialized form is the same shape RPC uses - the method is identified by
/// <see cref="ServiceType"/> and an RPC-style <see cref="MethodName"/>, which is what makes the
/// argument list's type known on the receiving side, so the arguments travel as an opaque payload
/// with per-argument type info only where a declared type is polymorphic.
/// <see cref="ArgumentList"/> itself is deliberately not serializable.
/// </remarks>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record ServiceCall
{
    // Both the writer and the reader must agree on these
    public static ArgumentListSerializer ByteArgumentListSerializer { get; set; }
        = new ByteArgumentListSerializer(MessagePackByteSerializer.Default);
    public static ArgumentListSerializer TextArgumentListSerializer { get; set; }
        = new TextArgumentListSerializer(SystemJsonSerializer.Default);

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public TypeRef ServiceType { get; init; }
    [DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    public string MethodName { get; init; } = "";


    // MessagePack and MemoryPack. Get-only: the payload is a projection of Arguments rather than
    // a second copy of them, so there is no state here that can disagree with Arguments.
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, DataMember(Order = 2), MemoryPackOrder(2), Key(2)]
    public byte[] ArgumentData
        => ByteArgumentListSerializer.Serialize(Arguments, needsPolymorphism: true);

    // STJ and Newtonsoft.JSON
    // No IgnoreDataMember here: Newtonsoft honours it too, and that is what would hide this
    // property from it. MemoryPack and MessagePack are excluded by their own attributes.
    [JsonInclude, MemoryPackIgnore, IgnoreMember]
    public string ArgumentText
        => EncodingExt.Utf8NoBom.GetString(TextArgumentListSerializer.Serialize(Arguments, needsPolymorphism: true));

    // A Result rather than an ArgumentList: a record for a service this host doesn't know still
    // has to travel through it - the applier drops such a record by its ServiceType, and only a
    // reader that actually needs the arguments should see why they couldn't be decoded.
    private readonly Result<ArgumentList> _arguments = ArgumentList.Empty;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ArgumentList Arguments {
        get => _arguments.Value;
        init => _arguments = value;
    }

    public ServiceCall() { }

    // The two deserialization constructors take the payload together with the method identity that
    // decodes it, so the arguments are materialized here rather than on first use: a record always
    // holds its Arguments, whatever produced it, and never a second encoded copy of them.
    [MemoryPackConstructor, SerializationConstructor]
    public ServiceCall(TypeRef serviceType, string methodName, byte[] argumentData)
    {
        ServiceType = serviceType;
        MethodName = methodName;
        _arguments = DeserializeArguments(serviceType, methodName, argumentData, DataFormat.Bytes);
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor]
    public ServiceCall(TypeRef serviceType, string methodName, string argumentText)
    {
        ServiceType = serviceType;
        MethodName = methodName;
        _arguments = DeserializeArguments(
            serviceType, methodName, EncodingExt.Utf8NoBom.GetBytes(argumentText), DataFormat.Text);
    }

    public static ServiceCall New(Type serviceType, MethodInfo method, ArgumentList arguments)
    {
        var methodName = method.GetRpcStyleName();
        return new() {
            ServiceType = NewServiceTypeRef(serviceType, method, methodName),
            MethodName = methodName,
            Arguments = arguments
        };
    }

    public static ServiceCall New(Type serviceType, MethodInfo method, byte[] argumentData)
    {
        var methodName = method.GetRpcStyleName();
        return new(NewServiceTypeRef(serviceType, method, methodName), methodName, argumentData);
    }

    public override string ToString()
        => _arguments.HasError
            ? $"{ServiceType.TypeName}.{MethodName}(<undecodable>)"
            : $"{ServiceType.TypeName}.{MethodName}({_arguments.ValueOrDefault})";

    // This record relies on referential equality: comparing two of them by content compares their
    // argument lists, which is more work than a dedupe-free path should pay. Use
    // ContentEqualityComparer where content equality is what's actually wanted.

    public bool Equals(ServiceCall? other)
        => ReferenceEquals(this, other);
    public override int GetHashCode()
        => RuntimeHelpers.GetHashCode(this);

    // Nested types

    /// <summary>
    /// Compares <see cref="ServiceCall"/>s by what they invoke rather than by reference.
    /// </summary>
    public sealed class ContentEqualityComparer : IEqualityComparer<ServiceCall>
    {
        public static readonly ContentEqualityComparer Instance = new();

        public bool Equals(ServiceCall? x, ServiceCall? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;

            // ArgumentList is a record, so comparing Arguments is structural
            return string.Equals(x.MethodName, y.MethodName, StringComparison.Ordinal)
                && x.ServiceType.Equals(y.ServiceType)
                && x.Arguments.Equals(y.Arguments);
        }

        public int GetHashCode(ServiceCall obj)
            => HashCode.Combine(obj.ServiceType, obj.MethodName, obj.Arguments);
    }

    // Private methods

    // Normally the type the service is registered as, so a rename of the implementation can't break
    // a record in flight. But a compute method can be protected, and then it isn't on that type at
    // all - and a record has to name a type that describes its arguments. The applier maps an
    // implementation type back to what the container knows.
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "We assume service code is preserved")]
    private static TypeRef NewServiceTypeRef(Type serviceType, MethodInfo method, string methodName)
    {
        var type = MethodInfoExt.TryGetByRpcStyleName(serviceType, methodName) is not null
            ? serviceType
            : method.DeclaringType!.NonProxyType();
        return new TypeRef(type).WithoutAssemblyVersions();
    }


    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "We assume serialization related code is fully preserved")]
    // Failures are captured rather than thrown: this runs while the carrier of a whole set of
    // records is being deserialized, and one record naming a type this host lacks must not take
    // the rest of the set with it.
    private static Result<ArgumentList> DeserializeArguments(
        TypeRef serviceType, string methodName, ReadOnlyMemory<byte> data, DataFormat format)
    {
        try {
            var methodInfo = MethodInfoExt.GetByRpcStyleName(serviceType.Resolve(), methodName);
            var arguments = ArgumentListType.Get(methodInfo).Factory.Invoke();
            if (data.IsEmpty)
                return arguments;

            var serializer = format == DataFormat.Text
                ? TextArgumentListSerializer
                : ByteArgumentListSerializer;
            serializer.Deserialize(ref arguments, needsPolymorphism: true, data);
            return arguments;
        }
        catch (Exception e) {
            return Result.NewError<ArgumentList>(e);
        }
    }
}
