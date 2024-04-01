namespace ActualLab.Fusion.Operations.Internal;

[method: Newtonsoft.Json.JsonConstructor]
public readonly record struct NestedOperation(ICommand Command, OptionSet Items);
