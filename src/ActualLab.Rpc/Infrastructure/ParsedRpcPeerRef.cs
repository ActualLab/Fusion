using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc.Infrastructure;

public record ParsedRpcPeerRef
{
    public const string LoopbackKeyPrefix = "loopback:";
    public const string LocalKeyPrefix = "local:";
    public const string NoneKeyPrefix = "none:";
    public const string BackendKeyPrefix = "backend.";
    public const string ServerKeyPrefix = "server.";
    public const char SerializationFormatDelimiter = '.';

    [field: AllowNull, MaybeNull]
    public string Key {
        get => field ??= FormatKey();
        init;
    }

    public bool IsServer { get; init; }
    public bool IsBackend { get; init; }
    public string SerializationFormatKey { get; init; } = "";
    public RpcPeerConnectionKind ConnectionKind { get; init; } = RpcPeerConnectionKind.Remote;
    public VersionSet Versions { get; init; } = VersionSet.Empty;
    public string Unparsed { get; init; } = "";

    public static ParsedRpcPeerRef Parse(string key)
    {
        if (key.IsNullOrEmpty())
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        var s = key;
        var connectionKind = RpcPeerConnectionKind.Remote;
        if (HasPrefix(ref s, LocalKeyPrefix))
            connectionKind = RpcPeerConnectionKind.Local;
        else if (HasPrefix(ref s, LoopbackKeyPrefix))
            connectionKind = RpcPeerConnectionKind.Loopback;
        else if (HasPrefix(ref s, NoneKeyPrefix))
            connectionKind = RpcPeerConnectionKind.None;

        var isBackend = HasPrefix(ref s, BackendKeyPrefix);
        var isServer = HasPrefix(ref s, BackendKeyPrefix);
        var serializationFormatKey = GetDelimitedPrefix(ref s, SerializationFormatDelimiter) ?? "";
        var versions = isBackend
            ? RpcDefaults.BackendPeerVersions
            : RpcDefaults.ApiPeerVersions;

        return new ParsedRpcPeerRef {
            Key = key,
            IsServer = isServer,
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormatKey = serializationFormatKey,
            Versions = versions,
            Unparsed = s,
        };
    }

    // Protected methods

    protected virtual string FormatKey()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append(ConnectionKind switch {
            RpcPeerConnectionKind.Remote => "",
            RpcPeerConnectionKind.Loopback => LoopbackKeyPrefix,
            RpcPeerConnectionKind.Local => LocalKeyPrefix,
            RpcPeerConnectionKind.None => NoneKeyPrefix,
            _ => throw new ArgumentOutOfRangeException(nameof(ConnectionKind), ConnectionKind, null),
        });
        if (IsBackend)
            sb.Append(BackendKeyPrefix);
        if (IsServer)
            sb.Append(ServerKeyPrefix);
        if (!SerializationFormatKey.IsNullOrEmpty())
            sb.Append(SerializationFormatKey).Append(SerializationFormatDelimiter);
        sb.Append(Unparsed);
        return sb.ToStringAndRelease();
    }

    // Helpers

    protected static bool HasPrefix(ref string s, string prefix) {
        if (!s.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        s = s[prefix.Length..];
        return true;
    }

    protected static string? GetDelimitedPrefix(ref string s, char delimiter)
    {
        var delimiterIndex = s.IndexOf(delimiter);
        if (delimiterIndex < 0)
            return null;

        var prefix = s[..delimiterIndex];
        s = s[(delimiterIndex + 1)..];
        return prefix;
    }

    protected static string? GetDelimitedSuffix(ref string s, char delimiter)
    {
        var delimiterIndex = s.LastIndexOf(delimiter);
        if (delimiterIndex < 0)
            return null;

        var prefix = s[(delimiterIndex + 1)..];
        s = s[..delimiterIndex];
        return prefix;
    }
}
