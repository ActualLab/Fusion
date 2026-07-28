#if !NETFRAMEWORK
using System.Globalization;
using System.Security.Claims;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Authentication.Endpoints;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Extensions.Services;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Endpoints;
using ActualLab.Reflection;
using ActualLab.Rpc;
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
    public void KeyValueStoreShouldNotBeReachableFromFrontendPeers()
    {
        var services = new ServiceCollection();
        var fusion = services.AddFusion().WithServiceMode(RpcServiceMode.Server, true);
        fusion.AddInMemoryKeyValueStore();
        fusion.AddSandboxedKeyValueStore<Unit>();
        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.RpcHub().ServiceRegistry;

        registry.Get<IKeyValueStore>().Should().BeNull();
        var storeMethodPrefix = typeof(IKeyValueStore).GetName() + ".";
        registry.ServerMethodResolver.MethodByFullName!.Keys
            .Should().NotContain(x => x.StartsWith(storeMethodPrefix, StringComparison.Ordinal));

        var sandboxedServiceDef = registry.Get<ISandboxedKeyValueStore>();
        sandboxedServiceDef.Should().NotBeNull();
        sandboxedServiceDef!.IsBackend.Should().BeFalse();
        sandboxedServiceDef.Methods.Should().NotBeEmpty();
        foreach (var methodDef in sandboxedServiceDef.Methods) {
            methodDef.IsBackend.Should().BeFalse();
            registry.ServerMethodResolver[methodDef.FullName].Should().BeSameAs(methodDef);
        }
    }

    [Fact]
    public void ExplicitlyExposedKeyValueStoreShouldStillBeBackendOnly()
    {
        var services = new ServiceCollection();
        var fusion = services.AddFusion();
        services.AddSingleton(_ => InMemoryKeyValueStore.Options.Default);
        fusion.AddService<IKeyValueStore, InMemoryKeyValueStore>(RpcServiceMode.Server);
        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.RpcHub().ServiceRegistry;

        var serviceDef = registry.Get<IKeyValueStore>();
        serviceDef.Should().NotBeNull();
        serviceDef!.IsBackend.Should().BeTrue();
        serviceDef.Methods.Should().NotBeEmpty();
        serviceDef.Methods.Should().OnlyContain(x => x.IsBackend);
    }

    [Fact]
    public void KeyValueStoreWriteCommandsShouldBeBackendOnly()
    {
        RpcMethodDef.IsCommandType(typeof(KeyValueStore_Set), out var isBackendCommand).Should().BeTrue();
        isBackendCommand.Should().BeTrue();

        var services = new ServiceCollection();
        services.AddFusion(RpcServiceMode.Server).AddService<IKeyValueStore, InMemoryKeyValueStore>();
        using var serviceProvider = services.BuildServiceProvider();
        var serviceDef = serviceProvider.RpcHub().ServiceRegistry[typeof(IKeyValueStore)];

        serviceDef["Set:2"].IsBackend.Should().BeTrue();
        serviceDef["Remove:2"].IsBackend.Should().BeTrue();
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
