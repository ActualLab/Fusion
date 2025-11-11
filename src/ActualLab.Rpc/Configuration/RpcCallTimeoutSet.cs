
using System.Diagnostics;

namespace ActualLab.Rpc;

public sealed record RpcCallTimeoutSet
{
    public static readonly RpcCallTimeoutSet None = new();
    public static int DelayedCallLogLimit { get; set; } = 10;

    public static class Defaults
    {
        public static bool UseDebug { get; set; } = Debugger.IsAttached;
        public static RpcCallTimeoutSet Debug { get; set; } = new(null, 3);

        public static RpcCallTimeoutSet Query { get; set; } = None;
        public static RpcCallTimeoutSet Command { get; set; } = new(1.5, 10);
        public static RpcCallTimeoutSet BackendQuery { get; set; } = None;
        public static RpcCallTimeoutSet BackendCommand { get; set; } = new(300, 300);
        public static TimeSpan LogTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public static RpcCallTimeoutSet GetDefault(RpcMethodDef methodDef)
    {
        if (Defaults.UseDebug)
            return Defaults.Debug;

        return (methodDef.Kind is RpcMethodKind.Command, methodDef.IsBackend) switch {
            (true, true) => Defaults.BackendCommand,
            (true, false) => Defaults.Command,
            (false, true) => Defaults.BackendQuery,
            (false, false) => Defaults.Query,
        };
    }

    public TimeSpan ConnectTimeout { get; init => field = value.Positive(); }
    public TimeSpan Timeout { get; init => field = value.Positive(); }
    public TimeSpan LogTimeout { get; init => field = value.Positive(); }

    // TimeSpan overloads

    public RpcCallTimeoutSet()
        : this(TimeSpan.MaxValue, TimeSpan.MaxValue)
    { }

    public RpcCallTimeoutSet(TimeSpan timeout)
        : this(TimeSpan.MaxValue, timeout.Positive())
    { }

    public RpcCallTimeoutSet(TimeSpan connectTimeout, TimeSpan timeout)
    {
        ConnectTimeout = connectTimeout;
        Timeout = timeout;
        LogTimeout = Defaults.LogTimeout;
    }

    // TimeSpan? overloads

    public RpcCallTimeoutSet(TimeSpan? timeout)
        : this(TimeSpan.MaxValue, ToTimeout(timeout))
    { }

    // double? overloads

    public RpcCallTimeoutSet(double? timeout)
        : this(TimeSpan.MaxValue, ToTimeout(timeout))
    { }

    public RpcCallTimeoutSet(double? connectTimeout, double? timeout)
        : this(ToTimeout(connectTimeout), ToTimeout(timeout))
    { }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan ToTimeout(TimeSpan? timeout)
        => timeout ?? TimeSpan.MaxValue;

    private static TimeSpan ToTimeout(double? timeout)
        => timeout is { } value
            ? TimeSpan.FromSeconds(value)
            : TimeSpan.MaxValue;
}
