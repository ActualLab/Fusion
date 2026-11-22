using System.Buffers;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActualLab.Api;
using ActualLab.Collections;
using ActualLab.Collections.Internal;
using ActualLab.Rpc;
using ActualLab.Serialization;
using MemoryPack;
using MessagePack;
using Newtonsoft.Json;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartS;

// Fake types for snippet compilation
public class MyClass
{
    public string Name { get; set; } = "";
}

[DataContract, MessagePackObject]
public partial class MyPayload { }
public class MyObject { }

// ============================================================================
// Core Abstractions - conceptual interfaces (actual ones are in ActualLab.Core)
// ============================================================================

/*
#region PartS_IByteSerializer
public interface IByteSerializer
{
    object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength);
    void Write(IBufferWriter<byte> bufferWriter, object? value, Type type);
}
#endregion

#region PartS_ITextSerializer
public interface ITextSerializer
{
    object? Read(string data, Type type);
    string Write(object? value, Type type);
}
#endregion
*/

// ============================================================================
// Default Serializers
// ============================================================================

public static class DefaultSerializers
{
    public static void Example()
    {
        #region PartS_DefaultSerializers
        // Binary serializer - MessagePack
        IByteSerializer binary = ByteSerializer.Default;

        // Text serializer - System.Text.Json
        ITextSerializer text = TextSerializer.Default;
        #endregion

        #region PartS_ChangeDefaultSerializer
        // Use Newtonsoft.Json as default text serializer
        // (requires ActualLab.Serialization.NewtonsoftJson)
        TextSerializer.Default = NewtonsoftJsonSerializer.Default;
        #endregion
    }

    public static void SerializerInstances()
    {
        #region PartS_SerializerInstances
        // Access default instances
        var msgPack = MessagePackByteSerializer.Default;
        var sysJson = SystemJsonSerializer.Default;
        var memPack = MemoryPackByteSerializer.Default;   // ActualLab.Serialization.MemoryPack
        var newtonsoft = NewtonsoftJsonSerializer.Default; // ActualLab.Serialization.NewtonsoftJson

        // Create with custom options
        var prettyJson = new SystemJsonSerializer(new JsonSerializerOptions { WriteIndented = true });
        #endregion
    }
}

// ============================================================================
// Type-Decorated Serialization
// ============================================================================

public static class TypeDecoratedSerialization
{
    public static void TextSerializerExample()
    {
        object value = new MyClass { Name = "test" };

        #region PartS_TypeDecoratingTextSerializer
        var serializer = TypeDecoratingTextSerializer.Default;

        // Serialize
        string json = serializer.Write(value, typeof(object));
        // Output: /* @type MyNamespace.MyClass, MyAssembly */ {"name":"test"}

        // Deserialize - type is recovered from the prefix
        object? result = serializer.Read(json, typeof(object));
        // result is MyClass
        #endregion
    }

    public static void ByteSerializerExample()
    {
        object value = new MyClass { Name = "test" };

        #region PartS_TypeDecoratingByteSerializer
        var serializer = TypeDecoratingByteSerializer.Default;

        // Type info is binary-encoded before the payload
        var buffer = serializer.Write(value, typeof(object));
        object? result = serializer.Read(buffer.WrittenMemory, typeof(object), out _);
        #endregion
    }

    public static void TypeDecoratedInstances()
    {
        #region PartS_TypeDecoratedInstances
        var sysJsonTD = SystemJsonSerializer.DefaultTypeDecorating;
        var msgPackTD = MessagePackByteSerializer.DefaultTypeDecorating;
        var newtonsoftTD = NewtonsoftJsonSerializer.DefaultTypeDecorating;
        var memPackTD = MemoryPackByteSerializer.DefaultTypeDecorating;
        #endregion
    }
}

// ============================================================================
// Serialized<T> Wrappers
// ============================================================================

#region PartS_ByteSerializedMessage
[DataContract, MessagePackObject]
public partial record MyMessage(
    [property: DataMember(Order = 0), Key(0)] ByteSerialized<MyPayload> Payload);
#endregion

public static class SerializedWrappers
{
    public static void ByteSerializedExample()
    {
        var myPayload = new MyPayload();
        byte[] bytes = Array.Empty<byte>();

        #region PartS_ByteSerializedUsage
        // Create with a value - serialization is deferred
        var wrapper1 = ByteSerialized.New(myPayload);

        // Or create from serialized data - deserialization is deferred
        var wrapper2 = ByteSerialized.New<MyPayload>(bytes);

        // Access triggers (de)serialization
        MyPayload value = wrapper1.Value;
        ReadOnlyMemory<byte> data = wrapper1.Data;
        #endregion
    }

    public static void TextSerializedExample()
    {
        var myObject = new MyObject();

        #region PartS_TextSerializedUsage
        var wrapper = TextSerialized.New(myObject);
        string json = wrapper.Data;    // Serialize on access
        MyObject value = wrapper.Value; // Deserialize on access
        #endregion
    }
}

// ============================================================================
// UniSerialized<T> - showing the conceptual structure (not compiled)
// ============================================================================

// The actual UniSerialized<T> is in ActualLab.Core. Below is a conceptual overview.
// The real implementation handles cross-format serialization by exposing different
// properties for each serializer.

/*
#region PartS_UniSerializedStructure
[JsonConverter(typeof(UniSerializedJsonConverter))]
[MessagePackFormatter(typeof(UniSerializedMessagePackFormatter<>))]
public readonly struct UniSerialized<T>
{
    public T Value { get; init; }
}

// Every format's converter writes Value as a nested payload produced by the serializer
// that serializes the UniSerialized<T> itself (ByteSerializer.Current), wrapped as:
// - System.Text.Json:      { "json": "..." }
// - Newtonsoft.Json:       { "NewtonsoftJson": "..." }
// - MessagePack, Nerdbank: [ bin ]
// - MemoryPack:            a version-tolerant object with a single byte array member
#endregion

*/

public static class UniSerializedExamples
{
    public static void TypeDecoratingUniSerializedExample()
    {
        object myValue = new MyClass();

        #region PartS_TypeDecoratingUniSerialized
        // Used by PropertyBag for storing heterogeneous values
        var item = TypeDecoratingUniSerialized.New<TypeSchema.Any, object>(myValue);
        #endregion
    }
}

// ============================================================================
// PropertyBag Serialization - conceptual structure
// ============================================================================

// The actual PropertyBagItem is in ActualLab.Collections.Internal namespace
// Below shows the structure:
/*
#region PartS_PropertyBagItem
// Internal structure of PropertyBagItem
[DataContract]
public partial record struct PropertyBagItem<TSchema>(
    [property: DataMember(Order = 0), Key(0)] string Key,
    [property: DataMember(Order = 1), Key(1)] TypeDecoratingUniSerialized<TSchema, object> Serialized)
    where TSchema : TypeSchema, new();
#endregion
*/

// ============================================================================
// Annotating Types for Serialization
// ============================================================================

#region PartS_AnnotatedRecord
[DataContract, MessagePackObject]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor]
public sealed partial record MyRecord(
    [property: DataMember(Order = 0), Key(0)] string Name,
    [property: DataMember(Order = 1), Key(1)] int Value);
#endregion

#region PartS_AnnotatedRecordMemoryPack
// Only if you use MemoryPack (ActualLab.Serialization.MemoryPack)
[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record MyMemoryPackRecord(
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0)] string Name,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1)] int Value);
#endregion

#region PartS_MemoryPackAllowSerialize
[DataContract, MessagePackObject, MemoryPackable(GenerateType.VersionTolerant)]
[method: System.Text.Json.Serialization.JsonConstructor, SerializationConstructor, MemoryPackConstructor]
public sealed partial record Note(
    // ActualLab types: [MemoryPackAllowSerialize] is required since v16
    [property: DataMember(Order = 0), Key(0), MemoryPackOrder(0), MemoryPackAllowSerialize] Session Session,
    [property: DataMember(Order = 1), Key(1), MemoryPackOrder(1), MemoryPackAllowSerialize] Symbol Tag,
    [property: DataMember(Order = 2), Key(2), MemoryPackOrder(2), MemoryPackAllowSerialize] Option<string> Title,
    // Not needed for ApiArray/ApiList/ApiSet/ApiMap, unmanaged structs
    // and Option/ApiOption/ApiNullable over an unmanaged T
    [property: DataMember(Order = 3), Key(3), MemoryPackOrder(3)] ApiArray<string> Lines,
    [property: DataMember(Order = 4), Key(4), MemoryPackOrder(4)] Option<int> Priority);
#endregion

// ============================================================================
// SerializerKind
// ============================================================================

public static class SerializerKindExamples
{
    public static void Example()
    {
        #region PartS_SerializerKindUsage
        // Get default serializer for a kind
        IByteSerializer serializer = SerializerKind.MessagePack.GetDefaultSerializer();

        // Get type-decorated variant
        IByteSerializer tdSerializer = SerializerKind.MessagePack.GetDefaultTypeDecoratingSerializer();

        // MemoryPack and NewtonsoftJson are available once their package registers them
        if (SerializerKind.MemoryPack.IsAvailable())
            serializer = SerializerKind.MemoryPack.GetDefaultSerializer();
        #endregion
    }
}

// ============================================================================
// Configuration
// ============================================================================

public static class SerializerConfiguration
{
    public static void Example()
    {
        #region PartS_GlobalConfiguration
        // Change default binary serializer
        ByteSerializer.Default = MessagePackByteSerializer.Default;

        // Change default text serializer (requires ActualLab.Serialization.NewtonsoftJson)
        TextSerializer.Default = NewtonsoftJsonSerializer.Default;

        // Change System.Text.Json options
        SystemJsonSerializer.DefaultOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        // Change Newtonsoft.Json settings
        NewtonsoftJsonSerializer.DefaultSettings = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore,
            // Keep it: ActualLab types carry no Newtonsoft.Json attributes since v16
            ContractResolver = new NewtonsoftJsonContractResolver(),
        };
        #endregion
    }
}

// ============================================================================
// Upgrading to v16
// ============================================================================

public static class UpgradingToV16
{
    public static void RpcFormats(IServiceCollection services)
    {
        #region PartS_UpgradeRpcFormats
        services.AddRpc().ConfigureWireFormats(f => {
            f.AddMemoryPack();     // ActualLab.Serialization.MemoryPack: mempack5(c), mempack6(c)
            f.AddNewtonsoftJson(); // ActualLab.Serialization.NewtonsoftJson: njson5, njson5np
            f.AddLZ4Variants(); // "-lz4" variants of the formats added so far
            f.DefaultFormat = f["msgpack6c-lz4"];
        });
        #endregion
    }
}

// ============================================================================
// DocPart class
// ============================================================================

public class PartS : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Reference verification");

        // Core interfaces
        _ = typeof(IByteSerializer);
        _ = typeof(ITextSerializer);
        _ = typeof(IByteSerializer<>);
        _ = typeof(ITextSerializer<>);

        // Serializer classes
        _ = typeof(MemoryPackByteSerializer);
        _ = typeof(MessagePackByteSerializer);
        _ = typeof(SystemJsonSerializer);
        _ = typeof(NewtonsoftJsonSerializer);

        // Type-decorated serializers
        _ = typeof(TypeDecoratingTextSerializer);
        _ = typeof(TypeDecoratingByteSerializer);

        // Serialized wrappers
        _ = typeof(ByteSerialized<>);
        _ = typeof(TextSerialized<>);
        _ = typeof(MemoryPackSerialized<>);
        _ = typeof(MessagePackSerialized<>);
        _ = typeof(SystemJsonSerialized<>);
        _ = typeof(NewtonsoftJsonSerialized<>);

        // UniSerialized types
        _ = typeof(UniSerialized<>);
        _ = typeof(TypeDecoratingUniSerialized<,>);

        // PropertyBag
        _ = typeof(PropertyBag);
        _ = typeof(MutablePropertyBag);
        _ = typeof(PropertyBagItem);

        // SerializerKind
        _ = typeof(SerializerKind);

        // Package registration
        MemoryPackSerialization.Register();
        NewtonsoftJsonSerialization.Register();
        _ = typeof(TypeDecoratingMemoryPackSerialized<,>);

        // Defaults
        if (ByteSerializer.Default != MessagePackByteSerializer.Default)
            throw new InvalidOperationException("ByteSerializer.Default must be MessagePack.");
        _ = ByteSerializer.Default;
        _ = TextSerializer.Default;

        WriteLine("All serialization references verified successfully!");
        WriteLine();

        await Task.CompletedTask;
    }
}
