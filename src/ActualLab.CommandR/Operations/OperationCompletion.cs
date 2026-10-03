using MessagePack;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Carries an operation's <see cref="ServiceCall"/>s to whichever host applies them - and so acts
/// as the recovery carrier for an operation whose origin host didn't finish applying them itself.
/// </summary>
/// <remarks>
/// <see cref="Command"/> is the mutation these calls belong to. It is never executed - it's there
/// so the host application can route this event the same way, by registering a shard key resolver
/// that unwraps it, and so the event is identifiable in an audit. It travels wrapped because it's
/// polymorphic and this command is persisted with whichever serializer
/// <c>DbLogEntrySerializer.Format</c> selects - a binary one decorates only the root type, so a
/// nested <see cref="ICommand"/> has to carry its own.
/// </remarks>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public sealed partial record OperationCompletion(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    TypeDecoratingUniSerialized<TypeSchema.Any, ICommand?> SerializedCommand,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] ServiceCall[] InvalidationCalls
) : ICommand<Unit>, IBackendCommand
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ICommand? Command => SerializedCommand.Value;

    // Factories rather than constructors: ICommand? converts implicitly to
    // TypeDecoratingUniSerialized<...>, so a constructor taking one would be ambiguous with the
    // serialization constructor for every concrete command type, and every caller would need a cast.
    public static OperationCompletion New(Operation operation)
        => New(operation.Command, operation.InvalidationCalls.ToArray());

    public static OperationCompletion New(ICommand? command, ServiceCall[] invalidationCalls)
        => new(TypeDecoratingUniSerialized.New<TypeSchema.Any, ICommand?>(command), invalidationCalls);

    public override string ToString()
        => $"{nameof(OperationCompletion)}({Command?.GetType().GetName() ?? "n/a"}, "
            + $"{InvalidationCalls.Length} invalidation call(s))";
}
