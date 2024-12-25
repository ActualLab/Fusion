using System.Globalization;
using System.Security;
using System.Security.Claims;
using System.Text.Json.Serialization;
using ActualLab.Fusion.Authentication;
using ActualLab.Requirements;
using ActualLab.Versioning;
using MessagePack;

namespace ActualLab.Fusion.Tests.Serialization;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial record OldUser : IHasId<Symbol>, IHasVersion<long>, IRequirementTarget
{
    public static string GuestName { get; set; } = "Guest";
    public static Requirement<OldUser> MustExist { get; set; }
        = new MustExistRequirement<OldUser>().With("You must sign-in to perform this action.", m => new SecurityException(m));
    public static Requirement<OldUser> MustBeAuthenticated { get; set; }
        = Requirement.New(
            (OldUser? u) => u?.IsAuthenticated() ?? false,
            new("User is not authenticated.", m => new SecurityException(m)));

    private Lazy<ClaimsPrincipal>? _claimsPrincipalLazy;

    [DataMember, MemoryPackOrder(0)]
    public Symbol Id { get; init; }
    [DataMember, MemoryPackOrder(1)]
    public string Name { get; init; }
    [DataMember, MemoryPackOrder(2)]
    public long Version { get; init; }
    [DataMember, MemoryPackOrder(3)]
    public ImmutableDictionary<string, string> Claims { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ImmutableDictionary<UserIdentity, string> Identities { get; init; }

    // Computed properties

    [DataMember(Name = nameof(Identities)), MemoryPackOrder(4)]
    [JsonPropertyName(nameof(Identities)),  Newtonsoft.Json.JsonProperty(nameof(Identities))]
    public Dictionary<string, string> JsonCompatibleIdentities {
        get => Identities.ToDictionary(p => p.Key.Id.Value, p => p.Value, StringComparer.Ordinal);
        init => Identities = value.ToImmutableDictionary(p => new UserIdentity(p.Key), p => p.Value);
    }

    public static OldUser NewGuest(string? name = null)
        => new(name ?? GuestName);

    public OldUser(string name) : this(Symbol.Empty, name) { }
    public OldUser(Symbol id, string name)
    {
        Id = id;
        Name = name;
        Claims = ImmutableDictionary<string, string>.Empty;
        Identities = ImmutableDictionary<UserIdentity, string>.Empty;
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public OldUser(
        Symbol id,
        string name,
        long version,
        ImmutableDictionary<string, string> claims,
        Dictionary<string, string> jsonCompatibleIdentities)
    {
        Id = id;
        Name = name;
        Version = version;
        Claims = claims;
        Identities = ImmutableDictionary<UserIdentity, string>.Empty;
        JsonCompatibleIdentities = jsonCompatibleIdentities;
    }

    // Record copy constructor.
    // Overriden to ensure _claimsPrincipalLazy is recreated.
    protected OldUser(OldUser other)
    {
        Id = other.Id;
        Version = other.Version;
        Name = other.Name;
        Claims = other.Claims;
        Identities = other.Identities;
        _claimsPrincipalLazy = new(CreateClaimsPrincipal);
    }

    public OldUser WithClaim(string name, string value)
        => this with { Claims = Claims.SetItem(name, value) };
    public OldUser WithIdentity(UserIdentity identity, string secret = "")
        => this with { Identities = Identities.SetItem(identity, secret) };

    public bool IsAuthenticated()
        => !Id.IsEmpty;
    public bool IsGuest()
        => Id.IsEmpty;
    public virtual bool IsInRole(string role)
        => Claims.ContainsKey($"{ClaimTypes.Role}/{role}");

    public virtual OldUser ToClientSideUser()
    {
        if (Identities.IsEmpty)
            return this;
        var maskedIdentities = ImmutableDictionary<UserIdentity, string>.Empty;
        foreach (var (id, _) in Identities)
            maskedIdentities = maskedIdentities.Add((id.Schema, "<hidden>"), "");
        return this with { Identities = maskedIdentities };
    }

    public ClaimsPrincipal ToClaimsPrincipal()
        => (_claimsPrincipalLazy ??= new(CreateClaimsPrincipal)).Value;

    // Equality is changed back to reference-based

    public virtual bool Equals(OldUser? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

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
