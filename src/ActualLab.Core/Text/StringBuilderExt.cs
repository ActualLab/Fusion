using System.Text;

namespace ActualLab.Text;

// StringBuilder caching implementation.
// Acquire/Release here is ~ 2x faster than ZString.CreateStringBuilder/Dispose.

/// <summary>
/// Provides thread-local pooling for <see cref="System.Text.StringBuilder"/> instances
/// via Acquire/Release pattern.
/// </summary>
public static class StringBuilderExt
{
    private const int MaxCountPerThread = 16;
    private const int MaxCapacity = 2048;
    [ThreadStatic]
    private static StringBuilder[]? _cache;
    [ThreadStatic]
    private static int _cacheSize;

    public static StringBuilder Acquire(int capacity = 256)
    {
        if (capacity <= MaxCapacity && _cacheSize > 0) {
            ref var sbRef = ref _cache![--_cacheSize];
            var sb = sbRef;
            sbRef = null!;
            // No capacity check here: we assume reusing is always better than creating a new StringBuilder
            return sb;
        }

        return new StringBuilder(capacity);
    }

    public static void Release(this StringBuilder sb)
    {
        if (sb.Capacity > MaxCapacity || _cacheSize >= MaxCountPerThread)
            return;

        sb.Clear();
        (_cache ??= new StringBuilder[MaxCountPerThread])[_cacheSize++] = sb;
    }

    public static string ToStringAndRelease(this StringBuilder sb)
    {
        var result = sb.ToString();
        Release(sb);
        return result;
    }

    public static string ToStringAndRelease(this StringBuilder sb, int start, int length)
    {
        var result = sb.ToString(start, length);
        Release(sb);
        return result;
    }
}
