using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve common Fusion
/// EntityFramework services such as <see cref="DbHub{TDbContext}"/> and entity resolvers.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DbHub<TDbContext> DbHub<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
        => services.GetRequiredService<DbHub<TDbContext>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDbShardResolver<TDbContext> DbShardResolver<TDbContext>(this IServiceProvider services)
        => services.GetRequiredService<IDbShardResolver<TDbContext>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDbEntityResolver<TKey, TDbEntity> DbEntityResolver<TKey, TDbEntity>(this IServiceProvider services)
        where TKey : notnull
        where TDbEntity : class
        => services.GetRequiredService<IDbEntityResolver<TKey, TDbEntity>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDbEntityConverter<TDbEntity, TEntity> DbEntityConverter<TDbEntity, TEntity>(this IServiceProvider services)
        where TEntity : notnull
        where TDbEntity : class
        => services.GetRequiredService<IDbEntityConverter<TDbEntity, TEntity>>();
}
