using System.Globalization;
using System.Text;

namespace ActualLab.Fusion.Diagnostics.Internal;

public sealed class InvalidationPathCounter
{
    private readonly Node _root = new("");
    private int _totalCount;

    public int TotalCount => _totalCount;

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        AppendTo(sb, CultureInfo.InvariantCulture);
        return sb.ToStringAndRelease();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(params ReadOnlySpan<string> levels)
        => Increment(1, levels);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(int delta, params ReadOnlySpan<string> levels)
    {
        if (levels.Length == 0)
            return;

        _totalCount += delta;
        var node = _root;
        for (var i = 0; i < levels.Length; i++) {
            var key = levels[i];
            node = node.GetOrAdd(key);
            node.Count += delta;
        }
    }

    public void AppendTo(StringBuilder sb, IFormatProvider fp, double multiplier = 1.0)
    {
        var children = _root.MaybeChildren;
        if (children is null || children.Count == 0)
            return;

        foreach (var (key, node) in GetOrderedNodes(children))
            AppendNode(key, node, 0);
        return;

        void AppendNode(string key, Node node, int level) {
            sb.Append("\r\n");
            for (var i = 0; i < level; i++)
                sb.Append("  ");

            var maybeChildren = node.MaybeChildren;
            if (maybeChildren is { Count: 0 })
                maybeChildren = null;

            var tailFormat = level switch {
                0 => ", invalidated:",
                1 => ", which invalidated:",
                _ => "",
            };
            if (maybeChildren is null)
                tailFormat = "";

            sb.AppendFormat(fp, "- {0:0.#} x {1}{2}", node.Count * multiplier, key, tailFormat);

            if (maybeChildren is not null)
                foreach (var (childKey, childNode) in GetOrderedNodes(maybeChildren))
                    AppendNode(childKey, childNode, level + 1);
        }

        static IEnumerable<KeyValuePair<string, Node>> GetOrderedNodes(Dictionary<string, Node>? source)
            => source?
                .OrderByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                ?? Enumerable.Empty<KeyValuePair<string, Node>>();
    }


    // Nested types

    private sealed class Node(string key)
    {
        public readonly string Key = key;
        public int Count; // aggregated value for this subtree
        public Dictionary<string, Node>? MaybeChildren; // Lazy-allocated

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Node GetOrAdd(string key)
        {
            var children = MaybeChildren ??= new Dictionary<string, Node>(StringComparer.Ordinal);
            if (!children.TryGetValue(key, out var node))
                children.Add(key, node = new Node(key));
            return node;
        }
    }
}
