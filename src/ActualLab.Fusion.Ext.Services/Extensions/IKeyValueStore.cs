using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Extensions;

public interface IKeyValueStore : IComputeService
{
    [CommandHandler]
    Task Set(KeyValueStore_Set command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Remove(KeyValueStore_Remove command, CancellationToken cancellationToken = default);

    [ComputeMethod]
    Task<string?> Get(DbShard shard, string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> Count(DbShard shard, string prefix, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<string[]> ListKeySuffixes(
        DbShard shard,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record KeyValueStore_Set(
    [property: DataMember, MemoryPackOrder(0)] DbShard Shard,
    [property: DataMember, MemoryPackOrder(1)] (string Key, string Value, Moment? ExpiresAt)[] Items
) : ICommand<Unit>, IBackendCommand;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record KeyValueStore_Remove(
    [property: DataMember, MemoryPackOrder(0)] DbShard Shard,
    [property: DataMember, MemoryPackOrder(1)] string[] Keys
) : ICommand<Unit>, IBackendCommand;
