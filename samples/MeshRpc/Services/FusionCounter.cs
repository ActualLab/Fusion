using System.Reactive;
using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;

namespace Samples.MeshRpc.Services;

public interface IFusionCounter : IComputeService
{
    [ComputeMethod]
    Task<CounterState> Get(ShardRef shardRef, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task Increment(FusionCounter_Increment command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record FusionCounter_Increment(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] ShardRef ShardRef
    ) : ICommand<Unit>, IHasShardRef;

public class FusionCounter(Host host) : IFusionCounter
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

    public virtual Task Increment(FusionCounter_Increment command, CancellationToken cancellationToken)
    {
        lock (_lock)
            _value++;

        using var _1 = Invalidation.Begin();
        _ = Get(default);

        return Task.CompletedTask;
    }
}
