using ActualLab.Fusion.EntityFramework;
using MessagePack;

namespace ActualLab.Fusion.Authentication;

public interface IAuthBackend : IComputeService
{
    // Commands
    [CommandHandler]
    public Task SignIn(AuthBackend_SignIn command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task<SessionInfo> SetupSession(AuthBackend_SetupSession command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task SetOptions(AuthBackend_SetSessionOptions command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<User?> GetUser(string shard, string userId, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetupSession(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string IPAddress,
    [property: DataMember, MemoryPackOrder(2)] string UserAgent,
    [property: DataMember, MemoryPackOrder(3)] ImmutableOptionSet Options
) : ISessionCommand<SessionInfo>, IBackendCommand, INotLogged
{
    public AuthBackend_SetupSession(Session session, string ipAddress = "", string userAgent = "")
        : this(session, ipAddress, userAgent, default) { }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SignIn(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] User User,
    [property: DataMember, MemoryPackOrder(2)] UserIdentity AuthenticatedIdentity
) : ISessionCommand<Unit>, IBackendCommand
{
    public AuthBackend_SignIn(Session session, User user)
        : this(session, user, user.Identities.Single().Key) { }
}
