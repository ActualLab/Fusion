using System.Reactive;
using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Rpc;
using ActualLab.Time;
using static Samples.MeshRpc.HostFactorySettings;

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

public class Counter(Host ownHost, MomentClockSet clocks) : ICounter
{
    private readonly object _lock = new();
    private int _value;

    public virtual async Task<CounterState> Get(ShardRef shardRef, CancellationToken cancellationToken = default)
    {
        var delay = CounterGetDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        lock (_lock)
            return new CounterState(ownHost.Id, clocks.CpuClock.Now, _value);
    }

    // [CommandHandler]
    public virtual async Task Increment(Counter_Increment command, CancellationToken cancellationToken)
    {
        var delay = CounterIncrementDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        lock (_lock)
            _value++;
    }
}
