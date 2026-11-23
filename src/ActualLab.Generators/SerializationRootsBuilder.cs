namespace ActualLab.Generators;
using static DiagnosticsHelpers;

// Collects the closed generic types this assembly's declarations use (plus the ones nested in
// the serializable types they reference), maps each to the closed formatter types serializers
// instantiate for it via reflection, and roots those from a module initializer.
internal sealed partial class SerializationRootsBuilder
{
    private const string CodeKeeperTypeName = "global::ActualLab.Trimming.CodeKeeper";
    private const string RootsTypeName = "ActualLabSerializationRoots";
    private const string GeneratedMessagePackResolverTypeName = "MessagePack.GeneratedMessagePackResolver";
    // Bounds the walk: [DataContract] class Node<T> { public Node<List<T>> Next; } expands forever
    private const int MaxDepth = 8;
    // MessagePack's DynamicGenericResolver closes these via MakeGenericType
    private static readonly (string TypeName, string FormatterTypeName)[] MessagePackBuiltInFormatters = [
        ("System.Collections.Generic.List`1", "MessagePack.Formatters.ListFormatter`1"),
        ("System.Collections.Generic.Dictionary`2", "MessagePack.Formatters.DictionaryFormatter`2"),
        ("System.Nullable`1", "MessagePack.Formatters.NullableFormatter`1"),
    ];
    private const string MessagePackArrayFormatterTypeName = "MessagePack.Formatters.ArrayFormatter`1";
    // The generated resolver of MessagePack's source generator closes generic formatters via MakeGenericType;
    // the roots below keep the instantiations this assembly uses
    private const string MessagePackResolverSuppression
        = "[assembly: global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"AOT\", \"IL3050\", "
        + "Scope = \"member\", "
        + "Target = \"M:MessagePack.GeneratedMessagePackResolver.GeneratedMessagePackResolverGetFormatterHelper"
        + ".GetFormatter(System.Type)\", "
        + "Justification = \"ActualLab.Generators roots the closed generic formatters this assembly uses\")]";

    private readonly Compilation _compilation;
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<ITypeSymbol> _visitedTypes = new(SymbolEqualityComparer.Default);
    private readonly List<ITypeSymbol> _closedTypes = new();
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private readonly INamedTypeSymbol? _messagePackObjectAttribute;
    private readonly INamedTypeSymbol? _messagePackFormatterAttribute;
    private readonly Dictionary<INamedTypeSymbol, INamedTypeSymbol> _messagePackBuiltInFormatters
        = new(SymbolEqualityComparer.Default);
    private readonly INamedTypeSymbol? _messagePackArrayFormatter;
    private readonly INamedTypeSymbol? _dataContractAttribute;
    private readonly INamedTypeSymbol? _obsoleteAttribute;

    public static SerializationRootsResult Build(Compilation compilation, CancellationToken cancellationToken)
    {
        if (compilation.GetTypeByMetadataName("ActualLab.Trimming.CodeKeeper") is null)
            return SerializationRootsResult.None;
        // NativeAOT targets have it; older targets don't need the roots
        if (compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is null)
            return SerializationRootsResult.None;

        var builder = new SerializationRootsBuilder(compilation, cancellationToken);
        builder.CollectDeclaredTypes(compilation.Assembly.GlobalNamespace);
        builder.CollectJsonSerializableTypes();
        var statements = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in builder._closedTypes) {
            builder.AddMessagePackRoots(type, statements);
            builder.AddSystemJsonRoots(type, statements);
        }
        var assemblyAttributes = new List<string>();
        if (builder._messagePackObjectAttribute is not null)
            assemblyAttributes.Add(MessagePackResolverSuppression);
        if (builder.GetJsonSerializableTypesAttribute() is { } jsonSerializableTypesAttribute)
            assemblyAttributes.Add(jsonSerializableTypesAttribute);
        var code = statements.Count == 0 && assemblyAttributes.Count == 0
            ? null
            : BuildSource(assemblyAttributes, statements);
        builder._diagnostics.AddRange(builder.GetSystemJsonDiagnostics());
        return new SerializationRootsResult(code, builder._diagnostics.ToImmutable());
    }

    private SerializationRootsBuilder(Compilation compilation, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _cancellationToken = cancellationToken;
        _messagePackObjectAttribute = compilation.GetTypeByMetadataName("MessagePack.MessagePackObjectAttribute");
        _messagePackFormatterAttribute = compilation.GetTypeByMetadataName("MessagePack.MessagePackFormatterAttribute");
        _dataContractAttribute = compilation.GetTypeByMetadataName(
            "System.Runtime.Serialization.DataContractAttribute");
        _obsoleteAttribute = compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
        InitializeSystemJson();
        if (_messagePackObjectAttribute is null)
            return;

        foreach (var (typeName, formatterTypeName) in MessagePackBuiltInFormatters) {
            var type = compilation.GetTypeByMetadataName(typeName);
            var formatterType = compilation.GetTypeByMetadataName(formatterTypeName);
            if (type is not null && formatterType is not null)
                _messagePackBuiltInFormatters[type] = formatterType;
        }
        _messagePackArrayFormatter = compilation.GetTypeByMetadataName(MessagePackArrayFormatterTypeName);
    }

    // Private methods

    private void CollectDeclaredTypes(INamespaceSymbol ns)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        foreach (var member in ns.GetMembers()) {
            if (member is INamespaceSymbol childNamespace)
                CollectDeclaredTypes(childNamespace);
            else if (member is INamedTypeSymbol type)
                CollectDeclaredType(type);
        }
    }

    private void CollectDeclaredType(INamedTypeSymbol type)
    {
        foreach (var nestedType in type.GetTypeMembers())
            CollectDeclaredType(nestedType);
        CollectSystemJsonDeclaration(type);

        Visit(type.BaseType, 0);
        foreach (var @interface in type.Interfaces)
            Visit(@interface, 0);
        // What gets serialized: the signatures other code can call (RPC services are among them),
        // and the data members of serializable types - not the private state of everything else
        foreach (var member in type.GetMembers())
            if (member is IMethodSymbol method && IsCallableFromOutside(method))
                VisitMember(method, 0);
        if (IsSerializable(type))
            VisitDataMembers(type, 0);
    }

    private static bool IsCallableFromOutside(IMethodSymbol method)
        => method.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected
            or Accessibility.ProtectedOrInternal or Accessibility.Internal
            && method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor;

    private void VisitDataMembers(INamedTypeSymbol type, int depth)
    {
        foreach (var member in type.GetMembers())
            if (member is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false })
                VisitMember(member, depth);
    }

    private void VisitMember(ISymbol member, int depth)
    {
        switch (member) {
        case IFieldSymbol field:
            Visit(field.Type, depth);
            break;
        case IPropertySymbol property:
            Visit(property.Type, depth);
            foreach (var parameter in property.Parameters)
                Visit(parameter.Type, depth);
            break;
        case IMethodSymbol method:
            Visit(method.ReturnType, depth);
            foreach (var parameter in method.Parameters)
                Visit(parameter.Type, depth);
            break;
        }
    }

    private void Visit(ITypeSymbol? type, int depth)
    {
        if (type is null || depth > MaxDepth || !_visitedTypes.Add(type))
            return;

        _cancellationToken.ThrowIfCancellationRequested();
        switch (type) {
        case IArrayTypeSymbol arrayType:
            Visit(arrayType.ElementType, depth + 1);
            if (!ContainsTypeParameters(arrayType) && IsRootable(arrayType))
                _closedTypes.Add(arrayType);
            break;
        case INamedTypeSymbol namedType:
            Visit(namedType.ContainingType, depth + 1);
            if (namedType.IsGenericType) {
                foreach (var typeArgument in namedType.TypeArguments)
                    Visit(typeArgument, depth + 1);
                if (!ContainsTypeParameters(namedType) && IsRootable(namedType))
                    _closedTypes.Add(namedType);
            }
            // The members of a closed generic serializable type, or of one declared elsewhere,
            // may use closed generic types this assembly's declarations don't
            var isForeign = !SymbolEqualityComparer.Default.Equals(namedType.ContainingAssembly, _compilation.Assembly);
            if ((isForeign || namedType.IsGenericType) && IsSerializable(namedType))
                VisitDataMembers(namedType, depth + 1);
            break;
        }
    }

    private bool IsSerializable(INamedTypeSymbol type)
        => HasAttribute(type.OriginalDefinition, _messagePackObjectAttribute)
            || HasAttribute(type.OriginalDefinition, _dataContractAttribute);

    // Generated code can name only well-formed, visible types: not dynamic, pointers, types
    // another generator produces (error types here), file-local or obsolete ones
    private bool IsRootable(ITypeSymbol type)
        => type switch {
            IArrayTypeSymbol arrayType => IsRootable(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeKind != TypeKind.Error
                && namedType.MetadataName.IndexOf('<') < 0 // File-local types have mangled names
                && !HasAttribute(namedType.OriginalDefinition, _obsoleteAttribute)
                && namedType.TypeArguments.All(IsRootable)
                && (namedType.ContainingType is null || IsRootable(namedType.ContainingType)),
            _ => false,
        };

    private void AddMessagePackRoots(ITypeSymbol type, SortedSet<string> statements)
    {
        if (_messagePackObjectAttribute is null)
            return;

        if (type is IArrayTypeSymbol { Rank: 1 } arrayType) {
            if (_messagePackArrayFormatter is not null)
                AddKeep(_messagePackArrayFormatter.Construct(arrayType.ElementType), statements);
            return;
        }
        if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
            return;

        var definition = namedType.OriginalDefinition;
        var formatterAttribute = GetAttribute(definition, _messagePackFormatterAttribute);
        if (formatterAttribute is { ConstructorArguments: [{ Value: INamedTypeSymbol formatterType }, ..] }) {
            formatterType = formatterType.OriginalDefinition;
            if (formatterType.Arity == namedType.Arity && namedType.ContainingType is null)
                AddKeep(formatterType.Construct(namedType.TypeArguments.ToArray()), statements);
            return;
        }
        if (_messagePackBuiltInFormatters.TryGetValue(definition, out var builtInFormatterType)) {
            AddKeep(builtInFormatterType.Construct(namedType.TypeArguments.ToArray()), statements);
            return;
        }
        if (!HasAttribute(definition, _messagePackObjectAttribute))
            return;

        // MessagePack's source generator names the formatters of nested types differently,
        // and the generated formatters can't be referenced from here, so a warning is all we can do
        if (namedType.ContainingType is not null) {
            _diagnostics.Add(UnrootedMessagePackFormatter(namedType));
            return;
        }

        // The formatter MessagePack's source generator creates for a generic [MessagePackObject] type is internal,
        // so it's rooted by name: it's nested into GeneratedMessagePackResolver via one class per namespace part
        var ns = definition.ContainingNamespace;
        var path = ns.IsGlobalNamespace
            ? GeneratedMessagePackResolverTypeName
            : $"{GeneratedMessagePackResolverTypeName}+{ns.ToDisplayString().Replace('.', '+')}";
        var formatterTypeName = $"{path}+{definition.Name}Formatter`{definition.Arity}"
            + GetTypeArgumentsReflectionName(namedType.TypeArguments)
            + $", {definition.ContainingAssembly.Name}";
        statements.Add($"{CodeKeeperTypeName}.Keep(\"{formatterTypeName}\");");
    }

    private void AddKeep(INamedTypeSymbol type, SortedSet<string> statements)
    {
        if (IsAccessible(type))
            statements.Add($"{CodeKeeperTypeName}.Keep<{type.ToGlobalName()}>();");
        else
            statements.Add($"{CodeKeeperTypeName}.Keep(\"{GetReflectionName(type, qualify: true)}\");");
    }

    private bool IsAccessible(ITypeSymbol type)
        => type switch {
            IArrayTypeSymbol arrayType => IsAccessible(arrayType.ElementType),
            INamedTypeSymbol namedType
                => _compilation.IsSymbolAccessibleWithin(namedType.OriginalDefinition, _compilation.Assembly)
                && namedType.TypeArguments.All(IsAccessible)
                && (namedType.ContainingType is null || IsAccessible(namedType.ContainingType)),
            _ => false,
        };

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
        => GetAttribute(symbol, attributeType) is not null;

    private static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
        => attributeType is null
            ? null
            : symbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));

    private static bool ContainsTypeParameters(ITypeSymbol type)
        => type switch {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol arrayType => ContainsTypeParameters(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsTypeParameters)
                || (namedType.ContainingType is { } containingType && ContainsTypeParameters(containingType)),
            _ => false,
        };

    private static string GetReflectionName(ITypeSymbol type, bool qualify)
    {
        string name;
        IAssemblySymbol? assembly;
        if (type is IArrayTypeSymbol arrayType) {
            name = GetReflectionName(arrayType.ElementType, qualify: false)
                + "[" + new string(',', arrayType.Rank - 1) + "]";
            assembly = GetElementType(arrayType).ContainingAssembly;
        }
        else {
            var namedType = (INamedTypeSymbol)type;
            var typeNames = new Stack<string>();
            var typeArguments = new List<ITypeSymbol>();
            for (var t = namedType; t is not null; t = t.ContainingType) {
                typeNames.Push(t.MetadataName);
                typeArguments.InsertRange(0, t.TypeArguments);
            }
            var ns = namedType.ContainingNamespace.IsGlobalNamespace
                ? ""
                : namedType.ContainingNamespace.ToDisplayString() + ".";
            name = ns + string.Join("+", typeNames) + GetTypeArgumentsReflectionName(typeArguments);
            assembly = namedType.ContainingAssembly;
        }
        // Reference assembly names are fine here: the trimmer follows their type forwarders
        return qualify && assembly is not null
            ? $"{name}, {assembly.Name}"
            : name;
    }

    private static string GetTypeArgumentsReflectionName(IReadOnlyCollection<ITypeSymbol> typeArguments)
        => typeArguments.Count == 0
            ? ""
            : "[" + string.Join(",", typeArguments.Select(a => $"[{GetReflectionName(a, qualify: true)}]")) + "]";

    private static ITypeSymbol GetElementType(IArrayTypeSymbol arrayType)
    {
        ITypeSymbol type = arrayType;
        while (type is IArrayTypeSymbol a)
            type = a.ElementType;
        return type;
    }

    private static string BuildSource(List<string> assemblyAttributes, SortedSet<string> statements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        // CS0612, CS0618: a rooted formatter or converter may be obsolete
        sb.AppendLine("#pragma warning disable CA2255, CS0612, CS0618");
        sb.AppendLine();
        foreach (var attribute in assemblyAttributes)
            sb.AppendLine(attribute);
        if (assemblyAttributes.Count != 0)
            sb.AppendLine();
        if (statements.Count == 0)
            return sb.ToString();

        sb.AppendLine($"internal static class {RootsTypeName}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        // Like CodeKeeper's own: the roots are never executed, they only make the compiler keep the code
        foreach (var (category, id) in new[] { ("Trimming", "IL2026"), ("AOT", "IL3050") })
            sb.AppendLine("    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("
                + $"\"{category}\", \"{id}\", Justification = \"The roots only keep the code\")]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine($"        if (!{CodeKeeperTypeName}.AlwaysFalse)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        foreach (var statement in statements)
            sb.AppendLine("        " + statement);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
