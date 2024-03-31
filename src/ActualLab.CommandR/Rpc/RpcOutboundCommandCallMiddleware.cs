using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Rpc;

public class RpcOutboundCommandCallMiddleware : RpcOutboundMiddleware
{
    public static TimeSpan DefaultConnectTimeout { get; set; } = TimeSpan.FromSeconds(1.5);
    public static TimeSpan DefaultCallTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public static TimeSpan DefaultBackendConnectTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public static TimeSpan DefaultBackendCallTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int DefaultPriority { get; set; } = 10;

    public TimeSpan ConnectTimeout { get; set; } = DefaultConnectTimeout;
    public TimeSpan CallTimeout { get; set; } = DefaultCallTimeout;
    public TimeSpan BackendConnectTimeout { get; set; } = DefaultBackendConnectTimeout;
    public TimeSpan BackendCallTimeout { get; set; } = DefaultBackendCallTimeout;

    public RpcOutboundCommandCallMiddleware(IServiceProvider services)
        : base(services)
        => Priority = DefaultPriority;

    public override void PrepareCall(RpcOutboundContext context)
    {
        var methodDef = context.MethodDef;
        var parameterTypes = methodDef!.ParameterTypes;
        if (parameterTypes.Length is < 1 or > 2)
            return;
        if (!typeof(ICommand).IsAssignableFrom(parameterTypes[0]))
            return;

        var call = context.Call!;
        if (methodDef.IsBackend) {
            call.ConnectTimeout = BackendConnectTimeout;
            call.Timeout = BackendCallTimeout;
        }
        else {
            call.ConnectTimeout = ConnectTimeout;
            call.Timeout = CallTimeout;
        }
    }
}
