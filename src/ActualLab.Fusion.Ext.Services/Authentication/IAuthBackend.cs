using ActualLab.Fusion.EntityFramework;
using ActualLab.Rpc;
using MessagePack;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Backend authentication service contract for sign-in, session setup,
/// and session options management.
/// </summary>
public interface IAuthBackend : IComputeService, IBackendService
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

/// <summary>
/// Backend command to set up or update session metadata (IP address, user agent, options).
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor
]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetupSession(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string IPAddress = "",
    [property: DataMember, MemoryPackOrder(2)] string UserAgent = "",
    [property: DataMember, MemoryPackOrder(3)] ImmutableOptionSet Options = default
) : ISessionCommand<SessionInfo>, IBackendCommand, INotLogged;

/// <summary>
/// Backend command to sign in a user with the specified identity.
/// </summary>
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
