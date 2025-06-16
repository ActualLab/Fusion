using System.Text;

namespace ActualLab.Text;

// StringBuilder caching.
// Prefer ZString.CreateStringBuilder instead, if possible.
// See https://referencesource.microsoft.com/#mscorlib/system/text/stringbuildercache.cs,a6dbe82674916ac0
public static class StringBuilderExt
{
    private const int MaxCountPerThread = 16;
    private const int MaxCapacity = 1024;
    [ThreadStatic]
    private static Stack<StringBuilder>? _cached;

    public static StringBuilder Acquire(int capacity = 64)
    {
        if (capacity <= MaxCapacity) {
            var cached = _cached ??= new Stack<StringBuilder>(MaxCountPerThread);
            if (cached.TryPop(out var sb)) {
                // No capacity check here: we assume reusing the existing one
                // is still more efficient than creating a new one
                return sb;
            }
        }
        return new StringBuilder(capacity);
    }

    public static void Release(this StringBuilder sb)
    {
        if (sb.Capacity > MaxCapacity)
            return;
        var cached = _cached ??= new Stack<StringBuilder>(MaxCountPerThread);
        if (cached.Count >= MaxCountPerThread)
            return;

        sb.Clear();
        cached.Push(sb);
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
