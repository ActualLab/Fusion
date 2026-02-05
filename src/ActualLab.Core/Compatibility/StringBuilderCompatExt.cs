#if NETSTANDARD2_0

// ReSharper disable once CheckNamespace
namespace System.Text;

/// <summary>
/// Compatibility extension methods for <see cref="StringBuilder"/> on .NET Standard 2.0.
/// </summary>
public static class StringBuilderCompatExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Append(this StringBuilder sb, ArraySegment<char> chars)
    {
        if (sb is null)
            throw new ArgumentNullException(nameof(sb));

        sb.Append(chars.Array, chars.Offset, chars.Count);
    }
}

#endif
