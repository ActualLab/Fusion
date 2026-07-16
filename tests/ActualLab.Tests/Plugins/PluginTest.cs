using System.Reflection;
using ActualLab.Caching;
using ActualLab.IO;
using ActualLab.Plugins;
using ActualLab.Plugins.Metadata;
using ActualLab.Reflection;
using ActualLab.Testing.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests.Plugins;

public class PluginTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void PluginHostBuilderTest()
    {
        var host = new PluginHostBuilder()
            .UsePluginFilter(typeof(ITestPlugin))
            .Build();

        RunPluginHostTests(host);
    }

    [Fact]
    public async Task AbstractPluginTest()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await new PluginHostBuilder()
                .UsePlugins(false, typeof(ITestPlugin))
                .BuildAsync();
        });
    }

    [Fact]
    public void PluginFilterTest()
    {
        var host = new PluginHostBuilder()
            .UsePluginFilter(typeof(ITestPlugin))
            .Build();
        host.GetPlugins<ITestPlugin>().Count().Should().Be(2);

        host = new PluginHostBuilder()
            .UsePluginFilter(typeof(ITestPlugin))
            .UsePluginFilter(p => p.Type != typeof(TestPlugin2))
            .Build();
        host.GetPlugins<ITestPlugin>().Count().Should().Be(1);
    }

    [Fact]
    public void SingletonPluginTest()
    {
        var host = new PluginHostBuilder()
            .UsePluginFilter(typeof(ITestPlugin))
            .Build();

        host.GetPlugins<ITestPlugin>().Count().Should().Be(2);
        host.GetPlugins<ITestSingletonPlugin>().Count().Should().Be(1);
    }

    [Fact]
    public async Task CombinedTest()
    {
        var logContent = await RunCombinedTest(mustClearCache: true);
        logContent.Should().Contain("populating");

        logContent = await RunCombinedTest();
        logContent.Should().Contain("Cached plugin set info found");
    }

    [Fact]
    public void PluginFinderCacheKeyShouldIncludeResultAffectingSettings()
    {
        var provider = new PluginInfoProvider();
        var finder1 = new ExposedFileSystemPluginFinder(
            new FileSystemPluginFinder.Options { DetectIndirectAssemblyDependencies = false },
            provider);
        var finder2 = new ExposedFileSystemPluginFinder(
            new FileSystemPluginFinder.Options { DetectIndirectAssemblyDependencies = true },
            provider);

        finder1.CacheKey.Should().NotBe(finder2.CacheKey);
        finder1.CacheKey.Should().StartWith("v1:");
        finder2.CacheKey.Should().StartWith("v1:");
    }

    [Fact]
    public async Task FailedPluginHostBuildShouldDisposeItsServiceProvider()
    {
        var finder = new TrackingPluginFinder(mustFail: true);
        var builder = new PluginHostBuilder();
        builder.Services.RemoveAll<IPluginFinder>();
        builder.Services.AddSingleton<IPluginFinder>(_ => finder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync());

        finder.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task SuccessfulPluginHostBuildShouldTransferServiceProviderOwnership()
    {
        var finder = new TrackingPluginFinder(mustFail: false);
        var builder = new PluginHostBuilder();
        builder.Services.RemoveAll<IPluginFinder>();
        builder.Services.AddSingleton<IPluginFinder>(_ => finder);

        var host = await builder.BuildAsync();
        finder.DisposeCount.Should().Be(0);

        await host.DisposeAsync();
        finder.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void PluginDiscoveryShouldRetainTypesFromPartiallyLoadableAssemblies()
    {
        var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = new LoggerFactory([loggerProvider]);
        var loaderError = new FileNotFoundException("Missing dependency.");
        var assembly = new PartiallyLoadableAssembly(loaderError);
        var finder = new ExposedFileSystemPluginFinder(
            new FileSystemPluginFinder.Options(),
            new PluginInfoProvider(),
            loggerFactory.CreateLogger<FileSystemPluginFinder>());

        finder.GetLoadableTypes(assembly).Should().Equal(typeof(TestPlugin1));
        loggerProvider.Content.Should().Contain(loaderError.Message);
    }

    [Fact]
    public void MissingPluginDependenciesShouldIdentifyPluginAndDependency()
    {
        var action = () => new PluginSetInfo([typeof(WrongPlugin)], new PluginInfoProvider(), false);

        var error = action.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().Contain(nameof(WrongPlugin));
        error.Message.Should().Contain(new TypeRef(typeof(TestPlugin2)).ToString());
    }

    private PluginHostBuilder CreateHostBuilder(bool mustClearCache = false)
    {
        var stringBuilderLoggerProvider = new CapturingLoggerProvider();
        var hostBuilder = new PluginHostBuilder()
            .UsePluginFilter(typeof(ITestPlugin))
            .ConfigureServices(services => {
                services.AddSingleton(stringBuilderLoggerProvider);
                services.AddLogging(logging => {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Debug);
                    logging.AddProvider(stringBuilderLoggerProvider);
                    logging.AddProvider(
#pragma warning disable CS0618
                        new XunitTestOutputLoggerProvider(
                            new TestOutputHelperAccessor() { Output = Out },
                            (_, level) => level >= LogLevel.Debug));
#pragma warning restore CS0618
                });
            });
        if (mustClearCache) {
            var serviceProvider = hostBuilder.ServiceProviderFactory(hostBuilder.Services);
            var pluginFinder = serviceProvider.GetService<IPluginFinder>();
            if (pluginFinder is FileSystemPluginFinder fileSystemPluginFinder) {
                var cache = (FileSystemCache<string, string>) fileSystemPluginFinder.Cache;
                cache.Clear();
            }
        }
        return hostBuilder;
    }

    private async Task<string> RunCombinedTest(bool mustClearCache = false)
    {
        var hostBuilder = CreateHostBuilder(mustClearCache);
        var host = await hostBuilder.BuildAsync();
        var plugins = host.FoundPlugins;
        plugins!.InfoByType.Count.Should().Be(3);

        // Capabilities extraction
        var testPlugin1Caps = plugins.InfoByType[typeof(TestPlugin1)].Capabilities;
        testPlugin1Caps.Items.Count.Should().Be(0);
        var testPlugin2Caps = plugins.InfoByType[typeof(TestPlugin2)].Capabilities;
        testPlugin2Caps.Items.Count.Should().Be(2);
        testPlugin2Caps.Get<bool>("Client").Should().Be(true);
        testPlugin2Caps.Get<bool>("Server").Should().Be(false);

        // Dependencies extraction
        var testPlugin1Deps = plugins.InfoByType[typeof(TestPlugin1)].Dependencies;
        testPlugin1Deps.Should().BeEquivalentTo(new [] {(TypeRef)typeof(TestPlugin2)});
        var testPlugin1AllDeps = plugins.InfoByType[typeof(TestPlugin1)].AllDependencies;
        testPlugin1AllDeps.Should().Contain((TypeRef)typeof(TestPlugin2));

        var testPlugin2Deps = plugins.InfoByType[typeof(TestPlugin2)].Dependencies;
        testPlugin2Deps.Count.Should().Be(0);
        var logContent = host.Services.GetRequiredService<CapturingLoggerProvider>().Content;

        hostBuilder = CreateHostBuilder()
            .UsePlugins(false, plugins.InfoByType.Keys.Select(t => t.Resolve()));
        host = await hostBuilder.BuildAsync();

        RunPluginHostTests(host);
        return logContent;
    }

    private static void RunPluginHostTests(IPluginHost host)
    {
        // GetPlugins -- simple form (all plugins)
        var testPlugins = host.GetPlugins<ITestPlugin>().ToArray();
        testPlugins.Length.Should().Be(2);
        testPlugins.Select(p => p.GetName())
            .Should().BeEquivalentTo("TestPlugin1", "TestPlugin2");

        var testPluginsEx = host.GetPlugins<ITestPluginEx>().ToArray();
        testPluginsEx.Length.Should().Be(1);
        testPluginsEx.Select(p => p.GetVersion())
            .Should().BeEquivalentTo("1.0");

        // GetPlugins -- filtering based on capabilities
        host.GetPlugins<ITestPlugin>(
                p => Equals(null, p.Capabilities["Server"]))
            .Select(p => p.GetName())
            .Should().BeEquivalentTo("TestPlugin1");
        host.GetPlugins<ITestPlugin>(
                p => Equals(true, p.Capabilities["Client"]))
            .Select(p => p.GetName())
            .Should().BeEquivalentTo("TestPlugin2");
        host.GetPlugins<ITestPlugin>(_ => false).Count().Should().Be(0);
        host.GetPlugins<ITestPlugin>(_ => true).Count().Should().Be(2);
    }

    private sealed class ExposedFileSystemPluginFinder(
        FileSystemPluginFinder.Options settings,
        IPluginInfoProvider pluginInfoProvider,
        ILogger<FileSystemPluginFinder>? log = null)
        : FileSystemPluginFinder(settings, pluginInfoProvider, log)
    {
        public string CacheKey => GetCacheKey();
        public IEnumerable<Type> GetLoadableTypes(Assembly assembly)
            => GetExportedTypes(assembly, "partial.dll");

        protected override FilePath[] GetPluginAssemblyNames()
            => [];
    }

    private sealed class PartiallyLoadableAssembly(Exception loaderError) : Assembly
    {
        public override IEnumerable<Type> ExportedTypes
            => throw new ReflectionTypeLoadException([typeof(TestPlugin1), null!], [loaderError]);
    }

    private sealed class TrackingPluginFinder(bool mustFail) : IPluginFinder, IAsyncDisposable
    {
        public PluginSetInfo? FoundPlugins { get; private set; }
        public int DisposeCount { get; private set; }

        public Task Run(CancellationToken cancellationToken = default)
        {
            if (mustFail)
                throw new InvalidOperationException("Failure requested by test.");
            FoundPlugins = PluginSetInfo.Empty;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return default;
        }
    }
}
