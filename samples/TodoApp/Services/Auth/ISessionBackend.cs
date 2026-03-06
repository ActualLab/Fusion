using ActualLab.Rpc;
using MessagePack;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Backend service for session management.
/// </summary>
public interface ISessionBackend : IComputeService, IBackendService, ISessionValidator
{
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<SessionInfo?> GetSessionInfo(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<ImmutableArray<SessionInfo>> GetUserSessions(UserId userId, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<SessionInfo> OnSetupSession(SessionBackend_SetupSession command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task OnSignIn(SessionBackend_SignIn command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task OnSignOut(SessionBackend_SignOut command, CancellationToken cancellationToken = default);

    public Task UpdatePresence(Session session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backend command to set up or update session metadata (IP address, user agent, options).
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
// ReSharper disable once InconsistentNaming
public partial record SessionBackend_SetupSession(
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
public partial record SessionBackend_SignIn(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] User User,
    [property: DataMember, MemoryPackOrder(2)] UserIdentity AuthenticatedIdentity
) : ISessionCommand<Unit>, IBackendCommand
{
    public SessionBackend_SignIn(Session session, User user)
        : this(session, user, user.Identities.Single().Key) { }
}

/// <summary>
/// Backend command to sign out a session, optionally kicking other user sessions.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record SessionBackend_SignOut : ISessionCommand<Unit>, IBackendCommand
{
    [DataMember, MemoryPackOrder(0)] public Session Session { get; init; }
    [DataMember, MemoryPackOrder(1)] public string? KickUserSessionHash { get; init; }
    [DataMember, MemoryPackOrder(2)] public bool KickAllUserSessions { get; init; }
    [DataMember, MemoryPackOrder(3)] public bool Force { get; init; }

    public SessionBackend_SignOut(Session session, bool force = false)
    {
        Session = session;
        Force = force;
    }

    public SessionBackend_SignOut(Session session, string kickUserSessionHash, bool force = false)
    {
        Session = session;
        KickUserSessionHash = kickUserSessionHash;
        Force = force;
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public SessionBackend_SignOut(
        Session session,
        string? kickUserSessionHash,
        bool kickAllUserSessions,
        bool force)
    {
        Session = session;
        KickUserSessionHash = kickUserSessionHash;
        KickAllUserSessions = kickAllUserSessions;
        Force = force;
    }
}
