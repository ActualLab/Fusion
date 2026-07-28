using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using MessagePack;

namespace ActualLab.Fusion;

/// <summary>
/// Represents an authenticated user session identified by a unique string <see cref="Id"/>.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackFormatter(typeof(SessionMessagePackFormatter))]
[JsonConverter(typeof(SessionJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(SessionNewtonsoftJsonConverter))]
[TypeConverter(typeof(SessionTypeConverter))]
public sealed partial class Session : IHasId<string>,
    IEquatable<Session>, IConvertibleTo<string>
{
    // A default Id is 20 chars over a 64-symbol alphabet, i.e. ~120 bits of entropy.
    // ToString() reveals IdPrefixLength of them = 24 bits, so ~96 bits stay secret -
    // still far out of brute-force reach, but enough for a human to match a log line
    // against a UI row or a DB record.
    // ':' isn't a hex digit, isn't in RandomStringGenerator.DefaultAlphabet ("...-_"),
    // and isn't part of the Id tag syntax ("&s=..."), so the two parts always split cleanly.
    public const string IdPrefixSeparator = ":";
    public const int IdPrefixLength = 4;

    public static readonly Session Default = new("~");
    public static SessionFactory Factory { get; set; } = DefaultSessionFactory.New();
    public static SessionValidator Validator { get; set; } = session => !session.IsDefault();

    private int _hashCode;
    private string? _toString;
    private byte[]? _sha256Hash;

    [DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter]
    public string Id { get; }
    // We use a non-cryptographic hash here because it's on the wire (SessionAuthInfo.SessionHash,
    // Auth_SignOut.KickUserSessionHash), and changing it would break those comparisons across
    // versions. The length of hash is much smaller than Session.Id, so it's still almost
    // impossible to guess SessionId by knowing it; on the other hand, ~4B hash variants are
    // enough to identify a Session of a given user, and that's the only purpose of this hash.
    // Use Sha256Hash when a strong, collision-resistant identifier is needed.
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Hash => field ??= ComputeHash();
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ReadOnlyMemory<byte> Sha256Hash => _sha256Hash ??= ComputeSha256Hash();

    public static Session New()
        => Factory.Invoke();

    [MemoryPackConstructor, SerializationConstructor]
    public Session(string id)
    {
        // The check is here to prevent use of sessions with empty or other special Ids,
        // which could be a source of security problems later.
        if (id.IsNullOrEmpty() || (id.Length < 8 && id is not ['~']))
            throw Errors.InvalidSessionId(id);

        Id = id;
    }

    public string GetTags()
    {
        var s = Id;
        var startIndex = s.IndexOf('&', StringComparison.Ordinal);
        return startIndex < 0 ? "" : s[(startIndex + 1)..];
    }

    public string GetTag(string tag)
    {
        var s = Id;
        var tagPrefix = $"&{tag}=";
        var startIndex = s.IndexOf(tagPrefix, StringComparison.Ordinal);
        if (startIndex < 0)
            return "";

        var valueIndex = startIndex + tagPrefix.Length;
        var endIndex = s.IndexOf('&', valueIndex);
        if (endIndex < 0)
            endIndex = s.Length;
        return s.Substring(valueIndex, endIndex - valueIndex);
    }

    public Session WithTags(string tags)
    {
        var id = Id;
        var startIndex = id.IndexOf('&', StringComparison.Ordinal);
        id = startIndex < 0 ? id : id[..startIndex];
        if (tags.IsNullOrEmpty())
            return startIndex < 0 ? this : new Session(id);
        return new Session(string.Concat(id, "&", tags));
    }

    public Session WithTag(string tag, string value)
    {
        var id = Id;
        var tagPrefix = $"&{tag}=";
        var startIndex = id.IndexOf(tagPrefix, StringComparison.Ordinal);
        if (startIndex > 0) {
            var endIndex = id.IndexOf('&', startIndex + tagPrefix.Length);
            if (endIndex < 0)
                id = id[..startIndex];
            else
#if NETCOREAPP3_1_OR_GREATER
                return new Session(string.Concat(
                    id.AsSpan(0, startIndex),
                    tagPrefix.AsSpan(),
                    value.AsSpan(),
                    id.AsSpan(endIndex)));
#else
                return new Session(string.Concat(
                    id.Substring(0, startIndex),
                    tagPrefix,
                    value,
                    id.Substring(endIndex)));
#endif
        }
        return new Session(string.Concat(id, tagPrefix, value));
    }

    // Conversion

    public override string ToString()
        => _toString ??= ComputeToString();

    string IConvertibleTo<string>.Convert() => Id;

    // Equality

    public bool Equals(Session? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (ReferenceEquals(null, other))
            return false;
        return string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
        => obj is Session s && Equals(s);

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        if (_hashCode == 0) {
            var hashCode = Id.GetOrdinalHashCode();
            if (hashCode == 0)
                hashCode = 1;
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            _hashCode = hashCode;
        }

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return _hashCode;
    }

    public static bool operator ==(Session? left, Session? right) => Equals(left, right);
    public static bool operator !=(Session? left, Session? right) => !Equals(left, right);

    // Private methods

    private string ComputeToString()
    {
        var id = Id;
        var prefix = id.Length <= IdPrefixLength ? id : id.Substring(0, IdPrefixLength);
        return string.Concat(prefix, IdPrefixSeparator, Hash);
    }

    private string ComputeHash()
        => ((uint)Id.GetXxHash3()).ToString("x8", CultureInfo.InvariantCulture);

    private byte[] ComputeSha256Hash()
    {
        // SHA-256 is fine everywhere this library runs, incl. Blazor WASM: the browser runtime
        // ships a managed SHA implementation (SHAManagedHashProvider), so this stays synchronous.
        var bytes = Encoding.UTF8.GetBytes(Id);
#if NET5_0_OR_GREATER
        return SHA256.HashData(bytes);
#else
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(bytes);
#endif
    }
}
