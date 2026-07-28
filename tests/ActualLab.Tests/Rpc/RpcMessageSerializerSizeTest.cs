using System.Text.Json;
using ActualLab.IO.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Tests.Rpc;

public class RpcMessageSerializerSizeTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const string MethodNameSuffix = ".Get:1";

    [Fact]
    public void HeaderValueAtLimitRoundTripsInEveryFormat()
    {
        var value = new string('v', RpcByteMessageSerializer.MaxHeaderSize);
        foreach (var format in RpcSerializationFormat.All) {
            var (peer, services) = NewPeer(format);
            using var _ = services;

            var (message, error) = WriteAndRead(peer, OkMethodDef(services), [new RpcHeader("h", value)]);

            error.Should().BeNull($"{format.Key} must accept a header value of {value.Length} bytes");
            message!.Headers!.Single().Value.Should().Be(value);
        }
    }

    [Fact]
    public void HeaderValueOverLimitIsRejectedInEveryFormat()
    {
        var value = new string('v', RpcByteMessageSerializer.MaxHeaderSize + 1);
        foreach (var format in RpcSerializationFormat.All) {
            var (peer, services) = NewPeer(format);
            using var _ = services;

            var (_, error) = WriteAndRead(peer, OkMethodDef(services), [new RpcHeader("h", value)]);

            // Text formats reject on write, binary ones on read - either way the message never lands
            error.Should().BeOfType<FormatException>($"{format.Key} must reject an over-limit header value");
        }
    }

    [Fact]
    public void MethodRefAtLimitRoundTripsInEveryFormat()
    {
        var serviceName = new string('s', RpcMethodRef.MaxUtf8NameLength - MethodNameSuffix.Length);
        foreach (var format in RpcSerializationFormat.All) {
            var (peer, services) = NewPeer(format, serviceName);
            using var _ = services;
            var methodDef = peer.Hub.ServiceRegistry[typeof(ILongNameTestService)].Methods.Single();
            methodDef.Ref.Utf8Name.Length.Should().Be(RpcMethodRef.MaxUtf8NameLength);

            var (message, error) = WriteAndRead(peer, methodDef, null);

            error.Should().BeNull($"{format.Key} must accept a {RpcMethodRef.MaxUtf8NameLength}-byte method ref");
            message!.MethodRef.HashCode.Should().Be(methodDef.Ref.HashCode);
        }
    }

    [Fact]
    public void OverLimitMethodRefCannotBeCreated()
    {
        var name = new string('s', RpcMethodRef.MaxUtf8NameLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RpcMethodRef(name));
        // A service name that long is rejected as soon as its RpcMethodDef is built
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewPeer(RpcSerializationFormat.MemoryPackV6, name).Peer.Hub.ServiceRegistry
                [typeof(ILongNameTestService)].Methods.Single());
    }

    [Fact]
    public void OverLimitInboundMethodRefIsRejected()
    {
        var (peer, services) = NewPeer(RpcSerializationFormat.MemoryPackV6);
        using var _1 = services;
        var serializer = peer.MessageSerializer;

        ReadV5Message(serializer, RpcMethodRef.MaxUtf8NameLength).Should().BeNull();
        ReadV5Message(serializer, RpcMethodRef.MaxUtf8NameLength + 1).Should().BeOfType<FormatException>();

        var (jsonPeer, jsonServices) = NewPeer(RpcSerializationFormat.SystemJsonV5);
        using var _2 = jsonServices;
        var jsonSerializer = jsonPeer.MessageSerializer;

        ReadJsonMessage(jsonSerializer, RpcTextMessageSerializerV3.MaxMethodRefSize).Should().BeNull();
        ReadJsonMessage(jsonSerializer, RpcTextMessageSerializerV3.MaxMethodRefSize + 1)
            .Should().BeOfType<FormatException>();
    }

    // Private methods

    private static (RpcInboundMessage? Message, Exception? Error) WriteAndRead(
        RpcPeer peer, RpcMethodDef methodDef, RpcHeader[]? headers)
    {
        var serializer = peer.MessageSerializer;
        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, 4096, mustClear: false);
        try {
            var message = new RpcOutboundMessage(
                new RpcOutboundContext(peer), methodDef, 1, false, headers, "args"u8.ToArray());
            serializer.WriteFunc.Invoke(buffer, message);
            return (serializer.ReadFunc.Invoke(buffer.WrittenMemory, out _), null);
        }
        catch (Exception e) {
            return (null, e);
        }
    }

    private static Exception? ReadV5Message(RpcMessageSerializer serializer, int methodRefSize)
    {
        var utf8Name = new byte[methodRefSize];
        utf8Name.AsSpan().Fill((byte)'s');
        var data = new byte[utf8Name.Length + 32];
        var writer = new SpanWriter(data);
        writer.Remaining[0] = 0; // CallTypeId = 0, headerCount = 0
        writer.WriteVarUInt64(1UL, 1); // RelatedId
        writer.WriteLVarSpan(utf8Name);
        writer.WriteL4Span(ReadOnlySpan<byte>.Empty);
        return Read(serializer, data.AsMemory(0, writer.Position));
    }

    private static Exception? ReadJsonMessage(RpcMessageSerializer serializer, int methodRefSize)
    {
        var envelope = JsonSerializer.SerializeToUtf8Bytes(
            new JsonRpcMessage(0, 1, new string('s', methodRefSize), null));
        var data = new byte[envelope.Length + 1];
        envelope.CopyTo(data, 0);
        data[envelope.Length] = (byte)'\n';
        return Read(serializer, data);
    }

    private static Exception? Read(RpcMessageSerializer serializer, ReadOnlyMemory<byte> data)
    {
        try {
            serializer.ReadFunc.Invoke(data, out _);
            return null;
        }
        catch (Exception e) {
            return e;
        }
    }

    private static RpcMethodDef OkMethodDef(IServiceProvider services)
        => services.GetRequiredService<RpcSystemCallSender>().OkMethodDef;

    private static (RpcClientPeer Peer, ServiceProvider Services) NewPeer(
        RpcSerializationFormat format, string longNameServiceName = "")
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        var rpc = services.AddRpc();
        if (!longNameServiceName.IsNullOrEmpty())
            rpc.AddServer<ILongNameTestService, LongNameTestService>(longNameServiceName);
        var serviceProvider = services.BuildServiceProvider();
        var rpcRef = RpcRef.NewClient("size-test", format.Key);
        return (new RpcClientPeer(serviceProvider.RpcHub(), rpcRef.Route), serviceProvider);
    }

    // Nested types

    public interface ILongNameTestService : IRpcService
    {
        Task<int> Get(CancellationToken cancellationToken);
    }

    public sealed class LongNameTestService : ILongNameTestService
    {
        public Task<int> Get(CancellationToken cancellationToken)
            => Task.FromResult(0);
    }
}
