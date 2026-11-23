using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using ActualLab.Interception;
using ActualLab.Interception.Serialization;
using ActualLab.Rpc;
using ActualLab.Compression;
using ActualLab.Rpc.Compression;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartRSerialization;

// Fake types for snippet compilation
public class MyArgumentListSerializer() : ArgumentListSerializer
{
    public override void Serialize(ArgumentList arguments, bool needsPolymorphism, ArrayPoolBuffer<byte> buffer)
        => throw new NotImplementedException();

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
        => throw new NotImplementedException();
}

public class MyMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    public override RpcInboundMessage Read(ReadOnlyMemory<byte> data, out int readLength)
        => throw new NotImplementedException();

    public override void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
        => throw new NotImplementedException();
}

// ============================================================================
// Format Structure
// ============================================================================

#region PartRSerialization_FormatStructure
public sealed class RpcWireFormatExample(
    string key,
    Func<ArgumentListSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory)
{
    public string Key { get; } = key;
    public ArgumentListSerializer ArgumentListSerializer { get; } = argumentSerializerFactory();
    public Func<RpcPeer, RpcMessageSerializer> MessageSerializerFactory { get; } = messageSerializerFactory;
}
#endregion

// ============================================================================
// Default Format
// ============================================================================

public static class DefaultFormat
{
    public static void UseDefault(IServiceCollection services)
    {
        #region PartRSerialization_UseDefault
        // Compact MessagePack with LZ4-compressed server-to-client frames
        services.AddRpc().ConfigureWireFormats(f => {
            f.AddCompressedVariants("lz4", LZ4ByteCodec.Default);
            f.DefaultFormat = f["msgpack6c-lz4"];
        });
        #endregion
    }
}

// ============================================================================
// Accessing Formats
// ============================================================================

public static class AccessingFormats
{
    public static void Example(IServiceProvider services)
    {
        #region PartRSerialization_AccessingFormats
        // The formats this container serves
        var resolver = services.GetRequiredService<RpcWireFormatResolver>();
        RpcWireFormat defaultFormat = resolver.DefaultFormat;
        var format = resolver.Get("msgpack6c");

        // The formats every container starts with (ActualLab.Rpc formats only)
        ImmutableList<RpcWireFormat> builtIn = RpcDefaultWireFormats.All;
        #endregion
    }
}

// ============================================================================
// Client-Selectable Formats
// ============================================================================

public static class ClientSelection
{
    public static void Example(IServiceCollection services)
    {
        #region PartRSerialization_ClientSelection
        services.AddRpc().ConfigureWireFormats(f
            => f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Union(["msgpack5", "msgpack5c"]));
        #endregion
    }
}

// ============================================================================
// Configuring Formats
// ============================================================================

public static class ConfiguringFormats
{
    public static void CustomLz4Format(IServiceCollection services)
    {
        #region PartRSerialization_CustomLz4Format
        var lz4 = new RpcCompressionFormat("lz4-1k",
            new LZ4ByteCodec { BlockSize = 8 * 1024 },
            new RpcCompressionOptions { MinCompressedFrameSize = 1024 });
        var format = new RpcWireFormat("msgpack6c-lz4-1k",
            () => new ByteArgumentListSerializer(MessagePackByteSerializer.Default),
            peer => new RpcByteMessageSerializerV5Compact(peer),
            DataFormat.Bytes,
            lz4, RpcCompressionMode.ServerToClient);
        services.AddRpc().ConfigureWireFormats(f => f.Add(format));
        #endregion
    }

    public static void Lz4Dictionary(IServiceCollection services, byte[] typicalFrames)
    {
        #region PartRSerialization_Lz4Dictionary
        // Create it once and share it: it prepares the dictionary once, on first use
        var lz4d1 = LZ4ByteCodec.Default with { DictionaryData = typicalFrames };
        services.AddRpc().ConfigureWireFormats(f => {
            f.AddCompressedVariants("lz4d1", lz4d1); // "msgpack6c-lz4d1", ...
            f.AddCompressedVariants("lz4d1f", lz4d1, RpcCompressionMode.Full); // "msgpack6c-lz4d1f", ...
            f.DefaultFormat = f["msgpack6c-lz4d1"];
        });
        #endregion
    }

    public static void AddFormats(IServiceCollection services)
    {
        #region PartRSerialization_AddFormats
        services.AddRpc().ConfigureWireFormats(f => {
            f.AddNerdbankMessagePack(); // nmsgpack6, nmsgpack6c
            f.AddCompressedVariants("lz4", LZ4ByteCodec.Default); // "msgpack6c-lz4", ...
            f.AddCompressedVariants("lz4f", LZ4ByteCodec.Default, RpcCompressionMode.Full); // "...-lz4f"
            f.DefaultFormat = f["msgpack6c-lz4"];
        });
        #endregion
    }

    public static void RegisterAdditionalFormat(IServiceCollection services)
    {
        #region PartRSerialization_RegisterFormat
        services.AddRpc().ConfigureWireFormats(f => f.Add(
            new RpcWireFormat(
                "custom",
                () => new MyArgumentListSerializer(),
                peer => new MyMessageSerializer(peer),
                DataFormat.Bytes)));
        #endregion
    }

    public static void RemoveOldFormats(IServiceCollection services)
    {
        #region PartRSerialization_RemoveFormats
        services.AddRpc().ConfigureWireFormats(f => f.Remove("msgpack5", "msgpack5c"));
        #endregion
    }
}

// ============================================================================
// RpcSerializableAttribute
// ============================================================================

#region PartRSerialization_RpcSerializableAttribute
// The underlying serializers handle polymorphism via union attributes,
// so we mark this type as RPC-serializable to opt out of TypeRef wrapping.
[RpcSerializable]
[MessagePackObject]
[Union(0, typeof(ShapeCircle))]
[Union(1, typeof(ShapeRect))]
[JsonDerivedType(typeof(ShapeCircle), "circle")]
[JsonDerivedType(typeof(ShapeRect), "rect")]
public abstract partial class Shape
{
    [DataMember, Key(0)]
    public string? Name { get; set; }
}

[DataContract, MessagePackObject]
public partial class ShapeCircle : Shape
{
    [DataMember, Key(1)]
    public double Radius { get; set; }
}

[DataContract, MessagePackObject]
public partial class ShapeRect : Shape
{
    [DataMember, Key(1)]
    public double Width { get; set; }

    [DataMember, Key(2)]
    public double Height { get; set; }
}
#endregion

// ============================================================================
// DocPart class
// ============================================================================

public class PartRSerialization : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Reference verification");

        // Core types
        _ = typeof(RpcWireFormat);
        _ = typeof(ArgumentListSerializer);
        _ = typeof(RpcMessageSerializer);
        _ = typeof(RpcPeer);

        // Built-in formats
        var formats = RpcDefaultWireFormats.All;

        // Text formats (JSON)
        var json5 = formats.First(f => f.Key == "json5");
        var json5np = formats.First(f => f.Key == "json5np");

        // Binary formats (MessagePack)
        var msgpack6c = formats.First(f => f.Key == "msgpack6c");
        var msgpack6 = formats.First(f => f.Key == "msgpack6");
        var msgpack5c = formats.First(f => f.Key == "msgpack5c");
        var msgpack5 = formats.First(f => f.Key == "msgpack5");
        // All LZ4 variants come from f.AddCompressedVariants("lz4", LZ4ByteCodec.Default)
        var msgpack6cLz4 = msgpack6c.WithCompression("lz4", LZ4ByteCodec.Default);

        // Package-provided formats
        _ = RpcNerdbankWireFormats.NerdbankMessagePackV6C;
        if (RpcDefaultWireFormats.DefaultFormat.Key != "msgpack6")
            throw new InvalidOperationException("Unexpected default format.");

        WriteLine($"Built-in formats: {formats.Count}");
        WriteLine("Format keys: " + string.Join(", ", formats.Select(f => f.Key)));
        WriteLine();

        WriteLine("All RPC Wire Formats references verified successfully!");
        WriteLine();

        await Task.CompletedTask;
    }
}
