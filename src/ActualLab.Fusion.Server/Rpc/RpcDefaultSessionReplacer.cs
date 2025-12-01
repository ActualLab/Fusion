using ActualLab.Fusion.Authentication;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.Fusion.Server.Rpc;

public sealed record RpcDefaultSessionReplacer : IRpcMiddleware
{
    public double Priority { get; init; } = RpcInboundMiddlewarePriority.ArgumentValidation - 1;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        var methodDef = context.MethodDef;
        var parameters = methodDef.Parameters;
        if (parameters.Length == 0)
            return next;

        var p0Type = parameters[0].ParameterType;
        if (p0Type == typeof(Session))
            return call => {
                if (!HasSessionBoundRpcConnection(call, out var connection))
                    return next.Invoke(call);

                var args = call.Arguments!;
                var session = (Session?)args.Get0Untyped();
                if (session is null) {
                    // We assume the nullability is validated by RpcInboundCallOptions.UseNullabilityArgumentValidator
                    return next.Invoke(call);
                }

                if (session.IsDefault()) {
                    session = connection.Session;
                    args.Set(0, session);
                }
                else
                    session.RequireValid();
                return next.Invoke(call);
            };

        if (typeof(ISessionCommand).IsAssignableFrom(p0Type))
            return call => {
                if (!HasSessionBoundRpcConnection(call, out var connection))
                    return next.Invoke(call);

                var args = call.Arguments!;
                var command = (ISessionCommand?)args.Get0Untyped();
                if (command is null) {
                    // We assume the nullability is validated by RpcInboundCallOptions.UseNullabilityArgumentValidator
                    return next.Invoke(call);
                }

                var session = command.Session;
                if (session.IsDefault())
                    command.SetSession(connection.Session);
                else
                    session.RequireValid();
                return next.Invoke(call);
            };

        return next;
    }

    // Protected methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSessionBoundRpcConnection(
        RpcInboundCall call,
        [NotNullWhen(true)] out SessionBoundRpcConnection? connection)
    {
        connection = call.Context.Peer.ConnectionState.Value.Connection as SessionBoundRpcConnection;
        return connection != null;
    }
}
