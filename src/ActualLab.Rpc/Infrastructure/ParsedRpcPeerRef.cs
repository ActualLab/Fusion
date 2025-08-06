using System.Diagnostics.CodeAnalysis;
using System.Text;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public record ParsedRpcPeerRef
{
    protected const char TagDelimiter = '.';
    protected const string DataDelimiter = "://";
    protected const string BackendTag = "backend";
    protected const string ServerTag = "server";

    [field: AllowNull, MaybeNull]
    public string Id {
        get => field ??= FormatId();
        init;
    }

    public bool IsServer { get; init; }
    public bool IsBackend { get; init; }
    public RpcPeerConnectionKind ConnectionKind { get; init; } = RpcPeerConnectionKind.Remote;
    public string SerializationFormat { get; init; } = "";
    public VersionSet Versions { get; init; } = VersionSet.Empty;
    public string Data { get; init; } = "";

    public static ParsedRpcPeerRef Parse(string id)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));

        var dataDelimiterIndex = id.IndexOf(DataDelimiter, StringComparison.Ordinal);
        if (dataDelimiterIndex < 0)
            throw Errors.InvalidRpcPeerRefIdFormat(id);

        var data = id[(dataDelimiterIndex + DataDelimiter.Length)..];
        var s = id.AsSpan(0, dataDelimiterIndex);

        if (!RpcPeerConnectionKindExt.TryParse(GetNextTag(ref s), out var connectionKind))
            throw Errors.InvalidRpcPeerRefIdFormat(id);

        var isBackend = HasNextTag(ref s, BackendTag);
        var isServer = HasNextTag(ref s, ServerTag);
        var serializationFormat = GetNextTag(ref s).ToString();
        var versions = isBackend
            ? RpcDefaults.BackendPeerVersions
            : RpcDefaults.ApiPeerVersions;

        return new ParsedRpcPeerRef {
            Id = id,
            IsServer = isServer,
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            Versions = versions,
            Data = data,
        };
    }

    // Protected methods

    protected virtual string FormatId()
    {
        var sb = StringBuilderExt.Acquire();
        AddTag(sb, ConnectionKind.Format());
        AddTag(sb, IsBackend ? BackendTag : "");
        AddTag(sb, IsServer ? ServerTag : "");
        AddTag(sb, SerializationFormat);
        sb.Append(DataDelimiter).Append(Data);
        return sb.ToStringAndRelease();
    }

    // Helpers

    protected static void AddTag(StringBuilder sb, string tag)
    {
        if (tag.IsNullOrEmpty())
            return;

        if (sb.Length != 0)
            sb.Append(TagDelimiter);
        sb.Append(tag);
    }

    protected static ReadOnlySpan<char> GetNextTag(ref ReadOnlySpan<char> s)
    {
        var delimiterIndex = s.IndexOf(TagDelimiter);
        ReadOnlySpan<char> tag;
        if (delimiterIndex < 0) {
            tag = s;
            s = ReadOnlySpan<char>.Empty;
        }
        else {
            tag = s[..delimiterIndex];
            s = s[(delimiterIndex + 1)..];
        }
        return tag;
    }

    protected static bool HasNextTag(ref ReadOnlySpan<char> s, string tag)
    {
        if (!s.StartsWith(tag, StringComparison.Ordinal))
            return false;

        var skipLength = tag.Length;
        if (s.Length > skipLength && s[skipLength] == TagDelimiter)
            skipLength++;

        s = s[skipLength..];
        return true;
    }

}
