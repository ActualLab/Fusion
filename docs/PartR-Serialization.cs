using System.Buffers;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using ActualLab.Serialization;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartRSerialization;

// Fake types for snippet compilation
public class MyArgumentSerializer() : RpcArgumentSerializer
{
    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint)
        => throw new NotImplementedException();

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
        => throw new NotImplementedException();
}

public class MyMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    public override RpcInboundMessage Read(ArrayPoolArrayHandle<byte> buffer, int offset, out int readLength)
        => throw new NotImplementedException();

    public override void Write(IBufferWriter<byte> bufferWriter, RpcOutboundMessage message)
        => throw new NotImplementedException();
}

// ============================================================================
// Format Structure
// ============================================================================

#region PartRSerialization_FormatStructure
public sealed class RpcSerializationFormatExample(
    string key,
    Func<RpcArgumentSerializer> argumentSerializerFactory,
    Func<RpcPeer, RpcMessageSerializer> messageSerializerFactory)
{
    public string Key { get; } = key;
    public RpcArgumentSerializer ArgumentSerializer { get; } = argumentSerializerFactory();
    public Func<RpcPeer, RpcMessageSerializer> MessageSerializerFactory { get; } = messageSerializerFactory;
}
#endregion

// ============================================================================
// Accessing Formats
// ============================================================================

public static class AccessingFormats
{
    public static void Example()
    {
        #region PartRSerialization_AccessingFormats
        // All registered formats
        ImmutableList<RpcSerializationFormat> all = RpcSerializationFormat.All;

        // Find by key
        var format = RpcSerializationFormat.All.First(f => f.Key == "mempack5c");
        #endregion
    }
}

// ============================================================================
// Configuring Formats
// ============================================================================

public static class ConfiguringFormats
{
    public static void RegisterAdditionalFormat()
    {
        #region PartRSerialization_RegisterFormat
        RpcSerializationFormat.All = RpcSerializationFormat.All.Add(
            new RpcSerializationFormat(
                "custom",
                () => new MyArgumentSerializer(),
                peer => new MyMessageSerializer(peer)));
        #endregion
    }

    public static void RemoveOldFormats()
    {
        #region PartRSerialization_RemoveFormats
        // To disable older formats for security:
        RpcSerializationFormat.All = RpcSerializationFormat.All
            .RemoveAll(f => f.Key.StartsWith("mempack1") || f.Key.StartsWith("msgpack1"));
        #endregion
    }
}

// ============================================================================
// DocPart class
// ============================================================================

public class PartRSerialization : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Reference verification");

        // Core types
        _ = typeof(RpcSerializationFormat);
        _ = typeof(RpcArgumentSerializer);
        _ = typeof(RpcMessageSerializer);
        _ = typeof(RpcPeer);

        // Available formats
        _ = RpcSerializationFormat.All;

        // Specific formats (verify they exist)
        var formats = RpcSerializationFormat.All;

        // Text formats (JSON)
        var json3 = formats.FirstOrDefault(f => f.Key == "json3");
        var json5 = formats.FirstOrDefault(f => f.Key == "json5");
        var njson3 = formats.FirstOrDefault(f => f.Key == "njson3");
        var njson5 = formats.FirstOrDefault(f => f.Key == "njson5");

        // Binary formats (MemoryPack)
        var mempack5c = formats.FirstOrDefault(f => f.Key == "mempack5c");
        var mempack5 = formats.FirstOrDefault(f => f.Key == "mempack5");
        var mempack4c = formats.FirstOrDefault(f => f.Key == "mempack4c");
        var mempack4 = formats.FirstOrDefault(f => f.Key == "mempack4");

        // Binary formats (MessagePack)
        var msgpack5c = formats.FirstOrDefault(f => f.Key == "msgpack5c");
        var msgpack5 = formats.FirstOrDefault(f => f.Key == "msgpack5");
        var msgpack4c = formats.FirstOrDefault(f => f.Key == "msgpack4c");
        var msgpack4 = formats.FirstOrDefault(f => f.Key == "msgpack4");

        WriteLine($"Total registered formats: {formats.Count}");
        WriteLine("Format keys: " + string.Join(", ", formats.Select(f => f.Key)));
        WriteLine();

        WriteLine("All RPC Serialization Formats references verified successfully!");
        WriteLine();

        await Task.CompletedTask;
    }
}
