using ActualLab.Fusion.EntityFramework;
using MessagePack;

namespace ActualLab.Fusion.Extensions;

public interface IKeyValueStore : IComputeService
{
    [CommandHandler]
    public Task Set(KeyValueStore_Set command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task Remove(KeyValueStore_Remove command, CancellationToken cancellationToken = default);

    [ComputeMethod]
    public Task<string?> Get(string shard, string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<int> Count(string shard, string prefix, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<string[]> ListKeySuffixes(
        string shard,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record KeyValueStore_Set(
    [property: DataMember, MemoryPackOrder(0)] string Shard,
    [property: DataMember, MemoryPackOrder(1)] (string Key, string Value, Moment? ExpiresAt)[] Items
) : ICommand<Unit>, IBackendCommand;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record KeyValueStore_Remove(
    [property: DataMember, MemoryPackOrder(0)] string Shard,
    [property: DataMember, MemoryPackOrder(1)] string[] Keys
) : ICommand<Unit>, IBackendCommand;
