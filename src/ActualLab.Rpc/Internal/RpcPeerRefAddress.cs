using System.Text;

namespace ActualLab.Rpc.Internal;

public static class RpcPeerRefAddress
{
    public const char TagDelimiter = '.';
    public const string HostInfoDelimiter = "://";
    public const string ServerTag = "server";
    public const string BackendTag = "backend";

    public static string Format(RpcPeerRef peerRef)
    {
        var sb = StringBuilderExt.Acquire();
        AddTag(sb, peerRef.ConnectionKind.Format());
        AddTag(sb, peerRef.IsBackend ? BackendTag : "");
        AddTag(sb, peerRef.IsServer ? ServerTag : "");
        AddTag(sb, peerRef.SerializationFormat);
        sb.Append(HostInfoDelimiter).Append(peerRef.HostInfo);
        return sb.ToStringAndRelease();
    }

    public static RpcPeerRef Parse(string address, bool initialize = true)
        => TryParse(address, initialize) ?? throw Errors.InvalidRpcPeerRefAddress(address);

    public static RpcPeerRef? TryParse(string address, bool initialize = true)
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
        var peerRef = isServer
            ? (RpcPeerRef)new RpcServerPeerRef {
                Address = address,
                IsBackend = isBackend,
                ConnectionKind = connectionKind,
                SerializationFormat = serializationFormat,
                HostInfo = hostInfo,
            }
            : new RpcClientPeerRef {
                Address = address,
                IsBackend = isBackend,
                ConnectionKind = connectionKind,
                SerializationFormat = serializationFormat,
                HostInfo = hostInfo,
            };
        if (initialize)
            peerRef.Initialize();
        return peerRef;
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
