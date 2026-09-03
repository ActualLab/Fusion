using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcDelayActionTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddRpc().AddServerAndClient<ITestDelayActionService, TestDelayActionService>();
        services.AddSingleton(_ => new RpcLimits(useDebugDefaults: false) {
            CallTimeoutCheckPeriod = TimeSpan.FromMilliseconds(20),
        });
    }

    [Fact]
    public async Task AttributeTest()
    {
        await using var services = CreateServices();
        var serviceDef = services.RpcHub().ServiceRegistry[typeof(ITestDelayActionService)];

        serviceDef["AbortOnDelay:2"].Attribute!.DelayAction.Should().Be(RpcDelayedCallAction.LogAndAbort);
        serviceDef["IgnoreDelay:2"].Attribute!.DelayAction.Should().Be(RpcDelayedCallAction.None);
        serviceDef["DefaultDelay:2"].Attribute.Should().BeNull();
    }

    [Fact]
    public async Task DelayHandlerTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = hub.GetClient<ITestDelayActionService>();
        var delayHandler = services.GetRequiredService<RpcOutboundCallOptions>().DelayHandler;

        (await GetDelayAction("IgnoreDelay:2", client.IgnoreDelay)).Should().Be(RpcDelayedCallAction.None);
        (await GetDelayAction("DefaultDelay:2", client.DefaultDelay)).Should().Be(RpcDelayedCallAction.Log);
        return;

        async Task<RpcDelayedCallAction> GetDelayAction(
            string methodName,
            Func<TimeSpan, CancellationToken, Task<TimeSpan>> method) {
            using var cts = new CancellationTokenSource();
            var callTask = method.Invoke(TimeSpan.FromSeconds(5), cts.Token);
            var action = delayHandler.Invoke(await WaitForCall(clientPeer, methodName), clientPeer);
            cts.Cancel();
            await callTask.SilentAwait();
            return action;
        }
    }

    [Fact]
    public async Task AbortOnDelayTest()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestDelayActionService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up — ensures connection is ready

        var timeout = await Assert.ThrowsAsync<RpcTimeoutException>(
            () => client.AbortOnDelay(TimeSpan.FromSeconds(5)));
        timeout.TimeoutKind.Should().Be(RpcTimeoutKind.Delay);
    }

    // Private methods

    private static async Task<RpcOutboundCall> WaitForCall(RpcPeer peer, string methodName)
    {
        for (var i = 0; i < 200; i++) {
            foreach (var call in peer.OutboundCalls)
                if (string.Equals(call.MethodDef.Name, methodName, StringComparison.Ordinal))
                    return call;

            await Task.Delay(10);
        }

        throw new TimeoutException($"'{methodName}' call wasn't registered in time.");
    }
}

public interface ITestDelayActionService : IRpcService
{
    [RpcMethod(DelayTimeout = 0.2, DelayAction = RpcDelayedCallAction.LogAndAbort)]
    public Task<TimeSpan> AbortOnDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    [RpcMethod(DelayTimeout = 0.2, DelayAction = RpcDelayedCallAction.None)]
    public Task<TimeSpan> IgnoreDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    public Task<TimeSpan> DefaultDelay(TimeSpan duration, CancellationToken cancellationToken = default);
}

public class TestDelayActionService : ITestDelayActionService
{
    public async Task<TimeSpan> AbortOnDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public async Task<TimeSpan> IgnoreDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public async Task<TimeSpan> DefaultDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }
}
