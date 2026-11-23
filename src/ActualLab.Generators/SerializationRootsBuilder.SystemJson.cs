namespace ActualLab.Generators;
using static DiagnosticsHelpers;

// System.Text.Json: roots the closed converters [JsonConverter] factories create via MakeGenericType,
// and warns about RPC-carried types no JsonSerializerContext covers - NativeAOT has no reflection-based
// System.Text.Json, so such types can't be serialized by the json5 formats there.
internal sealed partial class SerializationRootsBuilder
{
    private const string JsonSerializableTypesAttributeName = "ActualLab.Serialization.JsonSerializableTypesAttribute";

    private INamedTypeSymbol? _jsonConverterAttribute;
    private INamedTypeSymbol? _jsonSerializableAttribute;
    private INamedTypeSymbol? _jsonSerializerContextType;
    private INamedTypeSymbol? _rpcServiceType;
    private INamedTypeSymbol? _cancellationTokenType;
    private INamedTypeSymbol? _jsonSerializableTypesAttribute;
    private readonly List<INamedTypeSymbol> _jsonContexts = new();
    private readonly List<INamedTypeSymbol> _rpcServiceTypes = new();

    private void InitializeSystemJson()
    {
        _jsonConverterAttribute = _compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonConverterAttribute");
        _jsonSerializableAttribute = _compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializableAttribute");
        _jsonSerializerContextType = _compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext");
        _rpcServiceType = _compilation.GetTypeByMetadataName("ActualLab.Rpc.IRpcService");
        _cancellationTokenType = _compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        _jsonSerializableTypesAttribute = _compilation.GetTypeByMetadataName(JsonSerializableTypesAttributeName);
    }

    private void CollectSystemJsonDeclaration(INamedTypeSymbol type)
    {
        if (_jsonSerializerContextType is not null && InheritsFrom(type, _jsonSerializerContextType))
            _jsonContexts.Add(type);
        if (_rpcServiceType is not null && type.TypeKind == TypeKind.Interface
            && type.AllInterfaces.Contains(_rpcServiceType, SymbolEqualityComparer.Default))
            _rpcServiceTypes.Add(type);
    }

    private void AddSystemJsonRoots(ITypeSymbol type, SortedSet<string> statements)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true, ContainingType: null } namedType)
            return;

        // A converter factory closes its nested converter of the same arity over the type's arguments
        var attribute = GetAttribute(namedType.OriginalDefinition, _jsonConverterAttribute);
        if (attribute is not { ConstructorArguments: [{ Value: INamedTypeSymbol { IsGenericType: false } factoryType }, ..] })
            return;

        foreach (var converterType in factoryType.GetTypeMembers())
            if (converterType.Arity == namedType.Arity)
                AddKeep(converterType.Construct(namedType.TypeArguments.ToArray()), statements);
    }

    // Reported only for assemblies declaring a JsonSerializerContext, i.e. using source-generated System.Text.Json
    private ImmutableArray<Diagnostic> GetSystemJsonDiagnostics()
    {
        if (_jsonContexts.Count == 0 || _rpcServiceTypes.Count == 0 || _jsonSerializableAttribute is null)
            return ImmutableArray<Diagnostic>.Empty;
        // Contexts are added to SystemJsonSerializer only where it has the resolver for them (.NET 8+)
        if (_compilation.GetTypeByMetadataName("ActualLab.Serialization.SystemJsonTypeInfoResolver") is null)
            return ImmutableArray<Diagnostic>.Empty;

        var coveredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in GetJsonSerializableTypes().Concat(GetReferencedJsonSerializableTypes()))
            Cover(type, coveredTypes);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var reportedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var serviceType in _rpcServiceTypes)
            foreach (var method in serviceType.GetMembers().OfType<IMethodSymbol>()) {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                var types = method.Parameters.Select(p => p.Type).Append(UnwrapTask(method.ReturnType));
                foreach (var type in types) {
                    if (type is null or ITypeParameterSymbol || type.SpecialType == SpecialType.System_Void)
                        continue;
                    if (SymbolEqualityComparer.Default.Equals(type, _cancellationTokenType))
                        continue;
                    if (coveredTypes.Contains(type) || !reportedTypes.Add(type))
                        continue;

                    diagnostics.Add(UncoveredRpcJsonType(type, method));
                }
            }
        return diagnostics.ToImmutable();
    }

    // The [JsonSerializable] types of this assembly's contexts
    private List<ITypeSymbol> GetJsonSerializableTypes()
        => _jsonContexts
            .SelectMany(context => context.GetAttributes())
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _jsonSerializableAttribute))
            .Select(a => a.ConstructorArguments is [{ Value: ITypeSymbol type }, ..] ? type : null)
            .Where(type => type is not null)
            .Select(type => type!)
            .ToList();

    // Contexts are usually internal, and a compilation doesn't import the internal types of its references,
    // so the assemblies using ActualLab.Generators list their [JsonSerializable] types in an assembly attribute
    private string? GetJsonSerializableTypesAttribute()
    {
        if (_jsonSerializableTypesAttribute is null)
            return null;

        var types = GetJsonSerializableTypes()
            .Where(t => _compilation.IsSymbolAccessibleWithin(t, _compilation.Assembly))
            .Select(t => $"typeof({t.ToGlobalName()})")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return types.Count == 0
            ? null
            : $"[assembly: global::{JsonSerializableTypesAttributeName}({string.Join(", ", types)})]";
    }

    private IEnumerable<ITypeSymbol> GetReferencedJsonSerializableTypes()
    {
        if (_jsonSerializableTypesAttribute is null)
            yield break;

        foreach (var assembly in _compilation.SourceModule.ReferencedAssemblySymbols)
            foreach (var attribute in assembly.GetAttributes()) {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _jsonSerializableTypesAttribute))
                    continue;

                foreach (var argument in attribute.ConstructorArguments)
                    foreach (var value in argument.Kind == TypedConstantKind.Array ? argument.Values : ImmutableArray.Create(argument))
                        if (value.Value is ITypeSymbol type)
                            yield return type;
            }
    }

    // Mirrors what System.Text.Json's source generator includes for a [JsonSerializable] type
    private void Cover(ITypeSymbol type, HashSet<ITypeSymbol> coveredTypes)
    {
        if (!coveredTypes.Add(type))
            return;

        switch (type) {
        case IArrayTypeSymbol arrayType:
            Cover(arrayType.ElementType, coveredTypes);
            break;
        case INamedTypeSymbol namedType:
            foreach (var typeArgument in namedType.TypeArguments)
                Cover(typeArgument, coveredTypes);
            if (HasAttribute(namedType.OriginalDefinition, _jsonConverterAttribute))
                break; // A custom converter handles its members

            foreach (var member in namedType.GetMembers()) {
                if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property)
                    Cover(property.Type, coveredTypes);
                else if (member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field)
                    Cover(field.Type, coveredTypes);
            }
            break;
        }
    }

    private static ITypeSymbol? UnwrapTask(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true, Name: "Task" or "ValueTask", TypeArguments: [var resultType] }
            && string.Equals(type.ContainingNamespace.ToDisplayString(), "System.Threading.Tasks", StringComparison.Ordinal)
            ? resultType
            : type is INamedTypeSymbol { IsGenericType: false, Name: "Task" or "ValueTask" } ? null : type;

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        return false;
    }
}
