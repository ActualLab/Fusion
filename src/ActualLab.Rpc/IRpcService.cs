using ActualLab.Interception;

namespace ActualLab.Rpc;

/// <summary>
/// Marker interface for services that can be invoked via RPC.
/// </summary>
public interface IRpcService : IRequiresAsyncProxy;
