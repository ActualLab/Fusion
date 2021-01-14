using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.CommandR;
using Stl.Fusion.Tests.Services;
using Stl.Fusion.Tests.UIModels;
using Stl.Testing;
using Stl.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Fusion.Tests
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class KeyValueModelTest : FusionTestBase
    {
        public KeyValueModelTest(ITestOutputHelper @out, FusionTestOptions? options = null) : base(@out, options) { }

        [Fact]
        public async Task BasicTest()
        {
            var kv = Services.GetRequiredService<IKeyValueService<string>>();
            (await kv.TryGetAsync("")).Should().Be(Option.None<string>());
            (await kv.GetAsync("")).Should().BeNull();
            await kv.SetAsync("", "1");
            (await kv.TryGetAsync("")).Should().Be(Option.Some("1"));
            (await kv.GetAsync("")).Should().Be("1");

            await using var serving = await WebSocketHost.ServeAsync();
            using var kvm = ClientServices.GetRequiredService<ILiveState<KeyValueModel<string>>>();
            var kvc = ClientServices.GetRequiredService<IKeyValueServiceClient<string>>();

            // First read
            var c = kvm.Computed;
            c.IsConsistent().Should().BeFalse();
            c.Value.Key.Should().Be("");
            c.Value.Value.Should().BeNull();
            c.Value.UpdateCount.Should().Be(0);

            await TestEx.WhenMetAsync(() => {
                var snapshot = kvm.Snapshot;
                snapshot.Computed.HasValue.Should().BeTrue();
                var c = snapshot.Computed;
                c.IsConsistent().Should().BeTrue();
                c.Value.Key.Should().Be("");
                c.Value.Value.Should().Be("1");
                c.Value.UpdateCount.Should().Be(1);
            }, TimeSpan.FromSeconds(1));

            // Update
            await kvc.SetAsync(kvm.Computed.Value.Key, "2");
            await Task.Delay(300);
            c = kvm.Computed;
            c.IsConsistent().Should().BeFalse();
            c.Value.Value.Should().Be("1");
            c.Value.UpdateCount.Should().Be(1);

            await Task.Delay(1000);
            c = kvm.Computed;
            c.IsConsistent().Should().BeTrue();
            c.Value.Value.Should().Be("2");
            c.Value.UpdateCount.Should().Be(2);
        }

        [Fact]
        public async Task CommandTest()
        {
            // Server commands
            var kv = Services.GetRequiredService<IKeyValueService<string>>();
            (await kv.GetAsync("")).Should().BeNull();

            await kv.SetCommandAsync(new IKeyValueService<string>.SetCommand("", "1"));
            (await kv.GetAsync("")).Should().Be("1");

            await Services.Commander().CallAsync(new IKeyValueService<string>.SetCommand("", "2"));
            (await kv.GetAsync("")).Should().Be("2");

            // Client commands
            await using var serving = await WebSocketHost.ServeAsync();
            var kvc = ClientServices.GetRequiredService<IKeyValueServiceClient<string>>();

            await kvc.SetCommandAsync(new IKeyValueService<string>.SetCommand("", "1"));
            await Task.Delay(100); // Remote invalidation takes some time
            (await kvc.GetAsync("")).Should().Be("1");

            await ClientServices.Commander().CallAsync(new IKeyValueService<string>.SetCommand("", "2"));
            await Task.Delay(100); // Remote invalidation takes some time
            (await kvc.GetAsync("")).Should().Be("2");
        }

        [Fact]
        public async Task ExceptionTest()
        {
            var kv = Services.GetRequiredService<IKeyValueService<string>>();
            await using var serving = await WebSocketHost.ServeAsync();
            var kvc = ClientServices.GetRequiredService<IKeyValueServiceClient<string>>();

            try {
                await kvc.GetAsync("error");
            }
            catch (ApplicationException ae) {
                ae.Message.Should().Be("Error!");
            }
        }
    }
}
