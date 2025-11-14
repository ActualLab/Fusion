using System.Diagnostics;

namespace ActualLab.Rpc;

public sealed partial record RpcCallTimeouts
{
    public static class Default
    {
        public static bool UseDebug { get; set; } = Debugger.IsAttached;

        public static RpcCallTimeouts Debug { get; set; } = new(double.NaN, 300);
        public static RpcCallTimeouts Query { get; set; } = None;
        public static RpcCallTimeouts Command { get; set; } = new(1.5, 10);
        public static RpcCallTimeouts BackendQuery { get; set; } = None;
        public static RpcCallTimeouts BackendCommand { get; set; } = new(300, 300);

        public static RpcCallTimeouts Get(RpcMethodDef methodDef)
        {
            if (UseDebug)
                return Debug;

            return (methodDef.Kind is RpcMethodKind.Command, methodDef.IsBackend) switch {
                (true, true) => BackendCommand,
                (true, false) => Command,
                (false, true) => BackendQuery,
                (false, false) => Query,
            };
        }
    }
}
