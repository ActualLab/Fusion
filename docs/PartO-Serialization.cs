using System.Runtime.Serialization;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Interception;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using MessagePack;
using Newtonsoft.Json;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartOSerialization;

// Fake types for snippet compilation
public record Todo(string Id, string Title, string? Description);
public record User(string Id, string Name, string? Email);
public class MyCustomConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => throw new NotImplementedException();
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        => throw new NotImplementedException();
    public override bool CanConvert(Type objectType) => false;
}

public class MyDto { }

// ============================================================================
// DbOperation Storage
// ============================================================================

public static class DbOperationStorage
{
    public static void DefaultSerializerExample()
    {
        #region PartOSerialization_DefaultSerializer
        // Each column has its own serializer. The binary one is used by default;
        // the text one only when Format says so.
        IByteSerializer byteSerializer = DbLogEntrySerializer.Default.ByteSerializer;
        ITextSerializer textSerializer = DbLogEntrySerializer.Default.TextSerializer;
        #endregion
        _ = (byteSerializer, textSerializer);
    }

    public static void FormatExample(IServiceCollection services)
    {
        #region PartOSerialization_Format
        // One switch for both _Operations and _Events: it moves new rows between each payload's
        // text and binary column, and nothing else. Which serializer each column uses is a
        // separate decision.
        services.AddSingleton(_ => DbLogEntrySerializer.Default with {
            Format = DataFormat.Text,
        });
        #endregion
    }

    public static void IgnoreUnusedColumnsExample(ModelBuilder modelBuilder)
    {
        #region PartOSerialization_IgnoreUnusedColumns
        // Passing the serializer rather than a bare DataFormat: it can't disagree with the
        // one the app registered, and mapping away the column the writer uses would lose
        // the payload silently.
        modelBuilder.IgnoreUnusedOperationsFrameworkColumns(DbLogEntrySerializer.Default);
        #endregion
    }

    public static void NoLegacyEventsExample(IServiceCollection services)
    {
        #region PartOSerialization_NoLegacyEvents
        // Only once no event predates the current format: this drops DbEvent.ValueJson from
        // the model, and with it every event whose payload still lives there.
        services.AddSingleton(_ => DbLogEntrySerializer.Default with {
            MustDeserializeLegacyEvents = false,
        });
        #endregion
    }

    public static void InvalidationsSerializationExample(
        Operation operation, DbLogEntrySerializer serializer)
    {
        #region PartOSerialization_InvalidationsSerialization
        // How the recorded invalidation calls are serialized to DbOperation: an array rather
        // than the model's ImmutableList, because MessagePack's standard resolvers don't know
        // the immutable collections
        var (InvalidationCallsJson, InvalidationCallsData) = operation.InvalidationCalls.Count == 0
            ? default
            : serializer.Serialize(operation.InvalidationCalls.ToArray());
        #endregion
        _ = (InvalidationCallsJson, InvalidationCallsData);
    }
}

// ============================================================================
// ServiceCall - what a serialized invalidation call carries
// ============================================================================

public static class ServiceCallUsage
{
    public static void CallShape(ServiceCall call)
    {
        #region PartOSerialization_ServiceCall
        // The service the call targets, without assembly versions - so a call
        // survives an assembly version bump between the hosts that write and read it
        TypeRef serviceType = call.ServiceType;
        // The RPC-style method name, which carries the parameter count as its suffix
        string methodName = call.MethodName;
        // The arguments, deserialized against the resolved method's signature
        ArgumentList arguments = call.Arguments;
        #endregion
        _ = (serviceType, methodName, arguments);
    }
}

// ============================================================================
// Customizing Serialization
// ============================================================================

public static class CustomizingSerialization
{
    public static void ChangeDefaultSerializer(IServiceCollection services)
    {
        #region PartOSerialization_ChangeSerializer
        // Registered, so it replaces DbLogEntrySerializer.Default for this application
        services.AddSingleton(_ => DbLogEntrySerializer.Default with {
            TextSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                // Add custom converters if needed
                Converters = { new MyCustomConverter() },
            }),
        });
        #endregion
    }

    public static void UseTypeDecoratedSerializer(IServiceCollection services)
    {
        JsonSerializerSettings customSettings = new();

        #region PartOSerialization_TypeDecoratedSerializer
        services.AddSingleton(_ => DbLogEntrySerializer.Default with {
            TextSerializer = new TypeDecoratingTextSerializer(
                new NewtonsoftJsonSerializer(customSettings)),
            // The binary one is type-decorating out of the box - MessagePack has no equivalent of
            // Newtonsoft's TypeNameHandling, so the concrete type has to be written alongside
            ByteSerializer = MessagePackByteSerializer.DefaultTypeDecorating,
        });
        #endregion
    }
}

// ============================================================================
// Command Serialization
// ============================================================================

#region PartOSerialization_CommandRecord
// Command types must be serializable
[DataContract, MemoryPackable, MessagePackObject]
public sealed partial record CreateTodoCommand(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Title,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string? Description
) : ICommand<Todo>;
#endregion

#region PartOSerialization_FullyAnnotatedCommand
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public sealed partial record MyCommand(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Id,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] string Data
) : ICommand<string>
{
    [System.Text.Json.Serialization.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public MyCommand() : this("", "") { }
}
#endregion

// ============================================================================
// Schema Evolution
// ============================================================================

// Version 1
#region PartOSerialization_SchemaV1
public record CreateUserCommandV1(string Name) : ICommand<User>;
#endregion

// Version 2 - safe evolution
#region PartOSerialization_SchemaV2
public record CreateUserCommandV2(
    string Name,
    string? Email = null  // New optional property
) : ICommand<User>;
#endregion

// ============================================================================
// DocPart class
// ============================================================================

public class PartOSerialization : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Reference verification");

        // Core types
        _ = typeof(DbOperation);
        _ = typeof(Operation);
        _ = typeof(ServiceCall);

        // Serializers
        _ = typeof(NewtonsoftJsonSerializer);
        _ = typeof(TypeDecoratingTextSerializer);
        _ = typeof(TypeDecoratingUniSerialized<,>);

        // Log entry serialization
        _ = DbLogEntrySerializer.Default.Format;
        _ = DbLogEntrySerializer.Default.ByteSerializer;
        _ = DbLogEntrySerializer.Default.TextSerializer;

        // Command interface
        _ = typeof(ICommand<>);

        WriteLine("All Operations Framework Serialization references verified successfully!");
        WriteLine();

        await Task.CompletedTask;
    }
}
