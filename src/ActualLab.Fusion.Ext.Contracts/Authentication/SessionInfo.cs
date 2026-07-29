using System.Globalization;
using System.Security;
using System.Text;
using ActualLab.Compliance;
using ActualLab.Versioning;
using MessagePack;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Stores detailed information about a user session, including version, timestamps,
/// IP address, user agent, and additional options.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public partial record SessionInfo : SessionAuthInfo, IHasVersion<long>, ISanitized
{
    public static new Requirement<SessionInfo> MustBeAuthenticated { get; set; } = Requirement.New(
        (SessionInfo? i) => i?.IsAuthenticated() ?? false,
        new("Session is not authenticated.", m => new SecurityException(m)));

    [DataMember(Order = 10), MemoryPackOrder(10)] public long Version { get; init; }
    [DataMember(Order = 11), MemoryPackOrder(11)] public Moment CreatedAt { get; init; }
    [DataMember(Order = 12), MemoryPackOrder(12)] public Moment LastSeenAt { get; init; }
    [DataMember(Order = 13), MemoryPackOrder(13)] public string IPAddress { get => field ?? ""; init; }
    [DataMember(Order = 14), MemoryPackOrder(14)] public string UserAgent { get => field ?? ""; init; }
    [DataMember(Order = 15), MemoryPackOrder(15)] public PropertyBag Options { get; init; }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public SessionInfo()
    {
        IPAddress = "";
        UserAgent = "";
    }

    public SessionInfo(Moment createdAt) : this(null, createdAt) { }

    public SessionInfo(Session? session, Moment createdAt = default) : base(session)
    {
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
        IPAddress = "";
        UserAgent = "";
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

    // Protected methods

    protected override bool PrintMembers(StringBuilder builder)
    {
        // Options holds whatever the caller passed in - same as in AuthBackend_SetupSession
        base.PrintMembers(builder);
        builder.Append(", Version = ").Append(Version.ToString(CultureInfo.InvariantCulture))
            .Append(", CreatedAt = ").Append(CreatedAt)
            .Append(", LastSeenAt = ").Append(LastSeenAt)
            .Append(", IPAddress = ").Append(IPAddress)
            .Append(", UserAgent = ").Append(UserAgent)
            .Append(", Options = ")
            .Append(Sanitization.IsSuspended ? Options.ToString() : Sanitizers.HiddenValue);
        return true;
    }
}
