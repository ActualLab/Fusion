using System.Reactive;
using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Rpc;

namespace Samples.MeshRpc.Services;

public interface ICounter : IRpcService
{
    Task<CounterState> Get(ShardRef shardRef, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Increment(Counter_Increment command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record Counter_Increment(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] ShardRef ShardRef
) : ICommand<Unit>, IHasShardRef;

public class Counter(Host host) : ICounter
{
    private readonly object _lock = new();
    private int _value;

    public virtual async Task<CounterState> Get(ShardRef shardRef, CancellationToken cancellationToken = default)
    {
        var delay = host.Delay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        lock (_lock)
            return new CounterState(host.Id, _value);
    }

    public virtual Task Increment(Counter_Increment command, CancellationToken cancellationToken)
    {
        lock (_lock)
            _value++;
        return Task.CompletedTask;
    }
}
