namespace ActualLab.Generators;
using static DiagnosticsHelpers;
using static GenerationHelpers;

/// <summary>
/// An incremental source generator that creates proxy classes for types implementing
/// <c>IRequiresAsyncProxy</c> or <c>IRequiresFullProxy</c> interfaces.
/// </summary>
[Generator]
public class ProxyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var items = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeAugmented, MustAugment)
            .Where(i => i.TypeDef is not null)
            .Collect();
        context.RegisterSourceOutput(items, Generate);
    }

    private static bool CouldBeAugmented(SyntaxNode node, CancellationToken cancellationToken)
        => node is ClassDeclarationSyntax or InterfaceDeclarationSyntax;

    private static (SemanticModel SemanticModel, TypeDeclarationSyntax? TypeDef)
        MustAugment(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var semanticModel = context.SemanticModel;
        var typeDef = (TypeDeclarationSyntax)context.Node;

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDef, cancellationToken);
        if (typeSymbol is null)
            return default;
        if (typeSymbol.IsSealed)
            return default;
        if (typeSymbol is { TypeKind: TypeKind.Class, IsAbstract: true })
            return default;

        var declaredAccessibility = typeSymbol.DeclaredAccessibility;
        if (declaredAccessibility != Accessibility.Public && declaredAccessibility != Accessibility.Internal)
            return default;
        for (var containingType = typeSymbol.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType) {
            declaredAccessibility = containingType.DeclaredAccessibility;
            if (declaredAccessibility != Accessibility.Public && declaredAccessibility != Accessibility.Internal)
                return default;
        }

        var requiresProxy = typeSymbol.AllInterfaces.Any(t => Equals(t.ToFullName(), RequireAsyncProxyInterfaceName));
        if (!requiresProxy)
            return default;

        return (semanticModel, typeDef);
    }

    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<(SemanticModel SemanticModel, TypeDeclarationSyntax? TypeDef)> items)
    {
        if (items.Length == 0)
            return;
        try {
            var uniqueItems = new List<(SemanticModel SemanticModel, TypeDeclarationSyntax TypeDef)>();
            var itemIndexes = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
            foreach (var (semanticModel, typeDef) in items) {
                if (typeDef is null || semanticModel.GetDeclaredSymbol(typeDef) is not { } typeSymbol)
                    continue;
                if (!itemIndexes.TryGetValue(typeSymbol, out var itemIndex)) {
                    itemIndexes.Add(typeSymbol, uniqueItems.Count);
                    uniqueItems.Add((semanticModel, typeDef));
                    continue;
                }
                if (typeDef.ConstraintClauses.Count > uniqueItems[itemIndex].TypeDef.ConstraintClauses.Count)
                    uniqueItems[itemIndex] = (semanticModel, typeDef);
            }

            WriteDebug?.Invoke($"Found {uniqueItems.Count} type(s) to generate proxies.");
            foreach (var (semanticModel, typeDef) in uniqueItems) {

                var typeGenerator = new ProxyTypeGenerator(context, semanticModel, typeDef);
                var code = typeGenerator.GeneratedCode;
                if (string.IsNullOrEmpty(code)) {
                    WriteDebug?.Invoke($"Codegen: {typeDef.Identifier.ToFullString()} -> no code.");
                    continue;
                }

                WriteDebug?.Invoke($"Codegen: {typeDef.Identifier.ToFullString()} -> {code.Length} chars.");
                var typeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(typeDef)!;
                var fileName = GetSourceHintName(typeSymbol);
                context.AddSource(fileName, code);
#if DEBUG
                context.ReportDiagnostic(GenerateProxyTypeProcessedInfo(typeDef));
#endif
            }
        }
        catch (Exception e) {
            context.ReportDebug(e);
            throw;
        }
    }

    private static string GetSourceHintName(INamedTypeSymbol typeSymbol)
    {
        var typeNames = new Stack<string>();
        for (var type = typeSymbol; type is not null; type = type.ContainingType)
            typeNames.Push(type.MetadataName);
        var namespacePrefix = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : typeSymbol.ContainingNamespace + ".";
        return $"{namespacePrefix}{string.Join("+", typeNames)}{ProxyClassSuffix}.g.cs";
    }
}
