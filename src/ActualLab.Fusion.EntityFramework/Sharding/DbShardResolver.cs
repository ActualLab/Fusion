namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardResolver : IHasServices
{
    IDbShardRegistry ShardRegistry { get; }

    DbShard Resolve(object source);
}

public interface IDbShardResolver<TDbContext> : IDbShardResolver
{
    new IDbShardRegistry<TDbContext> ShardRegistry { get; }
}

public class DbShardResolver<TDbContext>(IServiceProvider services) : IDbShardResolver<TDbContext>
{
    private IDbShardRegistry<TDbContext>? _shardRegistry;

    public IServiceProvider Services { get; } = services;

    IDbShardRegistry IDbShardResolver.ShardRegistry => ShardRegistry;
    public IDbShardRegistry<TDbContext> ShardRegistry
        => _shardRegistry ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();

    public virtual DbShard Resolve(object source)
    {
        if (ShardRegistry.HasSingleShard)
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
}
