using ActualLab.Scalability.Internal;

namespace ActualLab.Scalability;

public abstract record ShardMapBuilder
{
    public static GreedyShardMapBuilder Greedy { get; } = new();
    public static RendezvousShardMapBuilder Rendezvous { get; } = new();
    public static ShardMapBuilder Default { get; set; } = Rendezvous;

    public Func<object, IEnumerable<int>> NodeHashSequenceProvider { get; init; } = GetDefaultHashSequence;

    public abstract int?[] Build(int shardCount, object[] nodes);

    // GetDefaultHashSequence

    public static IEnumerable<int> GetDefaultHashSequence(object node)
        => GetDefaultHashSequence(node.ToString() ?? "");

    public static IEnumerable<int> GetDefaultHashSequence(string source)
    {
        for (var i = 0;; i++)
            yield return $"{source}-{i:x8}".GetXxHash3();
        // ReSharper disable once IteratorNeverReturns
    }
}
