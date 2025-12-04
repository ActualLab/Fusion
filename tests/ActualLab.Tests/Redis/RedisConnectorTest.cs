using ActualLab.Redis;

namespace ActualLab.Tests.Redis;

public class RedisConnectorTest(ITestOutputHelper @out) : RedisTestBase(@out)
{
    [Fact(Skip = "This test must be executed manually w/ manual Redis start/stop")]
    public async Task WatchConnectionTest()
    {
        var hash = GetRedisDb().GetHash("watch-connection");
        while (true) {
            try {
                await hash.Clear();
                for (var i = 0;; i++) {
                    var value = await hash.Increment("x");
                    WriteLine(value.ToString());
                    value.Should().Be(i + 1);
                    await Delay(1);
                }
            }
            catch (Exception e) {
                WriteLine($"Error: {e.GetType()}, {e.Message}");
            }
        }
    }
}
