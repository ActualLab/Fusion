using System.Diagnostics;

namespace ActualLab.Rpc;

public sealed record RpcCallTimeouts
{
    public static Func<RpcMethodDef, RpcCallTimeouts> DefaultProvider { get; set; } =
        method => {
            if (Debugger.IsAttached)
                return Defaults.Debug;

            if (method.IsBackend)
                return method.IsCommand ? Defaults.BackendCommand : Defaults.BackendQuery;

            return method.IsCommand ? Defaults.Command : Defaults.Query;
        };

    public static readonly RpcCallTimeouts None = new();

    public static class Defaults
    {
        public static RpcCallTimeouts Debug { get; set; } = None;
        public static RpcCallTimeouts Query { get; set; } = None;
        public static RpcCallTimeouts Command { get; set; } = new(10, 1.5);
        public static RpcCallTimeouts BackendQuery { get; set; } = None;
        public static RpcCallTimeouts BackendCommand { get; set; } = new(300, 300);
    }

    public TimeSpan Timeout { get; init; }
    public TimeSpan ConnectTimeout { get; init; }

    public RpcCallTimeouts(TimeSpan timeout, TimeSpan connectTimeout, AssumeValid _)
    {
        Timeout = timeout;
        ConnectTimeout = connectTimeout;
    }

    public RpcCallTimeouts()
        : this(TimeSpan.MaxValue, TimeSpan.MaxValue, AssumeValid.Option) { }

    // TimeSpan overloads
    public RpcCallTimeouts(TimeSpan timeout)
        : this(ToTimeout(timeout), TimeSpan.MaxValue, AssumeValid.Option) { }
    public RpcCallTimeouts(TimeSpan timeout, TimeSpan? connectTimeout)
        : this(ToTimeout(timeout), ToTimeout(connectTimeout), AssumeValid.Option) { }

    // TimeSpan? overloads
    public RpcCallTimeouts(TimeSpan? timeout)
        : this(ToTimeout(timeout), TimeSpan.MaxValue, AssumeValid.Option) { }
    public RpcCallTimeouts(TimeSpan? timeout, TimeSpan? connectTimeout)
        : this(ToTimeout(timeout), ToTimeout(connectTimeout), AssumeValid.Option) { }

    // double? overloads
    public RpcCallTimeouts(double? timeout)
        : this(ToTimeout(timeout), TimeSpan.MaxValue, AssumeValid.Option) { }
    public RpcCallTimeouts(double? timeout, double? connectTimeout)
        : this(ToTimeout(timeout), ToTimeout(connectTimeout), AssumeValid.Option) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan ToTimeout(TimeSpan timeout)
        => timeout > TimeSpan.Zero ? timeout : TimeSpan.MaxValue;

    private static TimeSpan ToTimeout(TimeSpan? timeout)
        => timeout is { } value && value > TimeSpan.Zero
            ? value
            : TimeSpan.MaxValue;

    private static TimeSpan ToTimeout(double? timeout)
        => timeout is { } value and > 0d
            ? TimeSpan.FromSeconds(value)
            : TimeSpan.MaxValue;
}
