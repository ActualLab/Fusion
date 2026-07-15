#if !NETFRAMEWORK
using ActualLab.Fusion.Extensions.Services;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Endpoints;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Fusion.Tests.Extensions;

public class FusionServiceBoundaryAuditRegressionTest
{
    [Fact]
    public void SandboxedStoreShouldRejectKeysFromUsersWithLongerMatchingIds()
    {
        var keyChecker = new SandboxedKeyValueStore<Unit>.KeyChecker {
            Prefix = "@session/session-id",
            SecondaryPrefix = "@user/12",
        };

        var action = () => keyChecker.CheckKey("@user/123/private");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddWebServerShouldHonorDisabledBackendExposure()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddWebServer(false);
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<RpcWebSocketServerOptions>();

        options.ExposeBackend.Should().BeFalse();
    }

    [Fact]
    public async Task RenderModeEndpointShouldNotRedirectToExternalUrls()
    {
        var endpoint = new RenderModeEndpoint();

        var result = await endpoint.Invoke(
            new DefaultHttpContext(),
            renderMode: null,
            redirectTo: "https://attacker.example/path");

        result.Url.Should().Be("~/");
    }
}
#endif
