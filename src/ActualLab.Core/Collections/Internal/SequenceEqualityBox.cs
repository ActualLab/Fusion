namespace ActualLab.Collections.Internal;

public static class SequenceEqualityBox
{
    public readonly record struct ForArray<T>(T[] Source)
    {
        public bool Equals(ForArray<T> other)
            => Source.AsSpan().SequenceEqual(other.Source.AsSpan());

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in Source)
                hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

    public readonly record struct ForMemory<T>(ReadOnlyMemory<T> Source)
    {
        public bool Equals(ForMemory<T> other)
            => Source.Span.SequenceEqual(other.Source.Span);

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in Source.Span)
                hashCode = (359 * hashCode) + item?.GetHashCode() ?? 0;
            return hashCode;
        }
    }

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
