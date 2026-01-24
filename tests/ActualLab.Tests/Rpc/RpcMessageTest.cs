using System.Buffers;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Rpc;

public class RpcMessageTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void RpcInboundMessageBasicTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(256, false);

        // Write some test data
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        testData.CopyTo(poolBuffer.GetSpan(5));
        poolBuffer.Advance(5);

        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        var message = new RpcInboundMessage(
            1,
            42,
            default,
            holder.Array!.AsMemory(0, 5),
            null,
            holder.NewRef());

        message.ArgumentData.Length.Should().Be(5);
        message.ArgumentData.Span[0].Should().Be(1);
        message.ArgumentData.Span[4].Should().Be(5);

        // Dispose message
        message.MarkProcessed();
        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void RpcInboundMessageDoubleDisposeTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(10);
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        var message = new RpcInboundMessage(
            1,
            1,
            default,
            holder.Array!.AsMemory(0, 10),
            null,
            holder.NewRef());

        // Multiple disposes should be safe
        message.MarkProcessed();
        message.MarkProcessed();
        message.MarkProcessed();

        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void RpcInboundMessageMultipleMessagesFromSameBlockTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(256, false);

        // Write data for 3 messages
        for (var i = 0; i < 30; i++)
            poolBuffer.GetSpan(1)[0] = (byte)i;
        poolBuffer.Advance(30);

        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);
        var array = holder.Array!;

        // Manually write data since we can't write to poolBuffer anymore
        for (var i = 0; i < 30; i++)
            array[i] = (byte)i;

        // Create 3 messages from the same block
        var messages = new RpcInboundMessage[3];
        for (var i = 0; i < 3; i++) {
            messages[i] = new RpcInboundMessage(
                (byte)i,
                i,
                default,
                array.AsMemory(i * 10, 10),
                null,
                holder.NewRef());
        }

        holder.IsDisposed.Should().BeFalse();

        // Dispose first message
        messages[0].MarkProcessed();
        holder.IsDisposed.Should().BeFalse();

        // Other messages still valid
        messages[1].ArgumentData.Span[0].Should().Be(10);
        messages[2].ArgumentData.Span[0].Should().Be(20);

        // Dispose second
        messages[1].MarkProcessed();
        holder.IsDisposed.Should().BeFalse();

        // Dispose third - buffer should be released
        messages[2].MarkProcessed();
        holder.IsDisposed.Should().BeTrue();
    }

    // RpcOutboundMessage tests removed - the class now requires RpcOutboundContext and RpcMethodDef
    // which are complex types that can't be easily mocked in unit tests.
    // RpcOutboundMessage is tested indirectly through integration tests in RpcWebSocketTest.
}
