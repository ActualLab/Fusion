using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace ActualLab.Rpc.TypeScript;

/// <summary>
/// Maps .NET types to TypeScript type expressions using System.Text.Json metadata,
/// so the result matches the actual wire shape; collects declarations of the DTOs it meets.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runs on fully preserved contract types")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Runs on fully preserved contract types")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Runs on fully preserved contract types")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Runs on fully preserved contract types")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Runs on fully preserved contract types")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Runs on fully preserved contract types")]
public sealed class TypeScriptTypeMapper
{
    private static readonly Type[] TypeParameterMarkers = [
        typeof(TypeParameter0), typeof(TypeParameter1), typeof(TypeParameter2), typeof(TypeParameter3),
    ];
    private static readonly Dictionary<Type, string> PrimitiveTypes = new() {
        [typeof(string)] = "string",
        [typeof(char)] = "string",
        [typeof(Guid)] = "string",
        [typeof(DateTime)] = "string",
        [typeof(DateTimeOffset)] = "string",
        [typeof(DateOnly)] = "string",
        [typeof(TimeOnly)] = "string",
        [typeof(TimeSpan)] = "string",
        [typeof(Uri)] = "string",
        [typeof(Version)] = "string",
        [typeof(byte[])] = "string",
        [typeof(Memory<byte>)] = "string",
        [typeof(ReadOnlyMemory<byte>)] = "string",
        [typeof(bool)] = "boolean",
        [typeof(byte)] = "number",
        [typeof(sbyte)] = "number",
        [typeof(short)] = "number",
        [typeof(ushort)] = "number",
        [typeof(int)] = "number",
        [typeof(uint)] = "number",
        [typeof(long)] = "number",
        [typeof(ulong)] = "number",
        [typeof(float)] = "number",
        [typeof(double)] = "number",
        [typeof(decimal)] = "number",
        [typeof(Half)] = "number",
        [typeof(Int128)] = "number",
        [typeof(UInt128)] = "number",
        [typeof(object)] = "unknown",
        [typeof(JsonElement)] = "unknown",
        [typeof(JsonDocument)] = "unknown",
        [typeof(JsonNode)] = "unknown",
        [typeof(JsonObject)] = "Record<string, unknown>",
        [typeof(JsonArray)] = "unknown[]",
    };

    private readonly Dictionary<Type, string> _declaredNames = new();
    private readonly HashSet<Type> _nonGenericDefinitions = new();
    private readonly Dictionary<string, object> _ownerByName = new(StringComparer.Ordinal);
    private readonly List<string> _declarations = new();
    private readonly List<string> _warnings = new();
    private Dictionary<Type, string> _typeParameterNames = new();

    public JsonSerializerOptions JsonOptions { get; }
    public IReadOnlyDictionary<string, string> KnownTypes { get; }
    public IReadOnlyList<string> Declarations => _declarations;
    public IReadOnlyList<string> Warnings => _warnings;

    public TypeScriptTypeMapper(JsonSerializerOptions jsonOptions, IReadOnlyDictionary<string, string> knownTypes)
    {
        JsonOptions = new JsonSerializerOptions(jsonOptions) {
            // Makes JsonPropertyInfo.IsGetNullable reflect nullable reference type annotations
            RespectNullableAnnotations = true,
        };
        JsonOptions.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
        KnownTypes = knownTypes;
    }

    public string Map(Type type, bool isNullable = false)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
            return WithNull(Map(underlyingType), isNullable: true);

        return WithNull(MapNonNullable(type), isNullable && !type.IsValueType);
    }

    public string ReserveName(string name, object owner)
    {
        var result = name;
        for (var i = 2; _ownerByName.TryGetValue(result, out var existingOwner); i++) {
            if (Equals(existingOwner, owner))
                return result;

            result = name + i.ToString(CultureInfo.InvariantCulture);
        }
        _ownerByName.Add(result, owner);
        if (!ReferenceEquals(result, name))
            _warnings.Add($"TypeScript name '{name}' is already taken, so '{owner}' is declared as '{result}'.");
        return result;
    }

    public static string ArrayOf(string elementType)
        => elementType.Contains('|', StringComparison.Ordinal) ? $"({elementType})[]" : elementType + "[]";

    // Private methods

    private string MapNonNullable(Type type)
    {
        if (_typeParameterNames.TryGetValue(type, out var typeParameterName))
            return typeParameterName;
        if (type.FullName is { } fullName && KnownTypes.TryGetValue(fullName, out var knownType))
            return knownType;
        if (PrimitiveTypes.TryGetValue(type, out var primitiveType))
            return primitiveType;
        if (type.IsEnum)
            return DeclareEnum(type);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RpcStream<>)) {
            // Nested streams arrive as raw stream references (see toRpcStream in @actuallab/rpc),
            // but their item type is still declared, since it's what toRpcStream<T> gets
            Map(type.GetGenericArguments()[0]);
            return "unknown";
        }

        var typeInfo = JsonOptions.GetTypeInfo(type);
        switch (typeInfo.Kind) {
        case JsonTypeInfoKind.Enumerable:
            return ArrayOf(Map(typeInfo.ElementType!));
        case JsonTypeInfoKind.Dictionary:
            return $"Record<string, {Map(typeInfo.ElementType!)}>";
        case JsonTypeInfoKind.Object:
            return type.IsGenericType
                ? DeclareGenericInstance(type)
                : DeclareObject(type, type.Name);
        }

        if (GetEnumerableElementType(type) is { } elementType)
            return ArrayOf(Map(elementType));

        return Probe(type);
    }

    private string DeclareGenericInstance(Type type)
    {
        var definition = type.GetGenericTypeDefinition();
        var name = DeclareGenericDefinition(definition);
        if (name is null) {
            var argumentNames = type.GetGenericArguments().Select(t => StripArity(t.Name));
            return DeclareObject(type, $"{StripArity(definition.Name)}_{string.Join("_", argumentNames)}");
        }

        var arguments = type.GetGenericArguments().Select(t => Map(t));
        return $"{name}<{string.Join(", ", arguments)}>";
    }

    private string? DeclareGenericDefinition(Type definition)
    {
        if (_declaredNames.TryGetValue(definition, out var name))
            return name;
        if (_nonGenericDefinitions.Contains(definition))
            return null;

        var parameters = definition.GetGenericArguments();
        Type markedType;
        try {
            if (parameters.Length > TypeParameterMarkers.Length)
                throw new ArgumentOutOfRangeException(nameof(definition));

            markedType = definition.MakeGenericType(TypeParameterMarkers[..parameters.Length]);
        }
        catch (ArgumentException) {
            // Constraints prevent closing it with the markers, so each instance gets its own declaration
            _nonGenericDefinitions.Add(definition);
            return null;
        }

        name = ReserveName(StripArity(definition.Name), definition);
        _declaredNames.Add(definition, name);
        var oldTypeParameterNames = _typeParameterNames;
        _typeParameterNames = parameters
            .Select((p, i) => (Marker: TypeParameterMarkers[i], p.Name))
            .ToDictionary(x => x.Marker, x => x.Name);
        try {
            var body = GetObjectBody(markedType);
            var typeParameters = string.Join(", ", parameters.Select(p => p.Name));
            _declarations.Add($"export interface {name}<{typeParameters}> {body}");
        }
        finally {
            _typeParameterNames = oldTypeParameterNames;
        }
        return name;
    }

    private string DeclareObject(Type type, string preferredName)
    {
        if (_declaredNames.TryGetValue(type, out var name))
            return name;

        name = ReserveName(preferredName, type);
        _declaredNames.Add(type, name);
        var body = GetObjectBody(type);
        _declarations.Add($"export interface {name} {body}");
        return name;
    }

    private string GetObjectBody(Type type)
    {
        var properties = JsonOptions.GetTypeInfo(type).Properties;
        if (properties.Count == 0)
            return "{}";

        var sb = new StringBuilder("{\n");
        foreach (var property in properties) {
            var propertyType = Map(property.PropertyType, property.IsGetNullable);
            sb.Append("    ").Append(TypeScriptSyntax.PropertyName(property.Name))
                .Append(": ").Append(propertyType).Append(";\n");
        }
        return sb.Append('}').ToString();
    }

    private string DeclareEnum(Type type)
    {
        if (_declaredNames.TryGetValue(type, out var name))
            return name;

        name = ReserveName(type.Name, type);
        _declaredNames.Add(type, name);
        var names = Enum.GetNames(type);
        var jsonValues = Enum.GetValues(type).Cast<object>()
            .Select(v => JsonSerializer.Serialize(v, type, JsonOptions))
            .ToList();
        var sb = new StringBuilder();
        if (jsonValues.Count != 0 && jsonValues.All(v => v.StartsWith('"'))) {
            var literals = jsonValues.Select(v => TypeScriptSyntax.Quote(JsonSerializer.Deserialize<string>(v)!));
            sb.Append("export type ").Append(name).Append(" = ").AppendJoin(" | ", literals).Append(';');
        }
        else {
            sb.Append("export enum ").Append(name).Append(" {\n");
            for (var i = 0; i < names.Length; i++)
                sb.Append("    ").Append(names[i]).Append(" = ").Append(jsonValues[i]).Append(",\n");
            sb.Append('}');
        }
        _declarations.Add(sb.ToString());
        return name;
    }

    private string Probe(Type type)
    {
        // Types with custom converters (Ulid, Moment, Symbol, ...) are usually structs, so their default value
        // reveals the JSON shape they're written as
        if (type.IsValueType) {
            string? json = null;
            try {
                json = JsonSerializer.Serialize(Activator.CreateInstance(type), type, JsonOptions);
            }
            catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException) {
                // Falls through to the warning below
            }
            if (json is { Length: > 0 }) {
                var result = json[0] switch {
                    '"' => "string",
                    't' or 'f' => "boolean",
                    '[' => "unknown[]",
                    '{' => "Record<string, unknown>",
                    'n' => null,
                    _ => "number",
                };
                if (result is not null)
                    return result;
            }
        }

        _warnings.Add($"Can't map '{type.GetName(true)}' to a TypeScript type, so 'unknown' is used. "
            + $"Add it to {nameof(RpcTypeScriptGeneratorOptions)}.{nameof(RpcTypeScriptGeneratorOptions.KnownTypes)}.");
        return "unknown";
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string))
            return null;

        return type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static string WithNull(string type, bool isNullable)
        => isNullable && !string.Equals(type, "unknown", StringComparison.Ordinal)
            && !type.EndsWith(" | null", StringComparison.Ordinal)
            ? type + " | null"
            : type;

    private static string StripArity(string name)
    {
        var index = name.IndexOf('`', StringComparison.Ordinal);
        return index < 0 ? name : name[..index];
    }

    // Nested types

    private sealed class TypeParameter0;
    private sealed class TypeParameter1;
    private sealed class TypeParameter2;
    private sealed class TypeParameter3;
}
