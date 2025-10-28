using System.Collections.Concurrent;

namespace ActualLab.Fusion.Tests.Rpc.Services;


public class RpcRerouteTestService(TestHost ownHost) : IRpcRerouteTestService
{
    private static readonly ConcurrentDictionary<string, string> Storage = new();

    public virtual Task<ValueWithHostId> GetValue(string hostId, string key, CancellationToken cancellationToken = default)
    {
        var value = Storage.GetValueOrDefault(key, "");
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }

    public virtual Task<ValueWithHostId> SetValue(RpcRerouteTestService_SetValue command, CancellationToken cancellationToken = default)
    {
        var value = command.Value;
        Storage[command.Key] = value;
        return Task.FromResult(new ValueWithHostId(value, ownHost.Id));
    }
}
