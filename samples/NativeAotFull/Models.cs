using System.Runtime.Serialization;
using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Trimming;
using MemoryPack;
using MessagePack;

[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record SayHelloCommand(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] string Name
) : ICommand<string>;

[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record ApiDto(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] ApiOption<int> Option,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1)] ApiNullable<int> Nullable,
    [property: DataMember(Order = 2), Key(2), MemoryPackOrder(2)] ApiNullable8<long> Nullable8,
    [property: DataMember(Order = 3), Key(3), MemoryPackOrder(3), MemoryPackAllowSerialize]
    PropertyBag<TypeSchema.Any> Bag,
    [property: DataMember(Order = 4), Key(4), MemoryPackOrder(4), MemoryPackAllowSerialize]
    SanitizedString<Sanitizers.Hidden> Secret);

public sealed class CustomSchema : TypeSchema
{
    // Keeps the parameterless constructor in trimmed apps
    static CustomSchema() => CodeKeeper.Keep<CustomSchema>();

    public override bool IsAllowed(Type type) => true;
}

public sealed class CustomSanitizer : Sanitizer
{
    // Keeps the parameterless constructor in trimmed apps
    static CustomSanitizer() => CodeKeeper.Keep<CustomSanitizer>();

    protected override string Sanitize(string value) => "<custom>";
}
