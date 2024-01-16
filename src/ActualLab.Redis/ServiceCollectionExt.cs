using Cysharp.Text;
using StackExchange.Redis;

namespace ActualLab.Redis;

public static class ServiceCollectionExt
{
    // Single RedisDb (resolved via RedisDb w/o TContext parameter)

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        Func<IServiceProvider, string> configurationFactory,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => {
            var configuration = configurationFactory(c);
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            return new RedisDb(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        string configuration,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => {
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            return new RedisDb(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb(
        this IServiceCollection services,
        ConfigurationOptions configuration,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => {
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            return new RedisDb(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb(this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        string keyPrefix = "",
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => new RedisDb(connectionMultiplexer, keyPrefix, keyDelimiter));
        return services;
    }

    // Multiple RedisDb-s (resolved via RedisDb<TContext>)

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, string> configurationFactory,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(c => {
            var configuration = configurationFactory(c);
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        string configuration,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => {
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        ConfigurationOptions configuration,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => {
            var multiplexer = ConnectionMultiplexer.Connect(configuration);
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(multiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }

    public static IServiceCollection AddRedisDb<TContext>(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        string? keyPrefix = null,
        string? keyDelimiter = null)
    {
        services.AddSingleton(_ => {
            keyPrefix ??= typeof(TContext).GetName();
            return new RedisDb<TContext>(connectionMultiplexer, keyPrefix, keyDelimiter);
        });
        return services;
    }
}
