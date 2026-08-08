using System.Reflection;
using System.Runtime.CompilerServices;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing;

namespace ActualLab.Tests.Rpc;

[Trait("Category", "Rpc")]
public class RpcStreamAckWindowTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private const int AckPeriod = 10;
    private const int AckAdvance = 20;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly MethodInfo OnItemMethod =
        typeof(RpcStream).GetMethod("OnItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo OnBatchMethod =
        typeof(RpcStream).GetMethod("OnBatch", BindingFlags.Instance | BindingFlags.NonPublic)!;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<IAckWindowTestService, AckWindowTestService>();
    }

    [Fact]
    public async Task ItemsWithinAckWindowAreAccepted()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(AckPeriod, AckAdvance);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        // AckAdvance is exactly what a conforming sender may push before the first ack
        for (var i = 0; i < AckAdvance; i++)
            OnItem(stream, i, i);

        for (var i = 0; i < AckAdvance; i++) {
            (await enumerator.MoveNextAsync()).Should().BeTrue();
            enumerator.Current.Should().Be(i);
        }
    }

    [Fact]
    public async Task AckWindowSlidesAsItemsAreConsumed()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(AckPeriod, AckAdvance);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        // Feeding and draining a full window at a time must never trip the check,
        // no matter how many windows pass - otherwise no long stream could survive.
        var index = 0;
        for (var round = 0; round < 20; round++) {
            for (var i = 0; i < AckAdvance; i++)
                OnItem(stream, index + i, index + i);

            for (var i = 0; i < AckAdvance; i++) {
                (await enumerator.MoveNextAsync()).Should().BeTrue();
                enumerator.Current.Should().Be(index + i);
            }
            index += AckAdvance;
        }
    }

    [Fact]
    public async Task FloodPastAckWindowFailsTheStream()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(AckPeriod, AckAdvance);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        const int floodSize = 10_000;
        for (var i = 0; i < floodSize; i++)
            OnItem(stream, i, i);

        var bufferedCount = 0;
        var moveNext = async () => {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                bufferedCount++;
        };
        await moveNext.Should().ThrowAsync<RpcResourceLimitExceededException>();

        // The point of the fix: what a hostile sender can make us buffer is O(AckAdvance),
        // not O(what it sent)
        bufferedCount.Should().BeLessThanOrEqualTo((2 * AckAdvance) + AckPeriod);
    }

    [Fact]
    public async Task ForcedResetsDoNotWidenTheAckWindow()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(AckPeriod, AckAdvance);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        // An out-of-order index makes us send a reset ack, which re-bases the sender to what
        // we've received. If it credited our own window too, a peer could ratchet its limit
        // up one forced gap at a time and flood us anyway.
        var index = 0;
        for (var round = 0; round < 200; round++) {
            for (var i = 0; i < AckAdvance; i++)
                OnItem(stream, index++, index);

            OnItem(stream, index + 1000, -1); // Gap -> we reply with a reset ack
        }

        var bufferedCount = 0;
        var moveNext = async () => {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                bufferedCount++;
        };
        await moveNext.Should().ThrowAsync<RpcResourceLimitExceededException>();
        bufferedCount.Should().BeLessThanOrEqualTo((2 * AckAdvance) + AckPeriod);
    }

    [Fact]
    public async Task FloodPastAckWindowSendsAckEnd()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        using var cts = new CancellationTokenSource(Timeout);

        // A live source, not an idle one: a non-real-time sender waits on its source rather
        // than on acks (RpcSharedStream.OnRun step 3.3), so an idle one wouldn't observe
        // $sys.AckEnd until it produced something anyway.
        var stream = await client.GetStream(1_000_000, AckPeriod, AckAdvance);
        var localId = stream.Id.LocalId;
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        await TestExt.When(
            () => connection.ServerPeer.SharedObjects.Get(localId).Should().NotBeNull(),
            Timeout);

        for (var i = 0; i < 10_000; i++)
            OnItem(stream, i, i);

        // The sender releases its RpcSharedStream only when $sys.AckEnd arrives,
        // so this is what proves we told it to stop rather than just dropping items
        await TestExt.When(
            () => connection.ServerPeer.SharedObjects.Get(localId).Should().BeNull(),
            Timeout);
    }

    [Fact]
    public async Task BatchPastAckWindowIsRejectedWholesale()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(AckPeriod, AckAdvance);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        OnBatch(stream, 0, new int[RpcStream.MaxBatchSize]);

        var moveNext = async () => await enumerator.MoveNextAsync().ConfigureAwait(false);
        await moveNext.Should().ThrowAsync<RpcResourceLimitExceededException>();
    }

    [Fact]
    public async Task BatchLargerThanMaxBatchSizeIsRejected()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        // The ack window is wide enough to admit this batch, so only the batch size cap can reject it
        var stream = await client.GetIdleStream(ackPeriod: 100_000, ackAdvance: 1_000_000);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        OnBatch(stream, 0, new int[RpcStream.MaxBatchSize + 1]);

        var moveNext = async () => await enumerator.MoveNextAsync().ConfigureAwait(false);
        await moveNext.Should().ThrowAsync<RpcResourceLimitExceededException>();
    }

    [Fact]
    public async Task MaxBatchSizeBatchIsAccepted()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();
        using var cts = new CancellationTokenSource(Timeout);

        var stream = await client.GetIdleStream(ackPeriod: 100_000, ackAdvance: 1_000_000);
        var enumerator = stream.GetAsyncEnumerator(cts.Token);
        await using var _ = enumerator.ConfigureAwait(false);

        OnBatch(stream, 0, Enumerable.Range(0, RpcStream.MaxBatchSize).ToArray());

        for (var i = 0; i < RpcStream.MaxBatchSize; i++) {
            (await enumerator.MoveNextAsync()).Should().BeTrue();
            enumerator.Current.Should().Be(i);
        }
    }

    [Fact]
    public async Task SlowConsumerDoesNotTripTheWindow()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IAckWindowTestService>();

        // A real sender constantly sitting at the window edge is the false-positive case
        const int count = 250;
        var stream = await client.GetStream(count, AckPeriod, AckAdvance);
        var items = new List<int>();
        await foreach (var item in stream.ConfigureAwait(false)) {
            items.Add(item);
            if (items.Count % 25 == 0)
                await Task.Delay(10);
        }

        items.Should().Equal(Enumerable.Range(0, count));
    }

    // Private methods

    private static void OnItem(RpcStream stream, long index, int item)
        => OnItemMethod.Invoke(stream, [index, item]);

    private static void OnBatch(RpcStream stream, long index, int[] items)
        => OnBatchMethod.Invoke(stream, [index, items]);
}

public interface IAckWindowTestService : IRpcService
{
    Task<RpcStream<int>> GetIdleStream(int ackPeriod, int ackAdvance);
    Task<RpcStream<int>> GetStream(int count, int ackPeriod, int ackAdvance);
}

public class AckWindowTestService : IAckWindowTestService
{
    public Task<RpcStream<int>> GetIdleStream(int ackPeriod, int ackAdvance)
        => Task.FromResult(new RpcStream<int>(Idle()) {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
        });

    public Task<RpcStream<int>> GetStream(int count, int ackPeriod, int ackAdvance)
        => Task.FromResult(new RpcStream<int>(Enumerate(count)) {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
        });

    // Private methods

    // Yields nothing while the stream stays alive, so a test can drive the receive
    // path itself without racing the real sender.
    private static async IAsyncEnumerable<int> Idle(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        yield break;
    }

    private static async IAsyncEnumerable<int> Enumerate(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++) {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
    }
}
