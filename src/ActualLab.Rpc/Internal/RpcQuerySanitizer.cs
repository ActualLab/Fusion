using System.Globalization;

namespace ActualLab.Rpc.Internal;

/// <summary>
/// Rewrites request query strings into a form that's safe to log:
/// values are dropped unless their parameter is explicitly allowed,
/// because a query may carry bearer credentials (e.g. Fusion's <c>session</c>).
/// </summary>
public static class RpcQuerySanitizer
{
    public const string RedactedValue = "<redacted>";

    // "c" is the reconnect proof counter - not a secret, and useful when diagnosing a rejected
    // reconnect. Its companion "p" (the proof itself) is deliberately absent, i.e. redacted.
    public static ImmutableHashSet<string> AllowedParameterNames { get; set; }
        = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "f", "serializationFormat", "c");
    public static ImmutableHashSet<string> HashedParameterNames { get; set; }
        = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "clientId");

    public static string Sanitize(string? query)
    {
        if (query.IsNullOrEmpty())
            return "";

        var items = query[0] == '?' ? query[1..] : query;
        if (items.Length == 0)
            return "";

        var sb = StringBuilderExt.Acquire();
        sb.Append('?');
        var isFirst = true;
        foreach (var item in items.Split('&')) {
            if (!isFirst)
                sb.Append('&');
            isFirst = false;

            var equalsIndex = item.IndexOf("=", StringComparison.Ordinal);
            if (equalsIndex < 0) {
                sb.Append(item);
                continue;
            }

            var name = item[..equalsIndex];
            sb.Append(name).Append('=').Append(SanitizeValue(name, item[(equalsIndex + 1)..]));
        }
        return sb.ToStringAndRelease();
    }

    // The hash is non-cryptographic and short, but it's enough to correlate log records
    // originating from the same value - which is the only purpose it serves here.
    public static string Hash(string value)
        => ((uint)value.GetXxHash3()).ToString("x8", CultureInfo.InvariantCulture);

    // Private methods

    private static string SanitizeValue(string name, string value)
    {
        if (value.Length == 0)
            return value;
        if (AllowedParameterNames.Contains(name))
            return value;
        if (HashedParameterNames.Contains(name))
            return Hash(value);

        return RedactedValue;
    }
}
