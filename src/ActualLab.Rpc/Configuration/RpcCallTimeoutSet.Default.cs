using System.Diagnostics;

namespace ActualLab.Rpc;

public sealed partial record RpcCallTimeoutSet
{
    public static class Default
    {
        public static bool UseDebug { get; set; } = Debugger.IsAttached;

        public static RpcCallTimeoutSet Debug { get; set; } = new(connectTimeout: null, 300);
        public static RpcCallTimeoutSet Query { get; set; } = RpcCallTimeoutSet.None;
        public static RpcCallTimeoutSet Command { get; set; } = new(1.5, 10);
        public static RpcCallTimeoutSet BackendQuery { get; set; } = RpcCallTimeoutSet.None;
        public static RpcCallTimeoutSet BackendCommand { get; set; } = new(300, 300);

        public static RpcCallTimeoutSet Get(RpcMethodDef methodDef)
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
