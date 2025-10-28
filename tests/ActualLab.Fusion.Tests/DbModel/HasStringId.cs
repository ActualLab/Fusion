using MessagePack;

namespace ActualLab.Fusion.Tests.DbModel;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial record HasStringId(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Id
) : IHasId<string>;
