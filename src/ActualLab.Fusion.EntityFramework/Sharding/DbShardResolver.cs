namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Defines the contract for resolving the target database shard from a source object
/// such as a command or session.
/// </summary>
public interface IDbShardResolver : IHasServices
{
    public IDbShardRegistry ShardRegistry { get; }

    public string Resolve(object source);
}

/// <summary>
/// A typed <see cref="IDbShardResolver"/> scoped to a specific <see cref="DbContext"/> type.
/// </summary>
public interface IDbShardResolver<TDbContext> : IDbShardResolver
{
    public new IDbShardRegistry<TDbContext> ShardRegistry { get; }
}

/// <summary>
/// Abstract base for <see cref="IDbShardResolver"/> implementations, providing
/// a session shard tag and common resolution infrastructure.
/// </summary>
public abstract class DbShardResolver(IServiceProvider services) : IDbShardResolver
{
    public static string DefaultSessionShardTag { get; set; } = "s";

    public IServiceProvider Services { get; } = services;
    public string SessionShardTag { get; init; } = DefaultSessionShardTag;

    IDbShardRegistry IDbShardResolver.ShardRegistry => UntypedShardRegistry;
    protected abstract IDbShardRegistry UntypedShardRegistry { get; }

    public abstract string Resolve(object source);
}

/// <summary>
/// Default <see cref="IDbShardResolver{TDbContext}"/> that resolves shards from
/// <see cref="Session"/>, <see cref="IHasShard"/>, and <see cref="ISessionCommand"/> objects.
/// </summary>
public class DbShardResolver<TDbContext>(IServiceProvider services)
    : DbShardResolver(services), IDbShardResolver<TDbContext>
{
    protected override IDbShardRegistry UntypedShardRegistry => ShardRegistry;

    public IDbShardRegistry<TDbContext> ShardRegistry
        => field ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();

    public override string Resolve(object source)
    {
        if (ShardRegistry.HasSingleShard)
            return DbShard.Single;

        switch (source) {
            case Session session:
                return Validate(session.GetTag(SessionShardTag));
            case IHasShard hasShard:
                return Validate(hasShard.Shard);
            case ICommand command:
                if (command is ISessionCommand sessionCommand)
                    return Validate(sessionCommand.Session.GetTag(SessionShardTag));
                return DbShard.Single;
            default:
                return DbShard.Single;
        }
    }

    // Private methods

    private string Validate(string shard)
    {
        // Every caller uses the result as a per-shard cache key, so an unregistered shard must be
        // rejected here rather than on first use. DbShard.Validate still runs first: CanUse accepts
        // DbShard.Template, which must never be reachable from a client-supplied tag.
        DbShard.Validate(shard);
        return ShardRegistry.CanUse(shard)
            ? shard
            : throw Internal.Errors.NoShard(shard);
    }
}
