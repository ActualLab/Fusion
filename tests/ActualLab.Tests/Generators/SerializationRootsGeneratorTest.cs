using ActualLab.Generators;
using MessagePack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActualLab.Tests.Generators;

public class SerializationRootsGeneratorTest
{
    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview)
        .WithPreprocessorSymbols(SerializationRootsGenerator.EnableSymbol);
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
        code.Should().Contain("OptionMessagePackFormatter<global::System.Int32>>();");
        code.Should().Contain("Keep<global::MessagePack.Formatters.ListFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain("Formatters.NullableFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain("Keep<global::MessagePack.Formatters.ArrayFormatter<global::ActualLab.Time.Moment>>();");
        code.Should().Contain(
            "Formatters.DictionaryFormatter<global::System.String, global::ActualLab.Time.Moment>>();");
        // The generated formatter is internal, so it's rooted by its assembly-qualified name
        code.Should().Contain(
            "Keep(\"MessagePack.GeneratedMessagePackResolver+Demo+PageFormatter`1[[System.Int64, System.");
        // A data member of a closed serializable type
        code.Should().Contain("OptionMessagePackFormatter<global::System.Int64>>();");
        // Private state of a non-serializable type isn't serialized
        code.Should().NotContain("System.TimeSpan");
        code.Should().Contain("\"IL3050\"");
    }

    [Fact]
    public void SystemJsonTest()
    {
        var compilation = CreateCompilation("""
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using ActualLab.Api;
            using ActualLab.Rpc;

            namespace Demo;

            public interface IService : IRpcService
            {
                Task<Dto> Get(Other other, ApiArray<int> items, string text);
            }

            public class Dto
            {
                public Inner Inner { get; set; } = new();
            }

            public class Inner;

            public class Other;

            [JsonSerializable(typeof(Dto))]
            internal partial class AppJsonContext : JsonSerializerContext;
            """);

        // System.Text.Json's own generator doesn't run here, so the context doesn't compile
        var (_, result) = Run(compilation);

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var code = GetGeneratedCode(result);
        code.Should().Contain("ApiArrayJsonConverter.Converter<global::System.Int32>>();");
        code.Should().Contain("JsonSerializableTypesAttribute(typeof(global::Demo.Dto))]");
        // Dto and Inner are covered by the context, string by ActualLab.Core's one
        var uncoveredTypes = result.Diagnostics
            .Where(d => d.Id == "ALG0004")
            .Select(d => d.GetMessage().Split('\'')[1])
            .ToList();
        uncoveredTypes.Should().BeEquivalentTo("Demo.Other", "ActualLab.Api.ApiArray<int>");
    }

    [Fact]
    public void ContextUnrelatedToRpcShouldNotTriggerALG0004()
    {
        // Arrange
        var compilation = CreateCompilation("""
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using ActualLab.Rpc;

            namespace Demo;

            public interface IService : IRpcService
            {
                Task<Dto> Get(Other other, string text);
            }

            public class Dto;

            public class Other;

            public class Settings
            {
                public string Name { get; set; } = "";
            }

            [JsonSerializable(typeof(Settings))]
            [JsonSerializable(typeof(string))]
            internal partial class SettingsJsonContext : JsonSerializerContext;
            """);

        // Act
        var (_, result) = Run(compilation);

        // Assert
        result.Diagnostics.Where(d => d.Id == "ALG0004").Should().BeEmpty();
    }

    [Fact]
    public void IgnoreJsonCoverageShouldSuppressALG0004()
    {
        // Arrange
        var compilation = CreateCompilation("""
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using ActualLab.Api;
            using ActualLab.Rpc;
            using ActualLab.Serialization;

            [assembly: IgnoreJsonCoverage(typeof(Demo.Other))]
            [assembly: IgnoreJsonCoverage(typeof(ApiArray<>))]

            namespace Demo;

            public interface IService : IRpcService
            {
                Task<Dto> Get(Other other, ApiArray<int> items, ApiOption<int> option);
            }

            public class Dto;

            public class Other;

            [JsonSerializable(typeof(Dto))]
            internal partial class AppJsonContext : JsonSerializerContext;
            """);

        // Act
        var (_, result) = Run(compilation);

        // Assert
        var uncoveredTypes = result.Diagnostics
            .Where(d => d.Id == "ALG0004")
            .Select(d => d.GetMessage().Split('\'')[1])
            .ToList();
        uncoveredTypes.Should().BeEquivalentTo("ActualLab.Api.ApiOption<int>");
    }

    [Fact]
    public void UnusualTypesShouldNotBreakGeneratedCode()
    {
        // arrange
        var compilation = CreateCompilation("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using MessagePack;

            namespace Demo;

            public interface IService
            {
                Task<List<dynamic>> GetDynamic();
                Task<List<Old>> GetObsolete();
                Task<List<GeneratedElsewhere>> GetGenerated(); // Another generator's type: an error type here
                Task<Wrapper<int>> GetWrapper();
                Task<Node<int>> GetNode();
                Task<Outer.Page<long>> GetPage();
                unsafe void TakePointers(int*[] pointers);
            }

            [Obsolete("Old")]
            public class Old;

            // A factory with converters for value and reference type arguments
            [JsonConverter(typeof(WrapperConverter))]
            public class Wrapper<T>;

            public class WrapperConverter : JsonConverterFactory
            {
                public override bool CanConvert(Type typeToConvert) => true;
                public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
                    => null;

                public class StructConverter<T> where T : struct;
                public class ClassConverter<T> where T : class;
            }

            // Expands forever without a depth limit
            [DataContract]
            public class Node<T>
            {
                [DataMember] public Node<List<T>>? Next { get; set; }
            }

            public static class Outer
            {
                [MessagePackObject]
                public class Page<T>
                {
                    [Key(0)] public T? After { get; set; }
                }
            }
            """, allowUnsafe: true);

        // act
        var (outputCompilation, result) = Run(compilation);

        // assert
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        result.Results.Single().Exception.Should().BeNull();
        outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => d.Location.SourceTree?.FilePath.EndsWith(SerializationRootsGenerator.HintName) == true)
            .Should().BeEmpty();
        var code = GetGeneratedCode(result);
        code.Should().NotContain("dynamic").And.NotContain("Old").And.NotContain("ClassConverter")
            .And.NotContain("GeneratedElsewhere");
        code.Should().Contain("WrapperConverter.StructConverter<global::System.Int32>");
        result.Diagnostics.Should().Contain(d => d.Id == "ALG0005");
    }

    [Fact]
    public void JsonOnlyTypesShouldGetConverterRoots()
    {
        // arrange
        var compilation = CreateCompilation("""
            using System.Text.Json.Serialization;
            using ActualLab.Api;

            namespace Demo;

            public class Dto
            {
                public ApiArray<long> Items { get; set; }
            }

            [JsonSerializable(typeof(Dto))]
            internal partial class AppJsonContext : JsonSerializerContext;
            """);

        // act
        var (_, result) = Run(compilation);

        // assert
        GetGeneratedCode(result).Should()
            .Contain("Keep<global::ActualLab.Api.Internal.ApiArrayJsonConverter.Converter<global::System.Int64>>();");
    }

    [Fact]
    public void OptInTest()
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview);
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

    private static CSharpCompilation CreateCompilation(string source, bool allowUnsafe = false)
        => CSharpCompilation.Create(
            $"SerializationRootsGeneratorTest_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, ParseOptions, "Source0.cs")],
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe));

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
        paths.Add(typeof(ActualLab.Rpc.IRpcService).Assembly.Location);
        paths.Add(typeof(ActualLab.Interception.IRequiresAsyncProxy).Assembly.Location);
        return paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }
}
