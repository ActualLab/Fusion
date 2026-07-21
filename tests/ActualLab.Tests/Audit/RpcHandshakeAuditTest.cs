using System.Reflection;
using ActualLab.Channels;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using ActualLab.Tests.Rpc;

namespace ActualLab.Tests.Audit;

public class RpcHandshakeAuditTest
{
    [Fact]
    public void RpcRefNullOperatorsFollowEqualityContract()
    {
        RpcRef? left = null;
        RpcRef? right = null;

        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    [Fact]
    public void RequireBackendAcceptsOnlyBackendReferences()
    {
        var backend = RpcRef.NewClient("backend", isBackend: true);
        var frontend = RpcRef.NewClient("frontend", isBackend: false);

        backend.RequireBackend().Should().BeSameAs(backend);
        Assert.Throws<ArgumentOutOfRangeException>(() => frontend.RequireBackend());
    }

    [Fact]
    public void RpcCacheKeyRetainsCallerOwnedArgumentData()
    {
        var argumentData = new byte[] { 1, 2, 3 };
        var key = new RpcCacheKey("method", argumentData);

        MemoryMarshal.TryGetArray(key.ArgumentData, out var keyData).Should().BeTrue();
        keyData.Array.Should().BeSameAs(argumentData);
    }

    [Fact]
    public void RpcArgumentSerializerStabilizesCacheKeyStorage()
    {
        var smallBuffer = RpcArgumentSerializer.GetWriteBuffer();
        smallBuffer.Advance(1);
        var smallMemory = RpcArgumentSerializer.GetWriteBufferMemory(smallBuffer);
        MemoryMarshal.TryGetArray(smallMemory, out var smallData).Should().BeTrue();
        smallData.Array.Should().NotBeSameAs(smallBuffer.Array);

        var largeBuffer = RpcArgumentSerializer.GetWriteBuffer();
        largeBuffer.Advance(RpcArgumentSerializer.CopyThreshold + 1);
        var largeMemory = RpcArgumentSerializer.GetWriteBufferMemory(largeBuffer);
        MemoryMarshal.TryGetArray(largeMemory, out var largeData).Should().BeTrue();
        largeData.Array.Should().BeSameAs(largeBuffer.Array);

        var nextBuffer = RpcArgumentSerializer.GetWriteBuffer();
        nextBuffer.Array.Should().NotBeSameAs(largeBuffer.Array);
    }

    [Theory]
    [InlineData(0, 61)]
    [InlineData(-1, 61)]
    [InlineData(30, 0)]
    [InlineData(30, -1)]
    public void RpcStreamRejectsNonPositiveFlowControlSettings(int ackPeriod, int ackAdvance)
    {
        var create = () => new RpcStream<int> {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
        };

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 61)]
    [InlineData(30, 0)]
    public void RpcStreamRejectsNonPositiveDeserializedFlowControlSettings(int ackPeriod, int ackAdvance)
    {
        var serialized = $"{Guid.NewGuid()},1,{ackPeriod},{ackAdvance},1,0";

        var deserialize = () => RpcStream<int>.DeserializeFromString(serialized);

        deserialize.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RpcStreamRejectsBufferCapacityOverflow()
    {
        var createWithAckAdvance = () => new RpcStream<int> { AckAdvance = int.MaxValue };
        var createWithBufferSize = () => new RpcStream<int> { BufferSize = int.MaxValue };

        createWithAckAdvance.Should().Throw<ArgumentOutOfRangeException>();
        createWithBufferSize.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RpcSharedStreamSaturatesAckWindowAtLongBoundary()
    {
        var getMaxIndex = typeof(RpcSharedStream<int>).GetMethod(
            "GetMaxIndex",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var maxIndex = getMaxIndex.Invoke(null, [long.MaxValue - 1, 61]);

        maxIndex.Should().Be(long.MaxValue);
    }

    [Fact]
    public void YieldFrameDelayerHonorsHandshakeFrameCount()
    {
        var delayer = RpcFrameDelayers.Yield(handshakeFrameCount: 4)!;

        for (var i = 0; i < 4; i++)
            delayer(0).Should().BeSameAs(Task.CompletedTask);
    }

    [Fact]
    public void YieldFrameDelayerRejectsNegativeHandshakeFrameCount()
        => Assert.Throws<ArgumentOutOfRangeException>(() => RpcFrameDelayers.Yield(handshakeFrameCount: -1));

    [Fact]
    public void FrozenRpcConfigurationSnapshotsServices()
    {
        var configuration = new RpcConfiguration();
        var mutableServices = configuration.Services;

        configuration.Freeze();
        mutableServices.Add(typeof(string), null!);

        configuration.Services.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAfterCompletionReportsFailureToHandler()
    {
        var services = new ServiceCollection();
        services.AddRpc();
        await using var serviceProvider = services.BuildServiceProvider();
        var hub = serviceProvider.RpcHub();
        var peer = new RpcClientPeer(hub, RpcRef.Default.Route);
        await using var transport = new CompletedFrameTransport(peer);
        var method = hub.ServiceRegistry[typeof(IRpcSystemCalls)]["AckEnd:1"];
        var context = new RpcOutboundContext(peer) {
            Arguments = ArgumentList.New(default(Guid)),
            MethodDef = method,
        };
        var sendCount = 0;
        var sendCompleted = TaskCompletionSourceExt.New<Exception?>();
        var message = new RpcOutboundMessage(
            context,
            method,
            1,
            false,
            null,
            (_, _, error) => {
                Interlocked.Increment(ref sendCount);
                sendCompleted.TrySetResult(error);
            });

        transport.TryComplete();
        transport.Send(message);

        var handlerError = await sendCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handlerError.Should().BeOfType<ChannelClosedException>();
        sendCount.Should().Be(1);
    }

    [Fact]
    public async Task SimpleChannelSendReportsCanceledEnqueue()
    {
        var services = new ServiceCollection();
        services.AddRpc();
        await using var serviceProvider = services.BuildServiceProvider();
        var hub = serviceProvider.RpcHub();
        var peer = new RpcClientPeer(hub, RpcRef.Default.Route);
        var frameWriter = new CancelingFrameWriter();
        var channel = new CustomChannel<ArrayOwner<byte>>(
            Channel.CreateUnbounded<ArrayOwner<byte>>().Reader,
            frameWriter);
        await using var transport = new RpcSimpleChannelTransport(peer, channel);
        var method = hub.ServiceRegistry[typeof(IRpcSystemCalls)]["AckEnd:1"];
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var sendCount = 0;
        var sendCompleted = TaskCompletionSourceExt.New<Exception?>();

        try {
            transport.Send(NewMessage((_, _, error) => {
                Interlocked.Increment(ref sendCount);
                sendCompleted.TrySetResult(error);
            }), cancellationSource.Token);
            var handlerError = await sendCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            handlerError.Should().BeAssignableTo<OperationCanceledException>();
            sendCount.Should().Be(1);
            frameWriter.Frame.Should().NotBeNull();
            frameWriter.Frame!.IsDisposed.Should().BeTrue();
        }
        finally {
            transport.TryComplete();
        }
        return;

        RpcOutboundMessage NewMessage(RpcTransportSendHandler? sendHandler)
        {
            var context = new RpcOutboundContext(peer) {
                Arguments = ArgumentList.New(default(Guid)),
                MethodDef = method,
            };
            return new RpcOutboundMessage(context, method, 1, false, null, sendHandler);
        }
    }

    private sealed class CancelingFrameWriter : ChannelWriter<ArrayOwner<byte>>
    {
        public ArrayOwner<byte>? Frame { get; private set; }

        public override bool TryComplete(Exception? error = null)
            => true;

        public override bool TryWrite(ArrayOwner<byte> item)
        {
            Frame = item;
            return false;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
            => new(Task.FromCanceled<bool>(cancellationToken));

        public override ValueTask WriteAsync(ArrayOwner<byte> item, CancellationToken cancellationToken = default)
        {
            Frame = item;
            return new ValueTask(Task.FromCanceled(cancellationToken));
        }
    }

    [Fact]
    public async Task FirstNonHandshakeMessageMustNotBeDispatched()
    {
        var services = new ServiceCollection();
        var rpc = services.AddRpc();
        rpc.AddServer<ITestRpcService, TestRpcService>();
        await using var serviceProvider = services.BuildServiceProvider();
        var hub = serviceProvider.RpcHub();
        var peer = hub.GetServerPeer(RpcRef.NewServer("audit-peer"));
        var method = hub.ServiceRegistry[typeof(ITestRpcService)]["MaybeSet:2"];
        var message = new RpcInboundMessage(
            method.CallType.Id,
            1,
            method.Ref,
            default,
            null) {
            Arguments = ArgumentList.New("handshake-audit", "executed"),
        };
        await using var transport = new FirstMessageTransport(peer, message);

        await peer.SetNextConnection(new RpcConnection(transport));
        await transport.WhenCompleted.WaitAsync(TimeSpan.FromSeconds(5));

        var service = hub.GetServer<ITestRpcService>();
        (await service.Get("handshake-audit")).Should().BeNull();
    }

    [Fact]
    public async Task UnsupportedHandshakeProtocolVersionMustBeRejected()
    {
        var services = new ServiceCollection();
        services.AddRpc();
        await using var serviceProvider = services.BuildServiceProvider();
        var hub = serviceProvider.RpcHub();
        var peer = hub.GetServerPeer(RpcRef.NewServer("audit-peer"));
        var method = hub.ServiceRegistry[typeof(IRpcSystemCalls)]["Handshake:1"];
        var handshake = new RpcHandshake(
            Guid.NewGuid(),
            VersionSet.Empty,
            Guid.NewGuid(),
            int.MaxValue,
            1);
        var message = new RpcInboundMessage(
            method.CallType.Id,
            0,
            method.Ref,
            default,
            null) {
            Arguments = ArgumentList.New(handshake),
        };
        await using var transport = new FirstMessageTransport(peer, message);

        await peer.SetNextConnection(new RpcConnection(transport));
        var result = await peer.WhenConnected(TimeSpan.FromMilliseconds(500)).ResultAwait();

        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void FrontendPeerMustNotDispatchBackendMethod()
    {
        AuditBackend.ResetCallCount();
        var services = new ServiceCollection();
        var rpc = services.AddRpc();
        rpc.AddServer<IAuditBackend, AuditBackend>();
        using var serviceProvider = services.BuildServiceProvider();
        var hub = serviceProvider.RpcHub();
        var peer = new AuditServerPeer(hub, RpcRef.NewServer("audit-peer", isBackend: false).Route);
        var method = hub.ServiceRegistry[typeof(IAuditBackend)]["Mutate:1"];
        var message = new RpcInboundMessage(
            method.CallType.Id,
            1,
            method.Ref,
            default,
            null) {
            Arguments = ArgumentList.New("executed"),
        };

        peer.Dispatch(message);

        AuditBackend.CallCount.Should().Be(0);
    }

    private sealed class FirstMessageTransport : RpcTransport
    {
        private readonly RpcInboundMessage _message;
        private readonly TaskCompletionSource<Unit> _whenCompletedSource = TaskCompletionSourceExt.New<Unit>();

        public override Task WhenCompleted => _whenCompletedSource.Task;

        public FirstMessageTransport(RpcPeer peer, RpcInboundMessage message)
            : base(peer, null)
            => _message = message;

        public override void Send(RpcOutboundMessage message, CancellationToken cancellationToken = default)
            => CompleteSend(message);

        public override bool TryComplete(Exception? error = null)
            => _whenCompletedSource.TrySetFromResult(new Result<Unit>(default, error));

        public override IAsyncEnumerator<RpcInboundMessage> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
            => ReadAll(cancellationToken).GetAsyncEnumerator(cancellationToken);

        protected override Task DisposeAsyncCore()
        {
            TryComplete();
            return Task.CompletedTask;
        }

        private async IAsyncEnumerable<RpcInboundMessage> ReadAll(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return _message;
            await TaskExt.NeverEnding(cancellationToken);
        }
    }

    private sealed class AuditServerPeer(RpcHub hub, RpcRoute route) : RpcServerPeer(hub, route)
    {
        public RpcInboundContext? Dispatch(RpcInboundMessage message)
            => ProcessMessage(message, default, default);
    }

    private sealed class CompletedFrameTransport : RpcFrameBasedTransport
    {
        private static readonly MeterSet AuditMeters = new();

        public CompletedFrameTransport(RpcPeer peer)
            : base(
                peer,
                null,
                16,
                16,
                16,
                null,
                new UnboundedChannelOptions(),
                AuditMeters)
            => Start();

        protected override Task WriteFrame(ReadOnlyMemory<byte> frame)
            => Task.CompletedTask;

        protected override async IAsyncEnumerable<RpcInboundMessage> ReadAll(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        private sealed class MeterSet() : FrameMeterSet("audit", "Audit");
    }

    public interface IAuditBackend : IRpcService, IBackendService
    {
        Task<RpcNoWait> Mutate(string value);
    }

    public sealed class AuditBackend : IAuditBackend
    {
        private static int _callCount;

        public static int CallCount => _callCount;

        public static void ResetCallCount()
            => _callCount = 0;

        public Task<RpcNoWait> Mutate(string value)
        {
            Interlocked.Increment(ref _callCount);
            return RpcNoWait.Tasks.Completed;
        }
    }
}
