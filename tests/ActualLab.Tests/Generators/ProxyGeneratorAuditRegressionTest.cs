using ActualLab.Generators;
using ActualLab.Generators.Internal;
using ActualLab.Interception;
using ActualLab.Trimming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActualLab.Tests.Generators;

public class ProxyGeneratorAuditRegressionTest
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    [Fact]
    public void UnsupportedPassingModifiersShouldProduceOnlyGeneratorDiagnostics()
    {
        const string source = """
            using ActualLab.Interception;

            namespace GeneratorCases;

            public interface IUnsupportedProxy : IRequiresFullProxy
            {
                void Update(ref int value, out string text, in long stamp);
            }
            """;

        var (outputCompilation, generatorDiagnostics, runResult) = RunGenerator(source);

        var generatorErrors = generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        generatorErrors.Should().HaveCount(3);
        generatorErrors.Should().OnlyContain(d => d.Id == "ALG0002");
        GetErrors(outputCompilation).Should().BeEmpty();
        runResult.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void UnsupportedEscapedParameterIdentifierShouldProduceOnlyGeneratorDiagnostic()
    {
        const string source = """
            using ActualLab.Interception;

            namespace GeneratorCases;

            public interface IUnsupportedProxy : IRequiresFullProxy
            {
                void Update(int @event);
            }
            """;

        var (outputCompilation, generatorDiagnostics, runResult) = RunGenerator(source);

        var generatorErrors = generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        generatorErrors.Should().HaveCount(1);
        generatorErrors.Should().ContainSingle(d => d.Id == "ALG0002");
        GetErrors(outputCompilation).Should().BeEmpty();
        runResult.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void DiamondInterfaceMethodsShouldBeEmittedOnceWithoutDroppingOverloads()
    {
        const string source = """
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace GeneratorCases;

            public interface ILeft
            {
                Task Run();
                Task Select(int value);
            }

            public interface IRight
            {
                Task Run();
                Task Select(string value);
            }

            public interface IDiamondProxy : ILeft, IRight, IRequiresFullProxy { }

            public interface IGenericLeft
            {
                T Convert<T>(T value);
            }

            public interface IGenericRight
            {
                U Convert<U>(U value);
            }

            public interface IGenericDiamond : IGenericLeft, IGenericRight { }
            """;

        var (outputCompilation, generatorDiagnostics, runResult) = RunGenerator(source);

        generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        GetErrors(outputCompilation).Should().BeEmpty();
        var generatedSource = runResult.Results.Single().GeneratedSources.Single().SyntaxTree.GetRoot();
        var generatedMethods = generatedSource.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        generatedMethods.Count(m => m.Identifier.ValueText == "Run").Should().Be(1);
        generatedMethods.Count(m => m.Identifier.ValueText == "Select").Should().Be(2);
        var leftConvert = GetMethod(outputCompilation, "GeneratorCases.IGenericLeft", "Convert");
        var rightConvert = GetMethod(outputCompilation, "GeneratorCases.IGenericRight", "Convert");
        GenerationHelpers.HaveCompatibleCallableSignatures(leftConvert, rightConvert).Should().BeTrue();
    }

    [Fact]
    public void CovariantInterfaceReturnsShouldRemainDistinctWithoutExplicitImplementation()
    {
        const string source = """
            namespace GeneratorCases;

            public interface ILeft
            {
                object Read();
            }

            public interface IRight
            {
                string Read();
            }

            public sealed class InvalidImplementation : ILeft, IRight
            {
                public string Read() => "";
            }
            """;

        var compilation = CreateCompilation(source);
        var leftRead = GetMethod(compilation, "GeneratorCases.ILeft", "Read");
        var rightRead = GetMethod(compilation, "GeneratorCases.IRight", "Read");

        GenerationHelpers.HaveCompatibleCallableSignatures(leftRead, rightRead).Should().BeFalse();
        GetErrors(compilation).Should().ContainSingle(d => d.Id == "CS0738");
    }

    [Fact]
    public void ExcessiveMethodArityShouldProduceOnlyGeneratorDiagnostic()
    {
        const string source = """
            using ActualLab.Interception;

            namespace GeneratorCases;

            public interface IUnsupportedProxy : IRequiresFullProxy
            {
                void Update(
                    int a0, int a1, int a2, int a3, int a4, int a5,
                    int a6, int a7, int a8, int a9, int a10);
            }
            """;

        var (outputCompilation, generatorDiagnostics, runResult) = RunGenerator(source);

        var generatorErrors = generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        generatorErrors.Should().HaveCount(1);
        generatorErrors.Should().ContainSingle(d => d.Id == "ALG0003");
        GetErrors(outputCompilation).Should().BeEmpty();
        runResult.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    private static (Compilation OutputCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics,
        GeneratorDriverRunResult RunResult) RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ProxyGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        return (outputCompilation, generatorDiagnostics, driver.GetRunResult());
    }

    private static Diagnostic[] GetErrors(Compilation compilation)
        => compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

    private static CSharpCompilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            "GeneratorAuditCases",
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IMethodSymbol GetMethod(Compilation compilation, string typeName, string methodName)
        => compilation.GetTypeByMetadataName(typeName)!.GetMembers(methodName).OfType<IMethodSymbol>().Single();

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Concat([
                typeof(IRequiresFullProxy).Assembly.Location,
                typeof(CodeKeeper).Assembly.Location,
                typeof(System.Reactive.Unit).Assembly.Location,
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
