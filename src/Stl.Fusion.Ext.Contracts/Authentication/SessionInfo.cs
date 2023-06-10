using System.Security;
using Stl.Versioning;

namespace Stl.Fusion.Authentication;

public record SessionInfo : SessionAuthInfo, IHasVersion<long>
{
    public static new Requirement<SessionInfo> MustBeAuthenticated { get; set; } = Requirement.New(
        new("Session is not authenticated.", m => new SecurityException(m)),
        (SessionInfo? i) => i?.IsAuthenticated() ?? false);

    public long Version { get; init; }
    public Moment CreatedAt { get; init; }
    public Moment LastSeenAt { get; init; }
    public string IPAddress { get; init; } = "";
    public string UserAgent { get; init; } = "";
    public ImmutableOptionSet Options { get; init; } = ImmutableOptionSet.Empty;

    public SessionInfo() { }
    public SessionInfo(Moment createdAt) : this(null, createdAt) { }
    public SessionInfo(Session? session, Moment createdAt = default) : base(session)
    {
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
    }

    public SessionAuthInfo ToAuthInfo()
        => IsSignOutForced
            ? new() {
                SessionHash = SessionHash,
                IsSignOutForced = true,
            }
            : new() {
                SessionHash = SessionHash,
                AuthenticatedIdentity = AuthenticatedIdentity,
                UserId = UserId,
            };
}
