using StackExchange.Redis;

namespace ActualLab.Redis;

/// <summary>
/// Abstract base class for Redis pub/sub subscribers, managing subscription
/// lifecycle, timeout handling, and message dispatch.
/// </summary>
public abstract class RedisSubBase : ProcessorBase
{
    public static TimeSpan DefaultSubscribeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    private readonly Action<RedisChannel, RedisValue> _onMessage;

    public RedisDb RedisDb { get; }
    public string Key { get; }
    public RedisChannel.PatternMode PatternMode { get; }
    public string FullKey { get; }
    public TimeSpan SubscribeTimeout { get; }
    public RedisComponent<ISubscriber> Subscriber => RedisDb.Subscriber;
    public RedisChannel RedisChannel { get; }
    public Task? WhenSubscribed { get; private set; } = null!;

    protected RedisSubBase(
        RedisDb redisDb, RedisSubKey key,
        TimeSpan? subscribeTimeout = null,
        bool subscribe = true)
    {
        RedisDb = redisDb;
        Key = key.Key;
        PatternMode = key.PatternMode;
        FullKey = RedisDb.FullKey(Key);
        SubscribeTimeout = subscribeTimeout ?? DefaultSubscribeTimeout;
        RedisChannel = new RedisChannel(FullKey, PatternMode);
        _onMessage = OnMessage;
        if (subscribe)
            _ = Subscribe();
    }

    protected override async Task DisposeAsyncCore()
    {
        var whenSubscribed = WhenSubscribed;
        if (whenSubscribed is null)
            return;

        try {
            try {
                if (!whenSubscribed.IsCompleted)
                    await whenSubscribed.ConfigureAwait(false);
            }
            catch {
                // Intended
            }
            var subscriber = await Subscriber.Get().ConfigureAwait(false);
            await subscriber
                // ReSharper disable once InconsistentlySynchronizedField
                .UnsubscribeAsync(RedisChannel, _onMessage, CommandFlags.FireAndForget)
                .ConfigureAwait(false);
        }
        catch {
            // Intended
        }
    }

    public Task Subscribe()
    {
        if (WhenSubscribed is not null)
            return WhenSubscribed;

        lock (Lock) {
            WhenSubscribed ??= Task.Run(async () => {
                using var timeoutCts = StopToken.CreateLinkedTokenSource();
                var timeoutToken = timeoutCts.Token;
                timeoutCts.CancelAfter(SubscribeTimeout);
                try {
                    var subscriber = await Subscriber.Get(timeoutToken).ConfigureAwait(false);
                    await subscriber
                        .SubscribeAsync(RedisChannel, _onMessage)
                        .WaitAsync(timeoutToken)
                        .ConfigureAwait(false);
                }
                catch (Exception e) when (e.IsCancellationOfTimeoutToken(timeoutToken, StopToken)) {
                    throw new TimeoutException($"Timeout while subscribing in {GetType().GetName()}.");
                }
            }, default);
        }
        return WhenSubscribed;
    }

    protected abstract void OnMessage(RedisChannel redisChannel, RedisValue redisValue);
}
