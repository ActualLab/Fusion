using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public class RpcServerMethodResolverCacheTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private const int CacheCapacity = 16;

    protected override void StartServices(IServiceProvider services)
    { }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();

        var rpc = services.AddRpc();
        rpc.AddServer<ITestRpcService, TestRpcService>();
        rpc.AddServer<ITestRpcLegacyService, TestRpcLegacyService>();
        services.AddSingleton<RpcRegistryOptions>(_ => RpcRegistryOptions.Default with {
            LegacyMethodResolverCacheCapacity = CacheCapacity,
        });
    }

    [Fact]
    public async Task UnknownScopesCollapseTest()
    {
        await using var services = CreateServices();
        var registry = services.RpcHub().ServiceRegistry;
        registry.LegacyServerMethodResolverCacheSize.Should().Be(0);

        var versions = new VersionSet(RpcDefaults.ApiScope, "0.1");
        var expected = registry.GetServerMethodResolver(versions);
        registry.LegacyServerMethodResolverCacheSize.Should().Be(1);
        expected.MethodByRef!.Count.Should().Be(1);

        for (var i = 0; i < 2000; i++)
            registry.GetServerMethodResolver(WithUnknownScopes(versions, i))
                .Should().BeSameAs(expected);
        registry.LegacyServerMethodResolverCacheSize.Should().Be(1);

        // A set that is nothing but unknown scopes must land on the same entry the empty set does
        var empty = registry.GetServerMethodResolver(VersionSet.Empty);
        registry.LegacyServerMethodResolverCacheSize.Should().Be(2);
        for (var i = 0; i < 2000; i++)
            registry.GetServerMethodResolver(WithUnknownScopes(VersionSet.Empty, i))
                .Should().BeSameAs(empty);
        registry.LegacyServerMethodResolverCacheSize.Should().Be(2);
    }

    [Fact]
    public async Task CacheIsBoundedTest()
    {
        await using var services = CreateServices();
        var registry = services.RpcHub().ServiceRegistry;

        for (var i = 0; i < 40 * CacheCapacity; i++)
            registry.GetServerMethodResolver(new VersionSet(RpcDefaults.ApiScope, new Version(0, 1, i)));

        await TestExt.When(
            () => registry.LegacyServerMethodResolverCacheSize.Should().BeLessThanOrEqualTo(CacheCapacity),
            TimeSpan.FromSeconds(10));
    }

    // Private methods

    private static VersionSet WithUnknownScopes(VersionSet versions, int seed)
    {
        var items = versions.Items
            .Select(kv => (kv.Key, kv.Value))
            .Concat(Enumerable.Range(0, 4).Select(i => ($"Unknown{seed}_{i}", new Version(seed, i))))
            .ToArray();
        return new VersionSet(items);
    }
}
