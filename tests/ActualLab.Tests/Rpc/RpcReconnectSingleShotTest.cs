using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcReconnectSingleShotTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    [Fact]
    public async Task ReconnectIsAllowedOncePerConnectionTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        await clientPeer.WhenConnected(TimeSpan.FromSeconds(5));

        (await SendReconnect(clientPeer)).Should().NotBeNull();
        var error = await Assert.ThrowsAsync<RpcException>(() => SendReconnect(clientPeer));
        error.Message.Should().Contain("already reconnected");

        // The allowance is per connection, so the next one gets its own
        await connection.Reconnect();
        await clientPeer.WhenConnected(TimeSpan.FromSeconds(5));
        (await SendReconnect(clientPeer)).Should().NotBeNull();
        await Assert.ThrowsAsync<RpcException>(() => SendReconnect(clientPeer));
    }

    [Fact]
    public async Task ReconnectStillReconcilesInFlightCallsTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        await clientPeer.WhenConnected(TimeSpan.FromSeconds(5));

        // An unknown call id must come back as unknown - i.e. the single-shot gate doesn't
        // short-circuit the reconciliation the first Reconnect of a connection performs
        var stages = new Dictionary<int, byte[]> {
            { 0, IncreasingSeqCompressor.Serialize([777L]) },
        };
        var resultData = await SendReconnect(clientPeer, stages);
        IncreasingSeqCompressor.Deserialize(resultData).Should().Equal(777L);
    }

    // Private methods

    private static Task<byte[]> SendReconnect(RpcPeer peer, Dictionary<int, byte[]>? completedStagesData = null)
    {
        var handshakeIndex = peer.ConnectionState.Value.Handshake!.Index;
        Task<byte[]> resultTask;
        using (new RpcOutboundCallSetup(peer).Activate()) // No "await" inside this block!
            resultTask = peer.Hub.InternalServices.SystemCallSender.Client
                .Reconnect(handshakeIndex, completedStagesData ?? [], CancellationToken.None);
        return resultTask;
    }
}
