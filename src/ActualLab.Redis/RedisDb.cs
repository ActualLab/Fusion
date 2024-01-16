using Cysharp.Text;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace ActualLab.Redis;

public class RedisDb
{
    public static string DefaultKeyDelimiter { get; set; } = ".";

    public IConnectionMultiplexer Redis { get; }
    public string KeyPrefix { get; }
    public string KeyDelimiter { get; }
    public IDatabase Database { get; }

    public RedisDb(IConnectionMultiplexer redis, string keyPrefix = "", string? keyDelimiter = null)
    {
        Redis = redis;
        KeyPrefix = keyPrefix;
        KeyDelimiter = keyDelimiter ?? DefaultKeyDelimiter;
        Database = Redis.GetDatabase();
        if (!KeyPrefix.IsNullOrEmpty())
            Database = Database.WithKeyPrefix(ZString.Concat(KeyPrefix, KeyDelimiter));
    }

    public override string ToString()
        => $"{GetType().GetName()}(KeyPrefix = {KeyPrefix}, KeyDelimiter = {KeyDelimiter})";

    public string FullKey(string keySuffix)
        => KeyPrefix.IsNullOrEmpty()
            ? keySuffix
            : ZString.Concat(KeyPrefix, KeyDelimiter, keySuffix);

    public RedisDb WithKeyPrefix(string keyPrefix)
        => keyPrefix.IsNullOrEmpty()
            ? this
            : new RedisDb(Redis, FullKey(keyPrefix), KeyDelimiter);
}

public class RedisDb<TContext>(IConnectionMultiplexer redis, string keyPrefix = "", string? keyDelimiter = null)
    : RedisDb(redis, keyPrefix, keyDelimiter);
