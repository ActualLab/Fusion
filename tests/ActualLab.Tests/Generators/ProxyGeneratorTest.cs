using System.Reflection;
using ActualLab.Generators;
using ActualLab.Interception;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ActualLab.Tests.Generators;

public class ProxyGeneratorTest
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);
    private static readonly MetadataReference[] MetadataReferences = GetMetadataReferences();

    [Fact]
    public void SourceHintsMustIncludeGenericArity()
    {
        var compilation = CreateCompilation("""
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace Demo;

            public interface IArityProxy : IRequiresAsyncProxy
            {
                Task Run();
            }

            public interface IArityProxy<T> : IRequiresAsyncProxy
            {
                Task<T> Run();
            }

            public static class First
            {
                public interface INestedProxy : IRequiresAsyncProxy
                {
                    Task Run();
                }
            }

            public static class Second
            {
                public interface INestedProxy : IRequiresAsyncProxy
                {
                    Task Run();
                }
            }
            """);

        var (_, outputCompilation, result) = Run(compilation);

        AssertNoErrors(outputCompilation, result);
        result.Results.Single().GeneratedSources.Should().HaveCount(4);
        result.Results.Single().GeneratedSources
            .Select(x => x.HintName)
            .Should().BeEquivalentTo(
                "Demo.IArityProxyProxy.g.cs",
                "Demo.IArityProxy`1Proxy.g.cs",
                "Demo.First+INestedProxyProxy.g.cs",
                "Demo.Second+INestedProxyProxy.g.cs");
    }

    [Fact]
    public void NestedSourceHintMustDifferFromNamespaceHint()
    {
        var nestedCompilation = CreateCompilation("""
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace Demo;

            public static class Container
            {
                public interface INestedProxy : IRequiresAsyncProxy
                {
                    Task Run();
                }
            }
            """);
        var namespaceCompilation = CreateCompilation("""
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace Demo.Container;

            public interface INestedProxy : IRequiresAsyncProxy
            {
                Task Run();
            }
            """);

        var (_, nestedOutput, nestedResult) = Run(nestedCompilation);
        var (_, namespaceOutput, namespaceResult) = Run(namespaceCompilation);

        AssertNoErrors(nestedOutput, nestedResult);
        AssertNoErrors(namespaceOutput, namespaceResult);
        nestedResult.Results.Single().GeneratedSources.Single().HintName
            .Should().Be("Demo.Container+INestedProxyProxy.g.cs");
        namespaceResult.Results.Single().GeneratedSources.Single().HintName
            .Should().Be("Demo.Container.INestedProxyProxy.g.cs");
    }

    [Fact]
    public void NestedGenericProxyMustCompile()
    {
        var compilation = CreateCompilation("""
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace Demo;

            public class Container<TOuter>
                where TOuter : class
            {
                public interface INested<TInner> : IRequiresAsyncProxy
                    where TInner : struct
                {
                    Task<TOuter> Run(TInner value);
                }

                public interface INonGeneric : IRequiresAsyncProxy
                {
                    Task<TOuter> Run();
                }
            }
            """);

        var (_, outputCompilation, result) = Run(compilation);

        AssertNoErrors(outputCompilation, result);
        var proxyType = outputCompilation
            .GetSymbolsWithName("INestedProxy", SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Single();
        proxyType.ContainingType.Should().NotBeNull();
        proxyType.ContainingType!.Name.Should().Be("Container");
        proxyType.AllInterfaces.Should().Contain(x => x.Name == "INested");
        outputCompilation
            .GetSymbolsWithName("INonGenericProxy", SymbolFilter.Type)
            .Should().ContainSingle();
    }

    [Fact]
    public void PartialProxyUpdatesMustNotRetainGeneratorState()
    {
        var stableTree = Parse("""
            using System.Threading.Tasks;
            using ActualLab.Interception;

            namespace Demo;

            public partial interface IChangingProxy<T> : IRequiresAsyncProxy
                where T : class
            {
                Task<T> A();
            }
            """, "Stable.cs");
        var addedTree = Parse("""
            using System.Threading.Tasks;

            namespace Demo;

            public partial interface IChangingProxy<T>
            {
                Task<T> B();
            }
            """, "Changing.cs");
        var changedTree = Parse("""
            using System.Threading.Tasks;

            namespace Demo;

            public partial interface IChangingProxy<T>
            {
                Task<T> C();
            }
            """, "Changing.cs");
        var generator = new ProxyGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: ParseOptions);

        var compilation = CreateCompilation(stableTree);
        (driver, var outputCompilation, var result) = Run(compilation, driver);
        AssertNoErrors(outputCompilation, result);
        var code = GetGeneratedCode(result);
        code.Should().Contain(" A(").And.NotContain(" B(").And.NotContain(" C(");

        compilation = compilation.AddSyntaxTrees(addedTree);
        (driver, outputCompilation, result) = Run(compilation, driver);
        AssertNoErrors(outputCompilation, result);
        code = GetGeneratedCode(result);
        code.Should().Contain(" A(").And.Contain(" B(").And.NotContain(" C(");

        compilation = compilation.ReplaceSyntaxTree(addedTree, changedTree);
        (driver, outputCompilation, result) = Run(compilation, driver);
        AssertNoErrors(outputCompilation, result);
        code = GetGeneratedCode(result);
        code.Should().Contain(" A(").And.NotContain(" B(").And.Contain(" C(");

        compilation = compilation.RemoveSyntaxTrees(changedTree);
        (_, outputCompilation, result) = Run(compilation, driver);
        code = GetGeneratedCode(result);
        code.Should().Contain(" A(").And.NotContain(" B(").And.NotContain(" C(");
        AssertNoErrors(outputCompilation, result);

        typeof(ProxyGenerator)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeEmpty();
    }

    private static CSharpCompilation CreateCompilation(params string[] sources)
        => CreateCompilation(sources.Select((source, index) => Parse(source, $"Source{index}.cs")).ToArray());

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees)
        => CSharpCompilation.Create(
            $"ProxyGeneratorTest_{Guid.NewGuid():N}",
            syntaxTrees,
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static SyntaxTree Parse(string source, string path)
        => CSharpSyntaxTree.ParseText(source, ParseOptions, path);

    private static (GeneratorDriver Driver, Compilation Compilation, GeneratorDriverRunResult Result) Run(
        CSharpCompilation compilation,
        GeneratorDriver? driver = null)
    {
        driver ??= CSharpGeneratorDriver.Create(
            new[] { new ProxyGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return (driver, outputCompilation, driver.GetRunResult());
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

    private static MetadataReference[] GetMetadataReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        paths.Add(typeof(IRequiresAsyncProxy).Assembly.Location);
        paths.Add(typeof(System.Reactive.Unit).Assembly.Location);
        paths.Add(typeof(ActualLab.Trimming.CodeKeeper).Assembly.Location);
        return paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }
}
