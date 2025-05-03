using Newtonsoft.Json;

namespace ActualLab.CommandR.Operations;

[StructLayout(LayoutKind.Auto)]
[JsonObject(MemberSerialization.OptOut)]
[method: Newtonsoft.Json.JsonConstructor]
public sealed record NestedOperation(ICommand Command, PropertyBag Items);
