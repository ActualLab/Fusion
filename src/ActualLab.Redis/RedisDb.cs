using Cysharp.Text;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace ActualLab.Redis;

public class RedisDb
{
    public static string DefaultKeyDelimiter { get; set; } = ".";

    public RedisConnector Connector { get; }
    public string KeyPrefix { get; }
    public string KeyDelimiter { get; }
    public RedisComponent<IDatabase> Database { get; }
    public RedisComponent<ISubscriber> Subscriber { get; }

    public RedisDb(RedisConnector connector, string keyPrefix = "", string? keyDelimiter = null)
    {
        Connector = connector;
        KeyPrefix = keyPrefix;
        KeyDelimiter = keyDelimiter ?? DefaultKeyDelimiter;
        Database = new RedisComponent<IDatabase>(connector, GetDatabase);
        Subscriber = new RedisComponent<ISubscriber>(connector, m => m.GetSubscriber());
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
            : new RedisDb(Connector, FullKey(keyPrefix), KeyDelimiter);

    // Private methods

    private IDatabase GetDatabase(IConnectionMultiplexer multiplexer)
    {
        var database = multiplexer.GetDatabase();
        if (!KeyPrefix.IsNullOrEmpty())
            database = database.WithKeyPrefix(ZString.Concat(KeyPrefix, KeyDelimiter));
        return database;
    }
}

public class RedisDb<TContext>(
    RedisConnector connector,
    string keyPrefix = "",
    string? keyDelimiter = null)
    : RedisDb(connector, keyPrefix, keyDelimiter);
