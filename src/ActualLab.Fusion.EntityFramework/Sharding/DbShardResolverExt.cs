namespace ActualLab.Fusion.EntityFramework;

public static class DbShardResolverExt
{
    public static DbShard Resolve<TContext>(this IDbShardResolver shardResolver, object source)
        => shardResolver.Resolve(typeof(TContext), source);
}
