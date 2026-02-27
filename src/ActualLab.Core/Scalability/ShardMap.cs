using System.Text;
using StringBuilderExt = ActualLab.Text.StringBuilderExt;

namespace ActualLab.Scalability;

/// <summary>
/// Maps a fixed number of shards to a set of nodes.
/// </summary>
public class ShardMap<TNode>(int shardCount, TNode[] nodes, ShardMapBuilder builder)
    where TNode : class
{
    public int ShardCount { get; } = shardCount;
    public bool IsEmpty => Nodes.Length == 0;
    public TNode[] Nodes { get; } = nodes;
    // ReSharper disable once CoVariantArrayConversion
    public int?[] NodeIndexes { get; } = builder.Build(shardCount, nodes);

    // Indexers

    public TNode? this[int shardIndex] {
        get {
            var nodeIndex = NodeIndexes[shardIndex];
            return nodeIndex.HasValue ? Nodes[nodeIndex.GetValueOrDefault()] : null;
        }
    }

    public TNode? this[int shardIndex, int nodeOffset] {
        get {
            var nodeIndex = NodeIndexes[shardIndex];
            return nodeIndex.HasValue ? Nodes.GetRingItem(nodeOffset + nodeIndex.GetValueOrDefault()) : null;
        }
    }

    public ShardMap(int shardCount, TNode[] nodes)
        : this(shardCount, nodes, ShardMapBuilder.Default)
    { }

    // ReSharper disable once CoVariantArrayConversion

    public override string ToString()
    {
        var sb = StringBuilderExt.Acquire();
        sb.Append(GetType().GetName());
        sb.Append('(');
        AppendToStringArguments(sb);

        var nodeCount = Nodes.Length;
        sb.Append("): ").Append(ShardCount).Append(ShardCount == 1 ? " shard" : " shards");
        sb.Append(" -> ").Append(nodeCount).Append(nodeCount == 1 ? " node" : " nodes");
        if (IsEmpty)
            return sb.ToStringAndRelease();

        sb.AppendLine(" {");
        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
            var node = Nodes[nodeIndex];
            sb.Append("  ");
            foreach (var shardNodeIndex in NodeIndexes)
                sb.Append(shardNodeIndex == nodeIndex ? '+' : '-');
            sb.Append(": ").Append(node).AppendLine();
        }
        sb.Append('}');
        return sb.ToStringAndRelease();
    }

    // Protected & private methods

    protected virtual void AppendToStringArguments(StringBuilder sb)
    { }
}
