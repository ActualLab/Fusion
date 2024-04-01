namespace ActualLab.Fusion.Operations.Internal;

[method: Newtonsoft.Json.JsonConstructor]
public readonly record struct NestedCommand(ICommand Command, OptionSet Items);
