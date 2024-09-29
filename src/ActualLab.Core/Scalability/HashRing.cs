using CommunityToolkit.HighPerformance;

namespace ActualLab.Scalability;

public readonly struct HashRing<T>
    where T : notnull
{
    private static readonly IComparer<(T Value, int Hash)> Comparer = new ItemComparer();

    public static readonly Func<T, int> DefaultHasher = static v => v.GetHashCode();
    public static readonly Func<T, int> DefaultStringHasher = static v => v is string s ? s.GetDjb2HashCode() : v.GetHashCode();
    public static readonly HashRing<T> Empty = new(Array.Empty<T>());

    private readonly T[] _doubleNodes;

    public (T Value, int Hash)[] Nodes { get; }
    public int Count => Nodes.Length;
    public bool IsEmpty => Count == 0;
    public T this[int nodeIndex] => _doubleNodes[Count + (nodeIndex % Count)];

    public HashRing(IEnumerable<T> values, Func<T, int>? hasher = null)
    {
        hasher ??= typeof(T) == typeof(string) ? DefaultStringHasher : DefaultHasher;
        Nodes = values
            .Select(v => (Value: v, Hash: hasher.Invoke(v)))
            .OrderBy(i => i.Hash)
            .ToArray();
        _doubleNodes = new T[Nodes.Length * 2];
        for (var i = 0; i < _doubleNodes.Length; i++)
            _doubleNodes[i] = Nodes[i.PositiveModulo(Count)].Value;
    }

    public T FindNode(int hash, int offset = 0)
        => this[offset + FindNodeIndex(hash)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindNodeIndex(int hash)
        => ~Array.BinarySearch(Nodes, (default!, hash), Comparer);

    public ReadOnlySpan<T> Span(int hash, int count, int offset = 0)
    {
        count = count.Clamp(0, Count);
        if (count == 0)
            return Span<T>.Empty;

        offset = (offset + FindNodeIndex(hash)).PositiveModulo(Count);
        return _doubleNodes.AsSpan(offset, count);
    }

    public ArraySegment<T> Segment(int hash, int count, int offset = 0)
    {
        count = count.Clamp(0, Count);
        if (count == 0)
#if NETSTANDARD2_0
            return ArraySegmentCompatExt.Empty<T>();
#else
            return ArraySegment<T>.Empty;
#endif

        offset = (offset + FindNodeIndex(hash)).PositiveModulo(Count);
        return new ArraySegment<T>(_doubleNodes, offset, count);
    }

    // Nested types

    private sealed class ItemComparer : IComparer<(T Value, int Hash)>
    {
        public int Compare((T Value, int Hash) x, (T Value, int Hash) y)
        {
            var d = x.Hash - y.Hash;
            // We map "equals" here to 1 to make sure we find the item with higher or equal Hash
            return d >= 0 ? 1 : -1;
        }
    }
}
