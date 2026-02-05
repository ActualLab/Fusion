using System.Security;
using MessagePack;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Stores authentication-related information for a session, including the
/// authenticated identity, user ID, and forced sign-out status.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public partial record SessionAuthInfo : IRequirementTarget
{
    public static Requirement<SessionAuthInfo> MustBeAuthenticated { get; set; } = Requirement.New(
        (SessionAuthInfo? i) => i?.IsAuthenticated() ?? false,
        new("Session is not authenticated.", m => new SecurityException(m)));

    [DataMember(Order = 0), MemoryPackOrder(0)] public string SessionHash { get => field ?? ""; init; }

    // Authentication

    [DataMember(Order = 1), MemoryPackOrder(1)]
    public UserIdentity AuthenticatedIdentity { get; init; }

    [DataMember(Order = 2), MemoryPackOrder(2), StringAsSymbolMemoryPackFormatter]
    public string UserId { get; init; } = "";

    [DataMember(Order = 3), MemoryPackOrder(3)]
    public bool IsSignOutForced { get; init; }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public SessionAuthInfo()
        => SessionHash = "";
    public SessionAuthInfo(Session? session)
        => SessionHash = session?.Hash ?? "";

    public bool IsAuthenticated()
        => !(IsSignOutForced || UserId.IsNullOrEmpty());
}
