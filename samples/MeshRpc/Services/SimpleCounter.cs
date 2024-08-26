using System.Runtime.Serialization;
using MemoryPack;
using ActualLab.Rpc;
using static Samples.MeshRpc.HostFactorySettings;

namespace Samples.MeshRpc.Services;

public interface ISimpleCounter : IRpcService
{
    Task<CounterWithOrigin> Get(int key, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task<CounterWithOrigin> Increment(SimpleCounter_Increment command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record SimpleCounter_Increment(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int Key
) : ICommand<CounterWithOrigin>, IHasShardRef
{
    [IgnoreDataMember, MemoryPackIgnore]
    public ShardRef ShardRef => ShardRef.New(Key);
}

public class SimpleCounter(Host ownHost) : ISimpleCounter
{
    public virtual async Task<CounterWithOrigin> Get(int key, CancellationToken cancellationToken = default)
    {
        var delay = CounterGetDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        var counter = CounterStorage.Get(key);
        return new CounterWithOrigin(counter, ownHost.Id);
    }

    // [CommandHandler]
    public virtual async Task<CounterWithOrigin> Increment(SimpleCounter_Increment command, CancellationToken cancellationToken)
    {
        var delay = CounterIncrementDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        var counter = CounterStorage.Increment(command.Key);
        return new CounterWithOrigin(counter, ownHost.Id);
    }
}
