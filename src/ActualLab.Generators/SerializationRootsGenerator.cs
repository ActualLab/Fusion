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
    // Defining it (e.g. in <DefineConstants>) disables the generator for the assembly
    public const string DisableSymbol = "ACTUALLAB_NO_SERIALIZATION_ROOTS";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var isDisabled = context.ParseOptionsProvider.Select(static (options, _)
            => options.PreprocessorSymbolNames.Contains(DisableSymbol, StringComparer.Ordinal));
        var code = context.CompilationProvider
            .Combine(isDisabled)
            .Select(static (item, cancellationToken) => item.Right
                ? null
                : SerializationRootsBuilder.Build(item.Left, cancellationToken));
        context.RegisterSourceOutput(code, static (context, code) => {
            if (code is null)
                return;

            try {
                context.AddSource(HintName, code);
            }
            catch (Exception e) {
                context.ReportDebug(e);
                throw;
            }
        });
    }
}
