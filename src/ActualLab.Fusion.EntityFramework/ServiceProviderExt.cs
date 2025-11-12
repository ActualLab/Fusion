using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DbHub<TDbContext> DbHub<TDbContext>()
            where TDbContext : DbContext
            => services.GetRequiredService<DbHub<TDbContext>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDbShardResolver<TDbContext> DbShardResolver<TDbContext>()
            => services.GetRequiredService<IDbShardResolver<TDbContext>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDbEntityResolver<TKey, TDbEntity> DbEntityResolver<TKey, TDbEntity>()
            where TKey : notnull
            where TDbEntity : class
            => services.GetRequiredService<IDbEntityResolver<TKey, TDbEntity>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDbEntityConverter<TDbEntity, TEntity> DbEntityConverter<TDbEntity, TEntity>()
            where TEntity : notnull
            where TDbEntity : class
            => services.GetRequiredService<IDbEntityConverter<TDbEntity, TEntity>>();
    }
}
