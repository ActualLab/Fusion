using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;
using ActualLab.Testing.Logging;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcLimitsTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(1);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        commander.AddHandlers<TestRpcService>();
    }

    [Fact]
    public void DefaultLimitsAreAsExpected()
    {
        var limits = new RpcLimits(false);
        limits.CallCountLimit.Should().Be(int.MaxValue);
        limits.ObjectCountLimit.Should().Be(65536);
    }

    [Fact]
    public void LimitErrorsReportBothComponents()
    {
        RpcResourceLimitExceededException.CallCountLimitExceeded(RpcRef.Default, 7, 3, 5)
            .Message.Should().Contain("7 inbound + 3 outbound > 5");
        RpcResourceLimitExceededException.ObjectCountLimitExceeded(RpcRef.Default, 9, 2, 4)
            .Message.Should().Contain("9 shared + 2 remote > 4");
    }

    [Fact(Timeout = 30_000)]
    public async Task ObjectCountLimitResetsPeer()
    {
        const int objectCountLimit = 4;
        var logs = new CapturingLoggerProvider();
        await using var services = CreateServices(UseLimits(logs, objectCountLimit: objectCountLimit));
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var serverPeer = connection.ServerPeer;
        serverPeer.Hub.Limits.ObjectCountLimit.Should().Be(objectCountLimit);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2); // Warm-up + ensures connected

        // A returned RpcStream registers a shared stream on the server peer as soon as it's
        // serialized; never enumerating it keeps that object around till ObjectReleaseTimeout.
        for (var i = 0; i < 10; i++)
            await client.StreamInt32(3);
        serverPeer.SharedObjects.Count.Should().BeGreaterThan(objectCountLimit);

        await TestExt.When(
            () => serverPeer.SharedObjects.Count.Should().Be(0),
            TimeSpan.FromSeconds(10));

        var content = logs.Content;
        content.Should().Contain("object count limit is exceeded");
        content.Should().Contain(" shared + ");
        content.Should().Contain($" remote > {objectCountLimit}");
    }

    [Fact(Timeout = 60_000)]
    public async Task DefaultCallCountLimitNeverTrips()
    {
        var logs = new CapturingLoggerProvider();
        await using var services = CreateServices(UseLimits(logs));
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var serverPeer = connection.ServerPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2); // Warm-up + ensures connected

        var connectionState = serverPeer.ConnectionState.Value;
        var callTasks = Enumerable.Range(0, 100)
            .Select(_ => client.Delay(TimeSpan.FromSeconds(5)))
            .ToArray();
        await TestExt.When(
            () => serverPeer.InboundCalls.Count.Should().BeGreaterThan(50),
            TimeSpan.FromSeconds(10));

        // Spans several check periods with a call count far above any "sensible" finite cap
        await Task.WhenAll(callTasks);

        serverPeer.ConnectionState.Value.Should().BeSameAs(connectionState);
        logs.Content.Should().NotContain("count limit is exceeded");
    }

    // Private methods

    private static Action<IServiceCollection> UseLimits(
        CapturingLoggerProvider logs,
        int? objectCountLimit = null)
        => services => {
            services.AddLogging(logging => logging.AddProvider(logs));
            var limits = new RpcLimits(false) {
                ObjectReleasePeriod = CheckPeriod,
            };
            if (objectCountLimit is { } value)
                limits = limits with { ObjectCountLimit = value };
            services.AddSingleton(_ => limits);
        };
}
