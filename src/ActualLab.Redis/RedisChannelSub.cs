using StackExchange.Redis;

namespace ActualLab.Redis;

/// <summary>
/// A Redis subscriber that writes received raw messages to a
/// <see cref="Channel{RedisValue}"/> for asynchronous consumption.
/// </summary>
public sealed class RedisChannelSub(
    RedisDb redisDb,
    RedisSubKey key,
    Channel<RedisValue>? channel = null,
    TimeSpan? subscribeTimeout = null
    ) : RedisSubBase(redisDb, key, subscribeTimeout)
{
    private readonly Channel<RedisValue> _channel = channel ?? Channel.CreateUnbounded<RedisValue>(
        new UnboundedChannelOptions() {
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    public ChannelReader<RedisValue> Messages => _channel.Reader;

    protected override async Task DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);
        _channel.Writer.TryComplete();
    }

    protected override void OnMessage(RedisChannel redisChannel, RedisValue redisValue)
        => _channel.Writer.TryWrite(redisValue);
}

/// <summary>
/// A Redis subscriber that deserializes messages to <typeparamref name="T"/>
/// and writes them to a <see cref="Channel{T}"/> for asynchronous consumption.
/// </summary>
public sealed class RedisChannelSub<T>(
    RedisDb redisDb,
    RedisSubKey key,
    Channel<T>? channel = null,
    IByteSerializer<T>? serializer = null,
    TimeSpan? subscribeTimeout = null
    ) : RedisSubBase(redisDb, key, subscribeTimeout)
{
    private readonly Channel<T> _channel = channel ?? Channel.CreateUnbounded<T>(
        new UnboundedChannelOptions() {
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

    public IByteSerializer<T> Serializer { get; } = serializer ?? ByteSerializer<T>.Default;
    public ChannelReader<T> Messages => _channel.Reader;

    protected override async Task DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);
        _channel.Writer.TryComplete();
    }

    protected override void OnMessage(RedisChannel redisChannel, RedisValue redisValue)
    {
        try {
            var value = Serializer.Read(redisValue, out _);
            _channel.Writer.TryWrite(value);
        }
        catch (Exception e) {
            _channel.Writer.TryComplete(e);
        }
    }
}
