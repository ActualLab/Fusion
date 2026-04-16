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
    [InlineData(13, 6)]
    [InlineData(17, 6)]
    [InlineData(60, 20)]
    [InlineData(60, 20, 2)]
    [InlineData(61, 20)]
    [InlineData(120, 30)]
    [InlineData(120, 30, 2)]
    [InlineData(127, 30)]
    public void BuilderComparisonTest(int shardCount, int maxNodeCount, int maxImbalance = 1)
    {
        var greedy = ShardMapBuilder.Greedy;
        var maglev = ShardMapBuilder.Maglev;
        var rendez = ShardMapBuilder.Rendezvous with { MaxImbalance = maxImbalance };
        var names = new[] { "Rendez", "Maglev", "Greedy" };
        var winMaps = names.ToDictionary(x => x, _ => new List<char>());

        WriteLine($"Shards: {shardCount}, MaxImbalance: {maxImbalance}");
        for (var nodeCount = 2; nodeCount <= maxNodeCount; nodeCount++) {
            var idealMoveCount = shardCount / nodeCount;
            var gMoves = CollectMoves(shardCount, nodeCount, greedy);
            var mMoves = CollectMoves(shardCount, nodeCount, maglev);
            var rMoves = CollectMoves(shardCount, nodeCount, rendez);
            var gMedian = Median(gMoves);
            var mMedian = Median(mMoves);
            var rMedian = Median(rMoves);

            var medians = new[] { ("Rendez", rMedian, rMoves), ("Maglev", mMedian, mMoves), ("Greedy", gMedian, gMoves) };
            var sorted = medians.OrderBy(x => x.Item2).ThenBy(x => x.Item3[^1]).ToArray();
            var winners = sorted.TakeWhile(x => x.Item2 == sorted[0].Item2 && x.Item3[^1] == sorted[0].Item3[^1]).ToArray();
            var winnerNames = winners.Select(x => x.Item1).ToHashSet();
            var label = winners.Length > 1
                ? "tie: " + string.Join(", ", winnerNames)
                : "won: " + winners[0].Item1;

            foreach (var name in names)
                winMaps[name].Add(winnerNames.Contains(name) ? '1' : '0');

            WriteLine($"  {nodeCount - 1}<->{nodeCount}: ideal {idealMoveCount}, "
                + $"Rendez [{rMoves[0]} .. {rMedian:F0} .. {rMoves[^1]}], "
                + $"Maglev [{mMoves[0]} .. {mMedian:F0} .. {mMoves[^1]}], "
                + $"Greedy [{gMoves[0]} .. {gMedian:F0} .. {gMoves[^1]}], "
                + label);
        }

        // Print win/tie summary sorted by count
        WriteLine("Summary:");
        foreach (var (name, map) in winMaps.OrderByDescending(x => x.Value.Count(c => c == '1')))
            WriteLine($"  {name}: {map.Count(c => c == '1')} wins [{new string(map.ToArray())}]");
    }

    [Fact]
    public void MaglevBalanceTest()
    {
        // Maglev should produce perfectly balanced maps (max - min <= 1)
        var shardCounts = new[] { 12, 60, 120 };
        var nodeCounts = new[] { 2, 3, 5, 7, 10, 20 };
        var maglev = ShardMapBuilder.Maglev;

        foreach (var shardCount in shardCounts)
        foreach (var nodeCount in nodeCounts) {
            if (nodeCount > shardCount)
                continue;

            var nodes = Enumerable.Range(0, nodeCount).Select(i => $"node-{i}").ToArray();
            var map = new ShardMap<string>(shardCount, nodes, maglev);

            var nodeIndexes = map.NodeIndexes;
            nodeIndexes.All(x => x.HasValue).Should().BeTrue();
            var groups = nodeIndexes.GroupBy(x => x).ToArray();
            groups.Length.Should().Be(nodeCount);
            var minCount = groups.Min(g => g.Count());
            var maxCount = groups.Max(g => g.Count());
            var delta = maxCount - minCount;
            delta.Should().BeInRange(0, 1,
                $"Maglev should be perfectly balanced for {shardCount} shards, {nodeCount} nodes (was {minCount}-{maxCount})");
            WriteLine($"Shards: {shardCount}, Nodes: {nodeCount}, Range: {minCount}-{maxCount} - OK");
        }
    }

    [Fact]
    public void MaglevReallocationTest()
    {
        const int shardCount = 60;
        var maglev = ShardMapBuilder.Maglev;

        var nodes3 = new[] { "node-1", "node-2", "node-3" };
        var nodes2 = new[] { "node-1", "node-2" }; // node-3 dies

        var map3 = new ShardMap<string>(shardCount, nodes3, maglev);
        var map2 = new ShardMap<string>(shardCount, nodes2, maglev);

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
        WriteLine($"Extra reallocations: {reallocated}");
        WriteLine($"Map with 3 nodes:\n{map3}");
        WriteLine($"Map with 2 nodes:\n{map2}");

        // Maglev is not zero-disruption like rendezvous, but should be reasonable
        var totalMoves = node3Shards + reallocated;
        totalMoves.Should().BeGreaterThan(0, "some shards must move when a node dies");
        WriteLine($"Total moves: {totalMoves} (ideal: {node3Shards})");
    }

    [Fact]
    public void MaglevStressTest()
    {
        // Stress test with random node churn, like BalanceTest but using Maglev
        const int shardCount = 60;
        var maglev = ShardMapBuilder.Maglev;
        var rnd = new Random(42);
        var nodes = new List<string>();

        for (var i = 0; i < 200; i++) {
            var mustAdd = nodes.Count == 0 || rnd.Next(nodes.Count + 10) > nodes.Count;
            if (mustAdd)
                nodes.Add($"node-{i}");
            else
                nodes.RemoveAt(rnd.Next(nodes.Count));
            var shardMap = new ShardMap<string>(shardCount, [..nodes.OrderBy(x => x)], maglev);
            if (!shardMap.IsEmpty) {
                var nodeIndexes = shardMap.NodeIndexes;
                nodeIndexes.All(x => x.HasValue).Should().BeTrue();
                var shardGroups = nodeIndexes.GroupBy(x => x).ToArray();
                shardGroups.Length.Should().Be(Math.Min(nodes.Count, shardMap.NodeIndexes.Length));
                var minCount = shardGroups.Min(g => g.Count());
                var maxCount = shardGroups.Max(g => g.Count());
                var delta = maxCount - minCount;
                delta.Should().BeInRange(0, 1,
                    $"iteration {i}: {nodes.Count} nodes, balance {minCount}-{maxCount}");
            }
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

        var currentMap = new ShardMap<string>(shardCount, [..nodes.OrderBy(x => x)], builder: builder);
        for (var iter = 0; iter < iterations; iter++) {
            // Remove a random node
            var removeIndex = rnd.Next(nodes.Count);
            nodes.RemoveAt(removeIndex);
            var afterRemoveMap = new ShardMap<string>(shardCount, [..nodes.OrderBy(x => x)], builder: builder);
            movesList.Add(CountMoves(currentMap, afterRemoveMap, shardCount));
            currentMap = afterRemoveMap;

            // Add a new node
            nodes.Add($"node-{nextNodeId++}");
            var afterAddMap = new ShardMap<string>(shardCount, [..nodes.OrderBy(x => x)], builder: builder);
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
            var shardMap = new ShardMap<string>(shardCount, [..nodes.OrderBy(x => x)]);
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
