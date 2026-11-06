namespace ActualLab.Rpc.TypeScript;

/// <summary>
/// Options for <see cref="RpcTypeScriptGenerator"/>.
/// </summary>
public sealed record RpcTypeScriptGeneratorOptions
{
    public static readonly IReadOnlyDictionary<string, string> DefaultKnownTypes
        = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["ActualLab.Fusion.Session"] = "string",
            ["ActualLab.Serialization.JsonString"] = "unknown",
        };

    public static RpcTypeScriptGeneratorOptions Default { get; set; } = new();

    // Must match the options of the serializer the TypeScript client talks to ("json5np" by default)
    public JsonSerializerOptions JsonOptions { get; init; } = SystemJsonSerializer.DefaultOptions;
    // Keyed by Type.FullName; for types serialized by custom converters the generator can't probe
    public IReadOnlyDictionary<string, string> KnownTypes { get; init; } = DefaultKnownTypes;
    public string CoreModule { get; init; } = "@actuallab/core";
    public string RpcModule { get; init; } = "@actuallab/rpc";
    public string SourceName { get; init; } = "";
}
