using ActualLab.Generators;
using MessagePack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActualLab.Tests.Generators;

public class SerializationRootsGeneratorTest
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);
    private static readonly MetadataReference[] MetadataReferences = GetMetadataReferences();

    [Fact]
    public void MessagePackRootsTest()
    {
        var compilation = CreateCompilation("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using ActualLab;
            using ActualLab.Time;
            using MessagePack;

            namespace Demo;

            public interface IService
            {
                Task<Option<int>> GetOption(List<Moment> moments, Moment? moment);
                Task<Moment[]> GetArray(Dictionary<string, Moment> map);
                Task<Page<long>> GetPage();
            }

            [MessagePackObject]
            public class Page<T>
            {
                [Key(0)] public Option<T> After { get; set; }
            }

            public class Impl
            {
                private List<TimeSpan> _state = new();
            }
            """);

        var (outputCompilation, result) = Run(compilation);

        AssertNoErrors(outputCompilation, result);
        var code = GetGeneratedCode(result);
        code.Should().Contain("Keep<global::ActualLab.Serialization.Internal.OptionMessagePackFormatter<global::System.Int32>>();");
        code.Should().Contain("Keep<global::MessagePack.Formatters.ListFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain("Keep<global::MessagePack.Formatters.NullableFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain("Keep<global::MessagePack.Formatters.ArrayFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain(
            "Keep<global::MessagePack.Formatters.DictionaryFormatter<global::System.String, global::ActualLab.Time.Moment>>();");
        // The generated formatter is internal, so it's rooted by its assembly-qualified name
        code.Should().Contain(
            "Keep(\"MessagePack.GeneratedMessagePackResolver+Demo+PageFormatter`1[[System.Int64, System.");
        // A data member of a closed serializable type
        code.Should().Contain("Keep<global::ActualLab.Serialization.Internal.OptionMessagePackFormatter<global::System.Int64>>();");
        // Private state of a non-serializable type isn't serialized
        code.Should().NotContain("System.TimeSpan");
        code.Should().Contain("\"IL3050\"");
    }

    [Fact]
    public void DisableSymbolTest()
    {
        var options = ParseOptions.WithPreprocessorSymbols(SerializationRootsGenerator.DisableSymbol);
        var compilation = CSharpCompilation.Create(
            $"SerializationRootsGeneratorTest_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(
                "public interface IService { System.Collections.Generic.List<int> Get(); }", options)],
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var (_, result) = Run(compilation, options);

        result.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void NoRootsWithoutCodeKeeperTest()
    {
        var compilation = CSharpCompilation.Create(
            $"SerializationRootsGeneratorTest_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText("public class C { public System.Collections.Generic.List<int> X() => new(); }",
                ParseOptions)],
            GetFrameworkReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var (_, result) = Run(compilation);

        result.Results.Single().GeneratedSources.Should().BeEmpty();
    }

    // Private methods

    private static CSharpCompilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            $"SerializationRootsGeneratorTest_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, ParseOptions, "Source0.cs")],
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static (Compilation Compilation, GeneratorDriverRunResult Result) Run(
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions = null)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new SerializationRootsGenerator().AsSourceGenerator()],
            parseOptions: parseOptions ?? ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return (outputCompilation, driver.GetRunResult());
    }

    private static string GetGeneratedCode(GeneratorDriverRunResult result)
        => result.Results.Single().GeneratedSources.Single().SourceText.ToString();

    private static void AssertNoErrors(Compilation compilation, GeneratorDriverRunResult result)
    {
        result.Diagnostics
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
        compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();
    }

    private static MetadataReference[] GetFrameworkReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

    private static MetadataReference[] GetMetadataReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        paths.Add(typeof(ActualLab.Trimming.CodeKeeper).Assembly.Location);
        paths.Add(typeof(MessagePackObjectAttribute).Assembly.Location);
        return paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }
}
