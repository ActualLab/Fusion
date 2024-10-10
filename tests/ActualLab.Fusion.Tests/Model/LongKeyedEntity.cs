using System.ComponentModel.DataAnnotations.Schema;
using MessagePack;

namespace ActualLab.Fusion.Tests.Model;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial record LongKeyedEntity : IHasId<long>
{
    [System.ComponentModel.DataAnnotations.Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    [DataMember, MemoryPackOrder(0), Key(0)]
    public long Id { get; init; }
}
