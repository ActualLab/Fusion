using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace Stl.Tests.DependencyInjection;

public class SettingsTest
{
    public interface ITestSettings
    {
        string Value { get; set; }
    }

    public class XSettings : ITestSettings
    {
        public string Value { get; set; } = "";
    }

    public class YSettings : ITestSettings
    {
        public string Value { get; set; } = "";
    }

    public class ZCfg : ITestSettings
    {
        public string Value { get; set; } = "";
    }

    public class FSettings : ITestSettings
    {
        [Required] public string Value { get; set; } = "Hello!";
    }

    public class NoSettings : ITestSettings
    {
        [Required] public string Value { get; set; } = "";
    }

    [Fact]
    public void BasicTest()
    {
        var services = CreateServices();

        var x = services.GetRequiredService<XSettings>();
        x.Value.Should().Be("1");
        services.GetRequiredService<ITestSettings>().Should().BeSameAs(x);

        var y = services.GetRequiredService<YSettings>();
        y.Value.Should().Be("2");

        var z = services.GetRequiredService<ZCfg>();
        z.Value.Should().Be("3");

        Assert.Throws<ValidationException>(() => services.GetRequiredService<FSettings>());

        Assert.Throws<ValidationException>(() => services.GetRequiredService<NoSettings>());
    }

    private static IServiceProvider CreateServices()
    {
        static KeyValuePair<string, string> NewPair(string key, string value)
            => new(key, value);

        var cfg = new ConfigurationBuilder()
            .Add(new MemoryConfigurationSource {
                InitialData = new[] {
                    NewPair("XSettings_Manual:Value", "1"),
                    NewPair("YSettings:Value", "2"),
                    NewPair("Z:Value", "3"),
                    NewPair("F:Value", ""),
                }!
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton(cfg)
            .AddSingleton<IConfiguration>(cfg);

        services.AddSettings<XSettings>("XSettings_Manual");
        services.AddAlias<ITestSettings, XSettings>();
        services.AddSettings<YSettings>();
        services.AddSettings<ZCfg>();
        services.AddSettings<FSettings>();
        services.AddSettings<NoSettings>();

        return services.BuildServiceProvider();
    }
}
