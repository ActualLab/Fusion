using System;
using System.Diagnostics;
using System.Net.WebSockets;
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
    public class WebSocketTest : FusionTestBase
    {
        public WebSocketTest(ITestOutputHelper @out, FusionTestOptions? options = null)
            : base(@out, options) { }

        [Fact]
        public async Task ConnectToPublisherTest()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var channel = await ConnectToPublisherAsync();
            channel.Writer.Complete();
        }

        [Fact]
        public async Task TimerTest()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var tp = Services.GetRequiredService<ITimeService>();

            var pub = await Publisher.PublishAsync(_ => tp.GetTimeAsync());
            var rep = ClientReplicator.GetOrAdd<DateTime>(pub.Ref);
            await rep.RequestUpdateAsync().AsAsyncFunc()
                .Should().CompleteWithinAsync(TimeSpan.FromMinutes(1));

            var count = 0;
            using var state = StateFactory.NewLive<DateTime>(
                o => o.WithInstantUpdates(),
                async (_, ct) => await rep.Computed.UseAsync(ct));
            state.Updated += (s, _) => {
                Out.WriteLine($"Client: {s.Value}");
                count++;
            };

            await TestEx.WhenMetAsync(
                () => count.Should().BeGreaterThan(2),
                TimeSpan.FromSeconds(5));
        }

        [Fact(Timeout = 120_000)]
        public async Task NoConnectionTest()
        {
            await using var serving = await WebSocketHost.ServeAsync();
            var tp = Services.GetRequiredService<ITimeService>();

            var pub = await Publisher.PublishAsync(_ => tp.GetTimeAsync());
            var rep = ClientReplicator.GetOrAdd<DateTime>(("NoPublisher", pub.Id));
            await rep.RequestUpdateAsync().AsAsyncFunc()
                .Should().ThrowAsync<WebSocketException>();
        }

        [Fact(Timeout = 120_000)]
        public async Task DropReconnectTest()
        {
            if (TestRunnerInfo.IsBuildAgent())
                // TODO: Fix intermittent failures on GitHub
                return;

            var serving = await WebSocketHost.ServeAsync();
            var tp = Services.GetRequiredService<ITimeService>();

            Debug.WriteLine("0");
            var pub = await Publisher.PublishAsync(_ => tp.GetTimeAsync());
            var rep = ClientReplicator.GetOrAdd<DateTime>(pub.Ref);
            Debug.WriteLine("1");
            await rep.RequestUpdateAsync().AsAsyncFunc()
                .Should().CompleteWithinAsync(TimeSpan.FromMinutes(1));
            Debug.WriteLine("2");
            var state = ClientReplicator.GetPublisherConnectionState(pub.Publisher.Id);
            state.Computed.IsConsistent().Should().BeTrue();
            Debug.WriteLine("3");
            await state.Computed.UpdateAsync(false);
            Debug.WriteLine("4");
            state.Should().Be(ClientReplicator.GetPublisherConnectionState(pub.Publisher.Id));
            state.Value.Should().BeTrue();

            Debug.WriteLine("WebServer: stopping.");
            await serving.DisposeAsync();
            Debug.WriteLine("WebServer: stopped.");

            // First try -- should fail w/ WebSocketException or ChannelClosedException
            Debug.WriteLine("5");
            await rep.RequestUpdateAsync().AsAsyncFunc()
                .Should().ThrowAsync<Exception>();
            Debug.WriteLine("6");
            state.Should().Be(ClientReplicator.GetPublisherConnectionState(pub.Publisher.Id));
            await state.Computed.UpdateAsync(false);
            Debug.WriteLine("7");
            state.Should().Be(ClientReplicator.GetPublisherConnectionState(pub.Publisher.Id));
            state.Error.Should().BeAssignableTo<Exception>();

            // Second try -- should fail w/ WebSocketException
            Debug.WriteLine("8");
            await rep.Computed.UpdateAsync(false).AsAsyncFunc()
                .Should().ThrowAsync<WebSocketException>();
            Debug.WriteLine("9");
            rep.UpdateError.Should().BeOfType<WebSocketException>();
            await state.Computed.UpdateAsync(false);
            Debug.WriteLine("10");
            state.Error.Should().BeOfType<WebSocketException>();

            Debug.WriteLine("WebServer: starting.");
            serving = await WebSocketHost.ServeAsync();
            await Task.Delay(1000);
            Debug.WriteLine("WebServer: started.");

            Debug.WriteLine("11");
            await rep.RequestUpdateAsync().AsAsyncFunc()
                .Should().CompleteWithinAsync(TimeSpan.FromMinutes(1));
            Debug.WriteLine("12");
            await state.Computed.UpdateAsync(false);
            Debug.WriteLine("13");
            state.Value.Should().BeTrue();

            Debug.WriteLine("100");
            await serving.DisposeAsync();
            Debug.WriteLine("101");
        }
    }
}
