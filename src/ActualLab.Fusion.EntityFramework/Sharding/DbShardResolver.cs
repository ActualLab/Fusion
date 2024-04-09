namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardResolver
{
    DbShard Resolve(Type contextType, object source);
}

public class DbShardResolver(IServiceProvider services) : IDbShardResolver
{
    private readonly ConcurrentDictionary<Type, IDbShardRegistry> _shardRegistryCache = new();

    protected IServiceProvider Services { get; } = services;

    public DbShard Resolve(Type contextType, object source)
    {
        var shardRegistry = GetShardRegistry(contextType);
        if (shardRegistry.HasSingleShard)
            return default;

        switch (source) {
            case Session session:
                return new DbShard(session.GetTag(Session.ShardTag));
            case IHasShard hasShard:
                return hasShard.Shard;
            case ICommand command:
                if (command is ISessionCommand sessionCommand)
                    return new DbShard(sessionCommand.Session.GetTag(Session.ShardTag));
                return default;
            default:
                return default;
        }
    }

    protected IDbShardRegistry GetShardRegistry(Type dbContextType)
        => _shardRegistryCache.GetOrAdd(dbContextType,
            static (t, self) => (IDbShardRegistry)self.Services.GetRequiredService(typeof(IDbShardRegistry<>).MakeGenericType(t)),
            this);
}
