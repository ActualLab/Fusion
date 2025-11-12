namespace ActualLab.Rpc;

public sealed partial record RpcCallTimeoutSet
{
    public static readonly RpcCallTimeoutSet None = new();
    public static TimeSpan DefaultLogTimeout { get; set; } = TimeSpan.FromSeconds(30);

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
        LogTimeout = DefaultLogTimeout;
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
