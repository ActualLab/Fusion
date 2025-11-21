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

                var arguments = call.Arguments!;
                var session = arguments.Get<Session>(0);
                if (session.IsDefault()) {
                    session = connection.Session;
                    arguments.Set(0, session);
                }
                else
                    session.RequireValid();
                return Task.CompletedTask;
            };

        if (typeof(ISessionCommand).IsAssignableFrom(p0Type))
            return static call => {
                if (!HasSessionBoundRpcConnection(call, out var connection))
                    return Task.CompletedTask;

                var arguments = call.Arguments!;
                var command = arguments.Get<ISessionCommand>(0);
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
