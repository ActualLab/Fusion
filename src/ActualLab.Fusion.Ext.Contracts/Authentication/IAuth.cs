using MessagePack;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// The primary authentication service contract, providing sign-out, user editing,
/// presence updates, and session/user query methods.
/// </summary>
public interface IAuth : ISessionValidator, IComputeService
{
    // Commands
    [CommandHandler]
    public Task SignOut(Auth_SignOut command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task EditUser(Auth_EditUser command, CancellationToken cancellationToken = default);
    public Task UpdatePresence(Session session, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<bool> IsSignOutForced(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<SessionAuthInfo?> GetAuthInfo(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<SessionInfo?> GetSessionInfo(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<User?> GetUser(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<ImmutableArray<SessionInfo>> GetUserSessions(Session session, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backend command to set session options for the specified session.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetSessionOptions(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] ImmutableOptionSet Options,
    [property: DataMember, MemoryPackOrder(2)] long? ExpectedVersion = null
) : ISessionCommand<Unit>;

/// <summary>
/// Command to edit the current user's profile (e.g., name) within a session.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record Auth_EditUser(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string? Name
) : ISessionCommand<Unit>;

/// <summary>
/// Command to sign out a session, optionally kicking other user sessions.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record Auth_SignOut: ISessionCommand<Unit>
{
    [DataMember, MemoryPackOrder(0)] public Session Session { get; init; }
    [DataMember, MemoryPackOrder(1)] public string? KickUserSessionHash { get; init; }
    [DataMember, MemoryPackOrder(2)] public bool KickAllUserSessions { get; init; }
    [DataMember, MemoryPackOrder(3)] public bool Force { get; init; }

    public Auth_SignOut(Session session, bool force = false)
    {
        Session = session;
        Force = force;
    }

    public Auth_SignOut(Session session, string kickUserSessionHash, bool force = false)
    {
        Session = session;
        KickUserSessionHash = kickUserSessionHash;
        Force = force;
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public Auth_SignOut(
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
