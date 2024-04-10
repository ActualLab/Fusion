using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Rpc;

public class RpcOutboundCommandCallMiddleware : RpcOutboundMiddleware
{
    public static class Default
    {
        public static int Priority { get; set; } = 10;
        public static TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(1.5);
        public static TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public static TimeSpan BackendConnectTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public static TimeSpan BackendCallTimeout { get; set; } = TimeSpan.FromMinutes(5);
        // Timeouts when RpcSettings.DebugMode == true
        public static TimeSpan DebugModeTimeouts { get; set; } = TimeSpan.Zero; // Zero = no timeout!
    }

    public TimeSpan ConnectTimeout { get; set; }
    public TimeSpan CallTimeout { get; set; }
    public TimeSpan BackendConnectTimeout { get; set; }
    public TimeSpan BackendCallTimeout { get; set; }

    public RpcOutboundCommandCallMiddleware(IServiceProvider services)
        : base(services)
    {
        Priority = Default.Priority;
        ConnectTimeout = Default.ConnectTimeout;
        CallTimeout = Default.CallTimeout;
        BackendConnectTimeout = Default.BackendConnectTimeout;
        BackendCallTimeout = Default.BackendCallTimeout;
        if (RpcSettings.DebugMode)
            ConnectTimeout = CallTimeout = BackendConnectTimeout = BackendCallTimeout = Default.DebugModeTimeouts;
    }

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
