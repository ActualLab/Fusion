using System.IO.Hashing;
using CommunityToolkit.HighPerformance;

namespace ActualLab.Text;

public static class CharSpanExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this ReadOnlySpan<char> source)
        => XxHash3.HashToUInt64(source.Cast<char, byte>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this ReadOnlySpan<char> source)
        => unchecked((int)XxHash3.HashToUInt64(source.Cast<char, byte>()));
}
