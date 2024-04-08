using System.ComponentModel;
using System.Globalization;
using Microsoft.Toolkit.HighPerformance;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using Cysharp.Text;

namespace ActualLab.Fusion;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[JsonConverter(typeof(SessionJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(SessionNewtonsoftJsonConverter))]
[TypeConverter(typeof(SessionTypeConverter))]
public sealed partial class Session : IHasId<Symbol>,
    IEquatable<Session>, IConvertibleTo<string>, IConvertibleTo<Symbol>,
    IHasToStringProducingJson
{
    public static readonly Session Default = new("~");
    public static readonly string ShardTag = "s";
    public static SessionFactory Factory { get; set; } = DefaultSessionFactory.New();
    public static SessionValidator Validator { get; set; } = session => !session.IsDefault();

    private string? _hash;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public Symbol Id { get; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore]
    public string Hash => _hash ??= ComputeHash();

    public static Session New()
        => Factory.Invoke();

    [MemoryPackConstructor]
    public Session(Symbol id)
    {
        // The check is here to prevent use of sessions with empty or other special Ids,
        // which could be a source of security problems later.
        var idValue = id.Value;
        if (idValue.Length < 8 && !(idValue.Length == 1 && idValue[0] == '~'))
            throw Errors.InvalidSessionId(id);
        Id = id;
    }

    public string GetTags()
    {
        var s = Id.Value;
        var startIndex = s.IndexOf('&', StringComparison.Ordinal);
        return startIndex < 0 ? "" : s[(startIndex + 1)..];
    }

    public string GetTag(string tag)
    {
        var s = Id.Value;
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
        var s = Id.Value;
        var startIndex = s.IndexOf('&', StringComparison.Ordinal);
        s = startIndex < 0 ? s : s[startIndex..];
        if (tags.IsNullOrEmpty())
            return startIndex < 0 ? this : new Session(s);
        return new Session(ZString.Concat(s, '&', tags));
    }

    public Session WithTag(string tag, string value)
    {
        var s = Id.Value;
        var tagPrefix = $"&{tag}=";
        var startIndex = s.IndexOf(tagPrefix, StringComparison.Ordinal);
        if (startIndex > 0) {
            var endIndex = s.IndexOf('&', startIndex + tagPrefix.Length);
            s = endIndex < 0
                ? s[..startIndex]
#if NETCOREAPP3_1_OR_GREATER
                : string.Concat(s.AsSpan(0, startIndex), s.AsSpan(startIndex + s.Length));
#else
                : string.Concat(s.Substring(0, startIndex), s.Substring(startIndex + s.Length));
#endif
        }
        return new Session(ZString.Concat(s, tagPrefix, value));
    }

    // We use non-cryptographic hash here because System.Security.Cryptography isn't supported in Blazor.
    // The length of hash is much smaller than Session.Id, so it's still almost impossible to guess
    // SessionId by knowing it; on the other hand, ~4B hash variants are enough to identify
    // a Session of a given user, and that's the only purpose of this hash.
    private string ComputeHash()
        => ((uint) Id.Value.GetDjb2HashCode()).ToString("x8", CultureInfo.InvariantCulture);

    // Conversion

    public override string ToString() => Id.Value;

    Symbol IConvertibleTo<Symbol>.Convert() => Id;
    string IConvertibleTo<string>.Convert() => Id.Value;

    // Equality

    public bool Equals(Session? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (ReferenceEquals(null, other))
            return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Session s && Equals(s);
    public override int GetHashCode() => Id.HashCode;
    public static bool operator ==(Session? left, Session? right) => Equals(left, right);
    public static bool operator !=(Session? left, Session? right) => !Equals(left, right);
}
