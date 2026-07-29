using System.Runtime.CompilerServices;
using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

/// <summary>
/// Pins which ways of "creating" a struct run an explicit parameterless constructor (C# 10+)
/// and which produce the zeroed default - the two are easy to confuse, and
/// <see cref="ActivatorExt.CreateInstance(Type)"/> deliberately picks the second.
/// </summary>
public class ValueTypeConstructorTest(ITestOutputHelper @out)
{
    [Fact]
    public void DefaultNeverRunsTheConstructor()
    {
        // default(T) is all-zeroes by definition, for every struct and every C# version
        default(S).A.Should().Be(0);
        var array = new S[1];
        array[0].A.Should().Be(0);
        Array.CreateInstance(typeof(S), 1).GetValue(0).Should().Be(default(S));
    }

    [Fact]
    public void NewRunsTheConstructor()
        => new S().A.Should().Be(42);

    [Fact]
    public void ReflectionPathsDisagree()
    {
        // Activator honours the declared constructor...
        ((S)Activator.CreateInstance(typeof(S))!).A.Should().Be(42);
        // ...GetUninitializedObject never does, which is why CreateInstance uses it
        ((S)RuntimeHelpers.GetUninitializedObject(typeof(S))).A.Should().Be(0);
    }

    [Fact]
    public void ActualLabPathsProduceTheDefault()
    {
        // Both of ours are consistent with default(T), not with new S()
        ((S)typeof(S).CreateInstance()).A.Should().Be(0);
        ActivatorExt.New<S>().A.Should().Be(0);
    }

    // Nested types

    public struct S
    {
        public int A;

        public S()
            => A = 42;
    }
}
