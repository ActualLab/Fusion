using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardResolver : IHasServices
{
    public IDbShardRegistry ShardRegistry { get; }

    public DbShard Resolve(object source);
}

public interface IDbShardResolver<TDbContext> : IDbShardResolver
{
    public new IDbShardRegistry<TDbContext> ShardRegistry { get; }
}

public abstract class DbShardResolver(IServiceProvider services) : IDbShardResolver
{
    public static string DefaultSessionShardTag { get; set; } = "s";

    public IServiceProvider Services { get; } = services;
    public string SessionShardTag { get; init; } = DefaultSessionShardTag;

    IDbShardRegistry IDbShardResolver.ShardRegistry => UntypedShardRegistry;
    protected abstract IDbShardRegistry UntypedShardRegistry { get; }

    public abstract DbShard Resolve(object source);
}

public class DbShardResolver<TDbContext>(IServiceProvider services)
    : DbShardResolver(services), IDbShardResolver<TDbContext>
{
    protected override IDbShardRegistry UntypedShardRegistry => ShardRegistry;

    [field: AllowNull, MaybeNull]
    public IDbShardRegistry<TDbContext> ShardRegistry
        => field ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();

    public override DbShard Resolve(object source)
    {
        if (ShardRegistry.HasSingleShard)
            return default;

        switch (source) {
            case Session session:
                return new DbShard(session.GetTag(SessionShardTag));
            case IHasShard hasShard:
                return hasShard.Shard;
            case ICommand command:
                if (command is ISessionCommand sessionCommand)
                    return new DbShard(sessionCommand.Session.GetTag(SessionShardTag));
                return default;
            default:
                return default;
        }
    }
}
