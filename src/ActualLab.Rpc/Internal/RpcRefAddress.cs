using System.Text;

namespace ActualLab.Rpc.Internal;

/// <summary>
/// Formats and parses <see cref="RpcRef"/> address strings containing connection kind, tags, and host info.
/// </summary>
public static class RpcRefAddress
{
    public const char TagDelimiter = '.';
    public const string HostInfoDelimiter = "://";
    public const string ServerTag = "server";
    public const string BackendTag = "backend";

    public static string Format(RpcRef rpcRef)
    {
        var sb = StringBuilderExt.Acquire();
        AddTag(sb, rpcRef.ConnectionKind.Format());
        AddTag(sb, rpcRef.IsBackend ? BackendTag : "");
        AddTag(sb, rpcRef.IsServer ? ServerTag : "");
        AddTag(sb, rpcRef.SerializationFormat);
        sb.Append(HostInfoDelimiter).Append(rpcRef.HostInfo);
        return sb.ToStringAndRelease();
    }

    public static RpcRef Parse(string address, bool initialize = true)
        => TryParse(address, initialize) ?? throw Errors.InvalidRpcRefAddress(address);

    public static RpcRef? TryParse(string address, bool initialize = true)
    {
        if (address.IsNullOrEmpty())
            return null;

        var dataDelimiterIndex = address.IndexOf(HostInfoDelimiter, StringComparison.Ordinal);
        if (dataDelimiterIndex < 0)
            return null;

        var hostInfo = address[(dataDelimiterIndex + HostInfoDelimiter.Length)..];
        var s = address.AsSpan(0, dataDelimiterIndex);
        if (!RpcPeerConnectionKindExt.TryParse(GetNextTag(ref s), out var connectionKind))
            return null;

        var isBackend = HasNextTag(ref s, BackendTag);
        var isServer = HasNextTag(ref s, ServerTag);
        var serializationFormat = GetNextTag(ref s).ToString();
        var rpcRef = new RpcRef {
            Address = address,
            IsServer = isServer,
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            HostInfo = hostInfo,
        };
        if (initialize)
            rpcRef.Initialize();
        return rpcRef;
    }

    // Formatting and parsing helpers

    public static void AddTag(StringBuilder sb, string tag)
    {
        if (tag.IsNullOrEmpty())
            return;

        if (sb.Length != 0)
            sb.Append(TagDelimiter);
        sb.Append(tag);
    }

    public static ReadOnlySpan<char> GetNextTag(ref ReadOnlySpan<char> s)
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

    public static bool HasNextTag(ref ReadOnlySpan<char> s, string tag)
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
