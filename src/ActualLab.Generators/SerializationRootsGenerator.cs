namespace ActualLab.Generators;
using static DiagnosticsHelpers;

/// <summary>
/// An incremental source generator that roots the closed generic serialization code NativeAOT
/// can't discover on its own: serializers close generic formatters with <c>MakeGenericType</c>,
/// which works only for the instantiations the AOT compiler was made to generate.
/// </summary>
[Generator]
public class SerializationRootsGenerator : IIncrementalGenerator
{
    public const string HintName = "ActualLab.SerializationRoots.g.cs";
    // The generator runs only for assemblies that define it (e.g. in <DefineConstants>)
    public const string EnableSymbol = "ACTUALLAB_SERIALIZATION_ROOTS";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var isEnabled = context.ParseOptionsProvider.Select(static (options, _)
            => options.PreprocessorSymbolNames.Contains(EnableSymbol, StringComparer.Ordinal));
        var result = context.CompilationProvider
            .Combine(isEnabled)
            .Select(static (item, cancellationToken) => item.Right
                ? SerializationRootsBuilder.Build(item.Left, cancellationToken)
                : SerializationRootsResult.None);
        // Implementation-only: nothing in the output is visible to other code, so design-time builds skip it
        context.RegisterImplementationSourceOutput(result, static (context, result) => {
            try {
                foreach (var diagnostic in result.Diagnostics)
                    context.ReportDiagnostic(diagnostic);
                if (result.Code is not null)
                    context.AddSource(HintName, result.Code);
            }
            catch (Exception e) {
                context.ReportDebug(e);
                throw;
            }
        });
    }
}
