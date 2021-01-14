using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.Bridge;
using Stl.Fusion.Tests.Services;
using Stl.Testing;
using Stl.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Fusion.Tests
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class ReplicationTest : FusionTestBase, IAsyncLifetime
    {
        public ReplicationTest(ITestOutputHelper @out) : base(@out) { }

        [Fact(Timeout = 120_000)]
        public async Task BasicTest()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var sp = Services.GetRequiredService<ISimplestProvider>();

            sp.SetValue("");
            var p1 = await Publisher.PublishAsync(_ => sp.GetValueAsync());
            p1.Should().NotBeNull();

            var r1 = ClientReplicator.GetOrAdd<string>(p1.Ref, true);
            var r1c = await r1.Computed.UpdateAsync(false);
            r1c.IsConsistent().Should().BeTrue();
            r1c.Value.Should().Be("");
            r1.Computed.Should().Be(r1c);

            sp.SetValue("1");
            await Task.Delay(100);
            r1c.IsConsistent().Should().BeFalse();
            r1.Computed.Should().Be(r1c);

            r1c = await r1c.UpdateAsync(false);
            r1c.Value.Should().Be("1");

            var r1c1 = await r1c.UpdateAsync(false);
            r1c1.Should().Be(r1c);
        }

        [Fact(Timeout = 120_000)]
        public async Task TimerTest()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var tp = Services.GetRequiredService<ITimeService>();

            var pub = await Publisher.PublishAsync(_ => tp.GetTimeAsync());
            var rep = ClientReplicator.GetOrAdd<DateTime>(pub.Ref);

            var count = 0;
            using var state = StateFactory.NewLive<DateTime>(
                o => o.WithInstantUpdates(),
                async (_, ct) => await rep.Computed.UseAsync(ct));
            state.Updated += (s, _) => {
                Out.WriteLine($"{s.Value}");
                count++;
            };

            await TestEx.WhenMetAsync(
                () => count.Should().BeGreaterThan(2),
                TimeSpan.FromSeconds(5));
        }
    }
}
