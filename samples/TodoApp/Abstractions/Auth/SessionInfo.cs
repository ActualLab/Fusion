using System.Security;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

/// <summary>
/// Stores detailed information about a user session, including authentication state,
/// version, timestamps, IP address, user agent, and additional options.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public partial record SessionInfo : IRequirementTarget
{
    public static Requirement<SessionInfo> MustBeAuthenticated { get; set; } = Requirement.New(
        (SessionInfo? i) => i?.IsAuthenticated() ?? false,
        new("Session is not authenticated.", m => new SecurityException(m)));

    [DataMember(Order = 0), MemoryPackOrder(0)] public string SessionHash { get => field ?? ""; init; }

    // Authentication

    [DataMember(Order = 1), MemoryPackOrder(1)]
    public UserIdentity AuthenticatedIdentity { get; init; }

    [DataMember(Order = 2), MemoryPackOrder(2), StringAsSymbolMemoryPackFormatter]
    public string UserId { get; init; } = "";

    [DataMember(Order = 3), MemoryPackOrder(3)]
    public bool IsSignOutForced { get; init; }

    // Session metadata

    [DataMember(Order = 10), MemoryPackOrder(10)] public long Version { get; init; }
    [DataMember(Order = 11), MemoryPackOrder(11)] public Moment CreatedAt { get; init; }
    [DataMember(Order = 12), MemoryPackOrder(12)] public Moment LastSeenAt { get; init; }
    [DataMember(Order = 13), MemoryPackOrder(13)] public string IPAddress { get => field ?? ""; init; }
    [DataMember(Order = 14), MemoryPackOrder(14)] public string UserAgent { get => field ?? ""; init; }
    [DataMember(Order = 15), MemoryPackOrder(15)] public ImmutableOptionSet Options { get; init; }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public SessionInfo()
    {
        SessionHash = "";
        IPAddress = "";
        UserAgent = "";
    }

    public SessionInfo(Moment createdAt) : this(null, createdAt) { }

    public SessionInfo(Session? session, Moment createdAt = default)
    {
        SessionHash = session?.Hash ?? "";
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
        IPAddress = "";
        UserAgent = "";
    }

    public bool IsAuthenticated()
        => !(IsSignOutForced || UserId.IsNullOrEmpty());
}
