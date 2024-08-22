namespace ActualLab.Collections.Internal;

public static class SequenceEqualityBox
{
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct ForArray<T>(T[] Source)
    {
        public bool Equals(ForArray<T> other)
#if NET6_0_OR_GREATER
            => Source.AsSpan().SequenceEqual(other.Source.AsSpan());
#else
        {
            if (Source.Length != other.Source.Length)
                return false;

            for (var i = 0; i < Source.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(Source[i], other.Source[i]))
                    return false;

            return true;
        }
#endif

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in Source)
                hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct ForMemory<T>(ReadOnlyMemory<T> Source)
    {
        public bool Equals(ForMemory<T> other)
#if NET6_0_OR_GREATER
            => Source.Span.SequenceEqual(other.Source.Span);
#else
        {
            if (Source.Length != other.Source.Length)
                return false;

            var x = Source.Span;
            var y = other.Source.Span;
            for (var i = 0; i < x.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(x[i], y[i]))
                    return false;

            return true;
        }
#endif

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in Source.Span)
                hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct ForList<T>(IReadOnlyList<T> Source)
    {
        public bool Equals(ForList<T> other)
            => Source.SequenceEqual(other.Source);

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in Source)
                hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
            return hashCode;
        }
    }
}
