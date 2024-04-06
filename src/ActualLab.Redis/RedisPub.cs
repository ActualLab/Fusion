using System.Text;
using StackExchange.Redis;

namespace ActualLab.Redis;

public class RedisPub
{
    public RedisDb RedisDb { get; }
    public string Key { get; }
    public string FullKey { get; }
    public RedisChannel Channel { get; }
    public RedisComponent<ISubscriber> Subscriber => RedisDb.Subscriber;

    public RedisPub(RedisDb redisDb, string key)
    {
        RedisDb = redisDb;
        Key = key;
        FullKey = RedisDb.FullKey(Key);
        Channel = new RedisChannel(Encoding.UTF8.GetBytes(FullKey), RedisChannel.PatternMode.Auto);
    }

    public async Task<long> Publish(RedisValue item)
    {
        var subscriber = await Subscriber.Get().ConfigureAwait(false);
        return await subscriber.PublishAsync(Channel, item).ConfigureAwait(false);
    }
}

public sealed class RedisPub<T>(RedisDb redisDb, string key, IByteSerializer<T>? serializer = null)
    : RedisPub(redisDb, key)
{
    public IByteSerializer<T> Serializer { get; } = serializer ?? ByteSerializer<T>.Default;

    public async Task<long> Publish(T item)
    {
        using var bufferWriter = Serializer.Write(item);
        return await base.Publish(bufferWriter.WrittenMemory).ConfigureAwait(false);
    }
}
