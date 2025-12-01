using ActualLab.Fusion.Operations.Internal;
using ActualLab.Reflection;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

public interface IKeyValueService<TValue> : IComputeService
{
    [ComputeMethod]
    public Task<Option<TValue>> TryGet(string key, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<TValue> Get(string key, CancellationToken cancellationToken = default);
    public Task Set(string key, TValue value, CancellationToken cancellationToken = default);
    public Task Remove(string key, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task SetCmd(KeyValueService_Set<TValue> cmd, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task RemoveCmd(KeyValueService_Remove cmd, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record KeyValueService_Remove(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record KeyValueService_Set<TValue>(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] TValue Value
) : ICommand<Unit>;

public class KeyValueService<TValue> : IKeyValueService<TValue>
{
    private readonly ConcurrentDictionary<string, TValue> _values = new(StringComparer.Ordinal);

    private ICommander Commander { get; }

    public TimeSpan GetMethodDelay { get; set; }

    public KeyValueService(IServiceProvider services)
    {
        Commander = services.Commander();
        Debug.WriteLine($"{GetType().GetName()} created @ {services.GetHashCode()}.");
    }

    public virtual Task<Option<TValue>> TryGet(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.TryGetValue(key, out var v) ? Option.Some(v) : default);

#pragma warning disable 1998
    public virtual async Task<TValue> Get(string key, CancellationToken cancellationToken = default)
#pragma warning restore 1998
    {
        if (GetMethodDelay > TimeSpan.Zero)
            await Task.Delay(GetMethodDelay, cancellationToken).ConfigureAwait(false);
        if (key.EndsWith("error"))
            throw new ArgumentException("Error!", nameof(key));
        return _values.TryGetValue(key, out var value) ? value : default!;
    }

    public Task Set(string key, TValue value, CancellationToken cancellationToken = default)
        => Commander.Call(new KeyValueService_Set<TValue>(key, value), cancellationToken);

    public Task Remove(string key, CancellationToken cancellationToken = default)
        => Commander.Call(new KeyValueService_Remove(key), cancellationToken);

    public virtual Task SetCmd(KeyValueService_Set<TValue> cmd, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = TryGet(cmd.Key, default).AssertCompleted();
            _ = Get(cmd.Key, default).AssertCompleted();
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        _values[cmd.Key] = cmd.Value;
        return Task.CompletedTask;
    }

    public virtual Task RemoveCmd(KeyValueService_Remove cmd, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = TryGet(cmd.Key, default).AssertCompleted();
            _ = Get(cmd.Key, default).AssertCompleted();
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        _values.TryRemove(cmd.Key, out _);
        return Task.CompletedTask;
    }
}
