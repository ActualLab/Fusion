using System.Globalization;
using ActualLab.CommandR.Operations;
using ActualLab.Generators;
using ActualLab.OS;

namespace ActualLab.Fusion.EntityFramework.Internal;

public class TransactionIdGenerator : Generator<string>
{
    private long _nextId;
    protected string Prefix { get; init; }

    public TransactionIdGenerator(HostId hostId)
        => Prefix = hostId.Id.Value;

    public override string Next()
        => $"{Prefix}-{NextId().ToString(CultureInfo.InvariantCulture)}";

    // Protected methods

    protected long NextId()
        => Interlocked.Increment(ref _nextId);
}

public class TransactionIdGenerator<TContext> : TransactionIdGenerator
{
    public TransactionIdGenerator(HostId hostId) : base(hostId)
    {
        var contextName = typeof(TContext).Name;
        Prefix = $"{Prefix}-{contextName}";
    }
}
