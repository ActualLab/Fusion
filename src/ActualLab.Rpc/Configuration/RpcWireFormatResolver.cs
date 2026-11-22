namespace ActualLab.Rpc;

/// <summary>
/// Resolves <see cref="RpcWireFormat"/> instances by key, with a configurable default.
/// A container's resolver is built from <see cref="RpcConfiguration.WireFormats"/>.
/// </summary>
public sealed class RpcWireFormatResolver
{
    public IReadOnlyDictionary<string, RpcWireFormat> Formats { get; }
    public RpcWireFormat DefaultFormat { get; }
    public ImmutableHashSet<string> BackendOnlyFormatKeys { get; }

    // Freezes formats, so the resolver and the set it's built from can't diverge
    public RpcWireFormatResolver(RpcWireFormatConfiguration formats)
    {
        formats.Freeze();
        var defaultFormat = formats.DefaultFormat;
        if (!formats.TryGetValue(defaultFormat.Key, out var format))
            throw new ArgumentException(
                $"The default format '{defaultFormat.Key}' isn't one of the configured formats.", nameof(formats));
        if (!ReferenceEquals(format, defaultFormat))
            throw new ArgumentException(
                $"The default format '{defaultFormat.Key}' isn't the format configured under this key - "
                + $"use f.DefaultFormat = f[\"{defaultFormat.Key}\"].", nameof(formats));

        Formats = formats;
        DefaultFormat = defaultFormat;
        BackendOnlyFormatKeys = formats.BackendOnlyFormatKeys;
    }

    public override string ToString()
        => $"{GetType().GetName()}({DefaultFormat.Key}, [{Formats.Keys.ToDelimitedString()}])";

    // Get and TryGet

    public RpcWireFormat Get(string key)
        => TryGet(key, out var value)
            ? value
            : throw new KeyNotFoundException($"No format with key '{key}'.");

    public bool TryGet(string key, [MaybeNullWhen(false)] out RpcWireFormat value)
    {
        if (key.IsNullOrEmpty())
            key = DefaultFormat.Key;

        return Formats.TryGetValue(key, out value);
    }

    // Client-selected formats

    public bool IsClientSelectable(string key)
        => !BackendOnlyFormatKeys.Contains(key);

    // Servers call this to validate the format key a client pinned in its connection URL;
    // an empty key means "server picks", so it's always fine.
    public string? GetClientRejectionReason(string key)
    {
        if (!TryGet(key, out _))
            return $"unsupported RPC wire format '{key}'";
        if (!IsClientSelectable(key))
            return $"RPC wire format '{key}' can't be selected by a client - allow it with "
                + $"rpc.ConfigureWireFormats(f => f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Remove(\"{key}\"))";

        return null;
    }
}
