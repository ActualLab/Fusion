using ActualLab.Fusion.EntityFramework;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

/// <summary>
/// Identifies a user. The shard is embedded as a prefix of <see cref="Id"/> before the ':' delimiter.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public readonly partial record struct UserId : IHasShard
{
    public static readonly UserId None = default;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol Id { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Shard {
        get {
            var value = Id.Value;
            var idx = value.IndexOf(':');
            return idx >= 0 ? value[..idx] : "";
        }
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone => Id.IsEmpty;

    [MemoryPackConstructor, SerializationConstructor]
    public UserId(Symbol id) => Id = id;

    public static UserId New(string shard, string suffix)
        => new(shard.IsNullOrEmpty() ? suffix : $"{shard}:{suffix}");

    public static implicit operator UserId(string id) => new(id);
    public static implicit operator UserId(Symbol id) => new(id);

    public override string ToString() => Id.Value;
}
