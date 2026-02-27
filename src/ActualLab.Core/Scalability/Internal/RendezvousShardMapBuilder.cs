namespace ActualLab.Scalability.Internal;

/// <summary>
/// Builds a shard map using rendezvous (highest random weight) hashing
/// with optional rebalancing to enforce a maximum imbalance constraint.
/// Provides optimal minimal reallocation when nodes are added or removed.
/// </summary>
public sealed record RendezvousShardMapBuilder(int MaxImbalance = 1) : ShardMapBuilder
{
    public override int?[] Build(int shardCount, object[] nodes)
    {
#pragma warning disable CA2208
        if (MaxImbalance < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxImbalance));
#pragma warning restore CA2208

        var nodeCount = nodes.Length;
        var nodeIndexes = new int?[shardCount];

        if (nodeCount == 0 || shardCount == 0)
            return nodeIndexes;

        // Step 1: Collect weights for rendezvous hashing (shardCount hashes per node)
        var weights = new int[nodeCount][];
        for (var n = 0; n < nodeCount; n++)
            weights[n] = NodeHashSequenceProvider(nodes[n]).Take(shardCount).ToArray();

        // Step 2: Rendezvous hashing - assign each shard to node with highest weight
        var counts = new int[nodeCount];
        for (var s = 0; s < shardCount; s++) {
            var bestNode = 0;
            var bestWeight = (uint)weights[0][s];
            for (var n = 1; n < nodeCount; n++) {
                var w = (uint)weights[n][s];
                if (w > bestWeight) {
                    bestWeight = w;
                    bestNode = n;
                }
            }
            nodeIndexes[s] = bestNode;
            counts[bestNode]++;
        }

        // Step 3: Rebalance to enforce maxImbalance constraint
        var maxImbalance = MaxImbalance;
        if (shardCount % nodeCount != 0 && maxImbalance < 1)
            maxImbalance = 1;
        Rebalance(nodeIndexes, weights, counts, maxImbalance);

        return nodeIndexes;
    }

    private static void Rebalance(int?[] nodeIndexes, int[][] weights, int[] counts, int maxImbalance)
    {
        var shardCount = nodeIndexes.Length;
        var nodeCount = counts.Length;

        while (true) {
            // Find most overloaded and most underloaded nodes
            var maxNode = 0;
            var minNode = 0;
            for (var n = 1; n < nodeCount; n++) {
                if (counts[n] > counts[maxNode])
                    maxNode = n;
                if (counts[n] < counts[minNode])
                    minNode = n;
            }

            if (counts[maxNode] - counts[minNode] <= maxImbalance)
                break;

            // Among shards on maxNode, find the one where minNode has the highest weight
            // (i.e., minNode has the strongest claim to it - least "damage" from moving)
            var bestShard = -1;
            var bestWeight = 0u;
            for (var s = 0; s < shardCount; s++) {
                if (nodeIndexes[s] != maxNode)
                    continue;
                var w = (uint)weights[minNode][s];
                if (bestShard < 0 || w > bestWeight) {
                    bestWeight = w;
                    bestShard = s;
                }
            }

            nodeIndexes[bestShard] = minNode;
            counts[maxNode]--;
            counts[minNode]++;
        }
    }
}
