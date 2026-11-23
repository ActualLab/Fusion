namespace ActualLab.Serialization;

/// <summary>
/// Lists the types the source-generated System.Text.Json contexts of an assembly cover.
/// ActualLab.Generators emits it for the assemblies it runs for, and reads it from the referenced ones:
/// the contexts themselves are usually internal, so referencing compilations can't see them.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class JsonSerializableTypesAttribute(params Type[] types) : Attribute
{
    public Type[] Types { get; } = types;
}
