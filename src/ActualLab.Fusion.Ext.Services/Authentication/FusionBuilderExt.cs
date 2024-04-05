using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.Authentication.Services;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnreferencedCode = ActualLab.Fusion.Internal.UnreferencedCode;

namespace ActualLab.Fusion.Authentication;

public static class FusionBuilderExt
{
    // InMemoryAuthService

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddInMemoryAuthService(this FusionBuilder fusion)
    {
        var services = fusion.Services;
        // In-memory auth service relies on IDbAuthBackend,
        // which requires DbShard-based APIs, so we add fake IDbShardRegistry<Unit>
        // to let it use Unit as TDbContext.
        services.TryAddSingleton<IDbShardRegistry<Unit>>(c => new DbShardRegistry<Unit>(c, DbShard.None));
        services.TryAddSingleton<IDbShardResolver, DbShardResolver>();
        return fusion.AddAuthService(typeof(InMemoryAuthService));
    }

    // DbAuthService<...>

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddDbAuthService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbUserId>(
        this FusionBuilder fusion,
        Action<DbAuthServiceBuilder<TDbContext, DbSessionInfo<TDbUserId>, DbUser<TDbUserId>, TDbUserId>>? configure = null)
        where TDbContext : DbContext
        where TDbUserId : notnull
        => fusion.AddDbAuthService<TDbContext, DbSessionInfo<TDbUserId>, DbUser<TDbUserId>, TDbUserId>(configure);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddDbAuthService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbSessionInfo,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbUser,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbUserId>(
        this FusionBuilder fusion,
        Action<DbAuthServiceBuilder<TDbContext, TDbSessionInfo, TDbUser, TDbUserId>>? configure = null)
        where TDbContext : DbContext
        where TDbSessionInfo : DbSessionInfo<TDbUserId>, new()
        where TDbUser : DbUser<TDbUserId>, new()
        where TDbUserId : notnull
        => new DbAuthServiceBuilder<TDbContext, TDbSessionInfo, TDbUser, TDbUserId>(fusion, configure).Fusion;

    // Custom auth service

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddAuthService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TAuthService>
        (this FusionBuilder fusion)
        where TAuthService : class, IAuthBackend
        => fusion.AddAuthService(typeof(TAuthService));

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddAuthService(
        this FusionBuilder fusion,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        var services = fusion.Services;
        if (services.HasService<IAuthBackend>())
            return fusion;

        var tAuthBackend = typeof(IAuthBackend);
        if (!tAuthBackend.IsAssignableFrom(implementationType))
            throw Errors.MustImplement(implementationType, tAuthBackend, nameof(implementationType));

        fusion.AddService(typeof(IAuth), implementationType, addCommandHandlers: false);
        services.AddSingleton(c => (IAuthBackend)c.GetRequiredService<IAuth>());
        fusion.Commander.AddHandlers(typeof(IAuth));
        fusion.Commander.AddHandlers(typeof(IAuthBackend));
        return fusion;
    }
}
