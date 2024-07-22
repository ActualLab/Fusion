using StackExchange.Redis;

namespace ActualLab.Redis;

public sealed class RedisHash(RedisDb redisDb, string hashKey)
{
    public RedisDb RedisDb { get; } = redisDb;
    public string HashKey { get; } = hashKey;

    public async Task<RedisValue> Get(string key)
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.HashGetAsync(HashKey, key).ConfigureAwait(false);
    }

    public async Task<HashEntry[]> GetAll()
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.HashGetAllAsync(HashKey).ConfigureAwait(false);
    }

    public async Task<bool> Set(string key, RedisValue value)
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.HashSetAsync(HashKey, key, value).ConfigureAwait(false);
    }

    public async Task<bool> Remove(string key)
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.HashDeleteAsync(HashKey, key).ConfigureAwait(false);
    }

    public async Task<long> Increment(string key, long increment = 1)
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.HashIncrementAsync(HashKey, key, increment).ConfigureAwait(false);
    }

    public async Task<bool> Clear()
    {
        var database = await RedisDb.Database.Get().ConfigureAwait(false);
        return await database.KeyDeleteAsync(HashKey).ConfigureAwait(false);
    }
}
