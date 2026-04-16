namespace ActualLab.Scalability.Internal;

/// <summary>
/// Builds a shard map using Maglev consistent hashing (Google, 2016).
/// Each node generates a permutation of table positions; nodes take turns
/// claiming slots in round-robin order following their permutations.
/// Provides good balance and minimal disruption on node changes.
/// </summary>
public sealed record MaglevShardMapBuilder : ShardMapBuilder
{
    public override int?[] Build(int shardCount, object[] nodes)
    {
        var nodeCount = nodes.Length;
        var nodeIndexes = new int?[shardCount];

        if (nodeCount == 0 || shardCount == 0)
            return nodeIndexes;

        // Build permutation for each node: a full permutation of [0..shardCount)
        // using offset/skip with skip coprime to shardCount (guarantees full coverage)
        var permutations = new int[nodeCount][];
        for (var n = 0; n < nodeCount; n++) {
            var hashes = NodeHashSequenceProvider(nodes[n]).Take(2).ToArray();
            var offset = ((uint)hashes[0]) % (uint)shardCount;
            var skip = ((uint)hashes[1]) % (uint)(shardCount - 1) + 1;
            // Ensure skip is coprime with shardCount so the permutation covers all positions
            while (MathExt.Gcd(skip, (uint)shardCount) != 1)
                skip = skip % (uint)(shardCount - 1) + 1;
            var perm = new int[shardCount];
            for (var j = 0; j < shardCount; j++)
                perm[j] = (int)((offset + (uint)j * skip) % (uint)shardCount);
            permutations[n] = perm;
        }

        // Round-robin: each node advances through its permutation and claims the first free slot
        var next = new int[nodeCount]; // next index into permutation for each node
        var filled = 0;
        while (filled < shardCount) {
            for (var n = 0; n < nodeCount && filled < shardCount; n++) {
                var perm = permutations[n];
                // Advance until we find an unclaimed slot
                while (nodeIndexes[perm[next[n]]].HasValue)
                    next[n]++;
                nodeIndexes[perm[next[n]]] = n;
                next[n]++;
                filled++;
            }
        }

        return nodeIndexes;
    }
}
