using System.Globalization;

namespace ActualLab.Compliance;

// Sanitizers that know something about the shape of what they're sanitizing, rather than
// treating it as an opaque string. See Sanitizers.Basic.cs for the shape-agnostic ones.
public static partial class Sanitizers
{
    // Nested types

    /// <summary>
    /// Renders a Fusion session id the way <c>Session.ToString()</c> does: <c>"Ab3f:9644ea3b"</c>,
    /// a prefix to eyeball against a UI row plus a hash to correlate on. Of a default id's
    /// ~120 bits of entropy this reveals 24.
    /// </summary>
    public sealed class SessionString : Sanitizer
    {
        public const string PrefixSeparator = ":";
        public const int PrefixLength = 4;

        protected override string Sanitize(string value)
        {
            if (value.Length == 0)
                return value;

            // Deliberately not Get<Fingerprint>(): this format is Session.ToString()'s, and its
            // hash is bare rather than wrapped in the "<<...>>" every other sanitizer produces.
            var prefix = value.Length <= PrefixLength ? value : value[..PrefixLength];
            var hash = ((uint)value.GetXxHash3()).ToString("x8", CultureInfo.InvariantCulture);
            return string.Concat(prefix, PrefixSeparator, hash);
        }
    }

    /// <summary>
    /// Sanitizes a URL query parameter by parameter: the selector maps a parameter name to the
    /// <see cref="Sanitizer"/> for its value, or to <c>null</c> to leave it alone. Having no
    /// parameterless constructor, it can't be a <see cref="SanitizedString{TSanitizer}"/> argument -
    /// derive with a fixed policy, as <see cref="RpcRequestQuery"/> does.
    /// </summary>
    public class UriQuery(Func<string, Sanitizer?> parameterSanitizerSelector) : Sanitizer
    {
        protected override string Sanitize(string value)
        {
            if (value.Length == 0)
                return value;

            var items = value[0] == '?' ? value[1..] : value;
            if (items.Length == 0)
                return value;

            var sb = StringBuilderExt.Acquire();
            if (value[0] == '?')
                sb.Append('?');
            var isFirst = true;
            foreach (var item in items.Split('&')) {
                if (!isFirst)
                    sb.Append('&');
                isFirst = false;

                var equalsIndex = item.IndexOf('=', StringComparison.Ordinal);
                if (equalsIndex < 0) {
                    sb.Append(item);
                    continue;
                }

                var name = item[..equalsIndex];
                var itemValue = item[(equalsIndex + 1)..];
                if (parameterSanitizerSelector.Invoke(name) is { } sanitizer)
                    itemValue = sanitizer.Apply(itemValue);
                sb.Append(name).Append('=').Append(itemValue);
            }
            return sb.ToStringAndRelease();
        }
    }

    /// <summary>
    /// The <see cref="UriQuery"/> policy for ActualLab's own RPC endpoints. Deny by default:
    /// a parameter is readable only if it's explicitly listed, so a query parameter added later
    /// can't start leaking a credential just because no one remembered to mask it.
    /// </summary>
    public sealed class RpcRequestQuery() : UriQuery(SelectSanitizer)
    {
        // "c" is the reconnect proof counter - not a secret, and useful when diagnosing a
        // rejected reconnect. Its companion "p" (the proof itself) is deliberately absent.
        public static string[] OpenParameterNames { get; set; } = ["f", "serializationFormat", "c"];
        public static string[] SessionParameterNames { get; set; } = ["s", "session"];

        // Private methods

        private static Sanitizer? SelectSanitizer(string name)
        {
            if (Contains(OpenParameterNames, name))
                return null; // Logged verbatim

            return Contains(SessionParameterNames, name)
                ? Get<SessionString>()
                : Get<PrefixAndLengthHint>();
        }

        private static bool Contains(string[] names, string name)
        {
            foreach (var item in names)
                if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }
    }
}
