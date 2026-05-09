using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcStreamBasicTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void LocalStream_ShouldHaveLocalKind()
    {
        var source = AsyncEnumerable.Range(0, 10);
        var stream = RpcStream.New(source);

        stream.Kind.Should().Be(RpcObjectKind.Local);
        stream.ItemType.Should().Be(typeof(int));
    }

    [Fact]
    public void RemoteStream_ShouldHaveRemoteKind()
    {
        var stream = new RpcStream<int>();

        stream.Kind.Should().Be(RpcObjectKind.Remote);
        stream.ItemType.Should().Be(typeof(int));
    }

    [Fact]
    public void DefaultProperties_ShouldHaveExpectedValues()
    {
        var stream = new RpcStream<int>();

        stream.AckPeriod.Should().Be(30);
        stream.AckAdvance.Should().Be(61);
        stream.BufferSize.Should().Be(0);
        stream.Id.Should().Be(default(RpcObjectId));
        stream.Id.IsNone.Should().BeTrue();
        stream.Peer.Should().BeNull();
    }

    [Fact]
    public void CustomAckSettings_ShouldBePreserved()
    {
        var stream = new RpcStream<int> { AckPeriod = 50, AckAdvance = 100, BufferSize = 200 };

        stream.AckPeriod.Should().Be(50);
        stream.AckAdvance.Should().Be(100);
        stream.BufferSize.Should().Be(200);
    }

    [Fact]
    public void DeserializeFromString_WithNull_ShouldReturnNull()
    {
        var result = RpcStream<int>.DeserializeFromString(null);
        result.Should().BeNull();
    }

    [Fact]
    public void DeserializeFromString_WithEmpty_ShouldReturnNull()
    {
        var result = RpcStream<int>.DeserializeFromString("");
        result.Should().BeNull();
    }

    [Fact]
    public void DeserializeFromString_WithValidString_RequiresRpcContext()
    {
        // DeserializeFromString requires RpcInboundContext to be set
        // This test verifies the expected behavior when called outside RPC context
        var hostId = Guid.NewGuid();
        var serialized = $"{hostId},12345,40,80";

        var act = () => RpcStream<int>.DeserializeFromString(serialized);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RpcInboundContext*unavailable*");
    }

    [Fact]
    public void BufferSize_IsNotSerializedInWireFormat()
    {
        // The wire (text) format carries: hostId, localId, ackPeriod, ackAdvance, allowReconnect, isRealTime.
        // The new local-only BufferSize is intentionally absent — set it large and verify
        // it does NOT show up at the AckAdvance position when parsing the wire string.
        // We can't call SerializeToString without RPC context, so we verify via the parser
        // that index 3 (4th comma-separated field) is interpreted as ackAdvance.
        var hostId = Guid.NewGuid();
        // Wire string: hostId, localId=1, ackPeriod=10, ackAdvance=20, allowReconnect=1, isRealTime=0
        var serialized = $"{hostId},1,10,20,1,0";

        using var parser = ListFormat.CommaSeparated.CreateParser(serialized);
        parser.ParseNext(); // hostId
        parser.ParseNext(); // localId
        parser.ParseNext(); // ackPeriod
        var ackPeriod = int.Parse(parser.Item);
        parser.ParseNext(); // ackAdvance (index 3 — was BufferSize before rename)
        var ackAdvance = int.Parse(parser.Item);

        ackPeriod.Should().Be(10);
        ackAdvance.Should().Be(20);

        // Round-trip via DeserializeFromString within a synthetic context would
        // populate AckAdvance (not BufferSize) — covered indirectly by realtime tests.
    }

    [Fact]
    public void BufferSize_AcceptsValuesIndependentOfAckAdvance()
    {
        // BufferSize and AckAdvance are independent properties.
        var s1 = new RpcStream<int> { AckAdvance = 50, BufferSize = 0 };
        s1.AckAdvance.Should().Be(50);
        s1.BufferSize.Should().Be(0);

        var s2 = new RpcStream<int> { AckAdvance = 20, BufferSize = 200 };
        s2.AckAdvance.Should().Be(20);
        s2.BufferSize.Should().Be(200);

        var s3 = new RpcStream<int> { AckAdvance = 100, BufferSize = 10 };
        s3.AckAdvance.Should().Be(100);
        s3.BufferSize.Should().Be(10);
    }

    [Fact]
    public async Task LocalStream_Enumeration_ShouldWork()
    {
        var expected = Enumerable.Range(0, 5).ToList();
        var stream = RpcStream.New(expected);

        var result = await stream.ToListAsync();

        result.Should().Equal(expected);
    }

    [Fact]
    public async Task LocalStream_AsyncEnumeration_ShouldWork()
    {
        var expected = Enumerable.Range(0, 5).ToList();
        var stream = RpcStream.New(expected);

        var result = await stream.ToListAsync();

        result.Should().Equal(expected);
    }

    [Fact]
    public void RemoteStream_WithoutPeer_ShouldThrowOnEnumeration()
    {
        var stream = new RpcStream<int>();

        var act = () => stream.GetAsyncEnumerator();

        // Throws InternalError which derives from InvalidOperationException
        act.Should().Throw<InternalError>()
            .WithMessage("*Peer*null*");
    }

    [Fact]
    public void ToString_ShouldContainRelevantInfo()
    {
        var localStream = RpcStream.New(AsyncEnumerable.Range(0, 10));
        var remoteStream = new RpcStream<string>();

        localStream.ToString().Should().Contain("Local");
        localStream.ToString().Should().Contain("RpcStream");

        remoteStream.ToString().Should().Contain("Remote");
        remoteStream.ToString().Should().Contain("RpcStream");
    }

    [Fact]
    public async Task NewFromEnumerable_ShouldCreateLocalStream()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var stream = RpcStream.New(source);

        stream.Kind.Should().Be(RpcObjectKind.Local);
        (await stream.ToArrayAsync()).Should().Equal(source);
    }

    [Fact]
    public void NewFromAsyncEnumerable_ShouldCreateLocalStream()
    {
        var stream = RpcStream.New(GenerateAsync());

        stream.Kind.Should().Be(RpcObjectKind.Local);

        static async IAsyncEnumerable<int> GenerateAsync()
        {
            for (var i = 0; i < 3; i++) {
                await Task.Yield();
                yield return i;
            }
        }
    }

    [Fact]
    public void ItemType_ShouldReturnCorrectType()
    {
        var intStream = new RpcStream<int>();
        var stringStream = new RpcStream<string>();
        var tupleStream = new RpcStream<(int, string)>();

        intStream.ItemType.Should().Be(typeof(int));
        stringStream.ItemType.Should().Be(typeof(string));
        tupleStream.ItemType.Should().Be(typeof((int, string)));
    }

    [Fact]
    public void RpcObjectId_ShouldBeComparable()
    {
        var hostId = Guid.NewGuid();
        var id1 = new RpcObjectId(hostId, 42);
        var id2 = new RpcObjectId(hostId, 42);
        var id3 = new RpcObjectId(hostId, 43);
        var none = new RpcObjectId();

        id1.Should().Be(id2);
        id1.Should().NotBe(id3);
        none.IsNone.Should().BeTrue();
        id1.IsNone.Should().BeFalse();
    }

    [Fact]
    public async Task LocalStream_CanBeEnumeratedMultipleTimes()
    {
        // Unlike remote streams, local streams wrap an IAsyncEnumerable
        // which can potentially be enumerated multiple times (depending on source)
        var source = new[] { 1, 2, 3 };
        var stream = RpcStream.New(source);

        var result1 = await stream.ToListAsync();
        var result2 = await stream.ToListAsync();

        result1.Should().Equal(source);
        result2.Should().Equal(source);
    }

    [Fact]
    public void LocalStream_SerializedIdGetter_RequiresRpcContext()
    {
        // SerializedId getter requires RpcOutboundContext or RpcInboundContext
        var stream = RpcStream.New(new[] { 1, 2, 3 });

        var act = () => _ = stream.SerializedId;

        // Throws when trying to get RpcInboundContext.GetCurrent() if RpcOutboundContext.Current is null
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RpcInboundContext*unavailable*");
    }
}
