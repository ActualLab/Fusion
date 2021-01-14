using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.Tests.UIModels;
using Stl.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Fusion.Tests
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class ServerTimeModelTest : FusionTestBase
    {
        public ServerTimeModelTest(ITestOutputHelper @out, FusionTestOptions? options = null) : base(@out, options) { }

        [Fact]
        public async Task ServerTimeModelTest1()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            using var stm = ClientServices.GetRequiredService<ILiveState<ServerTimeModel1>>();

            var c = stm.Computed;
            c.IsConsistent().Should().BeFalse();
            c.Value.Time.Should().Be(default);

            Debug.WriteLine("0");
            stm.CancelUpdateDelay();
            Debug.WriteLine("1");
            await c.UpdateAsync(false);
            Debug.WriteLine("2");

            c = stm.Computed;
            c.IsConsistent().Should().BeTrue();
            (DateTime.Now - c.Value.Time).Should().BeLessThan(TimeSpan.FromSeconds(1));

            Debug.WriteLine("3");
            await Task.Delay(TimeSpan.FromSeconds(3));
            Debug.WriteLine("4");
            stm.CancelUpdateDelay();
            Debug.WriteLine("5");
            await Task.Delay(300); // Let's just wait for the updates to happen
            Debug.WriteLine("6");
            c = stm.Computed;
            Debug.WriteLine("7");

            // c.IsConsistent.Should().BeTrue(); // Hard to be sure here
            var delta = DateTime.Now - c.Value.Time;
            Debug.WriteLine(delta.TotalSeconds);
            delta.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task ServerTimeModelTest2()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            using var stm = ClientServices.GetRequiredService<ILiveState<ServerTimeModel2>>();

            var c = stm.Computed;
            c.IsConsistent().Should().BeFalse();
            c.Value.Time.Should().Be(default);

            Debug.WriteLine("0");
            stm.CancelUpdateDelay();
            Debug.WriteLine("1");
            await c.UpdateAsync(false);
            Debug.WriteLine("2");

            c = stm.Computed;
            c.IsConsistent().Should().BeTrue();
            (DateTime.Now - c.Value.Time).Should().BeLessThan(TimeSpan.FromSeconds(1));

            Debug.WriteLine("3");
            await Task.Delay(TimeSpan.FromSeconds(3));
            Debug.WriteLine("4");
            stm.CancelUpdateDelay();
            Debug.WriteLine("5");
            await Task.Delay(300); // Let's just wait for the updates to happen
            Debug.WriteLine("6");
            c = stm.Computed;
            Debug.WriteLine("7");

            // c.IsConsistent.Should().BeTrue(); // Hard to be sure here
            var delta = DateTime.Now - c.Value.Time;
            Debug.WriteLine(delta.TotalSeconds);
            delta.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
    }
}
