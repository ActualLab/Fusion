using System.Text;
using ActualLab.Reflection;

namespace ActualLab.Tests.Serialization;

// RejectedTypeNameIsNotResolvedTest asserts on the process-wide TypeRef.ResolveCache,
// so this class shares the non-parallel collection with TypeRefTest

[Collection(nameof(Tests.Reflection.TypeRefCollection))]
public class ExceptionInfoTest(ITestOutputHelper @out) : TestBase(@out)
{
#pragma warning disable RCS1194
    public class WeirdException() : Exception("")
    { }

    public class NestedException(string? message) : Exception(message);
    public class GenericException<T>(string? message) : Exception(message);
    public class GenericException<T1, T2>(string? message) : Exception(message);
#pragma warning restore RCS1194

    [Fact]
    public void BasicTest()
    {
        var e = new Exception("1");
        var i = e.ToExceptionInfo();
        i.Message.Should().Be("1");
        i.ToException().Should().BeOfType<Exception>()
            .Which.Message.Should().Be("1");

        // ReSharper disable once NotResolvedInText
#pragma warning disable MA0015
        e = new ArgumentNullException("none", "2");
#pragma warning restore MA0015
        i = e.ToExceptionInfo();
        i.Message.Should().Be(e.Message);
        i.ToException().Should().BeOfType<ArgumentNullException>()
            .Which.Message.Should().Be(e.Message);

        // ReSharper disable once NotResolvedInText
        i = new WeirdException().ToExceptionInfo();
        i.Message.Should().Be("");
        ((RemoteException) i.ToException()!).ExceptionInfo.Should().Be(i);
    }

    [Fact]
    public void AcceptedTypeNameIsResolvedTest()
    {
        ResetResolveCache();
        ToException<Exception>().Should().BeOfType<Exception>();
        TypeRef.ResolveCacheSize.Should().Be(1);

        ResetResolveCache();
        ToException<NestedException>().Should().BeOfType<NestedException>();
        TypeRef.ResolveCacheSize.Should().Be(1);

        ResetResolveCache();
        ToException<GenericException<int>>().Should().BeOfType<GenericException<int>>();
        TypeRef.ResolveCacheSize.Should().Be(1);
    }

    [Fact]
    public void EveryActualLabExceptionTypeIsAcceptableTest()
    {
        // Seeds make sure the assemblies below are loaded even when this test runs alone
        var seeds = new[] {
            typeof(ExceptionInfo).Assembly,
            typeof(ActualLab.Rpc.RpcException).Assembly,
            typeof(ActualLab.Fusion.EntityFramework.DbHub<>).Assembly,
            typeof(ActualLab.CommandR.ICommand).Assembly,
        };
        var exceptionTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(seeds)
            .Distinct()
            .Where(a => a.GetName().Name?.StartsWith("ActualLab", StringComparison.Ordinal) == true)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Exception).IsAssignableFrom(t) && !t.IsGenericTypeDefinition)
            .ToList();
        exceptionTypes.Should().HaveCountGreaterThan(10);

        foreach (var type in exceptionTypes) {
            var typeRef = new TypeRef(type).WithoutAssemblyVersions();
            typeRef.IsAcceptableName(ExceptionInfo.MaxTypeGenericArity, ExceptionInfo.TypeNameSuffixes)
                .Should().BeTrue($"'{typeRef}' must survive an ExceptionInfo round-trip");
        }
    }

    [Fact]
    public void RejectedTypeNameIsNotResolvedTest()
    {
        // Two generic parameters
        ResetResolveCache();
        ToException<GenericException<int, string>>().Should().BeOfType<RemoteException>();
        TypeRef.ResolveCacheSize.Should().Be(0);

        // The name doesn't end with "Exception" or "Error"
        ResetResolveCache();
        ToException<StringBuilder>().Should().BeOfType<RemoteException>();
        TypeRef.ResolveCacheSize.Should().Be(0);

        // An array of an otherwise acceptable type
        ResetResolveCache();
        ToException<Exception[]>().Should().BeOfType<RemoteException>();
        TypeRef.ResolveCacheSize.Should().Be(0);
    }

    [Fact]
    public void OldExceptionInfoTest()
    {
        var t = new TypeEvolutionTester<OldExceptionInfo, ExceptionInfo>(
            (li, i) => Assert.True(li.TypeRef == i.TypeRef && Equals(li.Message, i.Message)));
        t.CheckAllSerializers(new OldExceptionInfo(typeof(Exception), "1"));
    }

    // Private methods

    private static Exception? ToException<T>()
        => new ExceptionInfo(new TypeRef(typeof(T)).WithoutAssemblyVersions(), "1").ToException();

    private static void ResetResolveCache()
        // Assigning the capacity replaces the cache, which is what we want here
        => TypeRef.ResolveCacheCapacity = TypeRef.DefaultResolveCacheCapacity;
}
