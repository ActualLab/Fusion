using System.Collections.Concurrent;

namespace ActualLab.Fusion.Tests.MeshRpc;


public class RpcRerouteTestService(MeshHost ownHost) : IRpcRerouteTestService
{
    private readonly ConcurrentDictionary<string, string> _storage = new();

    public virtual Task<ValueWithHostId> GetValue(int shardKey, string key, CancellationToken cancellationToken = default)
    {
        var value = _storage.GetValueOrDefault(key, "");
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }

    public virtual Task<ValueWithHostId> SetValue(RpcRerouteTestService_SetValue command, CancellationToken cancellationToken = default)
    {
        var value = command.Value;
        _storage[command.Key] = value;
        using (Invalidation.Begin())
            _ = GetValue(command.ShardKey, command.Key, cancellationToken);
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }
}
