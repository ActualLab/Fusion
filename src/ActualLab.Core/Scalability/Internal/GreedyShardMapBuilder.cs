namespace ActualLab.Scalability.Internal;

/// <summary>
/// Builds a shard map using sequential greedy assignment.
/// Processes nodes in order, each node claims its share of shards
/// by probing from hash-derived positions.
/// Guarantees perfect balance (max - min &lt;= 1) but may cause
/// excessive reallocations when the node set changes.
/// </summary>
public sealed record GreedyShardMapBuilder : ShardMapBuilder
{
    public override int?[] Build(int shardCount, object[] nodes)
    {
        var nodeCount = nodes.Length;
        var nodeIndexes = new int?[shardCount];

        if (nodeCount == 0 || shardCount == 0)
            return nodeIndexes;

        var remainingNodeCount = nodeCount;
        var remainingShardCount = shardCount;
        while (remainingNodeCount != 0) {
            var nodeIndex = nodeCount - remainingNodeCount;
            var node = nodes[nodeIndex];
            var nodeShardCount = (remainingShardCount + remainingNodeCount - 1) / remainingNodeCount;
            var nodeHashes = NodeHashSequenceProvider(node).Take(nodeShardCount);
            foreach (var nodeHash in nodeHashes) {
                for (var i = 0; i < shardCount; i++) {
                    ref var shard = ref nodeIndexes[(nodeHash + i).PositiveModulo(shardCount)];
                    if (!shard.HasValue) {
                        shard = nodeIndex;
                        break;
                    }
                }
            }
            remainingShardCount -= nodeShardCount;
            remainingNodeCount--;
        }

        return nodeIndexes;
    }
}
