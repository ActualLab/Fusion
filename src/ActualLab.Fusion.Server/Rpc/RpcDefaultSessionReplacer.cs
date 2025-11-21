using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Authentication;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Server.Rpc;

public class RpcDefaultSessionReplacer : RpcInboundCallPreprocessor
{
    public override Func<RpcInboundCall, Task> CreateInboundCallPreprocessor(RpcMethodDef methodDef)
    {
        var parameters = methodDef.Parameters;
        if (parameters.Length == 0)
            return None;

        var p0Type = parameters[0].ParameterType;
        if (p0Type == typeof(Session))
            return static call => {
                if (!HasSessionBoundRpcConnection(call, out var connection))
                    return Task.CompletedTask;

                var args = call.Arguments!;
                var session = (Session?)args.Get0Untyped();
                if (session is null) {
                    // We assume the nullability is validated by RpcInboundCallOptions.UseNullabilityArgumentValidator
                    return Task.CompletedTask;
                }

                if (session.IsDefault()) {
                    session = connection.Session;
                    args.Set(0, session);
                }
                else
                    session.RequireValid();
                return Task.CompletedTask;
            };

        if (typeof(ISessionCommand).IsAssignableFrom(p0Type))
            return static call => {
                if (!HasSessionBoundRpcConnection(call, out var connection))
                    return Task.CompletedTask;

                var args = call.Arguments!;
                var command = (ISessionCommand?)args.Get0Untyped();
                if (command is null) {
                    // We assume the nullability is validated by RpcInboundCallOptions.UseNullabilityArgumentValidator
                    return Task.CompletedTask;
                }

                var session = command.Session;
                if (session.IsDefault())
                    command.SetSession(connection.Session);
                else
                    session.RequireValid();
                return Task.CompletedTask;
            };

        return None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool HasSessionBoundRpcConnection(
        RpcInboundCall call,
        [NotNullWhen(true)] out SessionBoundRpcConnection? connection)
    {
        connection = call.Context.Peer.ConnectionState.Value.Connection as SessionBoundRpcConnection;
        return connection != null;
    }
}
