using MessagePack;

namespace ActualLab.Fusion.Extensions;

/// <summary>
/// A session-scoped key-value store that enforces key prefix constraints
/// based on the current session and user.
/// </summary>
public interface ISandboxedKeyValueStore : IComputeService
{
    [CommandHandler]
    public Task Set(SandboxedKeyValueStore_Set command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task Remove(SandboxedKeyValueStore_Remove command, CancellationToken cancellationToken = default);

    [ComputeMethod]
    public Task<string?> Get(Session session, string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<int> Count(Session session, string prefix, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<string[]> ListKeySuffixes(
        Session session,
        string prefix,
        PageRef<string> pageRef,
        SortDirection sortDirection = SortDirection.Ascending,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Command to set one or more key-value entries in the <see cref="ISandboxedKeyValueStore"/>.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record SandboxedKeyValueStore_Set(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] (string Key, string Value, Moment? ExpiresAt)[] Items
) : ISessionCommand<Unit>;

/// <summary>
/// Command to remove one or more keys from the <see cref="ISandboxedKeyValueStore"/>.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record SandboxedKeyValueStore_Remove(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string[] Keys
) : ISessionCommand<Unit>;
