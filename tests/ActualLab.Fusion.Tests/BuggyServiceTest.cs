using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc.Testing;

namespace ActualLab.Fusion.Tests;

public class BuggyServiceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServer<IBuggyService, BuggyService>();
        var rpc = fusion.Rpc;
        rpc.AddClient<IBuggyServiceClient>(nameof(IBuggyService));
    }

    [Fact]
    public void BasicTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        Assert.Throws<InvalidOperationException>(() => {
            _ = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
        });
    }
}
