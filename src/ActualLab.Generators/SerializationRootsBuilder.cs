namespace ActualLab.Generators;

// Collects the closed generic types this assembly's declarations use (plus the ones nested in
// the serializable types they reference), maps each to the closed formatter types serializers
// instantiate for it via reflection, and roots those from a module initializer.
internal sealed class SerializationRootsBuilder
{
    private const string CodeKeeperTypeName = "global::ActualLab.Trimming.CodeKeeper";
    private const string GeneratedMessagePackResolverTypeName = "MessagePack.GeneratedMessagePackResolver";
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
        + "Target = \"M:MessagePack.GeneratedMessagePackResolver.GeneratedMessagePackResolverGetFormatterHelper.GetFormatter(System.Type)\", "
        + "Justification = \"ActualLab.Generators roots the closed generic formatters this assembly uses\")]";

    private readonly Compilation _compilation;
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<ITypeSymbol> _visitedTypes = new(SymbolEqualityComparer.Default);
    private readonly List<ITypeSymbol> _closedTypes = new();
    private readonly INamedTypeSymbol? _messagePackObjectAttribute;
    private readonly INamedTypeSymbol? _messagePackFormatterAttribute;
    private readonly Dictionary<INamedTypeSymbol, INamedTypeSymbol> _messagePackBuiltInFormatters
        = new(SymbolEqualityComparer.Default);
    private readonly INamedTypeSymbol? _messagePackArrayFormatter;

    public static string? Build(Compilation compilation, CancellationToken cancellationToken)
    {
        if (compilation.GetTypeByMetadataName("ActualLab.Trimming.CodeKeeper") is null)
            return null;
        if (compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is null)
            return null; // NativeAOT targets have it; older targets don't need the roots

        var builder = new SerializationRootsBuilder(compilation, cancellationToken);
        builder.CollectDeclaredTypes(compilation.Assembly.GlobalNamespace);
        var statements = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in builder._closedTypes)
            builder.AddMessagePackRoots(type, statements);
        var assemblyAttributes = new List<string>();
        if (builder._messagePackObjectAttribute is not null)
            assemblyAttributes.Add(MessagePackResolverSuppression);
        return statements.Count == 0 && assemblyAttributes.Count == 0
            ? null
            : BuildSource(assemblyAttributes, statements);
    }

    private SerializationRootsBuilder(Compilation compilation, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _cancellationToken = cancellationToken;
        _messagePackObjectAttribute = compilation.GetTypeByMetadataName("MessagePack.MessagePackObjectAttribute");
        _messagePackFormatterAttribute = compilation.GetTypeByMetadataName("MessagePack.MessagePackFormatterAttribute");
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

    // Type collection

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

        Visit(type.BaseType);
        foreach (var @interface in type.Interfaces)
            Visit(@interface);
        // What gets serialized: the signatures other code can call (RPC services are among them),
        // and the data members of serializable types - not the private state of everything else
        foreach (var member in type.GetMembers())
            if (member is IMethodSymbol method && IsCallableFromOutside(method))
                VisitMember(method);
        if (IsSerializable(type))
            VisitDataMembers(type);
    }

    private static bool IsCallableFromOutside(IMethodSymbol method)
        => method.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected
            or Accessibility.ProtectedOrInternal or Accessibility.Internal
            && method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor;

    private void VisitDataMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
            if (member is IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false })
                VisitMember(member);
    }

    private void VisitMember(ISymbol member)
    {
        switch (member) {
        case IFieldSymbol field:
            Visit(field.Type);
            break;
        case IPropertySymbol property:
            Visit(property.Type);
            foreach (var parameter in property.Parameters)
                Visit(parameter.Type);
            break;
        case IMethodSymbol method:
            Visit(method.ReturnType);
            foreach (var parameter in method.Parameters)
                Visit(parameter.Type);
            break;
        }
    }

    private void Visit(ITypeSymbol? type)
    {
        if (type is null || !_visitedTypes.Add(type))
            return;

        switch (type) {
        case IArrayTypeSymbol arrayType:
            Visit(arrayType.ElementType);
            if (!ContainsTypeParameters(arrayType))
                _closedTypes.Add(arrayType);
            break;
        case INamedTypeSymbol namedType:
            Visit(namedType.ContainingType);
            if (namedType.IsGenericType) {
                foreach (var typeArgument in namedType.TypeArguments)
                    Visit(typeArgument);
                if (!ContainsTypeParameters(namedType))
                    _closedTypes.Add(namedType);
            }
            // The members of a closed generic serializable type, or of one declared elsewhere,
            // may use closed generic types this assembly's declarations don't
            var isForeign = !SymbolEqualityComparer.Default.Equals(namedType.ContainingAssembly, _compilation.Assembly);
            if ((isForeign || namedType.IsGenericType) && IsSerializable(namedType))
                VisitDataMembers(namedType);
            break;
        }
    }

    private bool IsSerializable(INamedTypeSymbol type)
        => HasAttribute(type.OriginalDefinition, _messagePackObjectAttribute);

    // MessagePack

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
        var formatterAttribute = definition.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _messagePackFormatterAttribute));
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
        // The formatter MessagePack's source generator creates for a generic [MessagePackObject] type is internal,
        // so it's rooted by name: it's nested into GeneratedMessagePackResolver via one class per namespace part.
        // Its names for nested types differ, so these aren't rooted.
        if (HasAttribute(definition, _messagePackObjectAttribute) && namedType.ContainingType is null) {
            var path = definition.ContainingNamespace.IsGlobalNamespace
                ? GeneratedMessagePackResolverTypeName
                : $"{GeneratedMessagePackResolverTypeName}+{definition.ContainingNamespace.ToDisplayString().Replace('.', '+')}";
            var formatterTypeName = $"{path}+{definition.Name}Formatter`{definition.Arity}"
                + GetTypeArgumentsReflectionName(namedType.TypeArguments)
                + $", {definition.ContainingAssembly.Name}";
            statements.Add($"{CodeKeeperTypeName}.Keep(\"{formatterTypeName}\");");
        }
    }

    // Helpers

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
            INamedTypeSymbol namedType => _compilation.IsSymbolAccessibleWithin(namedType.OriginalDefinition, _compilation.Assembly)
                && namedType.TypeArguments.All(IsAccessible)
                && (namedType.ContainingType is null || IsAccessible(namedType.ContainingType)),
            _ => false,
        };

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
        => attributeType is not null && symbol.GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));

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
        sb.AppendLine("#pragma warning disable CA2255");
        sb.AppendLine();
        foreach (var attribute in assemblyAttributes)
            sb.AppendLine(attribute);
        if (assemblyAttributes.Count != 0)
            sb.AppendLine();
        if (statements.Count == 0)
            return sb.ToString();

        sb.AppendLine("internal static class ActualLabSerializationRoots");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
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
