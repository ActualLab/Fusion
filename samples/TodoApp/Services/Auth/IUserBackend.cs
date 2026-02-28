using ActualLab.Fusion.EntityFramework;
using ActualLab.Rpc;
using MessagePack;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Backend service for user management.
/// </summary>
public interface IUserBackend : IComputeService, IBackendService
{
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<User?> Get(UserId userId, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<User> OnUpsert(UserBackend_Upsert command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backend command to create or update a user.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
// ReSharper disable once InconsistentNaming
public partial record UserBackend_Upsert(
    [property: DataMember, MemoryPackOrder(0)] User User,
    [property: DataMember, MemoryPackOrder(1)] string Shard = ""
) : ICommand<User>, IBackendCommand, IHasShard
{
    string IHasShard.Shard => Shard;
}
