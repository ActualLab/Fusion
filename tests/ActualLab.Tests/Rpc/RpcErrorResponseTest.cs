using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.Reflection;
using ActualLab.Resilience;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Testing;
using ActualLab.Serialization;
using ActualLab.Versioning;

namespace ActualLab.Tests.Rpc;

// UnmatchedErrorIsNotResolvedTest asserts on the process-wide TypeRef.ResolveCache,
// so this class shares the non-parallel collection with TypeRefTest

[Collection(nameof(Tests.Reflection.TypeRefCollection))]
public class RpcErrorResponseTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    // A resolvable exception type that counts its own construction, so a test can tell
    // whether the $sys.Error payload was turned into an exception at all

#pragma warning disable RCS1194
    public class ProbeException : Exception
#pragma warning restore RCS1194
    {
        private static int _constructionCount;

        public static int ConstructionCount => Volatile.Read(ref _constructionCount);

        public ProbeException(string? message)
            : base(message)
            => Interlocked.Increment(ref _constructionCount);
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task ErrorRoundTripTest()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRpcService>();

        (await client.Div(6, 2)).Should().Be(3);
        await Assert.ThrowsAsync<DivideByZeroException>(() => client.Div(1, 0));
    }

    [Theory]
    [InlineData(typeof(RpcException))]
    [InlineData(typeof(RpcRerouteException))]
    [InlineData(typeof(RpcReconnectFailedException))]
    [InlineData(typeof(RpcStreamNotFoundException))]
    [InlineData(typeof(RpcResourceLimitExceededException))]
    [InlineData(typeof(RpcSerializationFormatException))]
    [InlineData(typeof(InternalError))]
    [InlineData(typeof(TransientException))]
    [InlineData(typeof(TerminalException))]
    [InlineData(typeof(RetryLimitExceededException))]
    [InlineData(typeof(VersionMismatchException))]
    [InlineData(typeof(ProbeException))]
    public void FusionExceptionTypesRoundTripTest(Type exceptionType)
    {
        var typeRef = new TypeRef(exceptionType).WithoutAssemblyVersions();
        var exception = new ExceptionInfo(typeRef, "Test").ToException();
        exception.Should().BeOfType(exceptionType);
    }

    [Fact]
    public async Task UnmatchedErrorIsNotResolvedTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await client.Div(6, 2); // Makes sure the connection is fully established

        var constructionCount = ProbeException.ConstructionCount;
        var resolveCacheSize = TypeRef.ResolveCacheSize;
        SendError(services, connection.ServerPeer, 1_000_000, ToExceptionInfo<ProbeException>());

        (await client.Div(6, 2)).Should().Be(3); // The $sys.Error above is processed by now
        ProbeException.ConstructionCount.Should().Be(constructionCount);
        TypeRef.ResolveCacheSize.Should().Be(resolveCacheSize);
    }

    [Fact]
    public async Task EmptyErrorCompletesTheCallTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var callTask = client.Delay(TimeSpan.FromMinutes(10));
        var call = await WaitForOutboundCall(clientPeer);
        SendError(services, connection.ServerPeer, call.Id, ExceptionInfo.None);

        await Assert.ThrowsAsync<RpcException>(() => callTask);
    }

    // Private methods

    private static ExceptionInfo ToExceptionInfo<TException>()
        => new(new TypeRef(typeof(TException)).WithoutAssemblyVersions(), "Test");

    private static void SendError(IServiceProvider services, RpcPeer peer, long callId, ExceptionInfo error)
    {
        var sender = services.GetRequiredService<RpcSystemCallSender>();
        var context = new RpcOutboundContext(peer, callId);
        var call = context.PrepareCallForSendNoWait(sender.ErrorMethodDef, ArgumentList.New(error))!;
        call.SendNoWait(needsPolymorphism: false);
    }

    private static async Task<RpcOutboundCall> WaitForOutboundCall(RpcPeer peer)
    {
        for (var i = 0; i < 200; i++) {
            foreach (var call in peer.OutboundCalls)
                return call;

            await Task.Delay(25);
        }
        throw new TimeoutException("No outbound call was registered.");
    }
}
