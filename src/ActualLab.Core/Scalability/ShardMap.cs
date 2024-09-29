using System.Text;
using CommunityToolkit.HighPerformance;
using StringBuilderExt = ActualLab.Text.StringBuilderExt;

namespace ActualLab.Scalability;

public class ShardMap<TNode>
    where TNode : class
{
    public int ShardCount { get; }
    public ImmutableArray<TNode> Nodes { get; }
    public ImmutableArray<int?> NodeIndexes { get; }
    public bool IsEmpty => Nodes.Length == 0;

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

    public ShardMap(
        int shardCount,
        ImmutableArray<TNode> nodes,
        Func<TNode, IEnumerable<int>>? nodeHashSequenceProvider = null)
    {
        ShardCount = shardCount;
        Nodes = nodes;
        nodeHashSequenceProvider ??= node => GetDefaultHashSequence(node.ToString() ?? "");
        var remainingNodeCount = nodes.Length;
        var shards = new int?[shardCount];
        var remainingShardCount = shardCount;
        while (remainingNodeCount != 0) {
            var nodeIndex = nodes.Length - remainingNodeCount;
            var node = nodes[nodeIndex];
            var nodeShardCount = (remainingShardCount + remainingNodeCount - 1) / remainingNodeCount;
            var nodeHashes = nodeHashSequenceProvider.Invoke(node).Take(nodeShardCount);
            foreach (var nodeHash in nodeHashes) {
                for (var i = 0; i < shardCount; i++) {
                    ref var shard = ref shards[(nodeHash + i).PositiveModulo(shardCount)];
                    if (!shard.HasValue) {
                        shard = nodeIndex;
                        break;
                    }
                }
            }
            remainingShardCount -= nodeShardCount;
            remainingNodeCount--;
        }
        NodeIndexes = ImmutableArray.Create(shards);
    }

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

    private static IEnumerable<int> GetDefaultHashSequence(string source)
    {
        for (var i = 0;; i++)
            yield return $"{source}-{i:x8}".GetDjb2HashCode();
        // ReSharper disable once IteratorNeverReturns
    }
}
