using Newtonsoft.Json;

namespace ActualLab.CommandR.Operations;

[JsonObject(MemberSerialization.OptOut)]
[method: Newtonsoft.Json.JsonConstructor]
public readonly record struct NestedOperation(ICommand Command, PropertyBag Items);
