using StackExchange.Redis;
using ActualLab.Redis;

namespace ActualLab.Tests.Redis;

public class RedisTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    public virtual RedisDb GetRedisDb()
    {
        var redis = ConnectionMultiplexer.Connect("localhost");
        return new RedisDb(redis).WithKeyPrefix("stl.fusion.tests").WithKeyPrefix(GetType().Name);
    }
}
