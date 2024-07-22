using ActualLab.Redis;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests.Redis;

public class RedisTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    public virtual RedisDb GetRedisDb()
    {
        var services = (IServiceCollection)new ServiceCollection();
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
            logging.AddDebug();
            // XUnit logging requires weird setup b/c otherwise it filters out
            // everything below LogLevel.Information
            logging.AddProvider(
#pragma warning disable CS0618
                new XunitTestOutputLoggerProvider(
                    new TestOutputHelperAccessor() { Output = Out },
                    LogFilter));
#pragma warning restore CS0618
        });
        services.AddRedisDb("localhost", $"actual-lab.fusion.tests.{GetType().Name}");

        var c = services.BuildServiceProvider();
        var redisDb = c.GetRequiredService<RedisDb>();
        redisDb.FullKey("").Should().EndWith(RedisDb.DefaultKeyDelimiter);
        return redisDb;
    }
}
