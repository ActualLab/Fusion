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
        stream.Id.Should().Be(default(RpcObjectId));
        stream.Id.IsNone.Should().BeTrue();
        stream.Peer.Should().BeNull();
    }

    [Fact]
    public void CustomAckSettings_ShouldBePreserved()
    {
        var stream = new RpcStream<int> { AckPeriod = 50, AckAdvance = 100 };

        stream.AckPeriod.Should().Be(50);
        stream.AckAdvance.Should().Be(100);
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
