namespace ActualLab.Rpc;

public sealed partial record RpcCallTimeouts
{
    public static readonly RpcCallTimeouts None = new();
    public static TimeSpan DefaultLogTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectTimeout { get; init => field = value.Positive(); }
    public TimeSpan RunTimeout { get; init => field = value.Positive(); }
    public TimeSpan LogTimeout { get; init => field = value.Positive(); } = DefaultLogTimeout;

    // TimeSpan overloads

    public RpcCallTimeouts()
        : this(TimeSpan.MaxValue, TimeSpan.MaxValue)
    { }

    public RpcCallTimeouts(TimeSpan runTimeout)
        : this(TimeSpan.MaxValue, runTimeout.Positive())
    { }

    public RpcCallTimeouts(TimeSpan connectTimeout, TimeSpan runTimeout)
    {
        ConnectTimeout = connectTimeout;
        RunTimeout = runTimeout;
    }

    // TimeSpan? overloads

    public RpcCallTimeouts(TimeSpan? runTimeout)
        : this(TimeSpan.MaxValue, ToTimeout(runTimeout))
    { }

    // double? overloads

    public RpcCallTimeouts(double? runTimeout)
        : this(TimeSpan.MaxValue, ToTimeout(runTimeout))
    { }

    public RpcCallTimeouts(double? connectTimeout, double? runTimeout)
        : this(ToTimeout(connectTimeout), ToTimeout(runTimeout))
    { }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan ToTimeout(TimeSpan? timeout)
        => timeout ?? TimeSpan.MaxValue;

    private static TimeSpan ToTimeout(double? timeout)
        => timeout is { } value and not double.NaN and not double.PositiveInfinity
            ? TimeSpan.FromSeconds(value)
            : TimeSpan.MaxValue;
}
