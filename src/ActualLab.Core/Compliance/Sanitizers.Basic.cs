using System.Globalization;

namespace ActualLab.Compliance;

// Each sanitizer is named after what it leaves visible - the only thing that distinguishes them.
public static partial class Sanitizers
{
    public const string HiddenValue = "<<hidden>>";

    // Nested types

    /// <summary>Reveals nothing: <c>"&lt;&lt;hidden&gt;&gt;"</c>.</summary>
    public sealed class Hidden : Sanitizer
    {
        public override string Apply(string value)
            => value.Length == 0 ? value : HiddenValue;
    }

    /// <summary>
    /// Reveals a length range and nothing else: <c>"&lt;&lt;* [8-15]&gt;&gt;"</c>.
    /// The range is a power-of-two bucket, so an exact length never leaks.
    /// </summary>
    public sealed class LengthHint : Sanitizer
    {
        public override string Apply(string value)
            => value.Length == 0 ? value : $"<<* {LengthRange.Get(value.Length)}>>";
    }

    /// <summary>
    /// Reveals the first two characters and a length range: <c>"&lt;&lt;ab* [8-15]&gt;&gt;"</c>.
    /// Values of 3 characters or fewer fall back to <see cref="LengthHint"/>.
    /// </summary>
    public sealed class PrefixAndLengthHint : Sanitizer
    {
        public const int PrefixLength = 2;

        public override string Apply(string value)
        {
            var length = value.Length;
            if (length == 0)
                return value;

            return length <= PrefixLength + 1
                ? $"<<* {LengthRange.Get(length)}>>"
                : $"<<{value[0]}{value[1]}* {LengthRange.Get(length)}>>";
        }
    }

    /// <summary>
    /// Reveals identity but no content: <c>"&lt;&lt;a1b2c3d4&gt;&gt;"</c>. Equal values produce
    /// equal output, which is what lets you follow one value across a log - and what makes this
    /// the wrong choice for anything low-entropy enough to be found by hashing candidates.
    /// </summary>
    public sealed class Fingerprint : Sanitizer
    {
        public override string Apply(string value)
            => value.Length == 0
                ? value
                : $"<<{((uint)value.GetXxHash3()).ToString("x8", CultureInfo.InvariantCulture)}>>";
    }

    // Private methods

    private static class LengthRange
    {
        // Pre-generated for 0..4095; anything longer is formatted on demand
        private static readonly string[] Ranges = Build();

        public static string Get(int length)
            => length < Ranges.Length ? Ranges[length] : Format(length);

        private static string Format(int length)
        {
            var highBit = 1;
            while (highBit <= length)
                highBit <<= 1;
            return $"[{highBit >> 1}-{highBit - 1}]";
        }

        private static string[] Build()
        {
            const int size = 4096;
            var result = new string[size];
            result[0] = "[0]";
            var lo = 1;
            while (lo < size) {
                var hi = Math.Min((lo * 2) - 1, size - 1);
                var label = lo == hi ? $"[{lo}]" : $"[{lo}-{hi}]";
                for (var i = lo; i <= hi; i++)
                    result[i] = label;
                lo = hi + 1;
            }
            return result;
        }
    }
}
