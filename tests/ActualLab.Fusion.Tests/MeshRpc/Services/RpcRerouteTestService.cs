using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Tests.MeshRpc;

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class RpcRerouteTestService(MeshHost ownHost) : IRpcRerouteTestService
{
    private readonly ConcurrentDictionary<string, string> _storage = new();

    private ICommander Commander { get; } = ownHost.Commander();

    public virtual Task<ValueWithHostId> GetValue(int shardKey, string key, CancellationToken cancellationToken = default)
    {
        var value = _storage.GetValueOrDefault(key, "");
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }

    public virtual Task<ValueWithHostId> GetValueDirect(int shardKey, string key, CancellationToken cancellationToken = default)
    {
        var value = _storage.GetValueOrDefault(key, "");
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }

    public virtual async Task<ValueWithHostId> SetValue(RpcRerouteTestService_SetValue command, CancellationToken cancellationToken = default)
    {
        var value = command.Value;
        _storage[command.Key] = value;
        TransientOperationScope.Require(); // We want to trigger operation invalidation handling here
        Invalidation.Defer(() => _ = GetValue(command.ShardKey, command.Key, default));

        // Make a recursive call if ExtraCount > 0
        if (command.ExtraCount > 0) {
            var nextKey = $"next_{command.Key}";
            var nextShardKey = command.ShardKey + 1;
            await Commander.Call(
                new RpcRerouteTestService_SetValue(nextShardKey, nextKey, value, command.ExtraCount - 1),
                cancellationToken);

            // Verify the recursive call result
            var nextValue = await GetValue(nextShardKey, nextKey, cancellationToken);
            if (nextValue.Value != value)
                throw new InvalidOperationException(
                    $"GetValue verification failed: expected '{value}', got '{nextValue.Value}'");

            nextValue = await GetValueDirect(nextShardKey, nextKey, cancellationToken);
            if (nextValue.Value != value)
                throw new InvalidOperationException(
                    $"GetValueDirect verification failed: expected '{value}', got '{nextValue.Value}'");
        }

        return new ValueWithHostId(value, ownHost.Id);
    }
}
