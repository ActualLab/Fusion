using StackExchange.Redis;

namespace ActualLab.Redis;

public sealed class RedisActionSub(
    RedisDb redisDb,
    RedisSubKey key,
    Action<RedisChannel, RedisValue> messageHandler,
    TimeSpan? subscribeTimeout = null
    ) : RedisSubBase(redisDb, key, subscribeTimeout)
{
    private Action<RedisChannel, RedisValue> MessageHandler { get; } = messageHandler;

    protected override void OnMessage(RedisChannel redisChannel, RedisValue redisValue)
        => MessageHandler(redisChannel, redisValue);
}

public sealed class RedisActionSub<T>(RedisDb redisDb,
        RedisSubKey key,
        Action<RedisChannel, T> messageHandler,
        IByteSerializer<T>? serializer = null,
        TimeSpan? subscribeTimeout = null)
    : RedisSubBase(redisDb, key, subscribeTimeout)
{
    private Action<RedisChannel, T> MessageHandler { get; } = messageHandler;

    public IByteSerializer<T> Serializer { get; } = serializer ?? ByteSerializer<T>.Default;

    protected override void OnMessage(RedisChannel redisChannel, RedisValue redisValue)
    {
        var value = Serializer.Read(redisValue);
        MessageHandler(redisChannel, value);
    }
}
