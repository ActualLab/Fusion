using System.ComponentModel;
using System.Globalization;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using MessagePack;

namespace ActualLab.Fusion;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackFormatter(typeof(SessionMessagePackFormatter))]
[JsonConverter(typeof(SessionJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(SessionNewtonsoftJsonConverter))]
[TypeConverter(typeof(SessionTypeConverter))]
public sealed partial class Session : IHasId<string>,
    IEquatable<Session>, IConvertibleTo<string>,
    IHasToStringProducingJson
{
    public static readonly Session Default = new("~");
    public static SessionFactory Factory { get; set; } = DefaultSessionFactory.New();
    public static SessionValidator Validator { get; set; } = session => !session.IsDefault();

    private int _hashCode;

    [DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter]
    public string Id { get; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Hash => field ??= ComputeHash();

    public static Session New()
        => Factory.Invoke();

    [MemoryPackConstructor, SerializationConstructor]
    public Session(string id)
    {
        // The check is here to prevent use of sessions with empty or other special Ids,
        // which could be a source of security problems later.
        if (id.IsNullOrEmpty() || (id.Length < 8 && !(id is ['~'])))
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
        id = startIndex < 0 ? id : id[startIndex..];
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
            id = endIndex < 0
                ? id[..startIndex]
#if NETCOREAPP3_1_OR_GREATER
                : string.Concat(id.AsSpan(0, startIndex), id.AsSpan(startIndex + id.Length));
#else
                : string.Concat(id.Substring(0, startIndex), id.Substring(startIndex + id.Length));
#endif
        }
        return new Session(string.Concat(id, tagPrefix, value));
    }

    // We use non-cryptographic hash here because System.Security.Cryptography isn't supported in Blazor.
    // The length of hash is much smaller than Session.Id, so it's still almost impossible to guess
    // SessionId by knowing it; on the other hand, ~4B hash variants are enough to identify
    // a Session of a given user, and that's the only purpose of this hash.
    private string ComputeHash()
        => ((uint)Id.GetXxHash3()).ToString("x8", CultureInfo.InvariantCulture);

    // Conversion

    public override string ToString()
        => Id;

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
}
