#if !NETFRAMEWORK
using System.Globalization;
using System.Security.Claims;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Authentication.Endpoints;
using ActualLab.Fusion.Extensions.Services;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Endpoints;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Authentication;
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
        var services = new ServiceCollection();
        services.AddFusion().AddWebServer();
        using var serviceProvider = services.BuildServiceProvider();
        var endpoint = serviceProvider.GetRequiredService<RenderModeEndpoint>();

        var result = await endpoint.Invoke(
            new DefaultHttpContext(),
            renderMode: null,
            redirectTo: "https://attacker.example/path");

        result.Url.Should().Be("~/");
    }

    [Fact]
    public async Task AuthEndpointsShouldNotRedirectToExternalUrls()
    {
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authentication);
        services.AddFusion().AddWebServer().AddAuthEndpoints();
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        var endpoint = serviceProvider.GetRequiredService<AuthEndpoints>();

        await endpoint.SignIn(context, "audit", "https://attacker.example/sign-in");
        authentication.ChallengeProperties!.RedirectUri.Should().Be("/");

        await endpoint.SignOut(context, "audit", "https://attacker.example/sign-out");
        authentication.SignOutProperties!.RedirectUri.Should().Be("/");
    }

    [Fact]
    public async Task RedirectUrlCheckerShouldBeReplaceableForAllEndpointFamilies()
    {
        var authentication = new RecordingAuthenticationService();
        var callCount = 0;
        RedirectUrlChecker urlChecker = _ => {
            callCount++;
            return true;
        };
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authentication);
        services.AddFusion().AddWebServer().AddAuthEndpoints();
        services.AddSingleton(urlChecker);
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        const string externalUrl = "https://allowed.example/path";

        var renderModeResult = await serviceProvider.GetRequiredService<RenderModeEndpoint>()
            .Invoke(context, null, externalUrl);
        await serviceProvider.GetRequiredService<AuthEndpoints>()
            .SignIn(context, "audit", externalUrl);

        renderModeResult.Url.Should().Be(externalUrl);
        authentication.ChallengeProperties!.RedirectUri.Should().Be(externalUrl);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task DefaultRedirectUrlCheckerFactoryShouldReplaceEarlierRegistration()
    {
        RedirectUrlChecker urlChecker = _ => true;
        var services = new ServiceCollection();
        services.AddSingleton(urlChecker);
        services.AddFusion().AddWebServer();
        using var serviceProvider = services.BuildServiceProvider();

        var result = await serviceProvider.GetRequiredService<RenderModeEndpoint>()
            .Invoke(new DefaultHttpContext(), null, "https://attacker.example/path");

        result.Url.Should().Be("~/");
    }

    // Nested types

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public AuthenticationProperties? ChallengeProperties { get; private set; }
        public AuthenticationProperties? SignOutProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            ChallengeProperties = properties;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignOutProperties = properties;
            return Task.CompletedTask;
        }
    }
}
#endif
