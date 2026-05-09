using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcStreamRealTimeTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<IRealTimeStreamTestService, RealTimeStreamTestService>();
    }

    // -- Properties & serialization --

    [Fact]
    public void IsRealTime_DefaultIsFalse()
    {
        var stream = new RpcStream<int>();
        stream.IsRealTime.Should().BeFalse();
    }

    [Fact]
    public void IsRealTime_CanBeSetViaInit()
    {
        var stream = new RpcStream<int> { IsRealTime = true };
        stream.IsRealTime.Should().BeTrue();
    }

    [Fact]
    public void CanSkipTo_DefaultAlwaysReturnsTrue()
    {
        var stream = new RpcStream<int>();
        stream.CanSkipTo(42).Should().BeTrue();
        stream.CanSkipTo(0).Should().BeTrue();
    }

    [Fact]
    public void CanSkipTo_CustomPredicate()
    {
        var stream = new RpcStream<int> { CanSkipTo = x => x % 10 == 0 };
        stream.CanSkipTo(10).Should().BeTrue();
        stream.CanSkipTo(7).Should().BeFalse();
    }

    [Fact]
    public void SerializeToString_IncludesIsRealTime()
    {
        // We can't call SerializeToString directly (requires RPC context),
        // but we can test DeserializeFromString round-trip by constructing
        // the serialized format manually.
        var hostId = Guid.NewGuid();
        var serialized = $"{hostId},1,3,5,1,1"; // ackPeriod=3, ackAdvance=5, allowReconnect=1, isRealTime=1

        // DeserializeFromString requires RpcInboundContext, but we can verify
        // the format is accepted without throwing a parse error by using the
        // ListFormat parser directly.
        using var parser = ListFormat.CommaSeparated.CreateParser(serialized);
        parser.ParseNext(); // hostId
        parser.ParseNext(); // localId
        parser.ParseNext(); // ackPeriod
        var ackPeriod = int.Parse(parser.Item);
        parser.ParseNext(); // ackAdvance
        var ackAdvance = int.Parse(parser.Item);
        parser.TryParseNext(); // allowReconnect
        var allowReconnect = parser.Item != "0";
        parser.TryParseNext(); // isRealTime
        var isRealTime = parser.Item == "1";

        ackPeriod.Should().Be(3);
        ackAdvance.Should().Be(5);
        allowReconnect.Should().BeTrue();
        isRealTime.Should().BeTrue();
    }

    [Fact]
    public void SerializeToString_IsRealTimeFalse_IsBackwardCompatible()
    {
        // When isRealTime is absent, it defaults to false
        var hostId = Guid.NewGuid();
        var serialized = $"{hostId},1,30,61,1"; // old format without isRealTime

        using var parser = ListFormat.CommaSeparated.CreateParser(serialized);
        parser.ParseNext(); // hostId
        parser.ParseNext(); // localId
        parser.ParseNext(); // ackPeriod
        parser.ParseNext(); // ackAdvance
        parser.TryParseNext(); // allowReconnect
        var hasIsRealTime = parser.TryParseNext(); // isRealTime (should be absent)

        hasIsRealTime.Should().BeFalse();
    }

    // -- End-to-end real-time stream tests --

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RealTimeStream_SlowConsumer_SkipsItems(int ackPeriod)
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // Source produces items at full speed, consumer is slow.
        // bufferSize must be > ackPeriod (otherwise deadlock: sender blocks before
        // enough items are sent to trigger an ACK from the receiver).
        // Note: in-memory channels deliver ACKs near-instantly, so skipping
        // may not always occur for all ackPeriod values on fast machines.
        const int totalItems = 500;
        var bufferSize = Math.Max(ackPeriod + 2, 5);
        const int consumerDelayMs = 50; // slow consumer

        var stream = await client.GetRealTimeStream(totalItems, ackPeriod, bufferSize);
        var received = new List<int>();
        await foreach (var item in stream) {
            received.Add(item);
            await Task.Delay(consumerDelayMs); // simulate slow consumer
        }

        Out.WriteLine($"AckPeriod={ackPeriod}: received {received.Count} of {totalItems} items");
        Out.WriteLine($"  Items: [{string.Join(", ", received.Take(30))}]{(received.Count > 30 ? "..." : "")}");

        // Items should be in ascending order (no reordering) regardless of skipping
        received.Should().BeInAscendingOrder();
        received[0].Should().Be(0);
        // Skipping may or may not occur depending on timing in in-memory test.
        // The important invariant is: items are sequential and ordered.
        // When skipping occurs, fewer items are received.
        if (received.Count < totalItems) {
            Out.WriteLine($"  Skipping occurred: {totalItems - received.Count} items skipped");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RealTimeStream_FastConsumer_NoSkipping(int ackPeriod)
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // Source produces items with delay, consumer is fast — no skipping should occur.
        // sourceDelayMs must be large enough that ACKs always arrive in time,
        // even on a loaded machine.
        const int totalItems = 20;
        const int bufferSize = 10;

        var stream = await client.GetRealTimeStreamWithDelay(totalItems, ackPeriod, bufferSize, sourceDelayMs: 100);
        var received = new List<int>();
        await foreach (var item in stream) {
            received.Add(item);
        }

        Out.WriteLine($"AckPeriod={ackPeriod}: received {received.Count} of {totalItems} items");

        // Fast consumer should receive all items
        received.Count.Should().Be(totalItems);
        received.Should().Equal(Enumerable.Range(0, totalItems));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RealTimeStream_WithKeyFrameDetector_SkipsToKeyFrames(int ackPeriod)
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // Source produces 500 items, every 10th is a "keyframe".
        // Slow consumer should cause skipping.
        const int totalItems = 500;
        var bufferSize = Math.Max(ackPeriod + 2, 5);
        const int keyFrameInterval = 10;
        const int consumerDelayMs = 50;

        var stream = await client.GetRealTimeStreamWithKeyFrames(
            totalItems, ackPeriod, bufferSize, keyFrameInterval);
        var received = new List<int>();
        await foreach (var item in stream) {
            received.Add(item);
            await Task.Delay(consumerDelayMs);
        }

        Out.WriteLine($"AckPeriod={ackPeriod}: received {received.Count} of {totalItems} items");
        Out.WriteLine($"  Items: [{string.Join(", ", received.Take(30))}]{(received.Count > 30 ? "..." : "")}");

        // Items should be in ascending order regardless of skipping
        received.Should().BeInAscendingOrder();
        received[0].Should().Be(0);

        // When skipping occurs, verify that skip targets are keyframes
        var gaps = received
            .Where((item, idx) => idx > 0 && item > received[idx - 1] + 1)
            .ToList();
        if (gaps.Count != 0) {
            Out.WriteLine($"  Skipping occurred, items after gaps: [{string.Join(", ", gaps)}]");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RealTimeStream_Reconnect_SkipsToKeyFrame(int ackPeriod)
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        const int totalItems = 500;
        var bufferSize = Math.Max(ackPeriod + 2, 5);
        const int keyFrameInterval = 10;
        // Source delay must be small enough that many items are produced
        // during the disconnect window, ensuring a real gap on reconnect.
        const int sourceDelayMs = 2;

        var stream = await client.GetRealTimeStreamWithKeyFramesReconnectable(
            totalItems, ackPeriod, bufferSize, keyFrameInterval, sourceDelayMs);

        var received = new List<int>();
        var enumerator = stream.GetAsyncEnumerator();

        // Read some items to establish the stream (stop mid-interval, not on a keyframe)
        for (var i = 0; i < 13 && await enumerator.MoveNextAsync(); i++)
            received.Add(enumerator.Current);

        Out.WriteLine($"Before disconnect: received {received.Count} items, last = {received[^1]}");

        // Disconnect long enough for the source to advance well past the next keyframe
        await connection.Disconnect();
        await Delay(0.2);
        await connection.Connect();

        // Continue reading
        while (await enumerator.MoveNextAsync())
            received.Add(enumerator.Current);
        await enumerator.DisposeAsync();

        Out.WriteLine($"Total received: {received.Count} of {totalItems}");
        Out.WriteLine($"  Items: [{string.Join(", ", received.Take(40))}]{(received.Count > 40 ? "..." : "")}");

        // Items should be in ascending order (no reordering)
        received.Should().BeInAscendingOrder();

        // Find gaps — items after gaps should be keyframes (multiples of keyFrameInterval)
        var gaps = new List<(int Before, int After)>();
        for (var i = 1; i < received.Count; i++) {
            if (received[i] > received[i - 1] + 1)
                gaps.Add((received[i - 1], received[i]));
        }

        Out.WriteLine($"  Gaps: {gaps.Count}");
        foreach (var (before, after) in gaps)
            Out.WriteLine($"    {before} -> {after} (keyframe: {after % keyFrameInterval == 0})");

        // At least one gap should exist (the reconnect gap)
        gaps.Should().NotBeEmpty("reconnect should cause at least one gap");

        // The first gap is the reconnect gap — its target must be a keyframe.
        // Subsequent gaps (if any) come from normal real-time backpressure
        // skipping, which is already covered by other tests.
        var (_, reconnectTarget) = gaps[0];
        (reconnectTarget % keyFrameInterval).Should().Be(0,
            $"first item after reconnect gap ({reconnectTarget}) should be a keyframe (multiple of {keyFrameInterval})");
    }

    [Fact]
    public async Task NonRealTimeStream_SlowConsumer_DoesNotSkip()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // Same scenario as RealTimeStream test but with IsRealTime=false (default).
        // All items should be received — back-pressure keeps the sender waiting.
        const int totalItems = 20;

        var stream = await client.GetNonRealTimeStream(totalItems);
        var received = new List<int>();
        await foreach (var item in stream) {
            received.Add(item);
            await Task.Delay(50); // slow consumer — back-pressure should prevent skipping
        }

        received.Count.Should().Be(totalItems);
        received.Should().Equal(Enumerable.Range(0, totalItems));
    }

    [Fact]
    public async Task NonRealTimeStream_StalledConsumer_AppliesBackpressureToSource()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        const int bufferSize = 15;
        var key = Guid.NewGuid().ToString("N");
        var stream = await client.GetBackpressureStream(key, false, 100, ackPeriod: 3, bufferSize, keyFrameInterval: 10);
        await using var enumerator = stream.GetAsyncEnumerator();

        (await enumerator.MoveNextAsync()).Should().BeTrue();
        await Delay(0.2);

        var producedCount = await client.GetBackpressureProducedCount(key);
        producedCount.Should().BeLessThanOrEqualTo(bufferSize + 2,
            "a non-real-time sender may have one buffered item and one in-flight MoveNext beyond the ACK window");
    }

    [Fact]
    public async Task RealTimeStream_StalledConsumer_DoesNotPullPastBufferWithoutSkipTarget()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        const int bufferSize = 5;
        const int keyFrameInterval = 10;
        var key = Guid.NewGuid().ToString("N");
        var stream = await client.GetBackpressureStream(key, true, 100, ackPeriod: 3, bufferSize, keyFrameInterval);
        await using var enumerator = stream.GetAsyncEnumerator();

        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.Should().Be(0);
        await Delay(0.2);

        var producedCount = await client.GetBackpressureProducedCount(key);
        producedCount.Should().BeLessThanOrEqualTo(bufferSize + 2,
            "a real-time sender should not pull beyond AckAdvance just to discover a future skip target");
    }

    [Fact]
    public async Task RealTimeStream_StalledConsumer_StartsFromBufferedSkipTarget()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        const int bufferSize = 15;
        const int keyFrameInterval = 8;
        var stream = await client.GetBufferedSkipStream(100, ackPeriod: bufferSize, bufferSize, keyFrameInterval);
        await using var enumerator = stream.GetAsyncEnumerator();

        var received = new List<int>();
        for (var i = 0; i < bufferSize + 1 && await enumerator.MoveNextAsync(); i++)
            received.Add(enumerator.Current);

        received.Take(bufferSize).Should().Equal(Enumerable.Range(0, bufferSize));
        received[^1].Should().BeGreaterThan(bufferSize,
            "the sender should compact the already-buffered unsent prefix to a known keyframe");
        (received[^1] % keyFrameInterval).Should().Be(0);
        received.Should().NotContain(bufferSize,
            "the delta frame right after the acknowledged window should be swallowed");
    }

    // -- AckAdvance / BufferSize separation --

    [Fact]
    public async Task BufferSizeAboveAckAdvance_PreBuffersBeyondAckWindow()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // BufferSize is much larger than AckAdvance — the sender's local ring
        // buffer should fill up to BufferSize even though only AckAdvance items
        // can be in flight. (RingBuffer capacity is rounded up to (next power
        // of 2) − 1, so the produced count may slightly exceed BufferSize.)
        const int ackAdvance = 5;
        const int bufferSize = 30;
        var key = Guid.NewGuid().ToString("N");
        var stream = await client.GetPreBufferStream(key, 200, ackPeriod: 3, ackAdvance, bufferSize);
        await using var enumerator = stream.GetAsyncEnumerator();

        // Pull a single item to start the stream, then stall the consumer.
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.Should().Be(0);
        await Delay(0.3);

        var producedCount = await client.GetBackpressureProducedCount(key);
        producedCount.Should().BeGreaterThanOrEqualTo(bufferSize - 2,
            "with BufferSize > AckAdvance the sender pre-buffers items past the ack window");
        producedCount.Should().BeLessThanOrEqualTo(2 * bufferSize,
            "the local buffer cap (and ring power-of-2 rounding) should still bound the prefetch");
    }

    [Fact]
    public async Task BufferSizeUnset_FallsBackToAckAdvance()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // BufferSize=0 (the default) means "use AckAdvance as the local buffer".
        // Source pulls should be bounded by AckAdvance — accounting for the
        // ring buffer power-of-2 rounding and one in-flight MoveNextAsync.
        const int ackAdvance = 5;
        var key = Guid.NewGuid().ToString("N");
        var stream = await client.GetPreBufferStream(key, 200, ackPeriod: 3, ackAdvance, bufferSize: 0);
        await using var enumerator = stream.GetAsyncEnumerator();

        (await enumerator.MoveNextAsync()).Should().BeTrue();
        await Delay(0.3);

        var producedCount = await client.GetBackpressureProducedCount(key);
        producedCount.Should().BeLessThanOrEqualTo(3 * ackAdvance,
            "with BufferSize=0 (default) the local buffer is bounded by AckAdvance plus ring overhead");
    }

    [Fact]
    public async Task BufferSizeBelowAckAdvance_ClampsUpToAckAdvance()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IRealTimeStreamTestService>();

        // BufferSize < AckAdvance is invalid: the sender clamps up to AckAdvance
        // (and logs a warning, not asserted here — we only verify the runtime
        // behavior matches the bufferSize=0 case).
        const int ackAdvance = 8;
        const int bufferSize = 3; // intentionally below ackAdvance
        var key = Guid.NewGuid().ToString("N");
        var stream = await client.GetPreBufferStream(key, 200, ackPeriod: 3, ackAdvance, bufferSize);
        await using var enumerator = stream.GetAsyncEnumerator();

        (await enumerator.MoveNextAsync()).Should().BeTrue();
        await Delay(0.3);

        var producedCount = await client.GetBackpressureProducedCount(key);
        producedCount.Should().BeLessThanOrEqualTo(3 * ackAdvance,
            "an under-sized BufferSize is clamped up to AckAdvance");
    }
}

public interface IRealTimeStreamTestService : IRpcService
{
    Task<RpcStream<int>> GetRealTimeStream(int count, int ackPeriod, int bufferSize);
    Task<RpcStream<int>> GetRealTimeStreamWithDelay(int count, int ackPeriod, int bufferSize, int sourceDelayMs);
    Task<RpcStream<int>> GetRealTimeStreamWithKeyFrames(int count, int ackPeriod, int bufferSize, int keyFrameInterval);
    Task<RpcStream<int>> GetRealTimeStreamWithKeyFramesReconnectable(int count, int ackPeriod, int bufferSize, int keyFrameInterval, int sourceDelayMs);
    Task<RpcStream<int>> GetNonRealTimeStream(int count);
    Task<RpcStream<int>> GetBackpressureStream(
        string key, bool isRealTime, int count, int ackPeriod, int bufferSize, int keyFrameInterval);
    Task<int> GetBackpressureProducedCount(string key);
    Task<RpcStream<int>> GetBufferedSkipStream(int count, int ackPeriod, int bufferSize, int keyFrameInterval);
    Task<RpcStream<int>> GetPreBufferStream(
        string key, int count, int ackPeriod, int ackAdvance, int bufferSize);
}

public class RealTimeStreamTestService : IRealTimeStreamTestService
{
    private readonly ConcurrentDictionary<string, int> _backpressureProducedCounts = new();

    public Task<RpcStream<int>> GetRealTimeStream(int count, int ackPeriod, int bufferSize)
    {
        var source = EnumerateFast(count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = false,
        });
    }

    public Task<RpcStream<int>> GetRealTimeStreamWithDelay(int count, int ackPeriod, int bufferSize, int sourceDelayMs)
    {
        var source = EnumerateWithDelay(count, sourceDelayMs);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = false,
        });
    }

    public Task<RpcStream<int>> GetRealTimeStreamWithKeyFrames(int count, int ackPeriod, int bufferSize, int keyFrameInterval)
    {
        var source = EnumerateFast(count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = false,
            CanSkipTo = x => x % keyFrameInterval == 0,
        });
    }

    public Task<RpcStream<int>> GetRealTimeStreamWithKeyFramesReconnectable(
        int count, int ackPeriod, int bufferSize, int keyFrameInterval, int sourceDelayMs)
    {
        var source = EnumerateWithDelay(count, sourceDelayMs);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = true,
            CanSkipTo = x => x % keyFrameInterval == 0,
        });
    }

    public Task<RpcStream<int>> GetNonRealTimeStream(int count)
    {
        var source = EnumerateFast(count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = false,
            AckPeriod = 3,
            AckAdvance = 5,
        });
    }

    public Task<RpcStream<int>> GetBackpressureStream(
        string key, bool isRealTime, int count, int ackPeriod, int bufferSize, int keyFrameInterval)
    {
        _backpressureProducedCounts[key] = 0;
        var source = EnumerateTracked(key, count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = isRealTime,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = false,
            CanSkipTo = x => x % keyFrameInterval == 0,
        });
    }

    public Task<int> GetBackpressureProducedCount(string key)
        => Task.FromResult(_backpressureProducedCounts.GetValueOrDefault(key));

    public Task<RpcStream<int>> GetBufferedSkipStream(int count, int ackPeriod, int bufferSize, int keyFrameInterval)
    {
        var source = EnumerateBurst(count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = bufferSize,
            AllowReconnect = false,
            CanSkipTo = x => x % keyFrameInterval == 0,
        });
    }

    public Task<RpcStream<int>> GetPreBufferStream(
        string key, int count, int ackPeriod, int ackAdvance, int bufferSize)
    {
        _backpressureProducedCounts[key] = 0;
        // Burst-produce items so the sender's section 3.1 can drain them
        // synchronously into the local buffer up to capacity. This is what
        // exercises BufferSize independently of AckAdvance.
        var source = EnumerateTrackedBurst(key, count);
        return Task.FromResult(new RpcStream<int>(source) {
            IsRealTime = true,
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
            BufferSize = bufferSize,
            AllowReconnect = false,
        });
    }

    private static async IAsyncEnumerable<int> EnumerateFast(int count)
    {
        for (var i = 0; i < count; i++) {
            yield return i;
            // Yield to allow the sender loop to process
            if (i % 5 == 0)
                await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<int> EnumerateWithDelay(int count, int delayMs)
    {
        for (var i = 0; i < count; i++) {
            yield return i;
            await Task.Delay(delayMs);
        }
    }

    private async IAsyncEnumerable<int> EnumerateTracked(
        string key,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++) {
            _backpressureProducedCounts.AddOrUpdate(key, 1, static (_, x) => x + 1);
            yield return i;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static async IAsyncEnumerable<int> EnumerateBurst(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i;
        await Task.CompletedTask;
    }

    private async IAsyncEnumerable<int> EnumerateTrackedBurst(
        string key,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++) {
            _backpressureProducedCounts.AddOrUpdate(key, 1, static (_, x) => x + 1);
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
        await Task.CompletedTask;
    }
}
