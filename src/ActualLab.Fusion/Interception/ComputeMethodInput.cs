using Cysharp.Text;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

public sealed class ComputeMethodInput : ComputedInput, IEquatable<ComputeMethodInput>
{
    public readonly ComputeMethodDef MethodDef;
    public readonly Invocation Invocation;

    // Shortcuts
    public object Service {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Invocation.Proxy;
    }

    public ArgumentList Arguments {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Invocation.Arguments;
    }

    public override bool IsDisposed
        => MethodDef.IsDisposable && Service is IHasIsDisposed { IsDisposed: true };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ComputeMethodInput(IComputeFunction function, ComputeMethodDef methodDef, Invocation invocation)
    {
        MethodDef = methodDef;
        Invocation = invocation;

        var arguments = invocation.Arguments;
        var hashCode = methodDef.Id
            ^ invocation.Proxy.GetHashCode()
            ^ arguments.GetHashCode(methodDef.CancellationTokenIndex);
        Initialize(function, hashCode);
    }

    public override string ToString()
        => ZString.Concat(Category, Arguments, "-Hash=", HashCode);

    public override ComputedOptions GetComputedOptions()
        => MethodDef.ComputedOptions;

    public override Computed? GetExistingComputed()
        => ComputedRegistry.Instance.Get(this);

    // Equality

    public bool Equals(ComputeMethodInput? other)
        => other != null
        && HashCode == other.HashCode
        && ReferenceEquals(MethodDef, other.MethodDef)
        && ReferenceEquals(Service, other.Service)
        && Arguments.Equals(other.Arguments, MethodDef.CancellationTokenIndex);

    public override bool Equals(ComputedInput? obj)
        => obj is ComputeMethodInput other && Equals(other);
    public override bool Equals(object? obj)
        => obj is ComputeMethodInput other && Equals(other);
}
