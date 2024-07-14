using System.Collections.Concurrent;
using System.Reactive;
using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
using ActualLab.Text;
using ActualLab.Time;
using static Samples.MeshRpc.HostFactorySettings;

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

public class FusionCounter(Host ownHost) : IFusionCounter
{
    private readonly object _lock = new();
    private int _value;

    public static readonly ConcurrentDictionary<Symbol, CpuTimestamp> IncrementedAt = new();

    public virtual async Task<CounterState> Get(ShardRef shardRef, CancellationToken cancellationToken = default)
    {
        var delay = CounterGetDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);


        lock (_lock)
            return new CounterState(ownHost.Id, _value);
    }

    public virtual async Task Increment(FusionCounter_Increment command, CancellationToken cancellationToken)
    {
        var delay = CounterIncrementDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        lock (_lock) {
            _value++;
            IncrementedAt[ownHost.Id] = CpuTimestamp.Now;
        }

        using var _1 = Invalidation.Begin();
        _ = Get(command.ShardRef, CancellationToken.None);
    }
}
