using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcOutboundMessageWhenSerializedTest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcServiceClient>(nameof(ITestRpcService));
            commander.AddService<ITestRpcServiceClient>();
            rpc.AddClient<ITestRpcBackend>();
            commander.AddService<ITestRpcBackend>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
            rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
            commander.AddService<TestRpcBackend>();
        }
    }

    [Fact]
    public async Task WhenSerialized_IsNullableBasedOnCtorFlag()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);

        var serviceDef = services.RpcHub().ServiceRegistry.Get<ITestRpcServiceClient>()!;
        var methodDef = serviceDef.Methods.Single(m => m.MethodInfo.Name == nameof(ITestRpcService.Div));
        var context = new RpcOutboundContext(peer);

        var tracked = new RpcOutboundMessage(
            context,
            methodDef,
            relatedId: 1,
            needsPolymorphism: false,
            tracksSerialization: true,
            headers: null);
        tracked.WhenSerialized.Should().NotBeNull();
        tracked.CompleteWhenSerialized();
        await tracked.WhenSerialized!;

        var untracked = new RpcOutboundMessage(
            context,
            methodDef,
            relatedId: 2,
            needsPolymorphism: false,
            tracksSerialization: false,
            headers: null,
            argumentData: new byte[] { 1 });
        untracked.WhenSerialized.Should().BeNull();
    }
}
