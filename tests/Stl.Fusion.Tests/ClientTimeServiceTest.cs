using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.Tests.Services;
using Stl.Testing;
using Stl.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Fusion.Tests
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class ClientTimeServiceTest : FusionTestBase
    {
        public ClientTimeServiceTest(ITestOutputHelper @out, FusionTestOptions? options = null) : base(@out, options) { }

        [Fact]
        public async Task Test1()
        {
            var epsilon = TimeSpan.FromSeconds(0.5);

            await using var serving = await WebSocketHost.ServeAsync();
            var client = ClientServices.GetRequiredService<IClientTimeService>();
            var cTime = await Computed.CaptureAsync(_ => client.GetTimeAsync(default));

            cTime.Options.AutoInvalidateTime.Should().Be(ComputedOptions.Default.AutoInvalidateTime);
            if (!cTime.IsConsistent()) {
                cTime = await cTime.UpdateAsync(false);
                cTime.IsConsistent().Should().BeTrue();
            }
            (DateTime.Now - cTime.Value).Should().BeLessThan(epsilon);

            await TestEx.WhenMetAsync(
                () => cTime.IsConsistent().Should().BeFalse(),
                TimeSpan.FromSeconds(5));
            var time = await cTime.UseAsync();
            (DateTime.Now - time).Should().BeLessThan(epsilon);
        }

        [Fact]
        public async Task Test2()
        {
            var epsilon = TimeSpan.FromSeconds(0.5);
            if (TestRunnerInfo.IsBuildAgent())
                epsilon *= 2;

            await using var serving = await WebSocketHost.ServeAsync();
            var service = ClientServices.GetRequiredService<IClientTimeService>();

            for (int i = 0; i < 20; i++) {
                var time = await service.GetTimeAsync();
                (DateTime.Now - time).Should().BeLessThan(epsilon);
                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }
        }

        [Fact]
        public async Task TestFormattedTime()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var service = ClientServices.GetRequiredService<IClientTimeService>();

            (await service.GetFormattedTimeAsync("")).Should().Be("");
            (await service.GetFormattedTimeAsync("null")).Should().Be("");

            var format = "{0}";
            var matchCount = 0;
            for (int i = 0; i < 20; i++) {
                var time = await service.GetTimeAsync();
                var formatted = await service.GetFormattedTimeAsync(format);
                var expected = string.Format(format, time);
                if (formatted == expected)
                    matchCount++;
                await Task.Delay(TimeSpan.FromSeconds(0.1));
            }
            matchCount.Should().BeGreaterThan(2);
        }
    }
}
