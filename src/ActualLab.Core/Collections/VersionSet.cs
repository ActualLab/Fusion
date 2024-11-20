using System.Diagnostics.CodeAnalysis;
using ActualLab.Comparison;
using ActualLab.Internal;
using MessagePack;

namespace ActualLab.Collections;

#pragma warning disable CA1721

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record VersionSet(
    [property: JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    IReadOnlyDictionary<Symbol, Version> Items
) {
    public static readonly Version ZeroVersion = new();
    public static readonly ListFormat ListFormat = ListFormat.CommaSeparated;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    [field: AllowNull, MaybeNull]
    public string Value => field ??= Format();

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int Count => Items.Count;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int HashCode {
        get {
            if (field == 0) {
                var hashCode = 0;
                foreach (var (scope, version) in Items)
                    hashCode ^= System.HashCode.Combine(scope.HashCode, version.GetHashCode());
                if (hashCode == 0)
                    hashCode = 1;
                field = hashCode;
            }
            return field;
        }
    }

    public Version this[Symbol scope]
        => Items.GetValueOrDefault(scope, ZeroVersion)!;

    // Constructors

    public VersionSet()
        : this(ImmutableDictionary<Symbol, Version>.Empty)
    { }

    public VersionSet(Symbol scope, Version version)
        : this(new Dictionary<Symbol, Version>() { { scope, version } })
    { }

    public VersionSet(Symbol scope, string version)
        : this(new Dictionary<Symbol, Version>() { { scope, VersionExt.Parse(version) } })
    { }

    public VersionSet(params (Symbol Scope, Version Version)[] versions)
        : this(versions.ToDictionary(kv => kv.Scope, kv => kv.Version))
    { }

    public VersionSet(params (Symbol Scope, string Version)[] versions)
        : this(versions.ToDictionary(kv => kv.Scope, kv => VersionExt.Parse(kv.Version)))
    { }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public VersionSet(string? value)
        : this(Parse(value).Items)
    { }

    // Conversion

    public override string ToString()
        => Value;

    // Equality

    public bool Equals(VersionSet? other)
    {
        if (ReferenceEquals(other, null) || Count != other.Count || HashCode != other.HashCode)
            return false;

        foreach (var (scope, version) in other.Items)
            if (Items.GetValueOrDefault(scope) != version)
                return false;
        return true;
    }

    public override int GetHashCode() => HashCode;

    // Parse and TryParse

    public static VersionSet Parse(string? s, bool ignoreErrors = false)
        => TryParse(s, ignoreErrors, out var result) ? result : throw Errors.Format<VersionSet>(s);

    public static bool TryParse(string? s, [MaybeNullWhen(false)] out VersionSet result)
        => TryParse(s, false, out result);
    public static bool TryParse(string? s, bool ignoreErrors, [MaybeNullWhen(false)] out VersionSet result)
    {
        if (s.IsNullOrEmpty()) {
            result = new VersionSet();
            return true;
        }

        result = null;
        var versions = new Dictionary<Symbol, Version>();
        using var parser = ListFormat.CreateParser(s);
        while (parser.TryParseNext()) {
            var item = parser.Item;
            var equalsIndex = item.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex < 0) {
                if (ignoreErrors)
                    continue;
                return false;
            }
#if NETSTANDARD2_0
            if (!Version.TryParse(item.Substring(equalsIndex + 1), out var version)) {
#else
            if (!Version.TryParse(item.AsSpan(equalsIndex + 1), out var version)) {
#endif
                if (ignoreErrors)
                    continue;
                return false;
            }

            var scope = (Symbol)item[..equalsIndex];
            versions[scope] = version;
        }

        result = new VersionSet(versions);
        return true;
    }

    // Private methods

    private string Format()
    {
        if (Items.Count == 0)
            return "";

        using var formatter = ListFormat.CreateFormatter();
        foreach (var (scope, version) in Items)
            formatter.Append($"{scope.Value}={version.Format()}");
        formatter.AppendEnd();
        return formatter.Output;
    }
}
