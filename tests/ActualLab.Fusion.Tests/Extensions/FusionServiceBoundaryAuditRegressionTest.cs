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

        // The host is dropped rather than the whole URL - what's left addresses this site only
        result.Url.Should().NotContain("attacker.example");
        result.Url.Should().Be("/path");
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
        authentication.ChallengeProperties!.RedirectUri.Should().NotContain("attacker.example");
        authentication.ChallengeProperties.RedirectUri.Should().Be("/sign-in");

        await endpoint.SignOut(context, "audit", "https://attacker.example/sign-out");
        authentication.SignOutProperties!.RedirectUri.Should().NotContain("attacker.example");
        authentication.SignOutProperties.RedirectUri.Should().Be("/sign-out");
    }

    [Fact]
    public async Task RedirectUrlHelperShouldBeReplaceableForAllEndpointFamilies()
    {
        var authentication = new RecordingAuthenticationService();
        var urlHelper = new CountingRedirectUrlHelper {
            AllowedHosts = ["allowed.example"],
            MustStripHost = false,
        };
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authentication);
        services.AddFusion().AddWebServer().AddAuthEndpoints();
        services.AddSingleton<RedirectUrlHelper>(urlHelper);
        using var serviceProvider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        const string externalUrl = "https://allowed.example/path";

        var renderModeResult = await serviceProvider.GetRequiredService<RenderModeEndpoint>()
            .Invoke(context, null, externalUrl);
        await serviceProvider.GetRequiredService<AuthEndpoints>()
            .SignIn(context, "audit", externalUrl);

        renderModeResult.Url.Should().Be(externalUrl);
        authentication.ChallengeProperties!.RedirectUri.Should().Be(externalUrl);
        urlHelper.CheckCallCount.Should().Be(2);
    }

    [Fact]
    public async Task DefaultRedirectUrlHelperFactoryShouldReplaceEarlierRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<RedirectUrlHelper>(new RedirectUrlHelper {
            AllowedHosts = ["attacker.example"],
            MustStripHost = false,
        });
        services.AddFusion().AddWebServer();
        using var serviceProvider = services.BuildServiceProvider();

        var result = await serviceProvider.GetRequiredService<RenderModeEndpoint>()
            .Invoke(new DefaultHttpContext(), null, "https://attacker.example/path");

        // The default helper won, so the host is dropped rather than honoured
        result.Url.Should().Be("/path");
    }

    [Fact]
    public void DefaultRedirectUrlHelperShouldNeverLeaveThisSite()
    {
        var urlHelper = new RedirectUrlHelper();
        // Stripping alone isn't enough: both of these reduce to something still not local
        urlHelper.Normalize("http://evil.example//attacker.example", "/fallback").Should().Be("/fallback");
        urlHelper.Normalize("javascript:alert(1)", "/fallback").Should().Be("/fallback");
        // Protocol-relative: no scheme to parse, so it stays relative and IsLocalUrl rejects it
        urlHelper.Normalize("//evil.example/path", "/fallback").Should().Be("/fallback");
        urlHelper.Normalize("https://evil.example@localhost/path", "/fallback").Should().Be("/path");
        urlHelper.Normalize("https://localhost:5005/auth?a=1#f", "/fallback").Should().Be("/auth?a=1#f");
        urlHelper.Normalize(null, "/fallback").Should().Be("/fallback");
        // Relative URLs pass through untouched, including the "~/" form
        urlHelper.Normalize("~/foo", "/fallback").Should().Be("~/foo");
        urlHelper.Normalize("/foo?a=1#f", "/fallback").Should().Be("/foo?a=1#f");
    }

    [Theory]
    [InlineData("/chat")]
    [InlineData("/chat?x=1#f")]
    [InlineData("/a/b/c?q=%20&r=1#frag")]
    public void RedirectUrlHelperShouldNotParseALocalUrlAsAFilePath(string url)
    {
        // Uri.TryCreate(url, UriKind.Absolute) is OS-dependent: on Unix a leading '/' is a valid
        // file path, so these used to parse as file:///... and come back with '?' and '#'
        // percent-encoded into the path - silently destroying the query and fragment on Linux
        // while working on Windows. Kept as a Theory so each mangling shows up on its own.
        var urlHelper = new RedirectUrlHelper();
        urlHelper.Check(url).Should().BeTrue();
        urlHelper.Normalize(url, "/fallback").Should().Be(url);
    }

    [Fact]
    public void RedirectUrlHelperShouldRejectAForeignHostWhenTheHostIsKept()
    {
        // MustStripHost = false without an allowlist would be an open redirect, so it rejects
        var urlHelper = new RedirectUrlHelper { MustStripHost = false };
        urlHelper.Normalize("https://evil.example/path", "/fallback").Should().Be("/fallback");
        urlHelper.Normalize("/foo", "/fallback").Should().Be("/foo");
    }

    // Nested types

    private sealed class CountingRedirectUrlHelper : RedirectUrlHelper
    {
        private int _checkCallCount;
        public int CheckCallCount => Volatile.Read(ref _checkCallCount);

        public override bool Check(string? url)
        {
            Interlocked.Increment(ref _checkCallCount);
            return base.Check(url);
        }
    }

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
