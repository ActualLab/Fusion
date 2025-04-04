using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardResolver : IHasServices
{
    public IDbShardRegistry ShardRegistry { get; }

    public string Resolve(object source);
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

    public abstract string Resolve(object source);
}

public class DbShardResolver<TDbContext>(IServiceProvider services)
    : DbShardResolver(services), IDbShardResolver<TDbContext>
{
    protected override IDbShardRegistry UntypedShardRegistry => ShardRegistry;

    [field: AllowNull, MaybeNull]
    public IDbShardRegistry<TDbContext> ShardRegistry
        => field ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();

    public override string Resolve(object source)
    {
        if (ShardRegistry.HasSingleShard)
            return DbShard.Single;

        switch (source) {
            case Session session:
                return DbShard.Validate(session.GetTag(SessionShardTag));
            case IHasShard hasShard:
                return DbShard.Validate(hasShard.Shard);
            case ICommand command:
                if (command is ISessionCommand sessionCommand)
                    return DbShard.Validate(sessionCommand.Session.GetTag(SessionShardTag));
                return DbShard.Single;
            default:
                return DbShard.Single;
        }
    }
}
