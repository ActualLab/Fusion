using StackExchange.Redis;

namespace ActualLab.Redis;

public abstract class RedisSubBase : ProcessorBase
{
    public static TimeSpan DefaultSubscribeTimeout { get; set; } = TimeSpan.FromSeconds(5);

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
                using var timeoutCts = new CancellationTokenSource(SubscribeTimeout);
                using var linkedCts = timeoutCts.Token.LinkWith(StopToken);
                var cancellationToken = linkedCts.Token;
                try {
                    var subscriber = await Subscriber.Get(cancellationToken).ConfigureAwait(false);
                    await subscriber
                        .SubscribeAsync(RedisChannel, _onMessage)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    if (WhenDisposed is not null)
                        throw;
                    if (timeoutCts.IsCancellationRequested)
                        throw new TimeoutException();
                    throw;
                }
            }, default);
        }
        return WhenSubscribed;
    }

    protected abstract void OnMessage(RedisChannel redisChannel, RedisValue redisValue);
}
