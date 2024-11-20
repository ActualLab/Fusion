using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;
using static Samples.MeshRpc.HostFactorySettings;

namespace Samples.MeshRpc.Services;

public interface IFusionCounter : IComputeService
{
    [ComputeMethod]
    public Task<CounterWithOrigin> Get(int key, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task<CounterWithOrigin> Increment(FusionCounter_Increment command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record FusionCounter_Increment(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] int Key
) : ICommand<CounterWithOrigin>, IHasShardRef
{
    [IgnoreDataMember, MemoryPackIgnore]
    public ShardRef ShardRef => ShardRef.New(Key);
}

public class FusionCounter(Host ownHost) : IFusionCounter
{
    // [ComputeMethod]
    public virtual async Task<CounterWithOrigin> Get(int key, CancellationToken cancellationToken = default)
    {
        var delay = CounterGetDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        var counter = await CounterStorage.Use(key, cancellationToken).ConfigureAwait(false);
        return new CounterWithOrigin(counter, ownHost.Id);
    }

    // [CommandHandler]
    public virtual async Task<CounterWithOrigin> Increment(FusionCounter_Increment command, CancellationToken cancellationToken)
    {
        var delay = CounterIncrementDelay.Next();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

        var counter = CounterStorage.Increment(command.Key);
        return new CounterWithOrigin(counter, ownHost.Id);
    }
}
