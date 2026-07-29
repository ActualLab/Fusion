using System.Globalization;
using System.Security;
using System.Security.Claims;
using System.Text;
using ActualLab.Compliance;
using ActualLab.Requirements;
using ActualLab.Versioning;
using MessagePack;

namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Represents an authenticated or guest user with claims, identities, and version tracking.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial record User : IHasId<string>, IHasVersion<long>, IRequirementTarget, ISanitized
{
    public static string GuestName { get; set; } = "Guest";
    public static Requirement<User> MustExist { get; set; }
        = new MustExistRequirement<User>().With("You must sign-in to perform this action.", m => new SecurityException(m));
    public static Requirement<User> MustBeAuthenticated { get; set; }
        = Requirement.New(
            (User? u) => u?.IsAuthenticated() ?? false,
            new("User is not authenticated.", m => new SecurityException(m)));

    private Lazy<ClaimsPrincipal>? _claimsPrincipalLazy;

    [DataMember, MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter]
    public string Id { get; init; }
    [DataMember, MemoryPackOrder(1)]
    public string Name { get; init; }
    [DataMember, MemoryPackOrder(2)]
    public long Version { get; init; }
    [DataMember, MemoryPackOrder(3)]
    public ApiMap<string, string> Claims { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ApiMap<UserIdentity, string> Identities { get; init; }

    // Computed properties

    [DataMember(Name = nameof(Identities)), MemoryPackOrder(4), Key(4)]
    [JsonPropertyName(nameof(Identities)),  Newtonsoft.Json.JsonProperty(nameof(Identities))]
    public ApiMap<string, string> JsonCompatibleIdentities {
        get => Identities.UnorderedItems.ToApiMap(p => p.Key.Id, p => p.Value, StringComparer.Ordinal);
        init => Identities = value.ToApiMap(p => new UserIdentity(p.Key), p => p.Value);
    }

    public static User NewGuest(string? name = null)
        => new(name ?? GuestName);

    public User(string name) : this("", name) { }
    public User(string id, string name)
    {
        Id = id;
        Name = name;
        Claims = new();
        Identities = new();
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public User(
        string id,
        string name,
        long version,
        ApiMap<string, string> claims,
        ApiMap<string, string> jsonCompatibleIdentities)
    {
        Id = id;
        Name = name;
        Version = version;
        Claims = claims;
        Identities = new();
        JsonCompatibleIdentities = jsonCompatibleIdentities;
    }

    // Record copy constructor.
    // Overriden to ensure _claimsPrincipalLazy is recreated.
    protected User(User other)
    {
        Id = other.Id;
        Version = other.Version;
        Name = other.Name;
        Claims = other.Claims;
        Identities = other.Identities;
        _claimsPrincipalLazy = new(CreateClaimsPrincipal);
    }

    public User WithClaim(string name, string value)
        => this with { Claims = Claims.With(name, value) };
    public User WithIdentity(UserIdentity identity, string secret = "")
        => this with { Identities = Identities.With(identity, secret) };

    public bool IsAuthenticated()
        => !Id.IsNullOrEmpty();
    public bool IsGuest()
        => Id.IsNullOrEmpty();
    public virtual bool IsInRole(string role)
        => Claims.ContainsKey($"{ClaimTypes.Role}/{role}");

    public virtual User ToClientSideUser()
    {
        if (Identities.IsEmpty)
            return this;

        var maskedIdentities = new ApiMap<UserIdentity, string>();
        foreach (var (id, _) in Identities)
            maskedIdentities.TryAdd((id.Schema, "<hidden>"), "");
        return this with { Identities = maskedIdentities };
    }

    public ClaimsPrincipal ToClaimsPrincipal()
        => (_claimsPrincipalLazy ??= new(CreateClaimsPrincipal)).Value;

    // Equality is changed back to reference-based

    public virtual bool Equals(User? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    // The default would print Identities, whose values are the per-provider secrets, plus
    // Claims and a second copy of the identities via JsonCompatibleIdentities. A whole User
    // travels inside AuthBackend_SignIn, and commands do get logged.
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Id = ").Append(Id)
            .Append(", Name = ").Append(Name)
            .Append(", Version = ").Append(Version.ToString(CultureInfo.InvariantCulture));
        if (Sanitization.IsSuspended)
            return PrintSecrets(builder);

        // The claim names and the identity schemas are the parts worth having in a log
        AppendNames(builder.Append(", Claims = "), Claims.Keys);
        AppendNames(builder.Append(", Identities = "), Identities.Keys.Select(x => x.Schema));
        return true;
    }

    // Private methods

    private bool PrintSecrets(StringBuilder builder)
    {
        builder.Append(", Claims = ").Append(Claims)
            .Append(", Identities = ").Append(Identities);
        return true;
    }

    private static void AppendNames(StringBuilder builder, IEnumerable<string> names)
    {
        builder.Append('[');
        var i = 0;
        foreach (var name in names) {
            if (i >= ApiCollectionExt.MaxToStringItems) {
                builder.Append(", ...");
                break;
            }
            if (i++ > 0)
                builder.Append(", ");
            builder.Append(name);
        }
        builder.Append(']');
    }

    // Protected methods

    protected virtual ClaimsPrincipal CreateClaimsPrincipal()
    {
        var claims = new List<Claim>();
        if (IsGuest()) {
            // Guest (not authenticated)
            if (!Name.IsNullOrEmpty())
                claims.Add(new(ClaimTypes.Name, Name, ClaimValueTypes.String));
            foreach (var (key, value) in Claims)
                claims.Add(new Claim(key, value));
            var claimsIdentity = new ClaimsIdentity(claims);
            return new ClaimsPrincipal(claimsIdentity);
        }
        else {
            // Authenticated
            claims.Add(new Claim(ClaimTypes.NameIdentifier, Id, ClaimValueTypes.String));
            claims.Add(new(ClaimTypes.Version, Version.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.String));
            if (!Name.IsNullOrEmpty())
                claims.Add(new(ClaimTypes.Name, Name, ClaimValueTypes.String));
            foreach (var (key, value) in Claims)
                claims.Add(new Claim(key, value));
            var claimsIdentity = new ClaimsIdentity(claims, UserIdentity.DefaultSchema);
            return new ClaimsPrincipal(claimsIdentity);
        }
    }
}
