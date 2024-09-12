using StackExchange.Redis;

namespace ActualLab.Redis;

public class RedisConnector
{
#if NET9_0_OR_GREATER
    protected readonly Lock Lock = new();
#else
    protected readonly object Lock = new();
#endif
    protected readonly Func<Task<IConnectionMultiplexer>> MultiplexerFactory;
    protected volatile AsyncState<Task<Temporary<IConnectionMultiplexer>>?> State = new(null, false);
    protected volatile CancellationTokenSource? GoneTokenSource;

    public RetryDelaySeq ReconnectDelays { get; init; } = RetryDelaySeq.Exp(0.5, 3, 0.33);
    public RandomTimeSpan WatchdogTestPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public TimeSpan WatchdogReconnectDelay { get; init; } = TimeSpan.FromSeconds(11);
    public MomentClock Clock { get; init; } = CpuClock.Instance;
    public ILogger? Log { get; init; }

    public RedisConnector(string configuration, bool mustStart = true)
        : this(async () => await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false), mustStart)
    { }

    public RedisConnector(ConfigurationOptions configuration, bool mustStart = true)
        : this(async () => await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false), mustStart)
    { }

    public RedisConnector(Func<Task<IConnectionMultiplexer>> multiplexerFactory, bool mustStart = true)
    {
        MultiplexerFactory = multiplexerFactory;
        if (mustStart)
            Reconnect();
    }

    public Task<Temporary<IConnectionMultiplexer>> GetMultiplexer(CancellationToken cancellationToken = default)
    {
        var state = State.Last;
        return state.Value?.WaitAsync(cancellationToken) ?? CompleteAsync(cancellationToken);

        async Task<Temporary<IConnectionMultiplexer>> CompleteAsync(CancellationToken ct)
        {
            var current = state.Last;
            while (current.Value == null)
                current = await current.WhenNext(ct).ConfigureAwait(false);
            return await current.Value.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public void Reconnect(IConnectionMultiplexer? failedMultiplexer = null)
    {
        lock (Lock) {
            var multiplexerTask = State.Value;
            if (multiplexerTask != null) {
                if (!multiplexerTask.IsCompletedSuccessfully())
                    return;

                var (multiplexer, _) = multiplexerTask.Result;
                if (failedMultiplexer != null && !ReferenceEquals(failedMultiplexer, multiplexer))
                    return;
            }

            var goneTokenSource = new CancellationTokenSource();
            State = State.SetNext(CreateMultiplexer(goneTokenSource.Token));
            GoneTokenSource?.CancelAndDisposeSilently();
            GoneTokenSource = goneTokenSource;
        }
    }

    // Protected methods

    protected virtual async Task<Temporary<IConnectionMultiplexer>> CreateMultiplexer(CancellationToken cancellationToken)
    {
        await Task.Yield(); // To make sure the task is created quickly inside the lock
        var delay = TimeSpan.Zero;
        for (var tryIndex = 0;;) {
            try {
                if (delay > TimeSpan.Zero)
                    await Clock.Delay(delay, cancellationToken).ConfigureAwait(false);

                var multiplexer = await MultiplexerFactory.Invoke().WaitAsync(cancellationToken).ConfigureAwait(false);
                _ = Watchdog(multiplexer, cancellationToken);
                return (multiplexer, cancellationToken);
            }
            catch (Exception e) {
                if (cancellationToken.IsCancellationRequested)
                    return (null!, cancellationToken); // Should never fail!

                ++tryIndex;
                delay = ReconnectDelays[tryIndex];
                Log?.LogError(e, "Failed to connect to Redis, will retry in {Delay}", delay.ToShortString());
            }
        }
    }

    protected virtual async Task Watchdog(IConnectionMultiplexer multiplexer, CancellationToken cancellationToken)
    {
        if (WatchdogTestPeriod == default)
            return;

        try {
            while (true) {
                await Clock.Delay(WatchdogTestPeriod.Next(), cancellationToken).ConfigureAwait(false);
                if (multiplexer.IsConnected)
                    continue;

                Log?.LogWarning("Watchdog: disconnect detected, waiting for recovery for {Delay}", WatchdogReconnectDelay);
                var disconnectedAt = Clock.Now;
                while (Clock.Now - disconnectedAt < WatchdogReconnectDelay) {
                    await Clock.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    if (multiplexer.IsConnected)
                        break;
                }
                if (multiplexer.IsConnected)
                    Log?.LogInformation("Watchdog: recovered");
                else
                    break;
            }
            Log?.LogError("Watchdog: disconnect confirmed, will reconnect");
        }
        catch (Exception e) {
            if (e.IsCancellationOf(cancellationToken)) {
                Log?.LogWarning("Watchdog: Reconnect() detected, will restart");
                return;
            }

            Log?.LogError(e, "Watchdog: failed, will reconnect");
        }
        Reconnect(multiplexer);
    }
}
