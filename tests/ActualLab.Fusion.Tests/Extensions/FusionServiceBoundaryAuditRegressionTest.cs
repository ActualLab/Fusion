#if !NETFRAMEWORK
using System.Globalization;
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
        var settings = new SandboxedKeyValueStore<Unit>.Options();
        var sessionPrefix = string.Format(
            CultureInfo.InvariantCulture, settings.SessionKeyPrefixFormat, "session-id");
        var userPrefix = string.Format(CultureInfo.InvariantCulture, settings.UserKeyPrefixFormat, "12");
        var keyChecker = new SandboxedKeyValueStore<Unit>.KeyChecker {
            Prefix = sessionPrefix,
            SecondaryPrefix = userPrefix,
        };

        var action = () => keyChecker.CheckKey("@user/123/private");

        action.Should().Throw<InvalidOperationException>();
        keyChecker.Invoking(x => x.CheckKey(userPrefix)).Should().NotThrow();
        keyChecker.Invoking(x => x.CheckKey(userPrefix + "/private")).Should().NotThrow();
        keyChecker.Invoking(x => x.CheckKey(sessionPrefix)).Should().NotThrow();
        keyChecker.Invoking(x => x.CheckKey(sessionPrefix + "/private")).Should().NotThrow();
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
