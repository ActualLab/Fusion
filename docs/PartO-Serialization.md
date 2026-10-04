# Operations Framework Serialization

The Operations Framework's own two tables &ndash; `_Operations` and `_Events` &ndash; serialize their
payloads themselves, outside of DI. This document covers how they do it and how to change it. It
applies to those two tables only; application entities serialize however you configure them.


## Storage

Every payload has **two columns**, one text and one binary, and only one of them is written:

| Entity | Payload | Text column | Binary column |
|--------|---------|-------------|---------------|
| `DbOperation` | The command that triggered the operation | `CommandJson` | `CommandData` |
| `DbOperation` | The recorded invalidation calls ([`Replicated` mode](./PartO-IM.md#replicated)) | `InvalidationCallsJson` | `InvalidationCallsData` |
| `DbEvent` | The event's value | `ValueJson` | `ValueData` |

### Format

`DbLogEntrySerializer.Format` is the single switch that moves both tables between the two. It's a
DI-registered service, so an application replaces it the usual way:

<!-- snippet: PartOSerialization_Format -->
```cs
// One switch for both _Operations and _Events: it moves new rows between each payload's
// text and binary column, and nothing else. Which serializer each column uses is a
// separate decision.
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    Format = DataFormat.Text,
});
```
<!-- endSnippet -->

It defaults to `DataFormat.Bytes`, i.e. MessagePack. A log entry can serialize itself
without DI, so `DbLogEntrySerializer.Default` is what one falls back to when it wasn't handed a
serializer &ndash; setting that static is the no-DI way to change the default. The registration is
storage-wide rather than per-`DbContext`: a format is a property of the schema.

**A read takes whichever column carries the payload**, not the one `Format` names. Flipping the
switch therefore only changes what *new* rows look like: rows written under the old format stay
readable, and a deployment can move between the two without a data migration.

### Dropping the unused columns

That safety costs one always-`NULL` column per payload. A schema can map the ones its format never
writes away:

<!-- snippet: PartOSerialization_IgnoreUnusedColumns -->
```cs
// Passing the serializer rather than a bare DataFormat: it can't disagree with the
// one the app registered, and mapping away the column the writer uses would lose
// the payload silently.
modelBuilder.IgnoreUnusedOperationsFrameworkColumns(DbLogEntrySerializer.Default);
```
<!-- endSnippet -->

**`DbEvent.ValueJson` is the exception: it stays mapped even under `DataFormat.Bytes`**, because
`DbLogEntrySerializer.MustDeserializeLegacyEvents` defaults to `true`. An event is the one payload
whose row can outlive a format change by an unbounded margin &ndash; a delayed event sits at
`State = New` until `DelayUntil` arrives, which may be months &ndash; and a read prefers `ValueData`
and falls back to `ValueJson`, so keeping it costs one always-`NULL` column and nothing else. Set the
property to `false` once no event predates the current format:

<!-- snippet: PartOSerialization_NoLegacyEvents -->
```cs
// Only once no event predates the current format: this drops DbEvent.ValueJson from
// the model, and with it every event whose payload still lives there.
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    MustDeserializeLegacyEvents = false,
});
```
<!-- endSnippet -->

`_Operations` needs no such latch: its rows are consumed by the readers and then removed by the
trimmer on age, so nothing older than `MaxEntryAge` survives to be read.

Otherwise this trades the migration-free switch for the leaner schema: rows in the other format
become unreadable, because the column holding them is no longer in the model. To change format
later, stop calling this first, deploy, and only drop the old column once nothing needs it.

### Serializers

`Format` picks a column, not a serializer &ndash; each column has its own, and all three are set
independently on the same object:

<!-- snippet: PartOSerialization_DefaultSerializer -->
```cs
// Each column has its own serializer. The binary one is used by default;
// the text one only when Format says so.
IByteSerializer byteSerializer = DbLogEntrySerializer.Default.ByteSerializer;
ITextSerializer textSerializer = DbLogEntrySerializer.Default.TextSerializer;
```
<!-- endSnippet -->

Both must preserve the concrete type of a polymorphic payload, because a command and an event's
value are only known as `ICommand` and `object` when they're read back:

- The binary default is `MessagePackByteSerializer.DefaultTypeDecorating`. MessagePack has no
  equivalent of `TypeNameHandling`, so the type is written alongside the payload by the
  `TypeDecoratingByteSerializer` wrapper &ndash; drop the wrapper and polymorphic payloads stop
  round-tripping.
- The text default is `NewtonsoftJsonSerializer.Default`, which carries the type itself via
  `TypeNameHandling.Auto`. Newtonsoft is the text choice because it's forgiving with missing and
  extra properties during schema evolution.


## Invalidation Serialization

A handler in [`Replicated` mode](./PartO-IM.md#replicated) records its invalidation calls into
`Operation.InvalidationCalls`, and they're serialized to whichever invalidations column `Format` names:

<!-- snippet: PartOSerialization_InvalidationsSerialization -->
```cs
// How the recorded invalidation calls are serialized to DbOperation: an array rather
// than the model's ImmutableList, because MessagePack's standard resolvers don't know
// the immutable collections
var (InvalidationCallsJson, InvalidationCallsData) = operation.InvalidationCalls.Count == 0
    ? default
    : serializer.Serialize(operation.InvalidationCalls.ToArray());
```
<!-- endSnippet -->

### ServiceCall

Each recorded call is a `ServiceCall` &ndash; a service, a method, and the arguments to
invalidate with:

<!-- snippet: PartOSerialization_ServiceCall -->
```cs
// The service the call targets, without assembly versions - so a call
// survives an assembly version bump between the hosts that write and read it
TypeRef serviceType = call.ServiceType;
// The RPC-style method name, which carries the parameter count as its suffix
string methodName = call.MethodName;
// The arguments, deserialized against the resolved method's signature
ArgumentList arguments = call.Arguments;
```
<!-- endSnippet -->

The arguments are decoded eagerly, against the signature of the method the call names. A call whose
service or method no longer exists can't be decoded, so it's dropped on apply rather than applied to
the wrong thing &ndash; see [Invalidation Modes](./PartO-IM.md) for what that means for
a rolling deployment.


## Customizing Serialization

### Changing the Default Serializer

To use different serializer settings:

<!-- snippet: PartOSerialization_ChangeSerializer -->
```cs
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
```
<!-- endSnippet -->

### Using Type-Decorated Serializer

For explicit type information in the JSON:

<!-- snippet: PartOSerialization_TypeDecoratedSerializer -->
```cs
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    TextSerializer = new TypeDecoratingTextSerializer(
        new NewtonsoftJsonSerializer(customSettings)),
    // The binary one is type-decorating out of the box - MessagePack has no equivalent of
    // Newtonsoft's TypeNameHandling, so the concrete type has to be written alongside
    ByteSerializer = MessagePackByteSerializer.DefaultTypeDecorating,
});
```
<!-- endSnippet -->

This produces JSON like:
```json
/* @type MyNamespace.MyCommand, MyAssembly */ {"property": "value"}
```


## Command Serialization

A command is stored in `CommandData`, or in `CommandJson` when `DbLogEntrySerializer.Format` is
`DataFormat.Text`, and carries type information either way so it can be deserialized on the host
that reads it:

<!-- snippet: PartOSerialization_CommandRecord -->
```cs
// Command types must be serializable
[DataContract, MemoryPackable, MessagePackObject]
public sealed partial record CreateTodoCommand(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Title,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string? Description
) : ICommand<Todo>;
```
<!-- endSnippet -->

### Annotating Command Types

For reliable serialization across Operations Framework, RPC, and other subsystems, annotate commands
with all serialization attributes:

<!-- snippet: PartOSerialization_FullyAnnotatedCommand -->
```cs
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public sealed partial record MyCommand(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Id,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] string Data
) : ICommand<string>
{
    [System.Text.Json.Serialization.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public MyCommand() : this("", "") { }
}
```
<!-- endSnippet -->


## Schema Evolution

When evolving command schemas:

1. **Add new properties as optional** with default values
2. **Don't remove properties** from persisted commands (old operations may need reprocessing)
3. **Don't change property types** without a migration strategy

<!-- snippet: PartOSerialization_SchemaV1 -->
```cs
public record CreateUserCommandV1(string Name) : ICommand<User>;
```
<!-- endSnippet -->

<!-- snippet: PartOSerialization_SchemaV2 -->
```cs
public record CreateUserCommandV2(
    string Name,
    string? Email = null  // New optional property
) : ICommand<User>;
```
<!-- endSnippet -->

### Deployment Compatibility Contract

`DbOperation` persists the concrete command and its recorded invalidation calls as a polymorphic
payload &ndash; type-decorating MessagePack by default, or Newtonsoft JSON when
`DbLogEntrySerializer.Format` is `DataFormat.Text` &ndash; and other hosts deserialize them to apply
that invalidation. This ties command
serialization to your deployment process:

- A command type (or any type its recorded invalidation calls carry) **must remain deserializable
  for at least `MaxEntryAge`** (30 minutes by default &ndash; see [Operation Log Trimmer](./PartO-CS.md))
  **past its last producer**. In practice: don't rename or remove a command type and deploy that change
  within the same window; stage such changes across releases instead (e.g. keep the old type around,
  deprecated, for one extra release).
- If this contract is violated, deserialization fails on the reading host, and the operation eventually
  gets abandoned with a single **Error-level** log once its bounded retry budget is exhausted &ndash;
  it does *not* fail silently, but it also doesn't self-heal. Losing the invalidation for that operation
  means dependent caches on that host go stale until something else invalidates them.
- There's no built-in type-alias/rename mapping in the serialization binder today; if a specific rename
  can't be staged across releases, add one on demand rather than up front.

## Troubleshooting

### Missing Type Information

If deserialization fails with "Could not determine type", ensure:
- The type is in a loaded assembly
- Type names haven't changed (namespace, class name)
- `TypeNameHandling.Auto` is enabled in Newtonsoft.Json settings

### Invalidation Calls Not Applying

Check that:
- The service is registered on the host reading the log &ndash; a record for a service it doesn't
  have is dropped by design
- The method still exists with the same name and parameter count
- The argument types are serializable and haven't changed shape


## Related Topics

- [Core Serialization](./PartS.md) - General serialization infrastructure
- [Operations Framework](./PartO.md) - Operations Framework overview
- [Reprocessing](./PartO-RP.md) - How failed operations are retried
