using StackExchange.Redis;

namespace ActualLab.Redis;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// <see cref="RedisDb"/> and <see cref="RedisConnector"/> services.
/// </summary>
public static class ServiceCollectionExt
{
    // Single RedisDb (resolved via RedisDb w/o TContext parameter)

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        Func<IServiceProvider, string> configurationFactory,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configurationFactory.Invoke(c)) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => new RedisDb(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter));
        return services;
    }

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        string configuration,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configuration) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => new RedisDb(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter));
        return services;
    }

    public static IServiceCollection AddRedisDb(
        this IServiceCollection services,
        ConfigurationOptions configuration,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configuration) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => new RedisDb(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter));
        return services;
    }

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        Func<Task<IConnectionMultiplexer>> multiplexerFactory,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(multiplexerFactory) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => new RedisDb(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter));
        return services;
    }

    // Multiple RedisDb-s (resolved via RedisDb<TContext>)

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, string> configurationFactory,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configurationFactory.Invoke(c)) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => {
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        string configuration,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configuration) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => {
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        ConfigurationOptions configuration,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(configuration) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => {
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        Func<Task<IConnectionMultiplexer>> multiplexerFactory,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => new RedisConnector(multiplexerFactory) {
            Log = c.LogFor<RedisConnector>(),
        });
        services.AddSingleton(c => {
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(c.GetRequiredService<RedisConnector>(), keyPrefix, keyDelimiter);
        });
        return services;
    }
}
