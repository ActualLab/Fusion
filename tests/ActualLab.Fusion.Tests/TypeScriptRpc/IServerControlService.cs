using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public interface IServerControlService : IRpcService
{
    Task RequestRestart();
}
