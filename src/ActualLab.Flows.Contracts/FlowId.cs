using System.ComponentModel;
using ActualLab.Fusion.Blazor;
using ActualLab.Identifiers.Internal;
using ActualLab.Internal;

namespace ActualLab.Flows;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[JsonConverter(typeof(SymbolIdentifierJsonConverter<FlowId>))]
[Newtonsoft.Json.JsonConverter(typeof(SymbolIdentifierNewtonsoftJsonConverter<FlowId>))]
[TypeConverter(typeof(SymbolIdentifierTypeConverter<FlowId>))]
[ParameterComparer(typeof(ByValueParameterComparer))]
[StructLayout(LayoutKind.Auto)]
public readonly partial struct FlowId : ISymbolIdentifier<FlowId>
{
    private static ILogger? _log;

    private static ILogger Log => _log ??= StaticLog.For<FlowId>();

    public static FlowId None => default;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol Id { get; }

    // Computed
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public string Value => Id.Value;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsNone => Id.IsEmpty;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    public FlowId(Symbol id)
        => this = Parse(id);
    public FlowId(string? id)
        => this = Parse(id);
    public FlowId(string? id, ParseOrNone _)
        => this = ParseOrNone(id);

    public FlowId(Symbol id, AssumeValid _)
        => Id = id;

    // Conversion

    public override string ToString() => Id.Value;
    public static implicit operator Symbol(FlowId source) => source.Id;
    public static implicit operator string(FlowId source) => source.Id.Value;

    // Equality

    public bool Equals(FlowId other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is FlowId other && Equals(other);
    public override int GetHashCode() => Id.HashCode;
    public static bool operator ==(FlowId left, FlowId right) => left.Equals(right);
    public static bool operator !=(FlowId left, FlowId right) => !left.Equals(right);

    // Parsing

    public static FlowId Parse(string? s)
        => TryParse(s, out var result) ? result : throw Errors.Format<FlowId>(s);
    public static FlowId ParseOrNone(string? s)
        => TryParse(s, out var result) ? result : Errors.Format<FlowId>(s).LogWarning(Log, result);

    public static bool TryParse(string? s, out FlowId result)
    {
        result = new FlowId(s, AssumeValid.Option);
        return true;
    }
}
