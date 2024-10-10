using MessagePack;

namespace ActualLab.Fusion.Tests.Model;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial record TestCommand<TValue>(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Id,
    [property: DataMember, MemoryPackOrder(1), Key(1)] TValue? Value = null
) : ICommand<Unit> where TValue : class, IHasId<string>;
