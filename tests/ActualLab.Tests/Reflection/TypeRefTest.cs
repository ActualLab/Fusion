using ActualLab.Reflection;
using ActualLab.Resilience;
using ActualLab.Rpc;

namespace ActualLab.Tests.Reflection;

[CollectionDefinition(nameof(TypeRefCollection), DisableParallelization = true)]
public class TypeRefCollection;

[Collection(nameof(TypeRefCollection))]
public class TypeRefTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var r = (TypeRef) typeof(TypeRefTest);
        r.Resolve().Should().Be(typeof(TypeRefTest));

        r = typeof(bool);
        r.Resolve().Should().Be(typeof(bool));

        r = typeof(Nested);
        r.Resolve().Should().Be(typeof(Nested));

        r = typeof(Nested.SubNested);
        r.Resolve().Should().Be(typeof(Nested.SubNested));

        r = typeof(PrivateNested);
        r.Resolve().Should().Be(typeof(PrivateNested));

        r = typeof(InternalNested);
        r.Resolve().Should().Be(typeof(InternalNested));

        r = typeof(ProtectedNested);
        r.Resolve().Should().Be(typeof(ProtectedNested));

        r = new TypeRef("NoSuchAssembly.NoSuchType");
        r.TryResolve().Should().BeNull();
        Assert.ThrowsAny<KeyNotFoundException>(() => r.Resolve());

        r = typeof(StaticType);
        r.Resolve().Should().Be(typeof(StaticType));

        r = typeof(StaticType.Nested);
        r.Resolve().Should().Be(typeof(StaticType.Nested));
    }

    [Fact]
    public void WithoutAssemblyVersionsTest()
    {
        var r = (TypeRef)typeof(TypeRefTest);
        var r1 = r.WithoutAssemblyVersions();
        r1.AssemblyQualifiedName.Should().Be("ActualLab.Tests.Reflection.TypeRefTest, ActualLab.Tests");
        r1.Resolve().Should().Be(typeof(TypeRefTest));

        r = (TypeRef)typeof(Option<int>);
        r1 = r.WithoutAssemblyVersions();
#if !NETFRAMEWORK
        r1.AssemblyQualifiedName.Should().Be("ActualLab.Option`1[[System.Int32, System.Private.CoreLib]], ActualLab.Core");
#else
        r1.AssemblyQualifiedName.Should().Be("ActualLab.Option`1[[System.Int32, mscorlib]], ActualLab.Core");
#endif
        r1.Resolve().Should().Be(typeof(Option<int>));
    }

    [Theory]
    [InlineData(typeof(TypeRefTest))]
    [InlineData(typeof(Nested))]
    [InlineData(typeof(Nested.SubNested))]
    [InlineData(typeof(StaticType.Nested))]
    [InlineData(typeof(int))]
    [InlineData(typeof(Exception))]
    [InlineData(typeof(RpcException))]
    [InlineData(typeof(RpcRerouteException))]
    [InlineData(typeof(TransientException))]
    [InlineData(typeof(Exception[]))]
    [InlineData(typeof(Exception[][]))]
    [InlineData(typeof(Exception[,]))]
    [InlineData(typeof(Option<int>))]
    [InlineData(typeof(Option<Exception>))]
    [InlineData(typeof(Dictionary<string, int>))]
    [InlineData(typeof(List<Nested.SubNested>))]
    [InlineData(typeof(Generic<int>.Inner))]
    [InlineData(typeof(Generic<Option<int>>.Inner))]
    public void CanonicalNameIsCachedTest(Type type)
    {
        ResetResolveCache();
        var name = new TypeRef(type).WithoutAssemblyVersions().AssemblyQualifiedName;
        TypeRef.Resolve(name).Should().Be(type);
        TypeRef.ResolveCacheSize.Should().Be(1);
        TypeRef.Resolve(name).Should().Be(type);
        TypeRef.ResolveCacheSize.Should().Be(1);
    }

    [Theory]
    [InlineData("ActualLab.Tests.Reflection.TypeRefTest, actuallab.tests", typeof(TypeRefTest))]
    [InlineData("ActualLab.Tests.Reflection.TypeRefTest,  ActualLab.Tests", typeof(TypeRefTest))]
    [InlineData("ActualLab.Tests.Reflection.TypeRefTest, ActualLab.Tests, Culture=neutral", typeof(TypeRefTest))]
    [InlineData("System.Int32", typeof(int))]
    [InlineData("System.Int32, System.Private.CoreLib, Culture=neutral", typeof(int))]
    public void NonCanonicalNameIsNotCachedTest(string name, Type expectedType)
    {
        ResetResolveCache();
        TypeRef.Resolve(name).Should().Be(expectedType);
        TypeRef.ResolveCacheSize.Should().Be(0);
    }

    [Fact]
    public void VersionedNameIsNotCachedTest()
    {
        ResetResolveCache();
        var name = typeof(TypeRefTest).AssemblyQualifiedName!;
        name.Should().Contain("PublicKeyToken=");
        TypeRef.Resolve(name).Should().Be(typeof(TypeRefTest));
        TypeRef.ResolveCacheSize.Should().Be(0);
    }

    [Fact]
    public void UnresolvableNameIsNotCachedTest()
    {
        ResetResolveCache();
        TypeRef.Resolve("NoSuchAssembly.NoSuchType, NoSuchAssembly").Should().BeNull();
        TypeRef.ResolveCacheSize.Should().Be(0);
    }

    [Theory]
    [InlineData("System.Exception, System.Private.CoreLib", true)]
    [InlineData("  System.Exception, System.Private.CoreLib  ", true)]
    [InlineData("System.Exception", true)]
    [InlineData("ActualLab.Internal.InternalError, ActualLab.Core", true)]
    [InlineData("Ns.Outer+InnerException, Asm", true)]
    [InlineData("Ns.MyException`1[[System.Int32, Corelib]], Asm", true)]
    [InlineData("Ns.Outer`1+InnerException[[System.Int32, Corelib]], Asm", true)]
    [InlineData("Ns.MyException`1, Asm", true)]
    [InlineData("", false)]
    [InlineData(", Asm", false)]
    [InlineData("System.String, System.Private.CoreLib", false)]
    [InlineData("Ns.ExceptionHandler, Asm", false)]
    [InlineData("System.Exception[], System.Private.CoreLib", false)]
    [InlineData("System.Exception[,], System.Private.CoreLib", false)]
    [InlineData("System.Exception[][], System.Private.CoreLib", false)]
    [InlineData("System.Exception*, System.Private.CoreLib", false)]
    [InlineData("System.Exception&, System.Private.CoreLib", false)]
    [InlineData("Ns.MyException`2[[System.Int32, Corelib],[System.Int32, Corelib]], Asm", false)]
    [InlineData("Ns.MyException`1[[Ns.MyException`1[[System.Int32, Corelib]], Asm]], Asm", false)]
    [InlineData("Ns.MyException`1[[System.Int32, Corelib]][], Asm", false)]
    [InlineData("Ns.MyException[[System.Int32, Corelib]], Asm", false)]
    [InlineData("Ns.MyException`, Asm", false)]
    [InlineData("Ns.MyException`123[[System.Int32, Corelib]], Asm", false)]
    [InlineData("Ns.MyException]], Asm", false)]
    [InlineData("Ns.MyException[[System.Int32, Corelib], Asm", false)]
    public void IsAcceptableNameTest(string name, bool isExpectedToBeAcceptable)
        => new TypeRef(name).IsAcceptableName(1, ["Exception", "Error"])
            .Should().Be(isExpectedToBeAcceptable);

    [Fact]
    public async Task ResolveCacheRespectsCapacityTest()
    {
        TypeRef.ResolveCacheCapacity = 8;
        try {
            foreach (var type in GetManyTypes(50)) {
                var name = new TypeRef(type).WithoutAssemblyVersions().AssemblyQualifiedName;
                TypeRef.Resolve(name).Should().Be(type);
            }
            // The cache is pruned in the background, so the bound is reached asynchronously
            await TestExt.When(
                () => TypeRef.ResolveCacheSize.Should().BeLessThanOrEqualTo(8),
                TimeSpan.FromSeconds(5));
        }
        finally {
            ResetResolveCache();
        }
    }

    [Fact]
    public async Task UnversionedNameCacheRespectsCapacityTest()
    {
        TypeRef.UnversionedNameCacheCapacity = 8;
        try {
            foreach (var type in GetManyTypes(50)) {
                var name = new TypeRef(type).WithoutAssemblyVersions().AssemblyQualifiedName;
                name.Should().NotContain("PublicKeyToken=");
            }
            // The cache is pruned in the background, so the bound is reached asynchronously
            await TestExt.When(
                () => TypeRef.UnversionedNameCacheSize.Should().BeLessThanOrEqualTo(8),
                TimeSpan.FromSeconds(5));
        }
        finally {
            ResetUnversionedNameCache();
        }
    }

    [Fact]
    public async Task ConcurrentResolveTest()
    {
        const int threadCount = 8;
        const int roundCount = 20;
        const int capacity = 32;

        TypeRef.ResolveCacheCapacity = capacity;
        try {
            var hotTypes = new[] { typeof(int), typeof(string), typeof(TypeRefTest), typeof(Nested.SubNested) };
            var coldTypes = GetManyTypes(50).ToArray();
            var tasks = Enumerable.Range(0, threadCount)
                .Select(threadIndex => Task.Run(() => {
                    for (var round = 0; round < roundCount; round++) {
                        foreach (var type in hotTypes)
                            Resolve(type);
                        for (var i = 0; i < coldTypes.Length; i++)
                            Resolve(coldTypes[((i * 7) + threadIndex + round) % coldTypes.Length]);
                    }
                    return;

                    void Resolve(Type type) {
                        var name = new TypeRef(type).WithoutAssemblyVersions().AssemblyQualifiedName;
                        TypeRef.Resolve(name).Should().Be(type);
                    }
                }))
                .ToArray();
            await Task.WhenAll(tasks);

            // A prune requested by the very last adds can be swallowed by one that's still running,
            // so under concurrency the ceiling is the capacity plus a few entries
            await TestExt.When(
                () => TypeRef.ResolveCacheSize.Should().BeLessThanOrEqualTo(capacity + threadCount),
                TimeSpan.FromSeconds(5));
        }
        finally {
            ResetResolveCache();
        }
    }

    // Private methods

    private static void ResetResolveCache()
        // Assigning the capacity replaces the cache, which is what we want here
        => TypeRef.ResolveCacheCapacity = TypeRef.DefaultResolveCacheCapacity;

    private static void ResetUnversionedNameCache()
        => TypeRef.UnversionedNameCacheCapacity = TypeRef.DefaultUnversionedNameCacheCapacity;

    private static IEnumerable<Type> GetManyTypes(int count)
    {
        var leafTypes = new[] {
            typeof(int), typeof(long), typeof(short), typeof(byte), typeof(string),
            typeof(bool), typeof(char), typeof(float), typeof(double), typeof(decimal),
        };
        var wrapperTypes = new[] {
            typeof(List<>), typeof(HashSet<>), typeof(Queue<>), typeof(Stack<>), typeof(LinkedList<>),
        };
        for (var i = 0; i < count; i++) {
            var leafType = leafTypes[i % leafTypes.Length];
            var wrapperType = wrapperTypes[(i / leafTypes.Length) % wrapperTypes.Length];
            yield return wrapperType.MakeGenericType(leafType);
        }
    }

#pragma warning disable RCS1102
    public class Nested
    {
        public class SubNested;
    }

    public class Generic<T>
    {
        public class Inner;
    }

    private class PrivateNested;
    internal class InternalNested;
    protected class ProtectedNested;
#pragma warning restore RCS1102
}

public static class StaticType
{
    public class Nested;
}
