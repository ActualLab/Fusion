using Newtonsoft.Json;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Represents a nested command operation recorded during execution of a parent operation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[JsonObject(MemberSerialization.OptOut)]
[method: Newtonsoft.Json.JsonConstructor]
public sealed record NestedOperation(ICommand Command, PropertyBag Items);
