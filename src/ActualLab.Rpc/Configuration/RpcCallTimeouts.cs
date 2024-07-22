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
    private static readonly RpcCallTimeouts Default = new(null, 30) { TimeoutAction = RpcCallTimeoutAction.Log };

    public static class Defaults
    {
        public static RpcCallTimeouts Debug { get; set; } = new(null, 3) { TimeoutAction = RpcCallTimeoutAction.Log };
        public static RpcCallTimeouts Query { get; set; } = Default;
        public static RpcCallTimeouts Command { get; set; } = new(1.5, 10);
        public static RpcCallTimeouts BackendQuery { get; set; } = Default;
        public static RpcCallTimeouts BackendCommand { get; set; } = new(300, 300);
    }

    public TimeSpan ConnectTimeout { get; init; }
    public TimeSpan Timeout { get; init; }
    public RpcCallTimeoutAction TimeoutAction { get; init; }

    public RpcCallTimeouts(TimeSpan connectTimeout, TimeSpan timeout, AssumeValid _)
    {
        ConnectTimeout = connectTimeout;
        Timeout = timeout;
        TimeoutAction = timeout > TimeSpan.Zero && timeout != TimeSpan.MaxValue
            ? RpcCallTimeoutAction.LogAndThrow
            : RpcCallTimeoutAction.None;
    }

    public RpcCallTimeouts()
        : this(TimeSpan.MaxValue, TimeSpan.MaxValue, AssumeValid.Option)
    { }

    // TimeSpan overloads
    public RpcCallTimeouts(TimeSpan timeout)
        : this(TimeSpan.MaxValue, ToTimeout(timeout), AssumeValid.Option) { }
    public RpcCallTimeouts(TimeSpan connectTimeout, TimeSpan timeout)
        : this(ToTimeout(connectTimeout), ToTimeout(timeout), AssumeValid.Option) { }

    // TimeSpan? overloads
    public RpcCallTimeouts(TimeSpan? timeout)
        : this(TimeSpan.MaxValue, ToTimeout(timeout), AssumeValid.Option) { }
    public RpcCallTimeouts(TimeSpan? connectTimeout, TimeSpan? timeout)
        : this(ToTimeout(connectTimeout), ToTimeout(timeout), AssumeValid.Option) { }

    // double? overloads
    public RpcCallTimeouts(double? timeout)
        : this(TimeSpan.MaxValue, ToTimeout(timeout), AssumeValid.Option) { }
    public RpcCallTimeouts(double? connectTimeout, double? timeout)
        : this(ToTimeout(connectTimeout), ToTimeout(timeout), AssumeValid.Option) { }

    public RpcCallTimeouts Normalize()
    {
        var timeoutAction = Timeout > TimeSpan.Zero && Timeout != TimeSpan.MaxValue
            ? TimeoutAction
            : RpcCallTimeoutAction.None;
        return TimeoutAction == timeoutAction
            ? this
            : this with { TimeoutAction = timeoutAction };
    }

    // Private methods

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
