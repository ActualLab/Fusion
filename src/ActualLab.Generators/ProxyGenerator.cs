using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<ITypeSymbol, bool> _processedTypes = new(SymbolEqualityComparer.Default);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        _processedTypes.Clear();
        var items = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeAugmented, MustAugment)
            .Where(i => i.TypeDef is not null)
            .Collect();
        context.RegisterSourceOutput(items, Generate);
        _processedTypes.Clear();
    }

    private bool CouldBeAugmented(SyntaxNode node, CancellationToken cancellationToken)
    {
        if (node is not (ClassDeclarationSyntax or InterfaceDeclarationSyntax))
            return false;

        return node.Parent
            is NamespaceDeclarationSyntax
            or FileScopedNamespaceDeclarationSyntax
            or CompilationUnitSyntax;
    }

    private (SemanticModel SemanticModel, TypeDeclarationSyntax? TypeDef)
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

        var requiresProxy = typeSymbol.AllInterfaces.Any(t => Equals(t.ToFullName(), RequireAsyncProxyInterfaceName));
        if (!requiresProxy)
            return default;

        // It might be a partial class w/o generic constraint clauses (even though the type has ones),
        // so we might need to "wait" for the one with generic constraint clauses
        var hasConstraints = typeSymbol.TypeParameters.Any(p => p.HasConstraints());
        if (hasConstraints && !typeDef.ConstraintClauses.Any()) {
            WriteDebug?.Invoke($"[- Type] No constraints: {typeSymbol}");
            return default;
        }

        // There might be a few parts of the same class
        if (typeDef.Modifiers.Any(SyntaxKind.PartialKeyword) && !_processedTypes.TryAdd(typeSymbol, true)) {
            WriteDebug?.Invoke($"[- Type] Already processed: {typeSymbol}");
            return default;
        }

        return (semanticModel, typeDef);
    }

    private void Generate(
        SourceProductionContext context,
        ImmutableArray<(SemanticModel SemanticModel, TypeDeclarationSyntax? TypeDef)> items)
    {
        if (items.Length == 0)
            return;
        try {
            WriteDebug?.Invoke($"Found {items.Length} type(s) to generate proxies.");
            foreach (var (semanticModel, typeDef) in items) {
                if (typeDef is null)
                    continue;

                var typeGenerator = new ProxyTypeGenerator(context, semanticModel, typeDef);
                var code = typeGenerator.GeneratedCode;
                if (string.IsNullOrEmpty(code)) {
                    WriteDebug?.Invoke($"Codegen: {typeDef.Identifier.ToFullString()} -> no code.");
                    continue;
                }

                WriteDebug?.Invoke($"Codegen: {typeDef.Identifier.ToFullString()} -> {code.Length} chars.");
                var typeType = (ITypeSymbol)semanticModel.GetDeclaredSymbol(typeDef)!;
                var fileName = typeType.ContainingNamespace.IsGlobalNamespace
                    ? $"{typeType.Name}{ProxyClassSuffix}.g.cs"
                    : $"{typeType.ContainingNamespace}.{typeType.Name}{ProxyClassSuffix}.g.cs";
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
}
