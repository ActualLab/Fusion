using ActualLab.Scalability;
using ActualLab.Scalability.Internal;

namespace ActualLab.Tests.Scalability;

public class ShardMapTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SmallNodeCountTest()
        => BalanceTest(12, 4);

    [Fact]
    public void LargeNodeCountTest()
        => BalanceTest(12, 20);

    [Fact]
    public void ReallocationTest()
    {
        const int shardCount = 12;

        // Test: removing a node should only reallocate ~shardCount/nodeCount shards
        var nodes3 = new[] { "node-1", "node-2", "node-3" };
        var nodes2 = new[] { "node-1", "node-2" }; // node-3 dies

        var map3 = new ShardMap<string>(shardCount, nodes3);
        var map2 = new ShardMap<string>(shardCount, nodes2);

        var reallocated = 0;
        var node3Shards = 0;
        for (var s = 0; s < shardCount; s++) {
            var owner3 = map3[s];
            var owner2 = map2[s];
            if (owner3 == "node-3")
                node3Shards++;
            else if (owner3 != owner2)
                reallocated++;
        }

        WriteLine($"Shards on dead node: {node3Shards}");
        WriteLine($"Extra reallocations (beyond dead node's shards): {reallocated}");
        WriteLine($"Map with 3 nodes:\n{map3}");
        WriteLine($"Map with 2 nodes:\n{map2}");

        // With rendezvous hashing, extra reallocations should be very small (0-2 from rebalancing)
        reallocated.Should().BeInRange(0, 2,
            "rendezvous hashing should cause minimal extra reallocations beyond the dead node's shards");
    }

    [Theory]
    [InlineData(12, 6)]
    [InlineData(60, 20)]
    [InlineData(60, 20, 2)]
    [InlineData(120, 30)]
    [InlineData(120, 30, 2)]
    public void BuilderComparisonTest(int shardCount, int maxNodeCount, int maxImbalance = 1)
    {
        var greedy = ShardMapBuilder.Greedy;
        var rendezvous = ShardMapBuilder.Rendezvous with { MaxImbalance = maxImbalance };
        WriteLine($"Shards: {shardCount}");
        for (var nodeCount = 2; nodeCount <= maxNodeCount; nodeCount++) {
            var idealMoveCount = shardCount / nodeCount;
            var gMoves = CollectMoves(shardCount, nodeCount, greedy);
            var rMoves = CollectMoves(shardCount, nodeCount, rendezvous);
            var gMedian = Median(gMoves);
            var rMedian = Median(rMoves);
            var cmp = rMedian.CompareTo(gMedian);
            if (cmp == 0)
                cmp = rMoves[^1].CompareTo(gMoves[^1]);
            var winner = cmp < 0 ? "Rend." : cmp > 0 ? "Greedy" : "tie";
            WriteLine($"  {nodeCount - 1}<->{nodeCount}: ideal {idealMoveCount}, "
                + $"Rend. [{rMoves[0]} .. {rMedian:F0} .. {rMoves[^1]}], "
                + $"Greedy [{gMoves[0]} .. {gMedian:F0} .. {gMoves[^1]}], "
                + $"won: {winner}");
        }
    }

    // Private methods

    private static List<int> CollectMoves(int shardCount, int nodeCount, ShardMapBuilder builder)
    {
        var rnd = new Random(42 + shardCount * 100 + nodeCount);
        var nextNodeId = 0;

        var nodes = new List<string>();
        for (var i = 0; i < nodeCount; i++)
            nodes.Add($"node-{nextNodeId++}");

        var iterations = nodeCount * 50;
        var movesList = new List<int>(iterations * 2);

        var currentMap = new ShardMap<string>(shardCount, [..nodes.Order()], builder: builder);
        for (var iter = 0; iter < iterations; iter++) {
            // Remove a random node
            var removeIndex = rnd.Next(nodes.Count);
            nodes.RemoveAt(removeIndex);
            var afterRemoveMap = new ShardMap<string>(shardCount, [..nodes.Order()], builder: builder);
            movesList.Add(CountMoves(currentMap, afterRemoveMap, shardCount));
            currentMap = afterRemoveMap;

            // Add a new node
            nodes.Add($"node-{nextNodeId++}");
            var afterAddMap = new ShardMap<string>(shardCount, [..nodes.Order()], builder: builder);
            movesList.Add(CountMoves(currentMap, afterAddMap, shardCount));
            currentMap = afterAddMap;
        }

        movesList.Sort();
        return movesList;
    }

    private static double Median(List<int> sorted)
        => sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;

    private static int CountMoves(ShardMap<string> before, ShardMap<string> after, int shardCount)
    {
        var moves = 0;
        for (var s = 0; s < shardCount; s++) {
            if (before[s] != after[s])
                moves++;
        }
        return moves;
    }

    private void BalanceTest(int shardCount, int averageNodeCount)
    {
        var rnd = new Random(15 + averageNodeCount);
        var nodes = new List<string>();
        for (var i = 0; i < 200; i++) {
            var mustAdd = nodes.Count == 0 || rnd.Next(nodes.Count + averageNodeCount) > nodes.Count;
            if (mustAdd)
                nodes.Add($"node-{i}");
            else
                nodes.RemoveAt(rnd.Next(nodes.Count));
            var shardMap = new ShardMap<string>(shardCount, [..nodes.Order()]);
            if (!shardMap.IsEmpty) {
                var nodeIndexes = shardMap.NodeIndexes;
                nodeIndexes.All(x => x.HasValue).Should().BeTrue();
                var shardGroups = nodeIndexes.GroupBy(x => x).ToArray();
                shardGroups.Length.Should().Be(Math.Min(nodes.Count, shardMap.NodeIndexes.Length));
                var minCount = shardGroups.Min(g => g.Count());
                var maxCount = shardGroups.Max(g => g.Count());
                var delta = maxCount - minCount;
                delta.Should().BeInRange(0, 1);
            }
            WriteLine(shardMap.ToString());
        }
    }
}
