using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcInboundCallDuplicateIdTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private const long CallId = 1_000_000;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task DuplicateCallIdsDontLeakLinkedSources()
    {
        await using var services = CreateServices();
        var peer = GetServerPeer(services);
        var delayMethodDef = GetMethodDef(peer, "Delay");
        using var peerChangedCts = new CancellationTokenSource();
        var peerChangedToken = peerChangedCts.Token;

        var winner = NewCall(peer, delayMethodDef, LongDelayArguments(), peerChangedToken);
        var winnerTask = winner.Process(CancellationToken.None);
        peer.InboundCalls.Get(CallId).Should().BeSameAs(winner);

        var duplicates = new List<RpcInboundCall>();
        var duplicateTasks = new List<Task>();
        for (var i = 0; i < 100; i++) {
            var duplicate = NewCall(peer, delayMethodDef, LongDelayArguments(), peerChangedToken);
            duplicates.Add(duplicate);
            duplicateTasks.Add(duplicate.Process(CancellationToken.None));
        }

        peer.InboundCalls.Count.Should().Be(1);
        peer.InboundCalls.Get(CallId).Should().BeSameAs(winner);
        duplicates.Should().AllSatisfy(x => x.ResultTask.Should().BeNull());

        // Cancelling the peer-change token cancels every source still linked to it,
        // so an undisposed duplicate source is directly observable here
        peerChangedCts.Cancel();
        winner.CallCancelToken.IsCancellationRequested.Should().BeTrue();
        duplicates.Should().AllSatisfy(x => x.CallCancelToken.IsCancellationRequested.Should().BeFalse());

        await winnerTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(duplicateTasks).WaitAsync(TimeSpan.FromSeconds(5));
        peer.InboundCalls.Get(CallId).Should().BeNull();
    }

    [Fact]
    public async Task DuplicateCallIdsResolveToTheRegisteredCall()
    {
        await using var services = CreateServices();
        var peer = GetServerPeer(services);
        var delayMethodDef = GetMethodDef(peer, "Delay");
        using var peerChangedCts = new CancellationTokenSource();
        var peerChangedToken = peerChangedCts.Token;

        var duration = TimeSpan.FromSeconds(0.5);
        var winner = NewCall(peer, delayMethodDef,
            ArgumentList.New(duration, CancellationToken.None), peerChangedToken);
        var winnerTask = winner.Process(CancellationToken.None);

        var duplicateTasks = new List<Task>();
        for (var i = 0; i < 5; i++) {
            var duplicate = NewCall(peer, delayMethodDef,
                ArgumentList.New(duration, CancellationToken.None), peerChangedToken);
            duplicateTasks.Add(duplicate.Process(CancellationToken.None));
            duplicate.ResultTask.Should().BeNull(); // The duplicate must not invoke the method itself
        }
        duplicateTasks.Should().AllSatisfy(x => x.IsCompleted.Should().BeFalse());

        await winnerTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(duplicateTasks).WaitAsync(TimeSpan.FromSeconds(5));
        (await (Task<TimeSpan>)winner.ResultTask!).Should().Be(duration);
        peer.InboundCalls.Get(CallId).Should().BeNull();
    }

    [Fact]
    public async Task DuplicateCallIdWithAnotherMethodIsRejected()
    {
        await using var services = CreateServices();
        var peer = GetServerPeer(services);
        var delayMethodDef = GetMethodDef(peer, "Delay");
        using var peerChangedCts = new CancellationTokenSource();
        var peerChangedToken = peerChangedCts.Token;

        var winner = NewCall(peer, delayMethodDef, LongDelayArguments(), peerChangedToken);
        var winnerTask = winner.Process(CancellationToken.None);

        var otherMethodCall = NewCall(peer,
            GetMethodDef(peer, nameof(ITestRpcService.GetVersion)), ArgumentList.Empty, peerChangedToken);
        var otherCallTypeCall = NewCall(peer,
            delayMethodDef, LongDelayArguments(), peerChangedToken, RpcCallTypeIds.Compute);
        foreach (var call in new[] { otherMethodCall, otherCallTypeCall }) {
            Action process = () => _ = call.Process(CancellationToken.None);
            process.Should().Throw<RpcException>()
                .WithMessage($"*#{CallId}*");
        }

        peer.InboundCalls.Count.Should().Be(1);
        peer.InboundCalls.Get(CallId).Should().BeSameAs(winner);

        peerChangedCts.Cancel();
        otherMethodCall.CallCancelToken.IsCancellationRequested.Should().BeFalse();
        otherCallTypeCall.CallCancelToken.IsCancellationRequested.Should().BeFalse();
        await winnerTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DuplicateNoWaitCallIdsAreProcessedIndependently()
    {
        await using var services = CreateServices();
        var peer = GetServerPeer(services);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var methodDef = GetMethodDef(peer, nameof(ITestRpcService.MaybeSet));
        methodDef.NoWait.Should().BeTrue();
        using var peerChangedCts = new CancellationTokenSource();
        var peerChangedToken = peerChangedCts.Token;

        var key = Guid.NewGuid().ToString();
        var call1 = NewCall(peer, methodDef, ArgumentList.New(key, "v1"), peerChangedToken);
        var call2 = NewCall(peer, methodDef, ArgumentList.New(key, "v2"), peerChangedToken);
        await call1.Process(CancellationToken.None);
        await call2.Process(CancellationToken.None);

        call1.Id.Should().Be(0);
        call2.CallCancelToken.Should().Be(peerChangedToken);
        peer.InboundCalls.Count.Should().Be(0);
        (await client.Get(key)).Should().Be("v2");
    }

    // Private methods

    private static RpcPeer GetServerPeer(IServiceProvider services)
        => services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ServerPeer;

    private static RpcMethodDef GetMethodDef(RpcPeer peer, string methodName)
        => peer.Hub.ServiceRegistry[typeof(ITestRpcService)][typeof(ITestRpcService).GetMethod(methodName)!];

    private static ArgumentList LongDelayArguments()
        => ArgumentList.New(TimeSpan.FromSeconds(30), CancellationToken.None);

    private static RpcInboundCall NewCall(
        RpcPeer peer,
        RpcMethodDef methodDef,
        ArgumentList arguments,
        CancellationToken peerChangedToken,
        byte? callTypeId = null)
    {
        var message = new RpcInboundMessage(
            callTypeId ?? methodDef.CallType.Id, CallId, methodDef.Ref, default, null) {
            Arguments = arguments, // Prevents argument deserialization
        };
        return new RpcInboundContext(peer, message, peerChangedToken).Call;
    }
}
