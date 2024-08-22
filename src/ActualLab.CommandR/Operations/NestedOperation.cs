using Newtonsoft.Json;

namespace ActualLab.CommandR.Operations;

[StructLayout(LayoutKind.Auto)]
[JsonObject(MemberSerialization.OptOut)]
[method: Newtonsoft.Json.JsonConstructor]
public readonly record struct NestedOperation(ICommand Command, PropertyBag Items);
