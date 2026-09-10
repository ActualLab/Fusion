using ActualLab.DependencyInjection;
using ActualLab.IO;
using ActualLab.OS;
using ActualLab.Redis;
using ActualLab.Reflection;
using CommunityToolkit.HighPerformance;
using MartinCostello.Logging.XUnit;

namespace ActualLab.Tests.Redis;

public class RedisTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected bool UseLogging { get; set; } = true;
    protected bool UseDebugLog { get; set; } = true;

    public virtual RedisDb GetRedisDb()
    {
        var services = (IServiceCollection)new ServiceCollection();
        services.AddSingleton<TestServiceProviderTag>();
        if (UseLogging)
            services.AddLogging(logging => {
                var debugCategories = new List<string> {
                    "ActualLab.Redis",
                    "ActualLab.Tests",
                };

                bool LogFilter(string? category, LogLevel level)
                    => debugCategories.Any(x => category?.StartsWith(x) ?? false)
                        && level >= LogLevel.Debug;

                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter(LogFilter);
                if (UseDebugLog)
                    logging.AddDebug();
                // XUnit logging requires weird setup b/c otherwise it filters out
                // everything below LogLevel.Information
                logging.AddProvider(new XUnitLoggerProvider(Out, new XUnitLoggerOptions { Filter = LogFilter }));
            });
        services.AddRedisDb("127.0.0.1", GetTestRedisKeyPrefix());

        var c = services.BuildServiceProvider();
        var redisDb = c.GetRequiredService<RedisDb>();
        redisDb.FullKey("").Should().EndWith(RedisDb.DefaultKeyDelimiter);
        return redisDb;
    }
}
