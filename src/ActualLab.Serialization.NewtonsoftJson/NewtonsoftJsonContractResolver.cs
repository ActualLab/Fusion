using ActualLab.Serialization.Internal;
using Newtonsoft.Json.Serialization;
using JsonProperty = Newtonsoft.Json.Serialization.JsonProperty;
using NewtonsoftJsonConverter = Newtonsoft.Json.JsonConverter;
using NewtonsoftMemberSerialization = Newtonsoft.Json.MemberSerialization;
using StjJsonConstructorAttribute = System.Text.Json.Serialization.JsonConstructorAttribute;
using StjJsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace ActualLab.Serialization;

#pragma warning disable IL2026, IL2070, IL2072, IL2075

/// <summary>
/// A <see cref="DefaultContractResolver"/> that reads the System.Text.Json attributes
/// of ActualLab types the way it reads its own, and supplies their converters.
/// Types from other assemblies get the stock Newtonsoft.Json contracts.
/// </summary>
public class NewtonsoftJsonContractResolver : DefaultContractResolver
{
    private static readonly NewtonsoftJsonConverter[] FrameworkConverters = [
        new SymbolNewtonsoftJsonConverter(),
        new MomentNewtonsoftJsonConverter(),
        new TypeRefNewtonsoftJsonConverter(),
        new FilePathNewtonsoftJsonConverter(),
        new ByteStringNewtonsoftJsonConverter(),
        new JsonStringNewtonsoftJsonConverter(),
        new SanitizedStringNewtonsoftJsonConverter(),
        new ApiArrayNewtonsoftJsonConverter(),
        new SessionNewtonsoftJsonConverter(),
        new RpcStreamNewtonsoftJsonConverter(),
        new UniSerializedNewtonsoftJsonConverter(),
    ];

    // ActualLab.Core.Legacy is excluded: its types still carry Newtonsoft.Json attributes,
    // and those contradict their System.Text.Json ones on purpose.
    private static readonly HashSet<string> FrameworkAssemblyNames = new(StringComparer.Ordinal) {
        "ActualLab.CommandR",
        "ActualLab.Core",
        "ActualLab.Fusion",
        "ActualLab.Fusion.Blazor",
        "ActualLab.Fusion.Blazor.Authentication",
        "ActualLab.Fusion.EntityFramework",
        "ActualLab.Fusion.EntityFramework.Npgsql",
        "ActualLab.Fusion.EntityFramework.Redis",
        "ActualLab.Fusion.Ext.Backend.Contracts",
        "ActualLab.Fusion.Ext.Contracts",
        "ActualLab.Fusion.Ext.Services",
        "ActualLab.Fusion.Server",
        "ActualLab.Fusion.Server.NetFx",
        "ActualLab.Interception",
        "ActualLab.Plugins",
        "ActualLab.Redis",
        "ActualLab.RestEase",
        "ActualLab.Rpc",
        "ActualLab.Rpc.Server",
        "ActualLab.Rpc.Server.NetFx",
        "ActualLab.Serialization.MemoryPack",
        "ActualLab.Serialization.NerdbankMessagePack",
        "ActualLab.Serialization.NewtonsoftJson",
    };

    protected virtual bool IsFrameworkType(Type type)
        => type.Assembly.GetName().Name is { } assemblyName && FrameworkAssemblyNames.Contains(assemblyName);

    // Protected/internal methods

    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        var contract = base.CreateObjectContract(objectType);
        var type = Nullable.GetUnderlyingType(objectType) ?? objectType;
        if (!IsFrameworkType(type))
            return contract;

        contract.MemberSerialization = GetMemberSerialization(type, contract.MemberSerialization);
        if (HasNewtonsoftJsonConstructor(type))
            return contract;

        // The STJ [JsonConstructor] plays the role of the Newtonsoft.Json one, so it also wins over
        // the creator the base picked by its own rules, e.g. the "immutable constructor" of a struct.
        var constructor = GetJsonConstructor(type);
        if (constructor is null)
            return contract;

        contract.OverrideCreator = args => constructor.Invoke(args);
        contract.CreatorParameters.Clear();
        foreach (var parameter in CreateConstructorParameters(constructor, contract.Properties))
            contract.CreatorParameters.Add(parameter);
        return contract;
    }

    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        var members = base.GetSerializableMembers(objectType);
        if (!IsFrameworkType(objectType))
            return members;

        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var nonPublicMembers = objectType.GetFields(bindingFlags)
            .Concat<MemberInfo>(objectType.GetProperties(bindingFlags))
            .Where(m => m.IsDefined(typeof(JsonIncludeAttribute), true) && !members.Contains(m));
        members.AddRange(nonPublicMembers);
        return members;
    }

    protected override IList<JsonProperty> CreateProperties(
        Type type, NewtonsoftMemberSerialization memberSerialization)
    {
        if (IsFrameworkType(type))
            memberSerialization = GetMemberSerialization(type, memberSerialization);
        return base.CreateProperties(type, memberSerialization);
    }

    protected override JsonProperty CreateProperty(MemberInfo member, NewtonsoftMemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        if (member.DeclaringType is not { } declaringType || !IsFrameworkType(declaringType))
            return property;

        if (member.GetCustomAttribute<JsonPropertyNameAttribute>(true) is { } propertyNameAttribute)
            property.PropertyName = propertyNameAttribute.Name;

        // [JsonInclude] plays the role of [Newtonsoft.Json.JsonProperty], which unlocks non-public accessors
        if (!property.HasMemberAttribute && member.IsDefined(typeof(JsonIncludeAttribute), true)) {
            property.HasMemberAttribute = true;
            property.Readable = CanRead(member);
            property.Writable = CanWrite(member);
        }

        // [JsonIgnore] plays the role of [Newtonsoft.Json.JsonIgnore]
        if (member.GetCustomAttribute<StjJsonIgnoreAttribute>(true) is { Condition: JsonIgnoreCondition.Always })
            property.Ignored = true;
        return property;
    }

    protected override NewtonsoftJsonConverter? ResolveContractConverter(Type objectType)
    {
        var converter = base.ResolveContractConverter(objectType);
        if (converter is not null || !IsFrameworkType(objectType))
            return converter;

        foreach (var frameworkConverter in FrameworkConverters)
            if (frameworkConverter.CanConvert(objectType))
                return frameworkConverter;

        return null;
    }

    // Private methods

    // [DataContract] makes Newtonsoft.Json opt-in, but System.Text.Json ignores it and serializes
    // every public member that isn't [JsonIgnore]-d, so framework types are opt-out here.
    private static NewtonsoftMemberSerialization GetMemberSerialization(
        Type type, NewtonsoftMemberSerialization memberSerialization)
        => memberSerialization == NewtonsoftMemberSerialization.OptIn
            && !type.IsDefined(typeof(Newtonsoft.Json.JsonObjectAttribute), true)
            ? NewtonsoftMemberSerialization.OptOut
            : memberSerialization;

    private static bool HasNewtonsoftJsonConstructor(Type type)
        => type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(c => c.IsDefined(typeof(Newtonsoft.Json.JsonConstructorAttribute), true));

    private static ConstructorInfo? GetJsonConstructor(Type type)
        => type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c => c.IsDefined(typeof(StjJsonConstructorAttribute), true));

    private static bool CanRead(MemberInfo member)
        => member is not PropertyInfo property || property.GetMethod is not null;

    private static bool CanWrite(MemberInfo member)
        => member switch {
            PropertyInfo property => property.SetMethod is not null,
            FieldInfo field => !field.IsLiteral,
            _ => false,
        };
}
