using System.Text;
using ActualLab.Compliance;
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
[DataContract, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record AuthBackend_SetSessionOptions(
    [property: DataMember] Session Session,
    [property: DataMember] PropertyBag Options,
    [property: DataMember] long? ExpectedVersion = null
) : ISessionCommand<Unit>
{
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        // Options is whatever the caller put there - same reasoning as AuthBackend_SetupSession
        builder.Append("Session = ").Append(Session)
            .Append(", Options = ")
            .Append(Sanitizer.MaybeSanitize<Sanitizers.Hidden>(Options.ToString()))
            .Append(", ExpectedVersion = ").Append(ExpectedVersion);
        return true;
    }
}

/// <summary>
/// Command to edit the current user's profile (e.g., name) within a session.
/// </summary>
[DataContract, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record Auth_EditUser(
    [property: DataMember] Session Session,
    [property: DataMember] string? Name
) : ISessionCommand<Unit>;

/// <summary>
/// Command to sign out a session, optionally kicking other user sessions.
/// </summary>
[DataContract, MessagePackObject(true)]
// ReSharper disable once InconsistentNaming
public partial record Auth_SignOut: ISessionCommand<Unit>
{
    [DataMember] public Session Session { get; init; }
    [DataMember] public string? KickUserSessionHash { get; init; }
    [DataMember] public bool KickAllUserSessions { get; init; }
    [DataMember] public bool Force { get; init; }

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
    [JsonConstructor, SerializationConstructor]
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
