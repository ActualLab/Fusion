namespace ActualLab.Fusion.Authentication;

public interface IAuth : IComputeService
{
    // Commands
    [CommandHandler]
    Task SignOut(Auth_SignOut command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task EditUser(Auth_EditUser command, CancellationToken cancellationToken = default);
    Task UpdatePresence(Session session, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod(MinCacheDuration = 10)]
    Task<bool> IsSignOutForced(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    Task<SessionAuthInfo?> GetAuthInfo(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    Task<SessionInfo?> GetSessionInfo(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 10)]
    Task<User?> GetUser(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<ImmutableArray<SessionInfo>> GetUserSessions(Session session, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetSessionOptions(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] ImmutableOptionSet Options,
    [property: DataMember, MemoryPackOrder(2)] long? ExpectedVersion = null
) : ISessionCommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record Auth_EditUser(
    [property: DataMember, MemoryPackOrder(0)] Session Session,
    [property: DataMember, MemoryPackOrder(1)] string? Name
) : ISessionCommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record Auth_SignOut: ISessionCommand<Unit>
{
    [DataMember, MemoryPackOrder(0)]
    public Session Session { get; init; } = null!;
    [DataMember, MemoryPackOrder(1)]
    public string? KickUserSessionHash { get; init; }
    [DataMember, MemoryPackOrder(2)]
    public bool KickAllUserSessions { get; init; }
    [DataMember, MemoryPackOrder(3)]
    public bool Force { get; init; }

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

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
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
