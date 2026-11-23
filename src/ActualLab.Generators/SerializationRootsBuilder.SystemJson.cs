namespace ActualLab.Generators;
using static DiagnosticsHelpers;

// System.Text.Json: roots the closed converters [JsonConverter] factories create via MakeGenericType,
// and warns about RPC-carried types no JsonSerializerContext covers - NativeAOT has no reflection-based
// System.Text.Json, so such types can't be serialized by the json5 formats there.
internal sealed partial class SerializationRootsBuilder
{
    private const string JsonSerializableTypesAttributeName = "ActualLab.Serialization.JsonSerializableTypesAttribute";
    private const string IgnoreJsonCoverageAttributeName = "ActualLab.Serialization.IgnoreJsonCoverageAttribute";

    private INamedTypeSymbol? _jsonConverterAttribute;
    private INamedTypeSymbol? _jsonSerializableAttribute;
    private INamedTypeSymbol? _jsonSerializerContextType;
    private INamedTypeSymbol? _rpcServiceType;
    private INamedTypeSymbol? _cancellationTokenType;
    private INamedTypeSymbol? _jsonSerializableTypesAttribute;
    private INamedTypeSymbol? _ignoreJsonCoverageAttribute;
    private readonly List<INamedTypeSymbol> _jsonContexts = new();
    private readonly List<INamedTypeSymbol> _rpcServiceTypes = new();

    // Private methods

    private void InitializeSystemJson()
    {
        const string ns = "System.Text.Json.Serialization";
        _jsonConverterAttribute = _compilation.GetTypeByMetadataName($"{ns}.JsonConverterAttribute");
        _jsonSerializableAttribute = _compilation.GetTypeByMetadataName($"{ns}.JsonSerializableAttribute");
        _jsonSerializerContextType = _compilation.GetTypeByMetadataName($"{ns}.JsonSerializerContext");
        _rpcServiceType = _compilation.GetTypeByMetadataName("ActualLab.Rpc.IRpcService");
        _cancellationTokenType = _compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        _jsonSerializableTypesAttribute = _compilation.GetTypeByMetadataName(JsonSerializableTypesAttributeName);
        _ignoreJsonCoverageAttribute = _compilation.GetTypeByMetadataName(IgnoreJsonCoverageAttributeName);
    }

    private void CollectSystemJsonDeclaration(INamedTypeSymbol type)
    {
        if (_jsonSerializerContextType is not null && InheritsFrom(type, _jsonSerializerContextType))
            _jsonContexts.Add(type);
        if (_rpcServiceType is not null && type.TypeKind == TypeKind.Interface
            && type.AllInterfaces.Contains(_rpcServiceType, SymbolEqualityComparer.Default))
            _rpcServiceTypes.Add(type);
    }

    // The types System.Text.Json serializes needn't be [DataContract] or [MessagePackObject] ones,
    // so the closed generic types used in the public members of the [JsonSerializable] types are collected too
    private void CollectJsonSerializableTypes()
    {
        var visitedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in GetJsonSerializableTypes())
            VisitJsonGraph(type, visitedTypes, 0);
    }

    private void VisitJsonGraph(ITypeSymbol type, HashSet<ITypeSymbol> visitedTypes, int depth)
    {
        if (depth > MaxDepth || !visitedTypes.Add(type))
            return;

        Visit(type, 0);
        switch (type) {
        case IArrayTypeSymbol arrayType:
            VisitJsonGraph(arrayType.ElementType, visitedTypes, depth + 1);
            break;
        case INamedTypeSymbol namedType:
            foreach (var typeArgument in namedType.TypeArguments)
                VisitJsonGraph(typeArgument, visitedTypes, depth + 1);
            foreach (var memberType in GetPublicDataMemberTypes(namedType))
                VisitJsonGraph(memberType, visitedTypes, depth + 1);
            break;
        }
    }

    private void AddSystemJsonRoots(ITypeSymbol type, SortedSet<string> statements)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true, ContainingType: null } namedType)
            return;

        // A converter factory closes its nested converter of the same arity over the type's arguments;
        // when there are a few, e.g. for value and reference type arguments, the constraints tell which
        var attribute = GetAttribute(namedType.OriginalDefinition, _jsonConverterAttribute);
        if (attribute?.ConstructorArguments is not [{ Value: INamedTypeSymbol { IsGenericType: false } factory }, ..])
            return;

        foreach (var converterType in factory.GetTypeMembers())
            if (converterType.Arity == namedType.Arity && SatisfiesConstraints(converterType, namedType.TypeArguments))
                AddKeep(converterType.Construct(namedType.TypeArguments.ToArray()), statements);
    }

    // Reported only for assemblies whose own JsonSerializerContexts list a type RPC calls carry,
    // i.e. use source-generated System.Text.Json for RPC: a context used for anything else says nothing about it
    private ImmutableArray<Diagnostic> GetSystemJsonDiagnostics()
    {
        if (_jsonContexts.Count == 0 || _rpcServiceTypes.Count == 0 || _jsonSerializableAttribute is null)
            return ImmutableArray<Diagnostic>.Empty;
        // Contexts are added to SystemJsonSerializer only where it has the resolver for them (.NET 8+)
        if (_compilation.GetTypeByMetadataName("ActualLab.Serialization.SystemJsonTypeInfoResolver") is null)
            return ImmutableArray<Diagnostic>.Empty;

        var rpcTypes = GetRpcCarriedTypes();
        var rpcGraphTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var (type, _) in rpcTypes)
            Cover(type, rpcGraphTypes, 0);
        // Primitives are listed for anything, so they don't tell a context serves RPC
        var listedTypes = GetJsonSerializableTypes();
        if (!listedTypes.Any(t => t.SpecialType == SpecialType.None && rpcGraphTypes.Contains(t)))
            return ImmutableArray<Diagnostic>.Empty;

        var coveredTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in listedTypes.Concat(GetReferencedJsonSerializableTypes()))
            Cover(type, coveredTypes, 0);
        var ignoredTypes = GetTypeArguments(_compilation.Assembly, _ignoreJsonCoverageAttribute).ToList();

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var (type, method) in rpcTypes)
            if (!coveredTypes.Contains(type) && !IsIgnored(type, ignoredTypes))
                diagnostics.Add(UncoveredRpcJsonType(type, method));
        return diagnostics.ToImmutable();
    }

    // Each type the RPC methods of this assembly take or return, with the first method using it
    private List<(ITypeSymbol Type, IMethodSymbol Method)> GetRpcCarriedTypes()
    {
        var result = new List<(ITypeSymbol, IMethodSymbol)>();
        var seenTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var serviceType in _rpcServiceTypes)
            foreach (var method in serviceType.GetMembers().OfType<IMethodSymbol>()) {
                if (method.MethodKind != MethodKind.Ordinary)
                    continue;

                var types = method.Parameters.Select(p => p.Type).Append(UnwrapTask(method.ReturnType));
                foreach (var type in types) {
                    if (type is null || ContainsTypeParameters(type) || type.SpecialType == SpecialType.System_Void)
                        continue;
                    if (SymbolEqualityComparer.Default.Equals(type, _cancellationTokenType))
                        continue;
                    if (seenTypes.Add(type))
                        result.Add((type, method));
                }
            }
        return result;
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
            .Where(t => IsRootable(t) && _compilation.IsSymbolAccessibleWithin(t, _compilation.Assembly))
            .Select(t => $"typeof({t.ToGlobalName()})")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return types.Count == 0
            ? null
            : $"[assembly: global::{JsonSerializableTypesAttributeName}({string.Join(", ", types)})]";
    }

    private IEnumerable<ITypeSymbol> GetReferencedJsonSerializableTypes()
        => _compilation.SourceModule.ReferencedAssemblySymbols
            .SelectMany(assembly => GetTypeArguments(assembly, _jsonSerializableTypesAttribute));

    // An open generic type, e.g. typeof(Box<>), ignores each of its closed types
    private static bool IsIgnored(ITypeSymbol type, List<ITypeSymbol> ignoredTypes)
        => ignoredTypes.Any(ignoredType => ignoredType is INamedTypeSymbol { IsUnboundGenericType: true }
            ? SymbolEqualityComparer.Default.Equals(ignoredType.OriginalDefinition, type.OriginalDefinition)
            : SymbolEqualityComparer.Default.Equals(ignoredType, type));

    // The types listed by an assembly's attributes of the given type, which take them as params Type[]
    private static IEnumerable<ITypeSymbol> GetTypeArguments(IAssemblySymbol assembly, INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
            yield break;

        foreach (var attribute in assembly.GetAttributes()) {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                continue;

            foreach (var argument in attribute.ConstructorArguments) {
                var values = argument.Kind == TypedConstantKind.Array
                    ? argument.Values
                    : ImmutableArray.Create(argument);
                foreach (var value in values)
                    if (value.Value is ITypeSymbol type)
                        yield return type;
            }
        }
    }

    // Mirrors what System.Text.Json's source generator includes for a [JsonSerializable] type
    private void Cover(ITypeSymbol type, HashSet<ITypeSymbol> coveredTypes, int depth)
    {
        if (depth > MaxDepth || !coveredTypes.Add(type))
            return;

        switch (type) {
        case IArrayTypeSymbol arrayType:
            Cover(arrayType.ElementType, coveredTypes, depth + 1);
            break;
        case INamedTypeSymbol namedType:
            // A custom converter serializes the type itself, so the generator includes neither
            // its type arguments nor its members
            if (HasAttribute(namedType.OriginalDefinition, _jsonConverterAttribute))
                break;

            foreach (var typeArgument in namedType.TypeArguments)
                Cover(typeArgument, coveredTypes, depth + 1);
            foreach (var memberType in GetPublicDataMemberTypes(namedType))
                Cover(memberType, coveredTypes, depth + 1);
            break;
        }
    }

    private static IEnumerable<ITypeSymbol> GetPublicDataMemberTypes(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            foreach (var member in t.GetMembers()) {
                if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property)
                    yield return property.Type;
                else if (member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field)
                    yield return field.Type;
            }
    }

    private static bool SatisfiesConstraints(INamedTypeSymbol genericType, ImmutableArray<ITypeSymbol> typeArguments)
    {
        for (var i = 0; i < typeArguments.Length; i++) {
            var parameter = genericType.TypeParameters[i];
            var argument = typeArguments[i];
            var isNullableValueType = argument is INamedTypeSymbol {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
            };
            if (parameter.HasValueTypeConstraint && (!argument.IsValueType || isNullableValueType))
                return false;
            if (parameter.HasUnmanagedTypeConstraint && !argument.IsUnmanagedType)
                return false;
            if (parameter.HasReferenceTypeConstraint && !argument.IsReferenceType)
                return false;
            // Type constraints aren't checked, so a converter with one isn't rooted
            if (parameter.ConstraintTypes.Length != 0)
                return false;
        }
        return true;
    }

    private static ITypeSymbol? UnwrapTask(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true, Name: "Task" or "ValueTask", TypeArguments: [var result] }
            && string.Equals(
                type.ContainingNamespace.ToDisplayString(), "System.Threading.Tasks", StringComparison.Ordinal)
            ? result
            : type is INamedTypeSymbol { IsGenericType: false, Name: "Task" or "ValueTask" } ? null : type;

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        return false;
    }
}
