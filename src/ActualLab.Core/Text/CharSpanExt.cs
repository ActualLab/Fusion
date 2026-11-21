using System.IO.Hashing;

namespace ActualLab.Text;

/// <summary>
/// Extension methods for computing XxHash3 and Djb2 hashes over character spans.
/// </summary>
public static class CharSpanExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetXxHash3L(this ReadOnlySpan<char> source)
        => XxHash3.HashToUInt64(MemoryMarshal.AsBytes(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetXxHash3(this ReadOnlySpan<char> source)
        => unchecked((int)XxHash3.HashToUInt64(MemoryMarshal.AsBytes(source)));

    // Must produce the same values as CommunityToolkit.HighPerformance's GetDjb2HashCode:
    // FilePath.GetHashedName results are persisted as file and Npgsql channel names.
    // .NET Framework (which uses the netstandard builds) is fastest with the pointer-based loop,
    // .NET Core - with the ref-based one.
#if NETSTANDARD
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe int GetDjb2HashCode(this ReadOnlySpan<char> source)
    {
        var hash = 5381;
        fixed (char* start = source) {
            var p = start;
            var length = source.Length;
            while (length >= 4) {
                hash = unchecked(((hash << 5) + hash) ^ (p[0] | (p[0] << 16)));
                hash = unchecked(((hash << 5) + hash) ^ (p[1] | (p[1] << 16)));
                hash = unchecked(((hash << 5) + hash) ^ (p[2] | (p[2] << 16)));
                hash = unchecked(((hash << 5) + hash) ^ (p[3] | (p[3] << 16)));
                length -= 4;
                p += 4;
            }
            while (length > 0) {
                var c = *p;
                hash = unchecked(((hash << 5) + hash) ^ (c | (c << 16)));
                length--;
                p++;
            }
        }
        return hash;
    }
#else
    public static int GetDjb2HashCode(this ReadOnlySpan<char> source)
    {
        var hash = 5381;
        ref var r0 = ref MemoryMarshal.GetReference(source);
        nint length = source.Length;
        nint offset = 0;
        while (length >= 4) {
            hash = unchecked(((hash << 5) + hash)
                ^ (Unsafe.Add(ref r0, offset) | (Unsafe.Add(ref r0, offset) << 16)));
            hash = unchecked(((hash << 5) + hash)
                ^ (Unsafe.Add(ref r0, offset + 1) | (Unsafe.Add(ref r0, offset + 1) << 16)));
            hash = unchecked(((hash << 5) + hash)
                ^ (Unsafe.Add(ref r0, offset + 2) | (Unsafe.Add(ref r0, offset + 2) << 16)));
            hash = unchecked(((hash << 5) + hash)
                ^ (Unsafe.Add(ref r0, offset + 3) | (Unsafe.Add(ref r0, offset + 3) << 16)));
            length -= 4;
            offset += 4;
        }
        while (length > 0) {
            var c = Unsafe.Add(ref r0, offset);
            hash = unchecked(((hash << 5) + hash) ^ (c | (c << 16)));
            length--;
            offset++;
        }
        return hash;
    }
#endif
}
