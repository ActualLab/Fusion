using MessagePack;

namespace ActualLab.Fusion.Extensions;

public interface ISandboxedKeyValueStore : IComputeService
{
    [CommandHandler]
    Task Set(SandboxedKeyValueStore_Set command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(SandboxedKeyValueStore_Remove command, CancellationToken cancellationToken = default);

    [ComputeMethod]
    Task<string?> Get(Session session, string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> Count(Session session, string prefix, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<string[]> ListKeySuffixes(
        Session session,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record SandboxedKeyValueStore_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1), Key(1)] (string Key, string Value, Moment? ExpiresAt)[] Items
) : ISessionCommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record SandboxedKeyValueStore_Remove(
    [property: DataMember, MemoryPackOrder(0), Key(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string[] Keys
) : ISessionCommand<Unit>;
