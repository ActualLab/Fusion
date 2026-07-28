using ActualLab.Caching;
using ActualLab.IO;
using ActualLab.Plugins;
using ActualLab.Plugins.Metadata;
using ActualLab.Reflection;
using ActualLab.Testing.Logging;

namespace ActualLab.Tests.Plugins;

public class PluginCacheTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void PluginSetInfoShouldRoundTripThroughSystemJson()
    {
        var provider = new PluginInfoProvider();
        var source = new PluginSetInfo([typeof(TestPlugin1), typeof(TestPlugin2)], provider, false);

        var data = SystemJsonSerialized.New(source).Data;
        data.Should().NotContain("$type");

        var target = SystemJsonSerialized.New<PluginSetInfo>(data).Value;
        target.InfoByType.Keys.Should().BeEquivalentTo(source.InfoByType.Keys);
        target.InfoByType[typeof(TestPlugin1)].Dependencies
            .Should().BeEquivalentTo(source.InfoByType[typeof(TestPlugin1)].Dependencies);
        target.InfoByType[typeof(TestPlugin1)].CastableTo
            .Should().BeEquivalentTo(source.InfoByType[typeof(TestPlugin1)].CastableTo);
        target.InfoByType[typeof(TestPlugin2)].Capabilities.Get<bool>("Client").Should().BeTrue();
        target.InfoByType[typeof(TestPlugin2)].Capabilities.Get<bool>("Server").Should().BeFalse();
        target.TypesByBaseTypeOrderedByDependency[typeof(ITestPlugin)]
            .Should().Equal(source.TypesByBaseTypeOrderedByDependency[typeof(ITestPlugin)]);
    }

    [Fact]
    public async Task PlantedCacheNamingUndiscoveredTypeShouldBeRejected()
    {
        var (finder, cache, logs) = CreateFinder();
        var provider = new PluginInfoProvider();
        var planted = new PluginSetInfo([typeof(NotAPlugin)], provider, false);
        await cache.Set(finder.CacheKey, SystemJsonSerialized.New(planted).Data);

        await finder.Run();

        var foundPlugins = finder.FoundPlugins!;
        foundPlugins.InfoByType.Keys.Should().NotContain((TypeRef)typeof(NotAPlugin));
        foundPlugins.InfoByType.Keys.Should().Contain((TypeRef)typeof(TestPlugin1));
        logs.Content.Should().Contain("rejected");
    }

    [Fact]
    public async Task CorruptCacheShouldDegradeToCacheMiss()
    {
        var (finder, cache, logs) = CreateFinder();
        await cache.Set(finder.CacheKey, "{ this isn't JSON");

        await finder.Run();

        finder.FoundPlugins!.InfoByType.Keys.Should().Contain((TypeRef)typeof(TestPlugin1));
        logs.Content.Should().Contain("rejected");
    }

    [Fact]
    public void CacheDirWritableByOtherUsersShouldNotBeUsed()
    {
        var (finder, _, _) = CreateFinder();
        finder.Cache.Should().BeOfType<FileSystemCache<string, string>>();

#if NET7_0_OR_GREATER
        if (OperatingSystem.IsWindows())
            return;

        var (otherFinder, _, otherLogs) = CreateFinder();
        File.SetUnixFileMode(otherFinder.CacheDir.Value,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);

        otherFinder.Cache.Should().BeOfType<EmptyCache<string, string>>();
        otherLogs.Content.Should().Contain("writable by other users");
#endif
    }

    // Private methods

    private static (ExposedPluginFinder Finder, FileSystemCache<string, string> Cache, CapturingLoggerProvider Logs)
        CreateFinder()
    {
        var cacheDir = FilePath.GetApplicationTempDirectory() & $"plugin-cache-test-{Guid.NewGuid():N}";
        Directory.CreateDirectory(cacheDir);
        var logs = new CapturingLoggerProvider();
        var loggerFactory = new LoggerFactory([logs]);
        var finder = new ExposedPluginFinder(
            new FileSystemPluginFinder.Options { CacheDir = cacheDir },
            new PluginInfoProvider(),
            loggerFactory.CreateLogger<FileSystemPluginFinder>());
        return (finder, new FileSystemCache<string, string>(cacheDir), logs);
    }

    // Nested types

    private sealed class ExposedPluginFinder(
        FileSystemPluginFinder.Options settings,
        IPluginInfoProvider pluginInfoProvider,
        ILogger<FileSystemPluginFinder>? log = null)
        : FileSystemPluginFinder(settings, pluginInfoProvider, log)
    {
        public string CacheKey => GetCacheKey();
        public FilePath CacheDir => GetCacheDir();
    }
}
