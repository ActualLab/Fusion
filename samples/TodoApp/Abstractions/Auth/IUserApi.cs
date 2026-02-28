using MessagePack;

namespace Samples.TodoApp.Abstractions;

/// <summary>
/// Client-facing compute service for user and session queries.
/// </summary>
public interface IUserApi : IComputeService
{
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<User?> GetOwn(Session session, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<ImmutableArray<SessionInfo>> ListOwnSessions(Session session, CancellationToken cancellationToken = default);

    public Task UpdatePresence(Session session, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task OnSignOut(User_SignOut command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command to sign out a session, optionally kicking other user sessions.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record User_SignOut : ISessionCommand<Unit>
{
    [DataMember, MemoryPackOrder(0)] public Session Session { get; init; }
    [DataMember, MemoryPackOrder(1)] public string? KickUserSessionHash { get; init; }
    [DataMember, MemoryPackOrder(2)] public bool KickAllUserSessions { get; init; }
    [DataMember, MemoryPackOrder(3)] public bool Force { get; init; }

    public User_SignOut(Session session, bool force = false)
    {
        Session = session;
        Force = force;
    }

    public User_SignOut(Session session, string kickUserSessionHash, bool force = false)
    {
        Session = session;
        KickUserSessionHash = kickUserSessionHash;
        Force = force;
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public User_SignOut(
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
